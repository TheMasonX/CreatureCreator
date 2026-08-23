using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// The CreatureCreator editor window. Every field edit in this class funnels
    /// through MutateDefinition/ReplaceDefinition — the single mutation path
    /// (design doc §16): no GUI code ever assigns directly into _definition's
    /// fields, only into a clone that then becomes the new canonical
    /// _definition after validation runs. This is what made wiring up real
    /// Undo/Redo (below) a small addition rather than a rearchitecture — every
    /// mutation already funneled through one place for the undo hook to attach to.
    ///
    /// UNDO/REDO uses Unity's native Undo system via CreatureUndoState, a
    /// minimal ScriptableObject wrapper holding the canonical JSON — see that
    /// class for why CreatureDefinition itself can't be the direct
    /// Undo.RecordObject target. Reachable via the editor's normal Ctrl+Z /
    /// Edit > Undo, not a separate custom stack.
    ///
    /// KNOWN GRANULARITY LIMITATION: continuous drag edits still funnel through
    /// MutateDefinition once per GUI frame the value changes — e.g. dragging a
    /// Vector3Field's position slider or a part's viewport PositionHandle — which
    /// means those gestures currently produce many fine-grained undo steps rather
    /// than one "before drag / after drag" step. Collapsing those into one step
    /// needs mouse-down/mouse-up-aware grouping (Undo.CollapseUndoOperations
    /// around a drag session) that isn't implemented for them in this pass.
    ///
    /// The BODY SAMPLE viewport drag (CC-016) is the exception: it deliberately
    /// does NOT mutate during the drag. It solves every frame from a mouse-down
    /// snapshot, draws a transient preview, and commits exactly one mutation on
    /// release, so one body drag = one Undo (Esc cancels with no Undo entry).
    /// Undo still works correctly everywhere; it's just coarser-grained than
    /// ideal for the inspector/part-handle drags.
    ///
    /// SCOPE NOTE: this editor implements interactive 3D viewport manipulation
    /// (a position handle for the selected part, raycast-based placement of new
    /// parts against the generated preview mesh) in addition to the field-based
    /// inspector. Two things worth understanding about that:
    ///
    /// (1) VIEWPORT POSITIONS ARE WORLD/CREATURE-SPACE, BUT Transform.Position IS
    /// PARENT-LOCAL. Dragging the handle or clicking to place a part happens in
    /// creature-space (matching what's actually drawn), which this class
    /// converts back into the DNA's parent-relative Position by inverting the
    /// parent's resolved world matrix before committing. BoundsDefinition
    /// clamping is then applied to that LOCAL value, matching
    /// DefinitionValidator's existing OutOfBoundsTransform check (which also
    /// checks the local Position, not the resolved world position) — a
    /// pre-existing property of the bounds model from Phase 1, made more
    /// visible now that a world-space-manipulating handle exists: a child
    /// part's bounds are relative to its own parent, so a child can appear to
    /// sit outside the creature's visual silhouette while still being within
    /// its own local bounds, if its parent itself is offset.
    ///
    /// (2) RAYCAST PLACEMENT TARGETS THE LAST-REGENERATED MESH, not the live
    /// definition — this is delta-audit item #7 made concrete rather than
    /// hypothetical. The preview mesh (and its MeshCollider) only updates on
    /// "Regenerate Preview"; placing a part via viewport click after editing
    /// something without regenerating will raycast against stale geometry. A
    /// HelpBox surfaces this in the UI whenever Place Part Mode is active,
    /// rather than leaving it as an implementation detail the user has no way
    /// to know about.
    ///
    /// (3) BODY SAMPLE HANDLES AND PLACE-PART SNAPPING are the Spore-like
    /// authoring surface (CC-015). With the Body selected (and Place Part Mode
    /// off), each Body sample draws a clickable sphere cap; the active sample
    /// gets a position handle, and dragging bends the spine as an equal-length
    /// rigid chain so even spacing is preserved (see BodySplineAuthoring). In
    /// Place Part Mode, clicking the preview mesh with a part selected snaps
    /// that part to the hit point; with nothing selected a new part is created.
    /// Body sample edits target DNA positions directly — never the preview mesh.
    /// </summary>
    public sealed class CreatureEditorWindow : EditorWindow
    {
        private CreatureDefinition _definition;
        private ValidationResult _validation = ValidationResult.Valid();
        private string _selectedPartId;
        private int _activeBodySampleIndex = -1;

        // CC-016 Body sample drag gesture. A whole drag solves every frame from
        // the mouse-down snapshot and commits exactly one mutation on release, so
        // one drag = one Undo. Esc cancels with no mutation. The definition is
        // never mutated during the drag — the solved spline is drawn as a
        // transient SceneView preview and the mesh regenerates only after the
        // mouse-up commit (throttled), so the solver stays interactive even when
        // mesh generation lags (CC-008).
        private int _bodyDragIndex = -1;
        private BodyEditKind _bodyDragKind = BodyEditKind.InteriorBend;
        private Vector3[] _bodyDragSnapshot;
        private Vector3 _bodyDragFinalTarget;
        private Vector3[] _bodyDragPreview;

        private Vector2 _partListScroll;
        private Vector2 _validationScroll;
        private bool _showValidationPanel = true;
        private GameObject _previewGameObject;
        private CreatureUndoState _undoState;
        private bool _placementModeActive;
        private bool _autoRegenerate;
        private bool _showEditorSettings;
        private float _autoRegenerationDelaySeconds = 1f;
        private float _previewVoxelsPerUnit = 16f;
        private bool _logGenerationDiagnostics = true;
        private bool _usePortableSampling;
        private double _autoRegenerateAt = -1d;
        private string _currentFilePath;

        private static readonly IDnaSerializer Serializer = new JsonDnaSerializer();
        private const string PreviewObjectName = "CreatureCreator Preview";
        private const float MinimumAutoRegenerationDelaySeconds = 1f;
        private const string AutoRegenerationDelayKey = "ProceduralCreature.AutoRegenerationDelay";
        private const string PreviewVoxelsPerUnitKey = "ProceduralCreature.PreviewVoxelsPerUnit";
        private const string LogGenerationDiagnosticsKey = "ProceduralCreature.LogGenerationDiagnostics";
        private const string UsePortableSamplingKey = "ProceduralCreature.UsePortableSampling";
        private const string CurrentFilePathKey = "ProceduralCreature.CurrentFilePath";

        /// <summary>
        /// Part types that are valid to author in schema v2. Body, Root, and
        /// independent Tail are reserved by the validator and must not be offered.
        /// </summary>
        private static readonly PartType[] ValidV2PartTypes =
        {
            PartType.Limb,
            PartType.Leg,
            PartType.Arm,
            PartType.Foot,
        };

        /// <summary>
        /// A valid v2 starter creature: one Body spline along the Forward axis and
        /// no parts. Kept in sync with the schema's "exactly one Body root" rule.
        ///
        /// Symmetry defaults to MirrorAcrossXAxis (Spore-like): new parts are
        /// authored with MirrorAcrossSymmetryPlane = true, so a fresh creature is
        /// left/right symmetric across the X = 0 plane out of the box instead of
        /// silently asymmetric. The mirror itself is derived at generation time
        /// (SDF + skeleton); DNA still stores the single authored half.
        /// </summary>
        private static CreatureDefinition CreateDefaultCreature()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 0.9f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 0f),
                Radius = 1.0f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 3,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 0.9f,
            });
            return definition;
        }

        [MenuItem("Window/Procedural Creature/Creature Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<CreatureEditorWindow>();
            window.titleContent = new GUIContent("Creature Editor");
        }

        private void OnEnable()
        {
            _definition = CreatureEditorSession.TryLoad() ?? CreateDefaultCreature();
            Revalidate();

            _undoState = ScriptableObject.CreateInstance<CreatureUndoState>();
            _undoState.hideFlags = HideFlags.HideAndDontSave;
            _undoState.Json = Serializer.Serialize(_definition);
            _autoRegenerationDelaySeconds = Mathf.Max(
                MinimumAutoRegenerationDelaySeconds,
                EditorPrefs.GetFloat(AutoRegenerationDelayKey, MinimumAutoRegenerationDelaySeconds));
            _previewVoxelsPerUnit = Mathf.Max(1f, EditorPrefs.GetFloat(PreviewVoxelsPerUnitKey, 16f));
            _logGenerationDiagnostics = EditorPrefs.GetBool(LogGenerationDiagnosticsKey, true);
            _usePortableSampling = EditorPrefs.GetBool(UsePortableSamplingKey, true);
            _currentFilePath = SessionState.GetString(CurrentFilePathKey, string.Empty);

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += ProcessAutoRegeneration;

            _previewGameObject = GameObject.Find(PreviewObjectName);
        }

        private void OnDisable()
        {
            // Resolves delta-audit item #5: persist whatever's currently in
            // memory so a domain reload (or simply closing the window) doesn't
            // lose in-progress edits.
            CreatureEditorSession.Save(_definition);

            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= ProcessAutoRegeneration;

            if (_undoState != null)
            {
                Object.DestroyImmediate(_undoState);
                _undoState = null;
            }
        }

        private void OnUndoRedoPerformed()
        {
            if (_undoState == null) return;

            try
            {
                _definition = Serializer.Deserialize(_undoState.Json);
            }
            catch (DnaDeserializationException)
            {
                // A corrupted undo snapshot shouldn't normally happen — this
                // class only ever writes valid canonical JSON into _undoState —
                // but failing safe here (log + leave _definition as it was)
                // beats throwing out of Unity's undo callback, which would
                // break the editor's undo stack for everything else too.
                Debug.LogWarning("[CreatureCreator] Failed to restore an undo/redo snapshot; the creature definition was left unchanged.");
                return;
            }

            if (_selectedPartId != null
                && _selectedPartId != CreatureDefinition.BodyId
                && _definition.FindPart(_selectedPartId) == null)
            {
                _selectedPartId = null;
            }

            Revalidate();
            CreatureEditorSession.Save(_definition);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_placementModeActive)
            {
                EditorGUILayout.HelpBox(
                    "Place Part Mode: click the preview mesh in the Scene view. With a part selected, the " +
                    "part snaps to the clicked position. With no part selected, a new part is created there. " +
                    "This raycasts against the mesh from the last 'Regenerate Preview' click, which may be " +
                    "stale if you've edited the creature since then — regenerate first if placement looks off.",
                    MessageType.Info);
            }

            EditorGUILayout.BeginHorizontal();
            DrawPartList();
            DrawPartInspector();
            EditorGUILayout.EndHorizontal();

            DrawValidationPanel();
        }

        // ---- the single mutation path ------------------------------------------------

        private void MutateDefinition(string undoDescription, System.Action<CreatureDefinition> mutation)
        {
            CreatureDefinition working = _definition.Clone();
            mutation(working);
            ApplyDefinitionChange(undoDescription, working);
        }

        private void ReplaceDefinition(string undoDescription, CreatureDefinition newDefinition)
        {
            ApplyDefinitionChange(undoDescription, newDefinition);
        }

        private void ApplyDefinitionChange(string undoDescription, CreatureDefinition newDefinition)
        {
            // Snapshot the PRE-change state before mutating, matching
            // Undo.RecordObject's contract — this is what makes the change
            // reachable via Ctrl+Z / Edit > Undo afterward.
            Undo.RecordObject(_undoState, undoDescription);

            _definition = newDefinition;
            _undoState.Json = Serializer.Serialize(_definition);

            Revalidate();
            CreatureEditorSession.Save(_definition);
            ScheduleAutoRegeneration();
        }

        private void Revalidate()
        {
            _validation = DefinitionValidator.Validate(_definition);
        }

        // ---- toolbar -------------------------------------------------------------------

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New", EditorStyles.toolbarButton)) CreateNew();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton)) SaveCurrent();
            if (GUILayout.Button("Save As...", EditorStyles.toolbarButton)) SaveAs();
            if (GUILayout.Button("Load...", EditorStyles.toolbarButton)) LoadFromDisk();

            GUILayout.FlexibleSpace();

            GUI.enabled = _previewGameObject != null;
            bool newPlacementMode = GUILayout.Toggle(_placementModeActive, "Place Part Mode", EditorStyles.toolbarButton);
            if (newPlacementMode != _placementModeActive)
            {
                _placementModeActive = newPlacementMode;
                SceneView.RepaintAll();
            }
            GUI.enabled = true;

            if (GUILayout.Button("Regenerate Preview", EditorStyles.toolbarButton)) RegeneratePreview();
            bool newAutoRegenerate = GUILayout.Toggle(_autoRegenerate, "Auto", EditorStyles.toolbarButton, GUILayout.Width(48));
            if (newAutoRegenerate != _autoRegenerate)
            {
                _autoRegenerate = newAutoRegenerate;
                if (_autoRegenerate) ScheduleAutoRegeneration();
                else _autoRegenerateAt = -1d;
            }

            EditorGUILayout.EndHorizontal();
            DrawEditorSettings();
        }

        private void DrawEditorSettings()
        {
            _showEditorSettings = EditorGUILayout.Foldout(_showEditorSettings, "Editor Settings");
            if (!_showEditorSettings) return;

            float newQuality = Mathf.Max(1f, EditorGUILayout.FloatField("Preview Mesh Quality", _previewVoxelsPerUnit));
            if (!Mathf.Approximately(newQuality, _previewVoxelsPerUnit))
            {
                _previewVoxelsPerUnit = newQuality;
                EditorPrefs.SetFloat(PreviewVoxelsPerUnitKey, _previewVoxelsPerUnit);
                ScheduleAutoRegeneration();
            }

            long estimatedVoxels = new GenerationSettings { VoxelsPerUnit = _previewVoxelsPerUnit }
                .EstimateVoxelCount(_definition.Bounds);
            EditorGUILayout.LabelField(
                "Estimated Voxel Count",
                $"{estimatedVoxels:N0} / {GenerationTolerances.MaxVoxelBudget:N0}");

            float newDelay = Mathf.Max(
                MinimumAutoRegenerationDelaySeconds,
                EditorGUILayout.FloatField("Auto Regen Rate (seconds)", _autoRegenerationDelaySeconds));
            if (!Mathf.Approximately(newDelay, _autoRegenerationDelaySeconds))
            {
                _autoRegenerationDelaySeconds = newDelay;
                EditorPrefs.SetFloat(AutoRegenerationDelayKey, _autoRegenerationDelaySeconds);
                ScheduleAutoRegeneration();
            }

            bool newLogGenerationDiagnostics = EditorGUILayout.Toggle(
                "Log Generation Diagnostics", _logGenerationDiagnostics);
            if (newLogGenerationDiagnostics != _logGenerationDiagnostics)
            {
                _logGenerationDiagnostics = newLogGenerationDiagnostics;
                EditorPrefs.SetBool(LogGenerationDiagnosticsKey, _logGenerationDiagnostics);
            }

            bool newUsePortableSampling = EditorGUILayout.Toggle(
                "Use Burst SDF Sampling", _usePortableSampling);
            if (newUsePortableSampling != _usePortableSampling)
            {
                _usePortableSampling = newUsePortableSampling;
                EditorPrefs.SetBool(UsePortableSamplingKey, _usePortableSampling);
                ScheduleAutoRegeneration();
            }
        }

        [MenuItem("Window/Procedural Creature/Save Creature %s")]
        private static void SaveCurrentFromMenu()
        {
            GetWindow<CreatureEditorWindow>().SaveCurrent();
        }

        private void CreateNew()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "New Creature",
                "Discard the current creature and start a new one? (You can undo this with Ctrl+Z / Edit > Undo.)",
                "Discard", "Cancel");
            if (!proceed) return;

            _selectedPartId = null;
            ReplaceDefinition("New Creature", CreateDefaultCreature());
        }

        private void SaveCurrent()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAs();
                return;
            }

            WriteToDisk(_currentFilePath);
        }

        private void SaveAs()
        {
            string path = EditorUtility.SaveFilePanel("Save Creature", "", "creature", "json");
            if (string.IsNullOrEmpty(path)) return;

            _currentFilePath = path;
            SessionState.SetString(CurrentFilePathKey, _currentFilePath);
            WriteToDisk(_currentFilePath);
        }

        private void WriteToDisk(string path)
        {
            if (!_validation.IsValid)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Validation Errors",
                    "The current creature has validation errors. Save anyway? " +
                    "You can load and fix it later.",
                    "Save Anyway", "Cancel");
                if (!proceed) return;
            }

            try
            {
                File.WriteAllText(path, Serializer.Serialize(_definition));
            }
            catch (DomainException ex)
            {
                EditorUtility.DisplayDialog("Save Failed", ex.Message, "OK");
            }
        }

        private void LoadFromDisk()
        {
            string path = EditorUtility.OpenFilePanel("Load Creature", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            _currentFilePath = path;
            SessionState.SetString(CurrentFilePathKey, _currentFilePath);

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                EditorUtility.DisplayDialog("Load Failed", $"Could not read file: {ex.Message}", "OK");
                return;
            }

            CreatureDefinition loaded;
            try
            {
                loaded = Serializer.Deserialize(json);
            }
            catch (DnaDeserializationException ex)
            {
                EditorUtility.DisplayDialog("Load Failed", $"File is not a valid creature document:\n{ex.Message}", "OK");
                return;
            }

            // §14: validate before replacing current canonical state — never
            // trust a loaded file just because it parsed as structurally valid JSON.
            ValidationResult loadedValidation = DefinitionValidator.Validate(loaded);
            if (!loadedValidation.IsValid)
            {
                int errorCount = loadedValidation.Issues.Count(i => i.Severity == ValidationSeverity.Error);
                bool proceed = EditorUtility.DisplayDialog(
                    "Validation Errors",
                    $"The loaded creature has {errorCount} validation error(s). Load it anyway? " +
                    "You will need to fix them before generating a preview.",
                    "Load Anyway", "Cancel");
                if (!proceed) return;
            }

            _selectedPartId = null;
            ReplaceDefinition("Load Creature", loaded);
        }

        // ---- part list -------------------------------------------------------------------

        private void DrawPartList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);

            _partListScroll = EditorGUILayout.BeginScrollView(_partListScroll);

            DrawBodyNode();
            var visited = new HashSet<string> { CreatureDefinition.BodyId };
            foreach (CreaturePart child in ChildrenOf(CreatureDefinition.BodyId)
                .OrderBy(p => p.Id, System.StringComparer.Ordinal))
            {
                DrawPartNode(child, depth: 1, visited);
            }

            // Parts whose ParentId points at a missing part (or at a cycle) are
            // not reachable from the Body root; show them explicitly so the user
            // can reparent or remove them instead of the part silently vanishing
            // from the tree. The validator reports the underlying error.
            var reachable = new HashSet<string>(visited);
            IEnumerable<CreaturePart> orphans = _definition.Parts
                .Where(p => p != null && !reachable.Contains(p.Id))
                .OrderBy(p => p.Id, System.StringComparer.Ordinal);
            bool firstOrphan = true;
            foreach (CreaturePart orphan in orphans)
            {
                if (firstOrphan)
                {
                    EditorGUILayout.LabelField("Unparented", EditorStyles.boldLabel);
                    firstOrphan = false;
                }
                DrawPartNode(orphan, depth: 1, visited);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Part")) AddNewPart();

            GUI.enabled = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId;
            if (GUILayout.Button("Remove Selected")) RemoveSelectedPart();
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }

        private void DrawBodyNode()
        {
            bool isSelected = _selectedPartId == CreatureDefinition.BodyId;
            bool nowSelected = GUILayout.Toggle(isSelected, "Body", EditorStyles.toolbarButton);
            if (nowSelected && !isSelected)
            {
                _selectedPartId = CreatureDefinition.BodyId;
                _activeBodySampleIndex = -1; // start with no sample grabbed
            }
        }

        private void DrawPartNode(CreaturePart part, int depth, HashSet<string> visited)
        {
            if (!visited.Add(part.Id)) return; // parent cycle: stop descending, the validator flags it

            string indent = new string(' ', depth * 2);
            bool isSelected = part.Id == _selectedPartId;
            string label = $"{indent}{part.PartType}  {GetPartLabel(part)}";
            bool nowSelected = GUILayout.Toggle(isSelected, label, EditorStyles.toolbarButton);
            if (nowSelected && !isSelected)
            {
                _selectedPartId = part.Id;
                _activeBodySampleIndex = -1;
            }

            foreach (CreaturePart child in ChildrenOf(part.Id)
                .OrderBy(p => p.Id, System.StringComparer.Ordinal))
            {
                DrawPartNode(child, depth + 1, visited);
            }
        }

        private IEnumerable<CreaturePart> ChildrenOf(string parentId)
        {
            return _definition.Parts.Where(p => p.ParentId == parentId);
        }

        private void AddNewPart()
        {
            string newId = PartIdGenerator.CreateNew();
            string parentId = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId
                ? _selectedPartId
                : CreatureDefinition.BodyId; // default: attach directly to the Body

            MutateDefinition("Add Part", definition => definition.AddPart(new CreaturePart
            {
                Id = newId,
                ParentId = parentId,
                PartType = PartType.Limb,
                DisplayName = "Limb",
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            }));

            _selectedPartId = newId;
            _activeBodySampleIndex = -1;
        }

        private void RemoveSelectedPart()
        {
            if (_selectedPartId == null) return;
            string idToRemove = _selectedPartId;

            if (_definition.GetChildren(idToRemove).Any())
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Remove Part",
                    "This part has children that reference it as their parent. Removing it will " +
                    "leave them with a missing parent (shown as a validation error) until you " +
                    "reassign or remove them too. Continue?",
                    "Remove Anyway", "Cancel");
                if (!proceed) return;
            }

            MutateDefinition("Remove Part", definition => definition.RemovePart(idToRemove));
            _selectedPartId = null;
        }

        // ---- inspector -------------------------------------------------------------------

        private void DrawPartInspector()
        {
            EditorGUILayout.BeginVertical();

            if (_selectedPartId == CreatureDefinition.BodyId)
            {
                DrawBodyInspector();
                EditorGUILayout.EndVertical();
                return;
            }

            CreaturePart selected = _selectedPartId != null ? _definition.FindPart(_selectedPartId) : null;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Select a part to edit it, or click Add Part to create one.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField($"Editing: {GetPartLabel(selected)}", EditorStyles.boldLabel);

            string currentDisplayName = selected.DisplayName ?? selected.Id;
            string newDisplayName = EditorGUILayout.TextField("Name", currentDisplayName);
            if (newDisplayName != currentDisplayName)
            {
                string partId = selected.Id;
                MutateDefinition("Rename Part", definition => definition.FindPart(partId).DisplayName = newDisplayName);
                selected = _definition.FindPart(partId);
            }

            EditorGUILayout.LabelField("Unique Part Slug", selected.Id);

            DrawPartTypeField(selected);
            DrawParentPicker(selected);
            EditorGUILayout.Space();
            DrawTransformFields(selected);
            EditorGUILayout.Space();
            DrawShapeFields(selected);
            EditorGUILayout.Space();
            DrawAppearanceFields(selected);
            EditorGUILayout.Space();
            DrawSymmetryFields(selected);

            EditorGUILayout.EndVertical();
        }

        private void DrawPartTypeField(CreaturePart selected)
        {
            string[] typeNames = ValidV2PartTypes.Select(t => t.ToString()).ToArray();
            int currentIndex = System.Array.IndexOf(ValidV2PartTypes, selected.PartType);
            if (currentIndex < 0) currentIndex = 0; // legacy/unknown value maps to the first valid type for display

            int newIndex = EditorGUILayout.Popup("Part Type", currentIndex, typeNames);
            if (newIndex == currentIndex) return;

            PartType newType = ValidV2PartTypes[newIndex];
            string partId = selected.Id;
            MutateDefinition("Change Part Type", definition => definition.FindPart(partId).PartType = newType);
        }

        private void DrawBodyInspector()
        {
            EditorGUILayout.LabelField("Editing: Body", EditorStyles.boldLabel);

            Vector3 newForward = EditorGUILayout.Vector3Field("Forward", _definition.Forward);
            if (newForward != _definition.Forward)
            {
                MutateDefinition("Edit Body Forward", definition => definition.Forward = newForward);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Spline", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Samples: {_definition.Body.Samples.Count}");

            BodySample sampleToRemove = null;
            for (int i = 0; i < _definition.Body.Samples.Count; i++)
            {
                BodySample sample = _definition.Body.Samples[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"#{sample.Id}", GUILayout.Width(40));
                Vector3 newPosition = EditorGUILayout.Vector3Field("", sample.Position);
                if (newPosition != sample.Position)
                {
                    uint sampleId = sample.Id;
                    MutateDefinition("Move Body Sample",
                        definition => FindBodySample(definition, sampleId).Position = newPosition);
                    sample = _definition.Body.Samples[i];
                }
                float newRadius = EditorGUILayout.FloatField(sample.Radius, GUILayout.Width(60));
                if (!Mathf.Approximately(newRadius, sample.Radius))
                {
                    uint sampleId = sample.Id;
                    MutateDefinition("Resize Body Sample",
                        definition => FindBodySample(definition, sampleId).Radius = newRadius);
                    sample = _definition.Body.Samples[i];
                }
                if (GUILayout.Button("Remove", GUILayout.Width(60))) sampleToRemove = sample;
                EditorGUILayout.EndHorizontal();
            }

            if (sampleToRemove != null)
            {
                uint idToRemove = sampleToRemove.Id;
                MutateDefinition("Remove Body Sample",
                    definition => definition.Body.Samples.RemoveAll(s => s.Id == idToRemove));
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Body Sample"))
            {
                MutateDefinition("Add Body Sample", definition =>
                {
                    BodySplineAuthoring.AppendSample(definition.Body, definition.Forward);
                });
                // Select the freshly added sample so it can be dragged immediately.
                _activeBodySampleIndex = _definition.Body.Samples.Count - 1;
                Repaint();
            }
            if (GUILayout.Button("Space Evenly"))
            {
                MutateDefinition("Space Body Evenly",
                    definition => BodySplineAuthoring.SpaceEvenly(definition.Body));
            }
            EditorGUILayout.EndHorizontal();

            // Body Spacing density control (CC-015): re-samples the whole Body
            // to the target chord spacing, keeping the head and tail in place
            // (denser spacing adds samples, sparser removes them).
            //
            // CC-016 review note: this slider is treated as a developer/debug
            // control until its semantics are explicit. It is NOT part of the
            // core bend/length editing interaction, and it must never be applied
            // implicitly during a drag (no hidden global re-spacing coupling).
            float currentSpacing = CurrentBodySpacing(_definition.Body);
            float minSpacing = Mathf.Max(0.05f, currentSpacing * 0.2f);
            float maxSpacing = currentSpacing * 5f;
            float newSpacing = EditorGUILayout.Slider("Body Spacing", currentSpacing, minSpacing, maxSpacing);
            if (!Mathf.Approximately(newSpacing, currentSpacing))
            {
                float target = newSpacing;
                MutateDefinition("Set Body Spacing",
                    definition => BodySplineAuthoring.RespaceToTargetSpacing(definition.Body, target));
                _activeBodySampleIndex = -1; // sample count may have changed
                Repaint();
            }

            EditorGUILayout.HelpBox(
                "Adding a sample extends the Body along its tail at the current spacing. Drag the sample " +
                "spheres in the Scene view (select Body in the part list first): an interior sample bends " +
                "the spine locally (the selected sample moves, neighbors resist and move a little), and an " +
                "endpoint (the larger sphere) extends or shortens the body. One drag commits as a single " +
                "Undo step; Esc cancels a drag. The Body Spacing slider is a developer/debug density " +
                "control that re-samples the whole Body (head and tail stay put); its semantics are still " +
                "being defined and it is not part of the core bend/edit interaction. Space Evenly re-snaps " +
                "spacing after manual edits.",
                MessageType.None);
        }

        private static BodySample FindBodySample(CreatureDefinition definition, uint id)
        {
            return definition.Body.Samples.First(s => s.Id == id);
        }

        private static float CurrentBodySpacing(BodySpline spline)
        {
            if (spline == null || spline.Samples == null || spline.Samples.Count < 2) return 1f;
            float total = 0f;
            int pairs = 0;
            for (int i = 1; i < spline.Samples.Count; i++)
            {
                if (spline.Samples[i] == null || spline.Samples[i - 1] == null) continue;
                total += Vector3.Distance(spline.Samples[i].Position, spline.Samples[i - 1].Position);
                pairs++;
            }
            return pairs > 0 ? total / pairs : 1f;
        }

        private void DrawParentPicker(CreaturePart selected)
        {
            var candidateParts = _definition.Parts
                .Where(p => p.Id != selected.Id)
                .OrderBy(p => p.Id, System.StringComparer.Ordinal)
                .ToList();
            var candidateIds = candidateParts.Select(p => p.Id).ToList();
            var candidateLabels = candidateParts.Select(GetPartLabel).ToList();
            candidateIds.Insert(0, CreatureDefinition.BodyId);
            candidateLabels.Insert(0, "Body (root)");

            int currentIndex = selected.ParentId == null ? 0 : candidateIds.IndexOf(selected.ParentId);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup("Parent", currentIndex, candidateLabels.ToArray());
            string newParentId = candidateIds[newIndex];
            if (newParentId == selected.ParentId) return;

            string partId = selected.Id;
            // No cycle pre-check here — DefinitionValidator will flag a
            // resulting cycle as an Error in the panel below (fail-explicit
            // rather than silently blocking the edit before it's even made).
            MutateDefinition("Reparent Part", definition => definition.FindPart(partId).ParentId = newParentId);
        }

        private void DrawTransformFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);

            Vector3 rawPosition = EditorGUILayout.Vector3Field("Position", selected.Transform.Position);
            Vector3 clampedPosition = ClampToBounds(rawPosition, _definition.Bounds);
            if (clampedPosition != rawPosition)
            {
                // §5.1: hard-stop at the bounds limit, never squish geometry —
                // clamp the position, don't scale it.
                EditorGUILayout.HelpBox("Position clamped to the creature's authoring bounds.", MessageType.None);
            }

            Vector3 eulerRotation = EditorGUILayout.Vector3Field("Rotation (Euler)", selected.Transform.Rotation.eulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", selected.Transform.Scale);

            var newTransform = new TransformData
            {
                Position = clampedPosition,
                Rotation = Quaternion.Euler(eulerRotation),
                Scale = scale,
            };

            if (TransformsRoughlyEqual(newTransform, selected.Transform)) return;

            string partId = selected.Id;
            MutateDefinition("Edit Transform", definition => definition.FindPart(partId).Transform = newTransform);
        }

        private static Vector3 ClampToBounds(Vector3 position, BoundsDefinition bounds)
        {
            return new Vector3(
                Mathf.Clamp(position.x, -bounds.MaxX, bounds.MaxX),
                Mathf.Clamp(position.y, -bounds.MaxY, bounds.MaxY),
                Mathf.Clamp(position.z, -bounds.MaxZ, bounds.MaxZ));
        }

        private static bool TransformsRoughlyEqual(TransformData a, TransformData b)
        {
            return Vector3.Distance(a.Position, b.Position) < 1e-6f
                   && Quaternion.Angle(a.Rotation, b.Rotation) < 1e-4f
                   && Vector3.Distance(a.Scale, b.Scale) < 1e-6f;
        }

        private void DrawShapeFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

            var newShape = new ShapeDefinition
            {
                Type = (ShapeType)EditorGUILayout.EnumPopup("Shape Type", selected.Shape.Type),
                PrimarySize = EditorGUILayout.FloatField("Primary Size", selected.Shape.PrimarySize),
                SmoothBlendRadius = EditorGUILayout.FloatField("Smooth Blend Radius", selected.Shape.SmoothBlendRadius),
            };

            if (newShape.Equals(selected.Shape)) return;

            string partId = selected.Id;
            MutateDefinition("Edit Shape", definition => definition.FindPart(partId).Shape = newShape);
        }

        private void DrawAppearanceFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);

            var newAppearance = new AppearanceDefinition
            {
                BaseColor = EditorGUILayout.ColorField("Base Color", selected.Appearance.BaseColor),
                NoiseSeed = EditorGUILayout.IntField("Noise Seed", selected.Appearance.NoiseSeed),
                NoiseScale = EditorGUILayout.FloatField("Noise Scale", selected.Appearance.NoiseScale),
            };

            if (newAppearance.Equals(selected.Appearance)) return;

            string partId = selected.Id;
            MutateDefinition("Edit Appearance", definition => definition.FindPart(partId).Appearance = newAppearance);
        }

        private void DrawSymmetryFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Symmetry", EditorStyles.boldLabel);

            SymmetryMode newMode = (SymmetryMode)EditorGUILayout.EnumPopup("Creature Symmetry Mode", _definition.SymmetryMode);
            if (newMode != _definition.SymmetryMode)
            {
                MutateDefinition("Change Symmetry Mode", definition => definition.SymmetryMode = newMode);
            }

            bool newMirrorFlag = EditorGUILayout.Toggle("Mirror This Part", selected.MirrorAcrossSymmetryPlane);
            if (newMirrorFlag == selected.MirrorAcrossSymmetryPlane) return;

            if (newMirrorFlag && _definition.SymmetryMode == SymmetryMode.None)
            {
                EditorGUILayout.HelpBox(
                    "Creature Symmetry Mode is None — this flag has no visible effect until you " +
                    "set a symmetry mode above.", MessageType.Warning);
            }

            // Mirroring does NOT cascade to children — see SkeletonInferrer's
            // class doc comment. Flagging a parent here does not flag its
            // children; each part's flag is independent.
            string partId = selected.Id;
            MutateDefinition("Toggle Mirror", definition => definition.FindPart(partId).MirrorAcrossSymmetryPlane = newMirrorFlag);
        }

        // ---- validation panel -------------------------------------------------------------------

        private void DrawValidationPanel()
        {
            _showValidationPanel = EditorGUILayout.Foldout(
                _showValidationPanel, $"Validation ({_validation.Issues.Count} issue(s))");
            if (!_showValidationPanel) return;

            if (_validation.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues.", MessageType.Info);
                return;
            }

            _validationScroll = EditorGUILayout.BeginScrollView(_validationScroll, GUILayout.MaxHeight(120));
            foreach (ValidationIssue issue in _validation.Issues)
            {
                MessageType messageType = issue.Severity switch
                {
                    ValidationSeverity.Error => MessageType.Error,
                    ValidationSeverity.Warning => MessageType.Warning,
                    _ => MessageType.Info,
                };
                EditorGUILayout.HelpBox(issue.ToString(), messageType);
            }
            EditorGUILayout.EndScrollView();
        }

        // ---- viewport interaction -------------------------------------------------------------------

        private void OnSceneGUI(SceneView sceneView)
        {
            // Body sample handles are the Body's scene editing surface (CC-015).
            // They are suppressed in Place Part Mode so mesh clicks keep placing parts.
            if (_selectedPartId == CreatureDefinition.BodyId && !_placementModeActive)
            {
                DrawBodySampleHandles();
                return;
            }

            if (_bodyDragIndex >= 0) CancelBodyDrag(); // left Body selection mid-gesture

            DrawSelectedPartHandle();
            HandlePlacementClick();
        }

        private void DrawSelectedPartHandle()
        {
            if (_selectedPartId == null) return;

            CreaturePart selected = _definition.FindPart(_selectedPartId);
            if (selected == null) return;

            Matrix4x4 worldMatrix;
            try
            {
                worldMatrix = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(_definition, selected);
            }
            catch (DomainException)
            {
                // Invalid parent chain (already surfaced as an Error in the
                // validation panel) — nothing sensible to draw a handle for.
                return;
            }

            Vector3 worldPosition = worldMatrix.GetColumn(3);
            Quaternion worldRotation = worldMatrix.rotation;

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyViewportMove(selected, newWorldPosition);
            }
        }

        /// <summary>
        /// Spore-like Body sample editing (CC-015/CC-016). Each sample draws a
        /// clickable sphere cap (endpoints are larger to signal "length handle");
        /// the active sample gets a position handle.
        ///
        /// CC-016 replaces the FABRIK rigid-chain drag with BodyEditSolver: an
        /// interior sample drag is a local BEND (the selected sample dominates,
        /// neighbors resist with distance-based movement weights), an endpoint
        /// drag is a LENGTH edit, and every mouse frame solves from the mouse-down
        /// snapshot (never the previous frame, so long drags cannot drift).
        ///
        /// The definition is NOT mutated during the drag — the solved spline is
        /// drawn as a transient preview and the preview mesh is untouched. On
        /// release the whole gesture commits as exactly one mutation (one drag =
        /// one Undo); Esc cancels back to the snapshot with no Undo entry. The
        /// mesh regenerates only after the commit through the throttled auto-regen
        /// scheduler, so the solver stays interactive even when mesh generation
        /// lags (CC-008). Positions are DNA (the Body owns the creature frame),
        /// never preview mesh data.
        /// </summary>
        private void DrawBodySampleHandles()
        {
            BodySpline spline = _definition.Body;
            if (spline == null || spline.Samples == null || spline.Samples.Count == 0) return;

            // An in-flight drag first checks for release (commit) and cancel (Esc)
            // so the very last drag target is captured. Committing is idempotent.
            if (_bodyDragIndex >= 0)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CancelBodyDrag();
                    return;
                }
                if ((Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    || GUIUtility.hotControl == 0)
                {
                    // MouseUp delivered directly, or the position handle released
                    // hot control (the drag ended) — either way, commit.
                    CommitBodyDrag();
                }
            }

            for (int i = 0; i < spline.Samples.Count; i++)
            {
                if (i == _activeBodySampleIndex) continue; // the position handle draws there instead
                BodySample sample = spline.Samples[i];
                if (sample == null) continue;

                // During an active drag the neighbors are drawn at the solved
                // preview positions so the whole neighborhood follows the edit.
                Vector3 drawPosition = sample.Position;
                if (_bodyDragPreview != null && i < _bodyDragPreview.Length)
                {
                    drawPosition = _bodyDragPreview[i];
                }

                bool isEndpoint = i == 0 || i == spline.Samples.Count - 1;
                float handleSize = HandleUtility.GetHandleSize(drawPosition) * (isEndpoint ? 0.16f : 0.12f);
                if (Handles.Button(drawPosition, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
                {
                    if (_bodyDragIndex >= 0 && _bodyDragIndex != i) CancelBodyDrag();
                    _activeBodySampleIndex = i;
                    Repaint();
                }
            }

            if (_activeBodySampleIndex < 0 || _activeBodySampleIndex >= spline.Samples.Count) return;
            BodySample active = spline.Samples[_activeBodySampleIndex];
            if (active == null) return;

            bool activeIsEndpoint = _activeBodySampleIndex == 0 || _activeBodySampleIndex == spline.Samples.Count - 1;

            // The active handle rides the solved preview position during a drag so
            // the grabbed sample visibly follows the cursor.
            Vector3 handlePosition = active.Position;
            if (_bodyDragIndex == _activeBodySampleIndex && _bodyDragPreview != null)
            {
                handlePosition = _bodyDragPreview[_activeBodySampleIndex];
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(handlePosition, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                // Do not start a gesture on the release frame (the handle reports
                // a final change on MouseUp; a real drag starts on MouseDrag).
                if (_bodyDragIndex != _activeBodySampleIndex && Event.current.type != EventType.MouseUp)
                {
                    // Drag just started: freeze the mouse-down spline. Endpoints
                    // are length edits; interior samples are bends.
                    _bodyDragIndex = _activeBodySampleIndex;
                    _bodyDragKind = activeIsEndpoint ? BodyEditKind.EndpointLength : BodyEditKind.InteriorBend;
                    _bodyDragSnapshot = CopyBodyPositions(spline);
                    _bodyDragPreview = null;
                }
                _bodyDragFinalTarget = newPosition;
                _bodyDragPreview = SolveBodyDrag(_bodyDragIndex, _bodyDragFinalTarget);
                SceneView.RepaintAll();
            }

            // Draw the transient solved-spline preview (repaint only).
            if (Event.current.type == EventType.Repaint && _bodyDragPreview != null)
            {
                DrawBodyEditPreview(_bodyDragPreview);
            }
        }

        /// <summary>
        /// Solves one mouse frame from the frozen mouse-down snapshot (CC-016).
        /// Endpoints are length edits; interior samples are bends.
        /// </summary>
        private Vector3[] SolveBodyDrag(int index, Vector3 target)
        {
            if (_bodyDragSnapshot == null || index < 0 || index >= _bodyDragSnapshot.Length)
            {
                return _bodyDragSnapshot;
            }
            BodyEditResult result = _bodyDragKind == BodyEditKind.EndpointLength
                ? BodyEditSolver.SolveEndpointDrag(_bodyDragSnapshot, index, target)
                : BodyEditSolver.SolveInteriorDrag(_bodyDragSnapshot, index, target);
            return result.Positions;
        }

        /// <summary>
        /// Cheap transient preview of the solved spline. The definition and the
        /// preview mesh are NOT touched during the drag; the mesh regenerates once
        /// after the mouse-up commit.
        /// </summary>
        private void DrawBodyEditPreview(Vector3[] preview)
        {
            if (preview == null || preview.Length < 2) return;

            Handles.color = new Color(0.20f, 0.85f, 0.55f, 0.85f);
            for (int i = 1; i < preview.Length; i++)
            {
                Handles.DrawLine(preview[i - 1], preview[i]);
            }
            Handles.color = Color.white;
            for (int i = 0; i < preview.Length; i++)
            {
                float size = HandleUtility.GetHandleSize(preview[i]) * 0.09f;
                Handles.SphereHandleCap(i, preview[i], Quaternion.identity, size, EventType.Repaint);
            }
        }

        /// <summary>
        /// One whole drag commits exactly one mutation (one Undo). The definition
        /// was never mutated during the drag, so this is the single canonical
        /// write of the edited positions, flowed through the normal validation /
        /// Undo / session / auto-regen path.
        /// </summary>
        private void CommitBodyDrag()
        {
            if (_bodyDragIndex < 0) return; // no active gesture (idempotent)

            int index = _bodyDragIndex;
            BodyEditKind kind = _bodyDragKind;
            Vector3[] snapshot = _bodyDragSnapshot;
            Vector3 target = _bodyDragFinalTarget;

            // Clear gesture state BEFORE mutating so re-entrant GUI cannot
            // double-commit.
            _bodyDragIndex = -1;
            _bodyDragKind = BodyEditKind.InteriorBend;
            _bodyDragSnapshot = null;
            _bodyDragPreview = null;

            if (snapshot == null || index < 0 || index >= snapshot.Length) return;
            if (snapshot.Length != _definition.Body.Samples.Count) return; // definition changed mid-gesture; drop the stale edit

            // Deterministic re-solve from the frozen snapshot (identical to the
            // last preview frame by construction) to get full diagnostics.
            BodyEditResult final = kind == BodyEditKind.EndpointLength
                ? BodyEditSolver.SolveEndpointDrag(snapshot, index, target)
                : BodyEditSolver.SolveInteriorDrag(snapshot, index, target);

            if (_logGenerationDiagnostics)
            {
                Debug.Log(
                    $"[CreatureCreator] Body drag: selected displacement = {final.SelectedDisplacement:F3}, " +
                    $"max neighbor displacement = {final.MaxNeighborDisplacement:F3}, " +
                    $"arc length delta = {final.ArcLengthDelta:F3}, " +
                    $"min segment ratio = {final.MinSegmentRatio:F3}, " +
                    $"max curvature = {final.MaxCurvatureDegrees:F1}°");
            }

            MutateDefinition(kind == BodyEditKind.EndpointLength ? "Drag Body Endpoint (Viewport)" : "Drag Body Sample (Viewport)",
                definition =>
                {
                    BodySpline targetSpline = definition.Body;
                    for (int j = 0; j < final.Positions.Length && j < targetSpline.Samples.Count; j++)
                    {
                        targetSpline.Samples[j].Position = final.Positions[j];
                    }

                    // Repair/normalize only after the edit, as needed (CC-016): the
                    // solver preserves segment lengths softly, so the committed
                    // spline may be uneven. SpaceEvenly rides the edited polyline
                    // and re-snaps even chords, preserving the edited shape while
                    // keeping the committed definition valid for preview and save.
                    // (The future authored-controls / derived-evaluation-samples
                    // split is a separate schema decision, not part of CC-016.)
                    if (HasUnevenBodySpacing(definition))
                    {
                        BodySplineAuthoring.SpaceEvenly(targetSpline);
                    }
                });

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Esc during a drag: the definition was never mutated, so cancelling is
        /// just dropping the gesture state — no Undo entry is created.
        /// </summary>
        private void CancelBodyDrag()
        {
            _bodyDragIndex = -1;
            _bodyDragKind = BodyEditKind.InteriorBend;
            _bodyDragSnapshot = null;
            _bodyDragPreview = null;
            SceneView.RepaintAll();
        }

        private static Vector3[] CopyBodyPositions(BodySpline spline)
        {
            var positions = new Vector3[spline.Samples.Count];
            for (int i = 0; i < spline.Samples.Count; i++)
            {
                positions[i] = spline.Samples[i].Position;
            }
            return positions;
        }

        /// <summary>
        /// True when the spline violates DefinitionValidator's even-spacing
        /// invariant (UnevenBodySpacing). Uses the real validator so the drag
        /// repair decision cannot drift from what validation will report.
        /// </summary>
        private static bool HasUnevenBodySpacing(CreatureDefinition definition)
        {
            return DefinitionValidator.Validate(definition).Issues
                .Any(issue => issue.Code == ValidationCode.UnevenBodySpacing);
        }

        private void ApplyViewportMove(CreaturePart selected, Vector3 newWorldPosition)
        {
            string partId = selected.Id;
            Vector3 newLocalPosition = WorldToLocalPosition(newWorldPosition, selected.ParentId);
            Vector3 clampedLocalPosition = ClampToBounds(newLocalPosition, _definition.Bounds);

            TransformData newTransform = selected.Transform;
            newTransform.Position = clampedLocalPosition;

            // Same known granularity limitation as continuous inspector-field
            // drags (see class doc comment): this fires once per GUI frame the
            // handle moves during a drag, so one drag currently produces many
            // undo steps rather than one.
            MutateDefinition("Move Part (Viewport)", definition => definition.FindPart(partId).Transform = newTransform);

            Repaint(); // the main window doesn't auto-repaint from a SceneView-driven change
        }

        private void HandlePlacementClick()
        {
            if (!_placementModeActive) return;
            if (_previewGameObject == null) return;

            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (e.alt) return; // let Alt+Click camera orbit pass through untouched

            MeshCollider collider = _previewGameObject.GetComponent<MeshCollider>();
            if (collider == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!collider.Raycast(ray, out RaycastHit hit, 1000f)) return;

            // Place Part Mode snaps the selected part to the hit point (CC-015).
            // With no part selected it falls back to creating a new part there.
            CreaturePart selected = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId
                ? _definition.FindPart(_selectedPartId)
                : null;
            if (selected != null)
            {
                ApplyViewportMove(selected, hit.point);
            }
            else
            {
                PlaceNewPartAtWorldPosition(hit.point);
            }

            e.Use(); // consume the click so it doesn't also (de)select scene objects
        }

        private void PlaceNewPartAtWorldPosition(Vector3 worldPosition)
        {
            string parentId = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId
                ? _selectedPartId
                : CreatureDefinition.BodyId;
            Vector3 localPosition = WorldToLocalPosition(worldPosition, parentId, out bool parentResolvedSuccessfully);
            if (!parentResolvedSuccessfully) parentId = CreatureDefinition.BodyId; // fall back to the Body root rather than dropping the click

            Vector3 clampedPosition = ClampToBounds(localPosition, _definition.Bounds);
            string newId = PartIdGenerator.CreateNew();
            string finalParentId = parentId;

            MutateDefinition("Place Part (Viewport)", definition => definition.AddPart(new CreaturePart
            {
                Id = newId,
                ParentId = finalParentId,
                PartType = PartType.Limb,
                DisplayName = "Limb",
                Transform = new TransformData { Position = clampedPosition, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            }));

            _selectedPartId = newId;
            _activeBodySampleIndex = -1;
            Repaint();
        }

        /// <summary>
        /// Converts a creature-space (world) position into the parent-relative
        /// local position CreaturePart.Transform.Position actually stores, by
        /// inverting the parent's resolved world matrix — see the class doc
        /// comment's note on why this conversion exists at all (viewport
        /// interaction is world-space; DNA storage is parent-local).
        /// </summary>
        private Vector3 WorldToLocalPosition(Vector3 worldPosition, string parentId)
        {
            return WorldToLocalPosition(worldPosition, parentId, out _);
        }

        private Vector3 WorldToLocalPosition(Vector3 worldPosition, string parentId, out bool succeeded)
        {
            succeeded = true;

            // The Body owns the creature frame; a Body-child's local position is
            // already creature-space (the Body spline itself defines the origin).
            if (parentId == null || parentId == CreatureDefinition.BodyId) return worldPosition;

            CreaturePart parentPart = _definition.FindPart(parentId);
            if (parentPart == null)
            {
                succeeded = false;
                return worldPosition;
            }

            try
            {
                Matrix4x4 parentWorld = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(_definition, parentPart);
                return parentWorld.inverse.MultiplyPoint3x4(worldPosition);
            }
            catch (DomainException)
            {
                succeeded = false;
                return worldPosition;
            }
        }

        // ---- preview generation -------------------------------------------------------------------

        private void RegeneratePreview()
        {
            if (!_validation.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "Cannot Generate", "The current creature has validation errors. Fix them before generating a preview.", "OK");
                return;
            }

            var diagnostics = new GenerationDiagnostics(_logGenerationDiagnostics);
            try
            {
                CreatureDefinition generationDefinition = _definition.Clone();
                generationDefinition.Generation.VoxelsPerUnit = _previewVoxelsPerUnit;
                MeshTopologyReport topologyReport = null;
                Mesh unityMesh = CreatureMeshGenerator.Generate(
                    generationDefinition, out topologyReport, diagnostics, _usePortableSampling);
                ApplyPreviewMesh(unityMesh);
                _autoRegenerateAt = -1d;

                if (!topologyReport.IsWatertight)
                {
                    Debug.LogWarning(
                        $"[CreatureCreator] Generated mesh is not watertight: " +
                        $"{topologyReport.BoundaryEdgeCount} boundary edge(s), " +
                        $"{topologyReport.NonManifoldEdgeCount} non-manifold edge(s) out of " +
                        $"{topologyReport.TotalEdgeCount} total. See MeshTopologyValidator.");
                }

                if (_logGenerationDiagnostics)
                {
                    string timingReport = string.Join("\n",
                        diagnostics.Timings.Select(FormatDiagnosticTiming));
                    Debug.Log(
                        $"[CreatureCreator] Preview regenerated — " +
                        $"{unityMesh.triangles.Length / 3} triangles, " +
                        $"{unityMesh.vertexCount} vertices, " +
                        $"SDF Sampling: {(_usePortableSampling ? "Burst" : "Managed")}, " +
                        $"grid {diagnostics.GridCellsX}x{diagnostics.GridCellsY}x{diagnostics.GridCellsZ} " +
                        $"({diagnostics.GridSampleCount:N0} samples), " +
                        $"{diagnostics.MixedCellCount:N0} mixed cells, " +
                        $"{diagnostics.GradientEvaluationCount:N0} gradient evaluations.\n" +
                        $"  TotalGeneration: {diagnostics.TotalTime.TotalMilliseconds:F1}ms\n" +
                        timingReport);
                }
            }
            catch (DomainException ex)
            {
                string validationDetails = string.Join(
                    "\n", _validation.Issues.Select(issue => issue.Message));
                EditorUtility.DisplayDialog(
                    "Generation Failed",
                    $"Stage: {diagnostics.FailedStage}\n{ex.Message}\n\n{validationDetails}",
                    "OK");
            }
        }

        private static string FormatDiagnosticTiming(StageTiming timing)
        {
            bool isMeshSubtiming = timing.Stage == GenerationStage.MeshActiveCellConstruction
                                    || timing.Stage == GenerationStage.MeshContourResolution
                                    || timing.Stage == GenerationStage.MeshVertexWelding
                                    || timing.Stage == GenerationStage.MeshTriangleEmission;
            string indentation = isMeshSubtiming ? "    " : "  ";
            return $"{indentation}{timing.Stage}: {timing.Elapsed.TotalMilliseconds:F1}ms";
        }

        private void ApplyPreviewMesh(Mesh mesh)
        {
            // The _previewGameObject field reference is lost across a domain
            // reload; look the object up by name first so re-generating after a
            // recompile updates the existing preview instead of creating a
            // second one. OnEnable does the same lookup so "Place Part Mode"
            // correctly re-enables itself after a reload too, without requiring
            // an unnecessary re-regenerate first.
            if (_previewGameObject == null)
            {
                _previewGameObject = GameObject.Find(PreviewObjectName);
            }

            if (_previewGameObject == null)
            {
                _previewGameObject = new GameObject(PreviewObjectName);
                _previewGameObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = _previewGameObject.AddComponent<MeshRenderer>();
                Material material = CreateDefaultPreviewMaterial();
                if (material != null) renderer.sharedMaterial = material;
                _previewGameObject.AddComponent<MeshCollider>();
            }

            _previewGameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer previewRenderer = _previewGameObject.GetComponent<MeshRenderer>();
            if (previewRenderer == null) previewRenderer = _previewGameObject.AddComponent<MeshRenderer>();
            if (previewRenderer.sharedMaterial == null)
            {
                Material material = CreateDefaultPreviewMaterial();
                if (material != null) previewRenderer.sharedMaterial = material;
            }

            // MeshCollider needs sharedMesh reassigned (not just relying on the
            // same Mesh object being mutated) to pick up topology changes —
            // ToUnityMesh() always returns a brand-new Mesh each regenerate, so
            // this reassignment happens naturally every time.
            MeshCollider collider = _previewGameObject.GetComponent<MeshCollider>();
            if (collider == null) collider = _previewGameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private static Material CreateDefaultPreviewMaterial()
        {
            Shader shader = Shader.Find("Standard")
                             ?? Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[CreatureCreator] No default shader found; preview mesh will use Unity's fallback material.");
                return null;
            }
            return new Material(shader);
        }

        private static string GetPartLabel(CreaturePart part)
        {
            string displayName = string.IsNullOrWhiteSpace(part.DisplayName) ? part.Id : part.DisplayName;
            return $"{displayName} ({part.Id})";
        }

        private void ScheduleAutoRegeneration()
        {
            if (!_autoRegenerate) return;
            _autoRegenerateAt = EditorApplication.timeSinceStartup + _autoRegenerationDelaySeconds;
        }

        private void ProcessAutoRegeneration()
        {
            if (!_autoRegenerate || _autoRegenerateAt < 0d) return;
            if (EditorApplication.timeSinceStartup < _autoRegenerateAt) return;

            _autoRegenerateAt = -1d;
            RegeneratePreview();
        }
    }
}
