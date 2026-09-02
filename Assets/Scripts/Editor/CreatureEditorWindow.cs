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
// Alias: the namespace and its own Skeleton type share the same identifier, so
// the unqualified name resolves to the namespace. Alias to keep both usable.
using CreatureSkeleton = ProceduralCreature.Skeleton;

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
        private int _bodyRadiusDragIndex = -1;
        private float _bodyRadiusDragStartRadius = 1f;
        private float _bodyRadiusDragTargetRadius = 1f;

        private Vector2 _partListScroll;
        private Vector2 _bodySampleScroll;
        // CC-018 Phase 7: scroll state for the limb joint list in the inspector
        // (presentation state, never DNA), plus the viewport joint-drag gesture.
        // The gesture follows the CC-016 body-drag discipline: snapshot on
        // mouse-down, transient preview during the drag (definition untouched),
        // exactly ONE MutateDefinition on release (one drag = one Undo), Esc
        // cancels with no mutation. Joints are free points — no FABRIK, no
        // constraint solver; the commit clamps to the creature bounds and lets
        // DefinitionValidator flag min-separation.
        private Vector2 _limbJointScroll;
        private int _limbDragJointIndex = -1;
        private Vector3 _limbDragSnapshotLocal;
        private Vector3 _limbDragFinalTargetLocal;
        // CC-020: parts-tree expansion + Body inspector foldout state. This is
        // editor presentation state, never creature DNA. ExpandedPartIds is
        // persisted via SessionState so it survives selection, regeneration,
        // undo/redo, inspector changes, and domain reloads; the Body inspector
        // foldouts are session-scoped only (not persisted).
        private readonly HashSet<string> _expandedPartIds = new HashSet<string>();
        private bool _bodyShowGeneral = true;
        private bool _bodyShowSpline = true;
        private bool _bodyShowAppearance = true;
        private bool _bodyShowAdvanced;
        // CC-020 rev 2: active sibling-ordering strategy for the parts tree.
        // Presentation only — it never affects DNA, validation, or serialization.
        private readonly IPartSiblingOrderer _partSiblingOrder = PartSiblingOrderers.Alphabetical;
        private string _revealScrollId;
        private Rect _partListScrollViewRect;
        private Vector2 _validationScroll;
        private bool _showValidationPanel = true;
        private GameObject _previewGameObject;
        private readonly List<GameObject> _previewGeometryObjects = new List<GameObject>();
        private CreatureUndoState _undoState;
        private bool _placementModeActive;
        // CC-007: transient placement result shown under the Place Part Mode
        // HelpBox (clear editor result on miss/failure; editor state, never DNA).
        private string _placementFeedback;
        // CC-007 step 5: the Body fingerprint the last successful preview was
        // generated from (Body samples + Forward). Placement raycasts hit the
        // preview's MeshCollider, so when this no longer matches the live
        // definition the collider is stale and placement is blocked until the
        // user regenerates. Part edits do NOT change it — only the Body geometry
        // the placement anchor depends on.
        private string _previewBodyFingerprint;
        // CC-007 step 6: new-part placement DRAG gesture. Mouse-down on the Body
        // surface starts it (no part selected); a transient ghost follows the
        // cursor, projected onto the surface each frame through
        // TryProjectToAnchor + TryResolveSurfaceFrame (definition untouched);
        // release commits exactly ONE MutateDefinition through the same
        // button-driven placement path; Esc cancels with no mutation (the CC-016
        // gesture discipline). A plain click still places: down starts the
        // gesture, up commits at the same point.
        private int _placementDragControlId = -1;
        private bool _placementDragActive;
        private bool _placementDragHasValidFrame;
        private Vector3 _placementDragSurfacePosition;
        private Quaternion _placementDragSurfaceRotation;
        private bool _autoRegenerate;
        private bool _showEditorSettings;
        private float _autoRegenerationDelaySeconds = 1f;
        private float _previewVoxelsPerUnit = 16f;
        // CC-066: skeleton display mode — editor presentation state, persisted via
        // EditorPrefs, never DNA. Draws a read-only SceneView overlay of the
        // inferred rest Skeleton (bones as lines, joints as caps).
        private bool _showSkeleton;
        private CreatureGenerationConfig _generationConfig;
        private bool _logGenerationDiagnostics = true;
        private double _autoRegenerateAt = -1d;
        private CreatureDefinition _transientPreviewDefinition;
        private string _currentFilePath;
        private CreatureGenerationScheduler _generationScheduler;

        private static readonly IDnaSerializer Serializer = new JsonDnaSerializer();
        private const string PreviewObjectName = "CreatureCreator Preview";
        private const string PreviewGeometryChildPrefix = "CreatureCreator Preview Geometry ";
        private const float MinimumAutoRegenerationDelaySeconds = 0.01f;
        private const string AutoRegenerationDelayKey = "ProceduralCreature.AutoRegenerationDelay";
        private const string PreviewVoxelsPerUnitKey = "ProceduralCreature.PreviewVoxelsPerUnit";
        // CC-066: skeleton display mode persistence key.
        private const string SkeletonDisplayKey = "ProceduralCreature.ShowSkeleton";
        private const string GenerationConfigKey = "ProceduralCreature.GenerationConfig";
        private const string LogGenerationDiagnosticsKey = "ProceduralCreature.LogGenerationDiagnostics";
        private const string CurrentFilePathKey = "ProceduralCreature.CurrentFilePath";
        private const string ExpandedPartIdsKey = "ProceduralCreature.ExpandedPartIds";
        // CC-020 rev 2: tree rows use a fixed-width arrow slot so every level
        // indents by exactly TreeIndentWidth and children always render to the
        // RIGHT of their parent (the previous EditorGUILayout.Foldout was wider
        // than the leaf spacer, which pushed child labels left of their parents).
        private const float TreeIndentWidth = 16f;
        private const string CollapsedArrowGlyph = "\u25B6"; // ▶
        private const string ExpandedArrowGlyph = "\u25BC"; // ▼
        private const float BodySampleScrollMaxHeight = 220f;

        // CC-066: skeleton overlay color — warm amber, distinct from the white
        // body handles and the bluish limb-chain preview.
        private static readonly Color SkeletonOverlayColor = new Color(1f, 0.8f, 0.2f, 0.9f);

        /// <summary>
        /// Part types that are valid to author in schema v2. Body, Root, and
        /// independent Tail are reserved by the validator and must not be offered.
        /// Part is the generic default for a new part; Eye is authored like any
        /// other part (typically on a head or the Body).
        /// </summary>
        private static readonly PartType[] ValidV2PartTypes =
        {
            PartType.Part,
            PartType.Limb,
            PartType.Leg,
            PartType.Arm,
            PartType.Foot,
            PartType.Hand,
            PartType.Eye,
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
            _showSkeleton = EditorPrefs.GetBool(SkeletonDisplayKey, false);
            string generationConfigPath = EditorPrefs.GetString(GenerationConfigKey, string.Empty);
            if (!string.IsNullOrEmpty(generationConfigPath))
            {
                _generationConfig = AssetDatabase.LoadAssetAtPath<CreatureGenerationConfig>(generationConfigPath);
            }
            _logGenerationDiagnostics = EditorPrefs.GetBool(LogGenerationDiagnosticsKey, true);
            _currentFilePath = SessionState.GetString(CurrentFilePathKey, string.Empty);
            LoadExpandedPartIds();

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += ProcessAutoRegeneration;

            _previewGameObject = GameObject.Find(PreviewObjectName);
            _generationScheduler = new CreatureGenerationScheduler();
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

            _generationScheduler?.Dispose();
            _generationScheduler = null;

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

            // CC-020: expansion state is keyed by stable part Ids; drop ids that
            // no longer exist after an undo/redo restores a different definition.
            PruneExpandedPartIds();

            Revalidate();
            CreatureEditorSession.Save(_definition);
            if (_autoRegenerate)
            {
                ScheduleAutoRegeneration();
            }
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
                    "Placement raycasts against the mesh from the last 'Regenerate Preview', so it is blocked " +
                    "when the Body has changed since then — regenerate first.",
                    MessageType.Info);
                if (IsPreviewStale())
                {
                    EditorGUILayout.HelpBox(
                        "Preview is stale: the Body changed since the last 'Regenerate Preview'. " +
                        "Regenerate before placing parts.",
                        MessageType.Warning);
                }
                if (!string.IsNullOrEmpty(_placementFeedback))
                {
                    EditorGUILayout.HelpBox(_placementFeedback, MessageType.None);
                }
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
            if (working.Body != null && working.Body.Samples != null)
            {
                BodySplineAuthoring.RenumberSamplesInOrder(working.Body);
            }
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

            // CC-020: expansion state is editor presentation state, never DNA;
            // prune ids that no longer exist (part removed, definition replaced).
            PruneExpandedPartIds();

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
                _placementFeedback = null;
                if (!newPlacementMode) CancelPlacementDrag(); // leave a mid-drag gesture cleanly
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

            // CC-074: the surface material comes from the assigned Generation
            // Config's material palette default — no standalone preview material.
            CreatureGenerationConfig newGenerationConfig = (CreatureGenerationConfig)EditorGUILayout.ObjectField(
                "Generation Config", _generationConfig, typeof(CreatureGenerationConfig), allowSceneObjects: false);
            if (newGenerationConfig != _generationConfig)
            {
                _generationConfig = newGenerationConfig;
                EditorPrefs.SetString(
                    GenerationConfigKey,
                    _generationConfig != null ? AssetDatabase.GetAssetPath(_generationConfig) : string.Empty);
                Repaint();
            }

            if (EffectiveMeshPalette != null && EffectiveMeshPalette.HasDuplicateKeys(out string duplicateKey))
            {
                EditorGUILayout.HelpBox(
                    $"Mesh palette contains duplicate key '{duplicateKey}'. Remove the duplicate before generating.",
                    MessageType.Error);
            }

            if (EffectiveMaterialPalette != null && EffectiveMaterialPalette.HasDuplicateKeys(out string duplicateMaterialKey))
            {
                EditorGUILayout.HelpBox(
                    $"Material palette contains duplicate key '{duplicateMaterialKey}'. Remove the duplicate before generating.",
                    MessageType.Error);
            }

            float newQuality = Mathf.Max(1f, EditorGUILayout.FloatField("Preview Mesh Quality", _previewVoxelsPerUnit));
            if (!Mathf.Approximately(newQuality, _previewVoxelsPerUnit))
            {
                _previewVoxelsPerUnit = newQuality;
                EditorPrefs.SetFloat(PreviewVoxelsPerUnitKey, _previewVoxelsPerUnit);
                ScheduleAutoRegeneration();
            }

            // CC-066: skeleton display mode toggle (editor presentation state, persisted).
            bool newShowSkeleton = EditorGUILayout.Toggle("Show Skeleton", _showSkeleton);
            if (newShowSkeleton != _showSkeleton)
            {
                _showSkeleton = newShowSkeleton;
                EditorPrefs.SetBool(SkeletonDisplayKey, _showSkeleton);
                SceneView.RepaintAll();
            }
            if (_showSkeleton)
            {
                EditorGUILayout.HelpBox(
                    "Read-only overlay of the inferred rest skeleton (bones + joints). It never edits the definition.",
                    MessageType.Info);
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
            foreach (CreaturePart child in OrderedChildrenOf(CreatureDefinition.BodyId))
            {
                DrawPartNode(child, depth: 1, visited);
            }

            // Parts whose ParentId chain does not reach the Body root (missing
            // parent, or a cycle) are shown explicitly so the user can reparent or
            // remove them instead of the part silently vanishing from the tree. The
            // validator reports the underlying error.
            //
            // CC-020 fix: reachability is computed from the PARENT GRAPH, never from
            // the renderer's visited set. A collapsed node stops recursing, so a
            // renderer-derived set would misclassify its hidden descendants as
            // unparented — the "children jump to Unparented when I collapse" bug.
            HashSet<string> reachable = ReachableFromBody(_definition);
            IEnumerable<CreaturePart> orphans = _partSiblingOrder.OrderSiblings(
                _definition.Parts.Where(p => p != null && !reachable.Contains(p.Id)));
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

            // CC-036: capture the scroll view's viewport rect AFTER the scroll
            // group ends. GUILayoutUtility.GetLastRect immediately after
            // BeginScrollView is invalid ("You cannot call GetLast immediately
            // after beginning a group"), so it is read here, at a legal layout
            // boundary, and consumed by RevealScrollIfTarget on a later frame.
            _partListScrollViewRect = GUILayoutUtility.GetLastRect();

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
                SelectPart(CreatureDefinition.BodyId); // start with no sample grabbed
            }
        }

        private void DrawPartNode(CreaturePart part, int depth, HashSet<string> visited)
        {
            if (!visited.Add(part.Id)) return; // parent cycle: stop descending, the validator flags it

            bool hasChildren = ChildrenOf(part.Id).Any();
            bool isExpanded = _expandedPartIds.Contains(part.Id);
            bool isSelected = part.Id == _selectedPartId;

            // CC-020 rev 2: one explicit row per node. A fixed-width arrow button
            // (expansion only) occupies the SAME slot width as the leaf spacer, so
            // every level indents by exactly TreeIndentWidth and a child's label
            // always renders to the RIGHT of its parent's label. The selectable
            // label follows: a plain click selects without toggling expansion, and
            // the arrow toggles expansion without changing selection.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * TreeIndentWidth);

            if (hasChildren)
            {
                string arrow = isExpanded ? ExpandedArrowGlyph : CollapsedArrowGlyph;
                if (GUILayout.Button(arrow, EditorStyles.label, GUILayout.Width(TreeIndentWidth)))
                {
                    SetPartExpanded(part.Id, !isExpanded);
                }
            }
            else
            {
                GUILayout.Space(TreeIndentWidth); // align leaf labels with their expanded siblings
            }

            bool nowSelected = GUILayout.Toggle(isSelected, $"{part.PartType}  {GetPartLabel(part)}", EditorStyles.toolbarButton);
            if (nowSelected && !isSelected) SelectPart(part.Id);
            EditorGUILayout.EndHorizontal();

            RevealScrollIfTarget(part.Id);

            if (!hasChildren || !isExpanded) return;
            foreach (CreaturePart child in OrderedChildrenOf(part.Id))
            {
                DrawPartNode(child, depth + 1, visited);
            }
        }

        private IEnumerable<CreaturePart> ChildrenOf(string parentId)
        {
            return _definition.Parts.Where(p => p.ParentId == parentId);
        }

        /// <summary>Sibling parts in the tree's active ordering (strategy).</summary>
        private IEnumerable<CreaturePart> OrderedChildrenOf(string parentId)
        {
            return _partSiblingOrder.OrderSiblings(ChildrenOf(parentId));
        }

        // ---- CC-020: parts-tree expansion state and selection ------------------------

        /// <summary>
        /// CC-020: the single place a selection change happens so the parts tree
        /// can auto-reveal a hidden descendant — selecting any part expands every
        /// collapsed ancestor, making the tree and viewport two views of the same
        /// model. Selection never toggles expansion, and the foldout triangle never
        /// changes selection (they are separate controls in DrawPartNode).
        /// </summary>
        private void SelectPart(string partId)
        {
            _selectedPartId = partId;
            _activeBodySampleIndex = -1;

            if (partId == null || _definition == null || partId == CreatureDefinition.BodyId) return;

            foreach (string ancestorId in AncestorsToReveal(_definition, partId))
            {
                SetPartExpanded(ancestorId, true);
            }
            _revealScrollId = partId;
        }

        /// <summary>
        /// CC-020: the ancestor chain (root-most first, excluding the always-visible
        /// Body) that must be expanded for targetId to be reachable from the Body
        /// root in the parts tree. Empty when targetId is the Body, unknown, or its
        /// parent chain is broken (a missing parent is separately flagged by the
        /// validator). Pure function so EditMode tests cover the auto-reveal
        /// contract directly.
        /// </summary>
        internal static IReadOnlyList<string> AncestorsToReveal(CreatureDefinition definition, string targetId)
        {
            if (definition == null || string.IsNullOrEmpty(targetId) || targetId == CreatureDefinition.BodyId)
            {
                return System.Array.Empty<string>();
            }
            CreaturePart target = definition.FindPart(targetId);
            if (target == null) return System.Array.Empty<string>();

            var chain = new List<string>();
            string parentId = target.ParentId;
            while (!string.IsNullOrEmpty(parentId) && parentId != CreatureDefinition.BodyId)
            {
                CreaturePart parent = definition.FindPart(parentId);
                if (parent == null) break; // broken chain; the validator reports it separately
                chain.Add(parentId);
                parentId = parent.ParentId;
            }
            chain.Reverse(); // root-most first
            return chain;
        }

        /// <summary>
        /// CC-020: the set of part ids reachable from the Body root by following
        /// ParentId links (transitive closure). Independent of tree collapse state,
        /// so a collapsed node never causes its descendants to be misclassified as
        /// unparented. A broken parent link or a cycle simply leaves the affected
        /// parts unreachable (the validator flags those separately); the walk is
        /// bounded because every iteration must add at least one new id.
        /// </summary>
        internal static HashSet<string> ReachableFromBody(CreatureDefinition definition)
        {
            var reachable = new HashSet<string> { CreatureDefinition.BodyId };
            if (definition == null || definition.Parts == null) return reachable;

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (CreaturePart part in definition.Parts)
                {
                    if (part == null || part.Id == null) continue;
                    if (reachable.Contains(part.Id)) continue;
                    if (part.ParentId != null && reachable.Contains(part.ParentId))
                    {
                        reachable.Add(part.Id);
                        changed = true;
                    }
                }
            }
            return reachable;
        }

        private void SetPartExpanded(string partId, bool expanded)
        {
            if (expanded) _expandedPartIds.Add(partId);
            else _expandedPartIds.Remove(partId);
            PersistExpandedPartIds();
        }

        private void PersistExpandedPartIds()
        {
            SessionState.SetString(ExpandedPartIdsKey, SerializeExpandedIds(_expandedPartIds));
        }

        private void LoadExpandedPartIds()
        {
            _expandedPartIds.Clear();
            foreach (string id in DeserializeExpandedIds(SessionState.GetString(ExpandedPartIdsKey, string.Empty)))
            {
                _expandedPartIds.Add(id);
            }
        }

        /// <summary>
        /// Removes expansion entries for parts that no longer exist, keeping the
        /// persisted presentation state tidy without ever touching the definition.
        /// </summary>
        private void PruneExpandedPartIds()
        {
            bool changed = false;
            foreach (string id in _expandedPartIds.ToList())
            {
                if (id != CreatureDefinition.BodyId && _definition.FindPart(id) == null)
                {
                    _expandedPartIds.Remove(id);
                    changed = true;
                }
            }
            if (changed) PersistExpandedPartIds();
        }

        /// <summary>
        /// CC-020: best-effort scroll-into-view for a just-selected node. Called
        /// immediately after the node's row is laid out (before its children
        /// recurse) so GetLastRect is still the node's own row. No-op when the row
        /// is already within the visible scroll region.
        /// </summary>
        private void RevealScrollIfTarget(string partId)
        {
            if (partId != _revealScrollId) return;
            _revealScrollId = null;

            if (_partListScrollViewRect.height <= 0f) return;
            Rect row = GUILayoutUtility.GetLastRect();
            float visibleTop = _partListScroll.y;
            float visibleBottom = _partListScroll.y + _partListScrollViewRect.height;
            if (row.y < visibleTop)
            {
                _partListScroll.y = row.y;
            }
            else if (row.yMax > visibleBottom)
            {
                _partListScroll.y = row.yMax - _partListScrollViewRect.height;
            }
        }

        /// <summary>
        /// SessionState persistence format for ExpandedPartIds: sorted, comma
        /// separated. Stable ordering keeps the persisted string deterministic.
        /// </summary>
        internal static string SerializeExpandedIds(IEnumerable<string> ids)
        {
            return string.Join(",",
                ids.Where(id => !string.IsNullOrEmpty(id))
                   .Distinct()
                   .OrderBy(id => id, System.StringComparer.Ordinal));
        }

        internal static HashSet<string> DeserializeExpandedIds(string raw)
        {
            var result = new HashSet<string>();
            if (string.IsNullOrEmpty(raw)) return result;
            foreach (string id in raw.Split(','))
            {
                if (!string.IsNullOrEmpty(id)) result.Add(id);
            }
            return result;
        }

        private void AddNewPart()
        {
            bool hasSelectedPart = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId;
            string parentId = hasSelectedPart ? _selectedPartId : CreatureDefinition.BodyId;

            // CC-029: with a non-Body part selected, seed the new child from the
            // selected part's authoring properties (Spore-like "duplicate as child");
            // otherwise keep the Body-rooted generic default.
            CreaturePart created = hasSelectedPart
                ? _definition.ClonePartAsChild(_selectedPartId, parentId)
                : NewGenericPart(parentId);

            // CC-018 (child-at-tip frame): a new child's identity local transform
            // already means "at the limb tip" when the parent is a limb — the
            // resolver gives children of a limb a local space whose origin is the
            // limb's terminal joint — so no explicit placement is needed.
            MutateDefinition("Add Part", definition => definition.AddPart(created));
            SelectPart(created.Id); // auto-reveals the new child under its (possibly collapsed) parent
        }

        private static CreaturePart NewGenericPart(string parentId)
        {
            return new CreaturePart
            {
                Id = PartIdGenerator.CreateNew(),
                ParentId = parentId,
                PartType = PartType.Part,
                DisplayName = DefaultPartNameFor(PartType.Part),
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            };
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
            DrawMeshGeometryFields(selected);
            EditorGUILayout.Space();
            DrawLimbFields(selected);
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
            string nextDisplayName = ResolveDisplayNameAfterTypeChange(selected.DisplayName, selected.PartType, newType);
            MutateDefinition("Change Part Type", definition =>
            {
                CreaturePart part = definition.FindPart(partId);
                part.PartType = newType;
                part.DisplayName = nextDisplayName;

                // CC-018 Phase 7 + CC-040: when the type changes, reconcile the
                // authored limb state with the new semantic category so stale limb
                // data is removed immediately when leaving a limb-chain type.
                LimbAuthoring.ApplyLimbStateForTypeChange(part, newType);
            });
        }

        /// <summary>
        /// The author-facing default name for a part type, used when a part is
        /// first created and when its type changes while the name is still the
        /// auto-assigned default (so switching a "Part" to an "Eye" renames it to
        /// "Eye" unless the user customized the name).
        /// </summary>
        internal static string DefaultPartNameFor(PartType type)
        {
            return type.ToString();
        }

        /// <summary>
        /// Resolves the DisplayName to keep after a part type change. If the
        /// current name is still the default for the old type, adopt the new
        /// type's default name; otherwise preserve the user's custom name.
        /// </summary>
        internal static string ResolveDisplayNameAfterTypeChange(string currentDisplayName, PartType oldType, PartType newType)
        {
            return currentDisplayName == DefaultPartNameFor(oldType)
                ? DefaultPartNameFor(newType)
                : currentDisplayName;
        }

        private void DrawBodyInspector()
        {
            EditorGUILayout.LabelField("Editing: Body", EditorStyles.boldLabel);

            // CC-020: the Body inspector is split into collapsible sections so
            // dozens of samples never run the panel off-screen. The viewport stays
            // the primary Body editing surface; the inspector is for precise values
            // and bulk operations. Section foldout state is session-scoped editor
            // presentation state, never DNA.

            _bodyShowGeneral = EditorGUILayout.BeginFoldoutHeaderGroup(_bodyShowGeneral, "General");
            if (_bodyShowGeneral)
            {
                Vector3 newForward = EditorGUILayout.Vector3Field("Forward", _definition.Forward);
                if (newForward != _definition.Forward)
                {
                    MutateDefinition("Edit Body Forward", definition => definition.Forward = newForward);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _bodyShowSpline = EditorGUILayout.BeginFoldoutHeaderGroup(_bodyShowSpline, "Body Spline");
            if (_bodyShowSpline)
            {
                DrawBodySplineSection();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _bodyShowAppearance = EditorGUILayout.BeginFoldoutHeaderGroup(_bodyShowAppearance, "Appearance");
            if (_bodyShowAppearance)
            {
                DrawBodyAppearanceFields();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _bodyShowAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(_bodyShowAdvanced, "Advanced");
            if (_bodyShowAdvanced)
            {
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
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>
        /// The Body spline section of the inspector (CC-020): sample count, a
        /// bounded scroll region for precise per-sample editing, and the bulk
        /// add/space/spacing controls. The bounded scroll is what stops the panel
        /// from running off-screen with many samples; the viewport remains the
        /// primary sample editing surface.
        /// </summary>
        private void DrawBodySplineSection()
        {
            EditorGUILayout.LabelField($"Samples: {_definition.Body.Samples.Count}");

            _bodySampleScroll = EditorGUILayout.BeginScrollView(
                _bodySampleScroll, GUILayout.MaxHeight(BodySampleScrollMaxHeight));

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
            EditorGUILayout.EndScrollView();

            if (sampleToRemove != null)
            {
                uint idToRemove = sampleToRemove.Id;
                MutateDefinition("Remove Body Sample",
                    definition =>
                    {
                        definition.Body.Samples.RemoveAll(s => s.Id == idToRemove);
                        BodySplineAuthoring.RenumberSamplesInOrder(definition.Body);
                    });
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
        }

        /// <summary>
        /// Authoring surface for the Body vertical-gradient appearance
        /// (CC-025/CC-034): a top gradient and a bottom gradient keyed over body
        /// length, blended by a vertical-blend curve over the vertical sample.
        /// Every edit funnels through MutateDefinition like all other DNA fields.
        /// </summary>
        private void DrawBodyAppearanceFields()
        {
            EditorGUILayout.LabelField("Body Appearance", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The Body color is a vertical gradient: a top and a bottom gradient keyed over body " +
                "length, blended by the vertical sample (-1 = bottom .. +1 = top of the surface). In " +
                "each gradient, left (time 0) is the head and right (time 1) is the tail. The vertical " +
                "curve remaps bottom-to-top (0..1) to the top/bottom blend; the default linear curve " +
                "leaves the blend unchanged.",
                MessageType.None);

            BodyVerticalGradientAppearance appearance =
                _definition.Body.Appearance ?? BodyVerticalGradientAppearance.CreateDefault();

            UnityEngine.AnimationCurve currentCurve = appearance.VerticalCurve;
            UnityEngine.AnimationCurve editedCurve =
                EditorGUILayout.CurveField("Vertical Curve", CurveAdapter.Clone(currentCurve));
            if (!CurveAdapter.ContentEquals(editedCurve, currentCurve))
            {
                MutateDefinition("Edit Body Vertical Curve",
                    definition => definition.Body.Appearance.VerticalCurve = CurveAdapter.Clone(editedCurve));
            }

            UnityEngine.Gradient currentTop = appearance.TopGradient;
            UnityEngine.Gradient editedTop = EditorGUILayout.GradientField(
                "Top Gradient", GradientAdapter.Clone(currentTop));
            if (!GradientAdapter.ContentEquals(editedTop, currentTop))
            {
                MutateDefinition("Edit Body Top Gradient",
                    definition => definition.Body.Appearance.TopGradient = GradientAdapter.Clone(editedTop));
            }

            UnityEngine.Gradient currentBottom = appearance.BottomGradient;
            UnityEngine.Gradient editedBottom = EditorGUILayout.GradientField(
                "Bottom Gradient", GradientAdapter.Clone(currentBottom));
            if (!GradientAdapter.ContentEquals(editedBottom, currentBottom))
            {
                MutateDefinition("Edit Body Bottom Gradient",
                    definition => definition.Body.Appearance.BottomGradient = GradientAdapter.Clone(editedBottom));
            }
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

            ShapeDefinition current = selected.Shape;
            ShapeDefinition newShape = current;
            EditorGUI.BeginChangeCheck();
            newShape.Type = (ShapeType)EditorGUILayout.EnumPopup("Shape Type", current.Type);

            float legacySize = current.PrimarySize > 0f ? current.PrimarySize : 0.5f;
            newShape.Radius = current.Radius > 0f ? current.Radius : legacySize;
            newShape.CapsuleHeight = current.CapsuleHeight > 0f ? current.CapsuleHeight : 1f;
            newShape.EllipsoidRadii = current.EllipsoidRadii.x > 0f
                ? current.EllipsoidRadii
                : new Vector3(legacySize, legacySize, legacySize);
            newShape.BoxHalfExtents = current.BoxHalfExtents.x > 0f
                ? current.BoxHalfExtents
                : new Vector3(legacySize, legacySize, legacySize);

            switch (newShape.Type)
            {
                case ShapeType.Sphere:
                    newShape.Radius = EditorGUILayout.FloatField("Radius", newShape.Radius);
                    break;
                case ShapeType.Capsule:
                    newShape.CapsuleAxis = (ShapeAxis)EditorGUILayout.EnumPopup("Axis", newShape.CapsuleAxis);
                    newShape.Radius = EditorGUILayout.FloatField("Radius", newShape.Radius);
                    newShape.CapsuleHeight = EditorGUILayout.FloatField("Height", newShape.CapsuleHeight);
                    break;
                case ShapeType.Ellipsoid:
                    newShape.EllipsoidRadii = EditorGUILayout.Vector3Field("Radii", newShape.EllipsoidRadii);
                    break;
                case ShapeType.Box:
                    newShape.BoxHalfExtents = EditorGUILayout.Vector3Field("Half Extents", newShape.BoxHalfExtents);
                    break;
            }

            // CC-049: Shape.SmoothBlendRadius is inert for a limb with a joint
            // chain, so show it disabled (with a pointer to the Limb section)
            // instead of presenting a field that silently does nothing. A limb-
            // typed part WITHOUT a chain still renders from its Shape, so its
            // blend stays active.
            bool shapeBlendInert = selected.Limb != null;
            if (shapeBlendInert)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.FloatField(
                    new GUIContent("Smooth Blend Radius",
                        "Inert for a limb with a joint chain (CC-049). Author the part-to-field blend in the Limb section."),
                    current.SmoothBlendRadius);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                newShape.SmoothBlendRadius = EditorGUILayout.FloatField("Smooth Blend Radius", current.SmoothBlendRadius);
            }

            if (!EditorGUI.EndChangeCheck()) return;

            string partId = selected.Id;
            MutateDefinition("Edit Shape", definition => definition.FindPart(partId).Shape = newShape);
        }

        private void DrawMeshGeometryFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Mesh Geometry", EditorStyles.boldLabel);

            bool useMeshGeometry = EditorGUILayout.Toggle("Use Mesh Asset", selected.MeshGeometry != null);
            if (useMeshGeometry != (selected.MeshGeometry != null))
            {
                string partId = selected.Id;
                MutateDefinition("Change Mesh Geometry Source", definition =>
                {
                    CreaturePart part = definition.FindPart(partId);
                    part.MeshGeometry = useMeshGeometry
                        ? new MeshGeometry { MeshAssetKey = FirstPaletteKey() }
                        : null;
                    if (useMeshGeometry) part.Limb = null;
                });
                selected = _definition.FindPart(partId);
            }

            if (selected.MeshGeometry == null) return;

            string[] keys = EffectiveMeshPalette != null ? EffectiveMeshPalette.GetUsableKeys() : System.Array.Empty<string>();
            if (keys.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Assign a Generation Config with a mesh palette that has at least one usable key.",
                    MessageType.Warning);
                return;
            }

            int currentIndex = System.Array.IndexOf(keys, selected.MeshGeometry.MeshAssetKey);
            if (currentIndex < 0) currentIndex = 0;
            int newIndex = EditorGUILayout.Popup("Mesh Asset", currentIndex, keys);
            if (newIndex != currentIndex || selected.MeshGeometry.MeshAssetKey != keys[newIndex])
            {
                string partId = selected.Id;
                string key = keys[newIndex];
                MutateDefinition("Assign Mesh Asset", definition =>
                    definition.FindPart(partId).MeshGeometry.MeshAssetKey = key);
                selected = _definition.FindPart(partId);
            }

            GeometryAttachment attachment = selected.MeshGeometry.Attachment ?? new GeometryAttachment();
            Vector3 offset = EditorGUILayout.Vector3Field("Offset", attachment.Offset);
            Vector3 rotation = EditorGUILayout.Vector3Field("Rotation (Euler)", attachment.Orientation.eulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", attachment.Scale);
            if (offset != attachment.Offset || rotation != attachment.Orientation.eulerAngles || scale != attachment.Scale)
            {
                string partId = selected.Id;
                MutateDefinition("Edit Mesh Attachment", definition =>
                {
                    GeometryAttachment next = definition.FindPart(partId).MeshGeometry.Attachment ?? new GeometryAttachment();
                    next.Offset = offset;
                    next.Orientation = Quaternion.Euler(rotation);
                    next.Scale = scale;
                    definition.FindPart(partId).MeshGeometry.Attachment = next;
                });
            }
        }

        private string FirstPaletteKey()
        {
            string[] keys = EffectiveMeshPalette != null ? EffectiveMeshPalette.GetUsableKeys() : System.Array.Empty<string>();
            return keys.Length > 0 ? keys[0] : string.Empty;
        }

        /// <summary>
        /// The limb authoring surface (CC-018 Phase 7): joint count, per-joint
        /// positions (bounded scroll like the Body spline), and the thickness
        /// profile edited as a linear AnimationCurve. Every edit funnels through
        /// <see cref="MutateDefinition"/>. A limb-chain-typed part with no chain
        /// yet (e.g. an Arm authored before CC-018) gets an explicit "add default
        /// chain" button — the viewport joint handles are the primary surface, and
        /// Shape is inert once a chain exists (ADR-001).
        /// </summary>
        private void DrawLimbFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Limb", EditorStyles.boldLabel);

            if (selected.Limb == null)
            {
                if (LimbAuthoring.IsLimbChainType(selected.PartType))
                {
                    EditorGUILayout.HelpBox(
                        "This limb has no joint chain yet — its geometry is still its Shape. " +
                        "Add a default chain to author it as a metaball limb (the Shape then becomes inert).",
                        MessageType.Info);
                    if (GUILayout.Button("Add Default Limb Chain"))
                    {
                        string partId = selected.Id;
                        PartType type = selected.PartType;
                        MutateDefinition("Add Limb Chain",
                            definition => definition.FindPart(partId).Limb = LimbAuthoring.DefaultLimbChainForType(type));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "This part is not a limb-chain type; it renders from its Shape. " +
                        "Change its Part Type to Limb/Leg/Arm to author a joint chain.",
                        MessageType.Info);
                }
                return;
            }

            int minCount = GenerationTolerances.MinLimbJointCount;
            int maxCount = GenerationTolerances.MaxLimbJointCount;
            int newCount = EditorGUILayout.IntSlider("Joint Count", selected.Limb.Joints.Count, minCount, maxCount);
            if (newCount != selected.Limb.Joints.Count)
            {
                string partId = selected.Id;
                MutateDefinition("Resize Limb Joints",
                    definition => LimbAuthoring.ResizeLimbChain(definition.FindPart(partId).Limb, newCount));
                selected = _definition.FindPart(partId);
            }

            _limbJointScroll = EditorGUILayout.BeginScrollView(
                _limbJointScroll, GUILayout.MaxHeight(BodySampleScrollMaxHeight));

            for (int i = 0; i < selected.Limb.Joints.Count; i++)
            {
                LimbJoint joint = selected.Limb.Joints[i];
                bool isRoot = i == 0;
                bool isTerminal = i == selected.Limb.Joints.Count - 1;
                string label = isRoot ? "Root" : (isTerminal ? "Tip" : $"#{i}");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, GUILayout.Width(44));

                if (isRoot)
                {
                    // The root joint is locked to the local origin (the part's
                    // placement frame); it moves via the part placement handle.
                    EditorGUILayout.LabelField("origin (locked)", GUILayout.Width(210));
                }
                else
                {
                    Vector3 newPosition = EditorGUILayout.Vector3Field("", joint.Position);
                    if (newPosition != joint.Position)
                    {
                        int jointIndex = i;
                        string partId = selected.Id;
                        Vector3 clamped = LimbAuthoring.ClampJointToBounds(newPosition, jointIndex, _definition.Bounds);
                        MutateDefinition("Move Limb Joint",
                            definition => definition.FindPart(partId).Limb.Joints[jointIndex].Position = clamped);
                        selected = _definition.FindPart(partId);
                    }
                }

                if (!isRoot && GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    int jointIndex = i;
                    string partId = selected.Id;
                    MutateDefinition("Remove Limb Joint", definition =>
                    {
                        LimbChain chain = definition.FindPart(partId).Limb;
                        if (chain.Joints.Count > minCount)
                        {
                            chain.Joints.RemoveAt(jointIndex);
                        }
                    });
                    selected = _definition.FindPart(partId);
                    EditorGUILayout.EndHorizontal();
                    break; // the joint list changed; stop iterating this frame
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add Joint"))
            {
                string partId = selected.Id;
                MutateDefinition("Add Limb Joint", definition =>
                {
                    LimbChain chain = definition.FindPart(partId).Limb;
                    if (chain.Joints.Count < maxCount)
                    {
                        LimbAuthoring.ResizeLimbChain(chain, chain.Joints.Count + 1);
                    }
                });
                selected = _definition.FindPart(partId);
            }

            // CC-039 / CC-049: the limb's part-to-field union blend radius. Shape
            // is inert for a limb with a chain, so this is the only authoring
            // surface for the limb blend. Clamped to >= 0 (0 = hard union) because
            // a negative/non-finite value would throw in DefinitionCanonicalizer
            // during commit.
            float currentBlend = selected.Limb.BlendRadius;
            float editedBlend = EditorGUILayout.FloatField(
                new GUIContent("Blend Radius",
                    "Part-to-field union blend for this limb (CC-049). 0 = hard union; Shape.SmoothBlendRadius is inert for limbs."),
                currentBlend);
            if (float.IsNaN(editedBlend) || float.IsInfinity(editedBlend)) editedBlend = currentBlend;
            editedBlend = Mathf.Max(0f, editedBlend);
            if (!Mathf.Approximately(editedBlend, currentBlend))
            {
                string partId = selected.Id;
                MutateDefinition("Edit Limb Blend Radius", definition =>
                    definition.FindPart(partId).Limb.BlendRadius = editedBlend);
            }

            UnityEngine.AnimationCurve currentThickness = ThicknessCurveAdapter.ToCurve(selected.Limb.Thickness);
            UnityEngine.AnimationCurve editedThickness =
                EditorGUILayout.CurveField("Thickness", ThicknessCurveAdapter.Clone(currentThickness));
            if (!ThicknessCurveAdapter.ContentEquals(currentThickness, editedThickness))
            {
                string partId = selected.Id;
                MutateDefinition("Edit Limb Thickness", definition =>
                {
                    definition.FindPart(partId).Limb.Thickness = ThicknessCurveAdapter.ToProfile(editedThickness);
                });
            }
        }

        private void DrawAppearanceFields(CreaturePart selected)
        {
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);

            string currentKey = selected.Appearance.MaterialKey;
            string[] usableKeys = EffectiveMaterialPalette != null
                ? EffectiveMaterialPalette.GetUsableKeys()
                : System.Array.Empty<string>();
            var keys = new List<string>(usableKeys);
            if (!string.IsNullOrWhiteSpace(currentKey) && !keys.Contains(currentKey))
            {
                EditorGUILayout.HelpBox(
                    $"Material key '{currentKey}' is not in the assigned material palette. " +
                    "Reassign it here or add the key to the palette.",
                    MessageType.Error);
                keys.Add(currentKey);
            }

            var labels = new string[keys.Count + 1];
            labels[0] = "(none)";
            for (int i = 0; i < keys.Count; i++)
            {
                labels[i + 1] = EffectiveMaterialPalette != null ? EffectiveMaterialPalette.GetDisplayName(keys[i]) : keys[i];
            }
            int currentIndex = System.Array.IndexOf(keys.ToArray(), currentKey) + 1;
            if (currentIndex < 0) currentIndex = 0;
            int newIndex = EditorGUILayout.Popup("Material", currentIndex, labels);
            string newKey = newIndex == 0 ? null : keys[newIndex - 1];

            var newAppearance = new AppearanceDefinition
            {
                BaseColor = EditorGUILayout.ColorField("Base Color", selected.Appearance.BaseColor),
                NoiseSeed = EditorGUILayout.IntField("Noise Seed", selected.Appearance.NoiseSeed),
                NoiseScale = EditorGUILayout.FloatField("Noise Scale", selected.Appearance.NoiseScale),
                MaterialKey = newKey,
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

        /// <summary>
        /// CC-066: read-only skeleton overlay. When the display mode is enabled,
        /// infers the working definition's rest Skeleton and draws bone lines +
        /// joint caps in creature space (the same resolver geometry uses). Never
        /// mutates the definition and never creates an Undo entry. Invalid DNA (a
        /// DomainException from inference) draws nothing — the validation panel
        /// already surfaces the error.
        /// </summary>
        private void DrawSkeletonOverlay()
        {
            if (!_showSkeleton || _definition == null) return;

            CreatureSkeleton.Skeleton skeleton;
            try
            {
                skeleton = CreatureSkeleton.SkeletonInferrer.Infer(_definition);
            }
            catch (DomainException)
            {
                return;
            }

            List<SkeletonDisplay.BoneLine> lines = SkeletonDisplay.BuildBoneLines(skeleton);
            List<Vector3> joints = SkeletonDisplay.BuildJointPoints(skeleton);

            Handles.color = SkeletonOverlayColor;
            for (int i = 0; i < lines.Count; i++)
            {
                Handles.DrawLine(lines[i].Start, lines[i].End);
            }
            for (int i = 0; i < joints.Count; i++)
            {
                float handleSize = HandleUtility.GetHandleSize(joints[i]) * 0.06f;
                Handles.SphereHandleCap(0, joints[i], Quaternion.identity, handleSize, EventType.Repaint);
            }
            Handles.color = Color.white;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            // CC-066: read-only skeleton overlay draws first (behind every
            // selection/authoring handle), whenever the display mode is enabled.
            DrawSkeletonOverlay();

            // Body sample handles are the Body's scene editing surface (CC-015).
            // They are suppressed in Place Part Mode so mesh clicks keep placing parts.
            if (_selectedPartId == CreatureDefinition.BodyId && !_placementModeActive)
            {
                DrawBodySampleHandles();
                return;
            }

            if (_bodyDragIndex >= 0) CancelBodyDrag(); // left Body selection mid-gesture

            // CC-018 Phase 7: a selected limb part edits its joint chain in the
            // viewport (root locked, interior + terminal draggable), suppressed in
            // Place Part Mode so mesh clicks keep placing parts.
            CreaturePart selected = _selectedPartId != null ? _definition.FindPart(_selectedPartId) : null;
            if (selected != null && selected.Limb != null && !_placementModeActive)
            {
                DrawLimbJointHandles(selected);
            }
            else
            {
                DrawSelectedPartHandle();
            }

            // CC-007 step 6: the transient new-part ghost draws behind the
            // placement click handling (Repaint only, gesture active).
            DrawPlacementDragGhost();
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
        /// Viewport joint editing for a selected limb part (CC-018 Phase 7). The
        /// chain's joints draw in creature space through the part's resolved
        /// matrix (the same transform the SDF and skeleton use). The ROOT joint
        /// is drawn but not independently draggable — it moves only via the part
        /// placement handle, because Joints[0] ≈ zero is the placement invariant.
        /// Interior joints drag directly; the TERMINAL joint drags with a larger
        /// cap as the child-attachment target (matching the Body endpoint
        /// pattern).
        ///
        /// The gesture follows CC-016: snapshot on mouse-down, transient preview
        /// during the drag (definition untouched), exactly ONE MutateDefinition on
        /// release (one drag = one Undo), Esc cancels with no mutation. Joints are
        /// FREE points — no FABRIK, no constraint solver; the commit clamps to the
        /// creature bounds and DefinitionValidator flags min-separation.
        /// </summary>
        private void DrawLimbJointHandles(CreaturePart part)
        {
            LimbChain limb = part.Limb;
            if (limb == null || limb.Joints == null || limb.Joints.Count == 0) return;

            Matrix4x4 worldMatrix;
            try
            {
                worldMatrix = CreaturePartWorldTransformResolver.ResolveLocalToCreatureSpace(_definition, part);
            }
            catch (DomainException)
            {
                // Invalid parent chain (already surfaced in the validation panel).
                return;
            }

            // An in-flight drag first checks for release (commit) and Esc (cancel)
            // so the very last drag target is captured. _limbDragJointIndex is set
            // ONLY once a handle actually moves (in the BeginChangeCheck below), so
            // a release here reliably means a real drag finished — a plain click
            // never reaches this commit.
            if (_limbDragJointIndex >= 0)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CancelLimbJointDrag();
                    return;
                }
                if ((Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    || GUIUtility.hotControl == 0)
                {
                    CommitLimbJointDrag(part);
                }
            }

            Vector3[] worldPositions = new Vector3[limb.Joints.Count];
            for (int i = 0; i < limb.Joints.Count; i++)
            {
                LimbJoint joint = limb.Joints[i];
                if (joint != null)
                {
                    worldPositions[i] = LimbAuthoring.WorldJointPosition(worldMatrix, joint.Position);
                }
            }

            // Chain preview line.
            Handles.color = new Color(0.6f, 0.65f, 0.95f, 0.85f);
            for (int i = 1; i < worldPositions.Length; i++)
            {
                Handles.DrawLine(worldPositions[i - 1], worldPositions[i]);
            }
            Handles.color = Color.white;

            for (int i = 0; i < limb.Joints.Count; i++)
            {
                LimbJoint joint = limb.Joints[i];
                if (joint == null) continue;

                bool isRoot = i == 0;
                bool isTerminal = i == limb.Joints.Count - 1;

                Vector3 drawPosition = _limbDragJointIndex == i
                    ? LimbAuthoring.WorldJointPosition(worldMatrix,
                        LimbAuthoring.ClampJointToBounds(_limbDragFinalTargetLocal, i, _definition.Bounds))
                    : worldPositions[i];

                float handleSize = HandleUtility.GetHandleSize(drawPosition) * (isTerminal ? 0.16f : 0.12f);

                if (isRoot)
                {
                    // Root cap: distinct color, not independently draggable.
                    Handles.color = new Color(0.45f, 0.45f, 0.45f, 0.9f);
                    Handles.SphereHandleCap(0, drawPosition, Quaternion.identity, handleSize, EventType.Repaint);
                    Handles.color = Color.white;
                    continue;
                }

                // Every non-root joint is a ONE-GESTURE FreeMoveHandle: click-drag
                // to reposition directly. (The previous Button + PositionHandle
                // combo was broken: the selection click consumed the mouse-down and
                // the immediate release committed without moving, so points could
                // not be dragged.) The definition is NOT mutated during the drag —
                // the commit on release writes the clamped target exactly once
                // (one drag = one Undo).
                EditorGUI.BeginChangeCheck();
                Vector3 newWorld = Handles.FreeMoveHandle(drawPosition, handleSize, Vector3.zero, Handles.SphereHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    // First change of a drag captures the snapshot; only then is
                    // the gesture considered in-flight.
                    if (_limbDragJointIndex != i)
                    {
                        _limbDragJointIndex = i;
                        _limbDragSnapshotLocal = joint.Position;
                    }
                    _limbDragFinalTargetLocal = LimbAuthoring.LocalJointPosition(worldMatrix, newWorld);
                    ScheduleLimbDragPreview(part.Id, i, _limbDragFinalTargetLocal);
                }
            }
        }

        /// <summary>
        /// One whole limb-joint drag commits exactly one mutation (one Undo). The
        /// definition was never mutated during the drag, so this is the single
        /// canonical write of the edited joint, flowed through the normal
        /// validation / Undo / session / auto-regen path. The root joint (index 0)
        /// is never written here — it is not draggable in the viewport.
        /// </summary>
        private void CommitLimbJointDrag(CreaturePart part)
        {
            if (_limbDragJointIndex < 0) return; // no active gesture (idempotent)

            int index = _limbDragJointIndex;
            Vector3 targetLocal = _limbDragFinalTargetLocal;
            string partId = part.Id;

            // Clear gesture state BEFORE mutating so re-entrant GUI cannot
            // double-commit.
            _limbDragJointIndex = -1;
            _limbDragSnapshotLocal = Vector3.zero;
            _limbDragFinalTargetLocal = Vector3.zero;
            _transientPreviewDefinition = null;

            Vector3 clamped = LimbAuthoring.ClampJointToBounds(targetLocal, index, _definition.Bounds);
            MutateDefinition("Drag Limb Joint (Viewport)", definition =>
            {
                LimbChain chain = definition.FindPart(partId)?.Limb;
                if (chain != null && index > 0 && index < chain.Joints.Count)
                {
                    chain.Joints[index].Position = clamped;
                }
            });

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Esc during a limb-joint drag: the definition was never mutated, so
        /// cancelling is just dropping the gesture state — no Undo entry.
        /// </summary>
        private void CancelLimbJointDrag()
        {
            _limbDragJointIndex = -1;
            _limbDragSnapshotLocal = Vector3.zero;
            _limbDragFinalTargetLocal = Vector3.zero;
            _transientPreviewDefinition = null;
            SceneView.RepaintAll();
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

            if (_bodyRadiusDragIndex >= 0)
            {
                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    CancelBodyRadiusDrag();
                    return;
                }
                if ((Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    || GUIUtility.hotControl == 0)
                {
                    CommitBodyRadiusDrag();
                }
            }

            Vector3[] displayPositions = GetBodyDisplayPositions(spline);
            DrawBodySplineConnections(spline, displayPositions);

            for (int i = 0; i < spline.Samples.Count; i++)
            {
                BodySample sample = spline.Samples[i];
                if (sample == null) continue;

                Vector3 drawPosition = displayPositions[i];
                bool isEndpoint = i == 0 || i == spline.Samples.Count - 1;
                float handleSize = HandleUtility.GetHandleSize(drawPosition) * (isEndpoint ? 0.16f : 0.12f);
                float radiusScale = Mathf.Clamp(sample.Radius * 0.18f, 0.08f, 0.28f);
                handleSize = Mathf.Max(handleSize, HandleUtility.GetHandleSize(drawPosition) * radiusScale);
                if (i != _activeBodySampleIndex && Handles.Button(drawPosition, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
                {
                    if (_bodyDragIndex >= 0 && _bodyDragIndex != i) CancelBodyDrag();
                    _activeBodySampleIndex = i;
                    Repaint();
                }
            }

            if (_activeBodySampleIndex >= 0 && _activeBodySampleIndex < spline.Samples.Count)
            {
                BodySample activeSelected = spline.Samples[_activeBodySampleIndex];
                if (activeSelected != null)
                {
                    Vector3 activeDrawPosition = displayPositions[_activeBodySampleIndex];
                    float activeSize = HandleUtility.GetHandleSize(activeDrawPosition) * 0.12f;
                    float activeRadiusScale = Mathf.Clamp(activeSelected.Radius * 0.18f, 0.08f, 0.28f);
                    activeSize = Mathf.Max(activeSize, HandleUtility.GetHandleSize(activeDrawPosition) * activeRadiusScale);
                    Handles.color = Color.white;
                    Handles.SphereHandleCap(0, activeDrawPosition, Quaternion.identity, activeSize, EventType.Repaint);
                }
            }

            if (_activeBodySampleIndex < 0 || _activeBodySampleIndex >= spline.Samples.Count) return;
            BodySample active = spline.Samples[_activeBodySampleIndex];
            if (active == null) return;

            bool activeIsEndpoint = _activeBodySampleIndex == 0 || _activeBodySampleIndex == spline.Samples.Count - 1;

            Vector3 handlePosition = displayPositions[_activeBodySampleIndex];
            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(handlePosition, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                if (_bodyDragIndex != _activeBodySampleIndex && Event.current.type != EventType.MouseUp)
                {
                    _bodyDragIndex = _activeBodySampleIndex;
                    _bodyDragKind = activeIsEndpoint ? BodyEditKind.EndpointLength : BodyEditKind.InteriorBend;
                    _bodyDragSnapshot = CopyBodyPositions(spline);
                    _bodyDragPreview = null;
                }
                _bodyDragFinalTarget = newPosition;
                _bodyDragPreview = SolveBodyDrag(_bodyDragIndex, _bodyDragFinalTarget);
                SceneView.RepaintAll();
            }

            float radiusHandleSize = HandleUtility.GetHandleSize(handlePosition) * 0.12f;
            float radiusValue = Mathf.Max(active.Radius, 0.05f);
            if (_bodyRadiusDragIndex == _activeBodySampleIndex)
            {
                radiusValue = Mathf.Max(_bodyRadiusDragTargetRadius, 0.05f);
            }

            Vector3 viewDirection = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera.transform.forward
                : Vector3.forward;

            Vector3 tangent = Vector3.zero;
            if (_activeBodySampleIndex > 0 && _activeBodySampleIndex < spline.Samples.Count - 1)
            {
                tangent = displayPositions[_activeBodySampleIndex + 1] - displayPositions[_activeBodySampleIndex - 1];
            }
            else if (_activeBodySampleIndex == 0 && spline.Samples.Count > 1)
            {
                tangent = displayPositions[1] - displayPositions[0];
            }
            else if (spline.Samples.Count > 1)
            {
                tangent = displayPositions[spline.Samples.Count - 1] - displayPositions[spline.Samples.Count - 2];
            }

            if (tangent.sqrMagnitude < 1e-6f)
            {
                tangent = Vector3.right;
            }
            tangent = tangent.normalized;

            Vector3 radiusAxis = Vector3.Cross(tangent, viewDirection);
            if (radiusAxis.sqrMagnitude < 0.0001f)
            {
                radiusAxis = Vector3.Cross(tangent, Vector3.up);
            }
            if (radiusAxis.sqrMagnitude < 0.0001f)
            {
                radiusAxis = Vector3.Cross(tangent, Vector3.forward);
            }
            radiusAxis.Normalize();

            Vector3 radiusHandlePosition = BodySampleRadiusHandle.GetHandlePosition(handlePosition, radiusValue, radiusAxis);
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawLine(handlePosition, radiusHandlePosition);
            Handles.color = new Color(0.9f, 0.6f, 0.2f, 1f);
            Handles.SphereHandleCap(0, radiusHandlePosition, Quaternion.identity, radiusHandleSize, EventType.Repaint);
            Handles.color = Color.white;

            EditorGUI.BeginChangeCheck();
            var fmh_1143_92_639230328724397202 = Quaternion.identity; Vector3 newRadiusHandlePosition = Handles.FreeMoveHandle(radiusHandlePosition, radiusHandleSize, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                if (_bodyRadiusDragIndex != _activeBodySampleIndex && Event.current.type != EventType.MouseUp)
                {
                    _bodyRadiusDragIndex = _activeBodySampleIndex;
                    _bodyRadiusDragStartRadius = active.Radius;
                    _bodyRadiusDragTargetRadius = active.Radius;
                }
                _bodyRadiusDragTargetRadius = BodySampleRadiusHandle.ComputeRadius(handlePosition, newRadiusHandlePosition, 0.05f);
            }

            DrawBodyEndpointExpansionHandles(spline, displayPositions);

            if (Event.current.type == EventType.Repaint && _bodyDragPreview != null)
            {
                DrawBodyEditPreview(_bodyDragPreview);
            }
        }

        private Vector3[] GetBodyDisplayPositions(BodySpline spline)
        {
            var positions = new Vector3[spline.Samples.Count];
            for (int i = 0; i < spline.Samples.Count; i++)
            {
                positions[i] = spline.Samples[i] == null ? Vector3.zero : spline.Samples[i].Position;
                if (_bodyDragPreview != null && i < _bodyDragPreview.Length)
                {
                    positions[i] = _bodyDragPreview[i];
                }
            }
            return positions;
        }

        private void DrawBodySplineConnections(BodySpline spline, Vector3[] displayPositions)
        {
            if (displayPositions == null || displayPositions.Length < 2) return;

            Handles.color = new Color(1f, 1f, 1f, 0.65f);
            for (int i = 1; i < displayPositions.Length; i++)
            {
                Handles.DrawLine(displayPositions[i - 1], displayPositions[i]);
            }
            Handles.color = Color.white;
        }

        private void DrawBodyEndpointExpansionHandles(BodySpline spline, Vector3[] displayPositions)
        {
            if (spline == null || spline.Samples == null || spline.Samples.Count < 2) return;

            for (int endIndex = 0; endIndex < 2; endIndex++)
            {
                bool isHead = endIndex == 0;
                int sampleIndex = isHead ? 0 : spline.Samples.Count - 1;
                if (sampleIndex < 0 || sampleIndex >= displayPositions.Length) continue;

                Vector3 samplePosition = displayPositions[sampleIndex];
                Vector3 tangent = isHead
                    ? (displayPositions[1] - displayPositions[0])
                    : (displayPositions[displayPositions.Length - 1] - displayPositions[displayPositions.Length - 2]);
                if (tangent.sqrMagnitude < 1e-5f)
                {
                    tangent = Vector3.right;
                }
                tangent = tangent.normalized;

                Vector3 awayAxis = isHead ? -tangent : tangent;
                float length = Mathf.Max(HandleUtility.GetHandleSize(samplePosition) * 0.45f, 0.6f);
                Vector3 handlePosition = samplePosition + awayAxis * length;
                Handles.color = new Color(0.9f, 0.5f, 0.2f, 1f);
                Handles.ArrowHandleCap(0, handlePosition, Quaternion.LookRotation(awayAxis), HandleUtility.GetHandleSize(handlePosition) * 0.35f, EventType.Repaint);
                Handles.color = Color.white;

                EditorGUI.BeginChangeCheck();
                var fmh_1216_84_639230328724413263 = Quaternion.identity; Vector3 newHandlePosition = Handles.FreeMoveHandle(handlePosition, HandleUtility.GetHandleSize(handlePosition) * 0.12f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    float deadZone = HandleUtility.GetHandleSize(samplePosition) * 0.22f;
                    float dragDelta = Vector3.Dot(newHandlePosition - handlePosition, awayAxis);

                    if (dragDelta > deadZone)
                    {
                        MutateDefinition(isHead ? "Add Head Body Sample (Viewport)" : "Add Tail Body Sample (Viewport)", definition =>
                        {
                            BodySpline target = definition.Body;
                            if (isHead) BodySplineAuthoring.PrependSample(target, definition.Forward);
                            else BodySplineAuthoring.AppendSample(target, definition.Forward);

                            _activeBodySampleIndex = isHead ? 0 : target.Samples.Count - 1;
                            _bodyDragIndex = -1;
                            _bodyDragPreview = null;
                            _bodyRadiusDragIndex = -1;
                        });
                        return;
                    }

                    if (dragDelta < -deadZone)
                    {
                        bool canRemove = BodySplineAuthoring.TryRemoveEndpointSample(spline, isHead, BodySplineAuthoring.DefaultMinSampleCount);
                        if (canRemove)
                        {
                            MutateDefinition(isHead ? "Remove Head Body Sample (Viewport)" : "Remove Tail Body Sample (Viewport)", definition =>
                            {
                                BodySplineAuthoring.TryRemoveEndpointSample(definition.Body, isHead, BodySplineAuthoring.DefaultMinSampleCount);
                                _activeBodySampleIndex = isHead ? 0 : definition.Body.Samples.Count - 1;
                                _bodyDragIndex = -1;
                                _bodyDragPreview = null;
                                _bodyRadiusDragIndex = -1;
                            });
                            return;
                        }
                    }
                }
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

        private void CommitBodyRadiusDrag()
        {
            if (_bodyRadiusDragIndex < 0) return;

            int index = _bodyRadiusDragIndex;
            float startRadius = _bodyRadiusDragStartRadius;
            float targetRadius = Mathf.Max(_bodyRadiusDragTargetRadius, 0.05f);

            _bodyRadiusDragIndex = -1;
            _bodyRadiusDragStartRadius = 1f;
            _bodyRadiusDragTargetRadius = 1f;

            if (Mathf.Approximately(startRadius, targetRadius)) return;

            BodySpline spline = _definition.Body;
            if (spline == null || index < 0 || index >= spline.Samples.Count) return;

            uint sampleId = spline.Samples[index].Id;
            MutateDefinition("Resize Body Sample (Viewport)", definition =>
            {
                BodySample sample = FindBodySample(definition, sampleId);
                sample.Radius = Mathf.Max(targetRadius, 0.05f);
            });
        }

        private void CancelBodyRadiusDrag()
        {
            _bodyRadiusDragIndex = -1;
            _bodyRadiusDragStartRadius = 1f;
            _bodyRadiusDragTargetRadius = 1f;
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
            // CC-007: the part is passed so an ANCHORED Body child converts the
            // dragged world position into the anchor surface frame's local space
            // (the frame the resolver reads Transform.Position in) instead of
            // creature space — without this a drag on an anchored part explodes
            // along the frame's +Y (gizmo drag fix).
            Vector3 newLocalPosition = WorldToLocalPosition(newWorldPosition, selected.ParentId, selected);
            // CC-007: an anchored Body child's local position is surface-frame-
            // local, not creature-space, so creature-bounds clamping does not
            // apply to it (a large local offset along the frame's +Y is a
            // legitimate fine adjustment, not an out-of-bounds position).
            bool anchoredBodyChild = (selected.ParentId == null || selected.ParentId == CreatureDefinition.BodyId)
                && selected.ParentAttachment != null;
            Vector3 storedLocalPosition = anchoredBodyChild
                ? newLocalPosition
                : ClampToBounds(newLocalPosition, _definition.Bounds);

            TransformData newTransform = selected.Transform;
            newTransform.Position = storedLocalPosition;

            // Same known granularity limitation as continuous inspector-field
            // drags (see class doc comment): this fires once per GUI frame the
            // handle moves during a drag, so one drag currently produces many
            // undo steps rather than one.
            MutateDefinition("Move Part (Viewport)", definition => definition.FindPart(partId).Transform = newTransform);

            Repaint(); // the main window doesn't auto-repaint from a SceneView-driven change
        }

        /// <summary>
        /// A placement-scoped fingerprint of the definition: Body sample
        /// identity/positions/radii plus Forward. Placement depends only on the
        /// Body surface — the raycast hits the preview's Body collider and the
        /// anchor projects onto the resolved Body — so part edits never mark the
        /// preview stale (CC-007 step 5). Internal so EditMode tests can pin the
        /// granularity.
        /// </summary>
        internal static string BuildPlacementFingerprint(CreatureDefinition definition)
        {
            if (definition == null || definition.Body == null || definition.Body.Samples == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            sb.Append(definition.Forward.ToString("G6")).Append('|');
            foreach (BodySample sample in definition.Body.Samples)
            {
                if (sample == null) continue;
                sb.Append(sample.Id).Append(',')
                  .Append(sample.Position.x.ToString("G6")).Append(',')
                  .Append(sample.Position.y.ToString("G6")).Append(',')
                  .Append(sample.Position.z.ToString("G6")).Append(',')
                  .Append(sample.Radius.ToString("G6")).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>
        /// True when the preview's MeshCollider was generated from a different
        /// Body than the live definition (CC-007 step 5). A null fingerprint (a
        /// preview carried across a domain reload with no recorded generate) is
        /// treated as fresh so a recompile does not force a regenerate.
        /// </summary>
        private bool IsPreviewStale()
        {
            return _previewBodyFingerprint != null
                && BuildPlacementFingerprint(_definition) != _previewBodyFingerprint;
        }

        private void HandlePlacementClick()
        {
            if (!_placementModeActive) return;

            Event e = Event.current;

            // CC-007 step 6: an in-flight new-part placement drag first checks for
            // release (commit) and Esc (cancel) so the very last drag target is
            // captured — the same shape as the CC-016/CC-018 viewport gestures.
            // hotControl returning to 0 also means the drag ended.
            if (_placementDragActive)
            {
                if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                {
                    CancelPlacementDrag();
                    e.Use();
                    return;
                }
                if (e.type == EventType.MouseUp || GUIUtility.hotControl == 0)
                {
                    CommitPlacementDrag(e);
                    return; // CommitPlacementDrag consumes the event
                }
                if (e.type == EventType.MouseDrag || e.type == EventType.MouseMove)
                {
                    UpdatePlacementDrag(e);
                    e.Use();
                    return;
                }
                return; // Repaint and other event types leave the gesture alone
            }

            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (e.alt) return; // let Alt+Click camera orbit pass through untouched

            if (_previewGameObject == null)
            {
                _placementFeedback = "No preview object — regenerate a preview before placing parts.";
                return;
            }

            MeshCollider collider = _previewGameObject.GetComponent<MeshCollider>();
            if (collider == null)
            {
                _placementFeedback = "Preview has no collider — regenerate a preview before placing parts.";
                return;
            }

            // CC-007 step 5: the collider is from the last successful regenerate.
            // A Body change since then makes the raycast geometry stale (a hit no
            // longer lies on the current Body surface), so block placement until
            // the user regenerates. Part-only edits do not trip this gate.
            if (IsPreviewStale())
            {
                _placementFeedback = "Preview is stale — the Body changed since the last 'Regenerate Preview'. Regenerate first, then place.";
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!collider.Raycast(ray, out RaycastHit hit, 1000f))
            {
                _placementFeedback = "Placement missed the preview mesh; the definition was unchanged.";
                return;
            }

            // Place Part Mode snaps the selected part to the hit point (CC-015).
            // A direct Body child is placed through a semantic BodySurfaceAnchor
            // (CC-007): the hit's position + outward normal are input only, and
            // the anchor becomes the authoritative DNA. With no part selected, a
            // new part is created at the hit — but the placement is deferred to
            // the mouse-up so a DRAG can pick the drop point with a ghost (CC-007
            // step 6). A plain click places at once: down starts the gesture, up
            // commits at the same point, so click-to-place still works.
            CreaturePart selected = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId
                ? _definition.FindPart(_selectedPartId)
                : null;
            if (selected != null)
            {
                if (selected.ParentId == CreatureDefinition.BodyId)
                {
                    PlaceSelectedBodyChildOnSurface(selected, hit.point, hit.normal);
                }
                else
                {
                    ApplyViewportMove(selected, hit.point);
                }

                e.Use(); // consume the click so it doesn't also (de)select scene objects
                return;
            }

            // New-part placement drag: capture the hot control and project the
            // initial ghost from this first hit. The definition is not mutated
            // here; CommitPlacementDrag on release owns the single mutation.
            _placementDragControlId = GUIUtility.GetControlID(FocusType.Passive);
            _placementDragActive = true;
            _placementDragHasValidFrame = false;
            GUIUtility.hotControl = _placementDragControlId;
            UpdatePlacementDrag(e);
            e.Use();
        }

        /// <summary>
        /// Re-projects the drag's current cursor position onto the Body surface
        /// and stores the ghost frame. Pure preview state — the definition is
        /// never touched here (CC-007 step 6). A frame is only recorded when the
        /// raycast hits the preview collider AND the hit projects to a valid
        /// anchor, so the ghost simply freezes at its last valid position when the
        /// cursor leaves the body mid-drag.
        /// </summary>
        private void UpdatePlacementDrag(Event e)
        {
            _placementDragHasValidFrame = false;

            MeshCollider collider = _previewGameObject != null
                ? _previewGameObject.GetComponent<MeshCollider>()
                : null;
            if (collider == null) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!collider.Raycast(ray, out RaycastHit hit, 1000f)) return;

            if (BodyPlacementAuthoring.TryProjectToAnchor(
                    _definition, hit.point, hit.normal, out BodySurfaceAnchor anchor)
                && BodyPlacementAuthoring.TryResolveSurfaceFrame(
                    _definition, anchor, out _placementDragSurfacePosition, out _placementDragSurfaceRotation))
            {
                _placementDragHasValidFrame = true;
            }
        }

        /// <summary>
        /// Ends the placement drag by committing through the SAME button-driven
        /// placement path (<see cref="PlaceNewPartAtWorldPosition"/>): the release
        /// point is re-raycast and the hit (position + outward normal, input only)
        /// projects to the semantic anchor inside the mutation, so one drag = one
        /// MutateDefinition = one Undo (CC-007 step 6). A release off the mesh
        /// leaves the definition unchanged, matching the click-path invariant.
        /// </summary>
        private void CommitPlacementDrag(Event e)
        {
            int controlId = _placementDragControlId;
            _placementDragActive = false;
            _placementDragControlId = -1;
            _placementDragHasValidFrame = false;
            if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;

            MeshCollider collider = _previewGameObject != null
                ? _previewGameObject.GetComponent<MeshCollider>()
                : null;
            if (collider == null)
            {
                _placementFeedback = "Preview has no collider — regenerate a preview before placing parts.";
                e.Use();
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (!collider.Raycast(ray, out RaycastHit hit, 1000f))
            {
                _placementFeedback = "Placement missed the preview mesh; the definition was unchanged.";
                e.Use();
                return;
            }

            PlaceNewPartAtWorldPosition(hit.point, hit.normal);
            e.Use();
        }

        /// <summary>
        /// Esc during a placement drag: the definition was never mutated, so
        /// nothing to undo — the gesture state is simply dropped (CC-007 step 6).
        /// </summary>
        private void CancelPlacementDrag()
        {
            if (GUIUtility.hotControl == _placementDragControlId) GUIUtility.hotControl = 0;
            _placementDragActive = false;
            _placementDragControlId = -1;
            _placementDragHasValidFrame = false;
            _placementFeedback = "Placement drag cancelled; the definition was unchanged.";
        }

        /// <summary>
        /// Draws the transient new-part ghost during a placement drag (CC-007
        /// step 6): a small screen-relative sphere at the surface frame with a
        /// short line along the surface +Y (outward normal). It is a placement
        /// CURSOR, not a full-size part preview — a fixed DefaultSphere-sized
        /// ball read as a huge volume on a large creature and did not shrink
        /// with zoom, so the ghost follows the project's Scene-view handle
        /// convention (HandleUtility.GetHandleSize * factor, the same rule as
        /// the body-sample/limb/skeleton handles). Repaint only; editor state,
        /// never DNA.
        /// </summary>
        private void DrawPlacementDragGhost()
        {
            if (!_placementDragActive || !_placementDragHasValidFrame) return;
            if (Event.current.type != EventType.Repaint) return;

            float handleSize = HandleUtility.GetHandleSize(_placementDragSurfacePosition);
            Handles.color = new Color(0.35f, 0.75f, 1f, 0.6f);
            Handles.SphereHandleCap(0, _placementDragSurfacePosition, _placementDragSurfaceRotation,
                handleSize * 0.28f, EventType.Repaint);
            Handles.color = new Color(0.35f, 0.75f, 1f, 0.9f);
            Handles.DrawLine(
                _placementDragSurfacePosition,
                _placementDragSurfacePosition + _placementDragSurfaceRotation * Vector3.up * handleSize * 0.6f);
            Handles.color = Color.white;
        }

        private void PlaceNewPartAtWorldPosition(Vector3 worldPosition, Vector3 worldNormal)
        {
            string parentId = _selectedPartId != null && _selectedPartId != CreatureDefinition.BodyId
                ? _selectedPartId
                : CreatureDefinition.BodyId;
            string newId = PartIdGenerator.CreateNew();

            // CC-007: a direct Body child is placed through a semantic
            // BodySurfaceAnchor — the hit (position + outward normal) is input
            // only, and the anchor becomes the authoritative DNA. The part sits
            // exactly on the body surface at the hit and survives mesh resolution
            // changes. Falls back to the raw creature-space position when the
            // Body has no surface to project onto.
            if (parentId == CreatureDefinition.BodyId
                && BodyPlacementAuthoring.TryProjectToAnchor(
                    _definition, worldPosition, worldNormal, out BodySurfaceAnchor anchor))
            {
                MutateDefinition("Place Part on Body Surface (Viewport)", definition => definition.AddPart(new CreaturePart
                {
                    Id = newId,
                    ParentId = CreatureDefinition.BodyId,
                    PartType = PartType.Part,
                    DisplayName = DefaultPartNameFor(PartType.Part),
                    Transform = TransformData.Identity,
                    Shape = ShapeDefinition.DefaultSphere,
                    Appearance = AppearanceDefinition.Default,
                    MirrorAcrossSymmetryPlane = true,
                    ParentAttachment = anchor,
                }));
                _placementFeedback = $"Placed '{newId}' on the Body surface.";
                SelectPart(newId);
                Repaint();
                return;
            }

            // Non-Body parent (a selected limb or other part): the child is
            // authored in the parent's local frame (CC-018 child-at-tip for a
            // limb parent). Also the fallback when the Body has no surface to
            // anchor onto.
            // CC-007: the part being created does not exist yet, so it cannot
            // carry an anchor; a new part on the raw-position fallback path is a
            // NON-anchored Body child (local == creature space), which is the
            // correct conversion here.
            Vector3 localPosition = WorldToLocalPosition(worldPosition, parentId, null, out bool parentResolvedSuccessfully);
            if (!parentResolvedSuccessfully)
            {
                parentId = CreatureDefinition.BodyId; // fall back to the Body root rather than dropping the click
                _placementFeedback = "Placement fell back to the Body root because the intended parent could not be resolved.";
            }
            else if (parentId == CreatureDefinition.BodyId)
            {
                _placementFeedback = "Body has no surface to anchor to; placed at the raw creature-space position.";
            }

            // CC-018 (child-at-tip frame): a limb parent's terminal joint is the
            // origin of a child's local space, and WorldToLocalPosition already
            // converts the clicked point into that tip-relative frame — a drop at
            // identity lands on the tip and any click offsets from the tip.
            Vector3 clampedPosition = ClampToBounds(localPosition, _definition.Bounds);
            string finalParentId = parentId;

            MutateDefinition("Place Part (Viewport)", definition => definition.AddPart(new CreaturePart
            {
                Id = newId,
                ParentId = finalParentId,
                PartType = PartType.Part,
                DisplayName = DefaultPartNameFor(PartType.Part),
                Transform = new TransformData { Position = clampedPosition, Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
                MirrorAcrossSymmetryPlane = true,
            }));

            SelectPart(newId);
            Repaint();
        }

        /// <summary>
        /// Snaps an existing direct Body child to a preview-mesh hit by
        /// re-projecting the semantic BodySurfaceAnchor (CC-007). The anchor
        /// becomes the placement authority: the part's local position resets to
        /// zero so it sits exactly on the surface frame at the hit (scale, a size
        /// property, is preserved). A LIMB keeps its current world orientation
        /// (expressed in the surface frame's local space) so its chain keeps
        /// pointing outward/down instead of into the body — the surface frame's
        /// +Y is the outward normal, so a bare identity rotation would point a
        /// -Y-authored limb chain INTO the creature (CC-007 review fix). A
        /// non-limb Body child aligns to the surface frame. Falls back to a raw
        /// viewport move when the Body has no surface to project onto.
        /// </summary>
        private void PlaceSelectedBodyChildOnSurface(CreaturePart selected, Vector3 hitPosition, Vector3 hitNormal)
        {
            if (!BodyPlacementAuthoring.TryProjectToAnchor(
                    _definition, hitPosition, hitNormal, out BodySurfaceAnchor anchor))
            {
                ApplyViewportMove(selected, hitPosition);
                return;
            }

            string partId = selected.Id;
            bool isLimb = selected.Limb != null && selected.Limb.Joints != null && selected.Limb.Joints.Count > 0;

            // The rotation stored is the part's world orientation expressed in the
            // new anchor frame's local space: composed with the anchor surface
            // frame it reproduces the limb's authored world orientation at the new
            // surface position. A non-limb part keeps the surface-frame alignment
            // (identity local rotation, +Y outward) used for new parts.
            Quaternion localRotation = Quaternion.identity;
            if (isLimb)
            {
                try
                {
                    Quaternion currentWorld = CreaturePartWorldTransformResolver
                        .ResolvePartFrameToCreatureSpace(_definition, selected).rotation;
                    Quaternion surfaceFrame = BodyPlacementAuthoring.ResolveSurfaceFrameRotation(_definition, anchor);
                    localRotation = Quaternion.Inverse(surfaceFrame) * currentWorld;
                }
                catch (DomainException)
                {
                    localRotation = Quaternion.identity; // degenerate definition; never throw from the SceneView
                }
            }

            MutateDefinition("Place Part on Body Surface (Viewport)", definition =>
            {
                CreaturePart part = definition.FindPart(partId);
                part.ParentAttachment = anchor;
                TransformData transform = part.Transform;
                transform.Position = Vector3.zero;
                transform.Rotation = localRotation;
                part.Transform = transform;
            });
            _placementFeedback = isLimb
                ? $"Snapped '{partId}' to the Body surface (limb orientation preserved)."
                : $"Snapped '{partId}' to the Body surface.";
            Repaint();
        }

        /// <summary>
        /// Converts a creature-space (world) position into the parent-relative
        /// local position CreaturePart.Transform.Position actually stores, by
        /// inverting the parent's resolved world matrix — see the class doc
        /// comment's note on why this conversion exists at all (viewport
        /// interaction is world-space; DNA storage is parent-local).
        /// </summary>
        private Vector3 WorldToLocalPosition(Vector3 worldPosition, string parentId, CreaturePart part)
        {
            return WorldToLocalPosition(worldPosition, parentId, part, out _);
        }

        private Vector3 WorldToLocalPosition(Vector3 worldPosition, string parentId, CreaturePart part, out bool succeeded)
        {
            succeeded = true;

            // The Body owns the creature frame. A NON-anchored Body child's local
            // position is already creature-space (the Body spline defines the
            // origin), but an ANCHORED Body child (ParentAttachment) is placed by
            // the resolver in the anchor SURFACE FRAME's local space (ADR-002 §7
            // fine adjustment), so the world->local conversion must invert that
            // surface frame — otherwise a gizmo drag writes a creature-space
            // offset that the resolver misreads as a surface-frame-local offset
            // and the part explodes along the frame's +Y (CC-007 gizmo-drag fix).
            if (parentId == null || parentId == CreatureDefinition.BodyId)
            {
                if (part != null && part.ParentAttachment != null
                    && BodyPlacementAuthoring.TryResolveSurfaceFrame(
                        _definition, part.ParentAttachment,
                        out Vector3 surfacePosition, out Quaternion surfaceRotation))
                {
                    return Quaternion.Inverse(surfaceRotation) * (worldPosition - surfacePosition);
                }
                return worldPosition;
            }

            CreaturePart parentPart = _definition.FindPart(parentId);
            if (parentPart == null)
            {
                succeeded = false;
                return worldPosition;
            }

            try
            {
                // CC-018 (child-at-tip frame): a child of a limb is authored in the
                // limb's TERMINAL joint frame (the tip), so the conversion inverts
                // the child frame, not the parent's placement frame.
                Matrix4x4 parentWorld =
                    CreaturePartWorldTransformResolver.ResolveChildFrameToCreatureSpace(_definition, parentPart);
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

            if (EffectiveMeshPalette != null && EffectiveMeshPalette.HasDuplicateKeys(out string duplicateKey))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Generate",
                    $"Mesh palette contains duplicate key '{duplicateKey}'. Remove the duplicate before generating a preview.",
                    "OK");
                return;
            }

            if (EffectiveMaterialPalette != null && EffectiveMaterialPalette.HasDuplicateKeys(out string duplicateMaterialKey))
            {
                EditorUtility.DisplayDialog(
                    "Cannot Generate",
                    $"Material palette contains duplicate key '{duplicateMaterialKey}'. Remove the duplicate before generating a preview.",
                    "OK");
                return;
            }

            EnqueuePreviewGeneration(_definition);
            _autoRegenerateAt = -1d;
        }

        private void EnqueuePreviewGeneration(CreatureDefinition definition)
        {
            definition = definition.Clone();
            definition.Generation.VoxelsPerUnit = _previewVoxelsPerUnit;
            _generationScheduler.Enqueue(definition, new GenerationDiagnostics(_logGenerationDiagnostics));
        }

        private void ProcessGenerationCompletions()
        {
            if (_generationScheduler == null) return;
            while (_generationScheduler.TryTakeCompleted(out CreatureGenerationResult result))
            {
                if (result.IsStale) continue;
                if (!result.Succeeded)
                {
                    string validationDetails = string.Join("\n", _validation.Issues.Select(issue => issue.Message));
                    EditorUtility.DisplayDialog(
                        "Generation Failed",
                        $"Stage: {result.Diagnostics?.FailedStage}\n{result.Exception.Message}\n\n{validationDetails}",
                        "OK");
                    continue;
                }

                try
                {
                    GeneratedCreature generated = CreatureMeshGenerator.Assemble(result.Data, ResolveMeshAsset);
                    Mesh unityMesh = generated.MainMesh;
                    ApplyPreviewGeometry(generated);
                    _previewBodyFingerprint = BuildPlacementFingerprint(_definition);
                    MeshTopologyReport topologyReport = result.Data.TopologyReport;
                    if (!topologyReport.IsWatertight)
                    {
                        Debug.LogWarning($"[CreatureCreator] Generated mesh is not watertight: " +
                            $"{topologyReport.BoundaryEdgeCount} boundary edge(s), " +
                            $"{topologyReport.NonManifoldEdgeCount} non-manifold edge(s) out of " +
                            $"{topologyReport.TotalEdgeCount} total. See MeshTopologyValidator.");
                    }
                    if (_logGenerationDiagnostics)
                    {
                        string timingReport = string.Join("\n", result.Diagnostics.Timings.Select(FormatDiagnosticTiming));
                        Debug.Log($"[CreatureCreator] Preview regenerated — " +
                            $"{unityMesh.triangles.Length / 3} triangles, {unityMesh.vertexCount} vertices, " +
                            $"TotalGeneration: {result.Diagnostics.TotalTime.TotalMilliseconds:F1}ms\n{timingReport}");
                    }
                }
                catch (DomainException ex)
                {
                    EditorUtility.DisplayDialog("Generation Failed", ex.Message, "OK");
                }
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
                Material material = ResolveDefaultMaterial();
                if (material != null) renderer.sharedMaterial = material;
                _previewGameObject.AddComponent<MeshCollider>();
            }

            _previewGameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer previewRenderer = _previewGameObject.GetComponent<MeshRenderer>();
            if (previewRenderer == null) previewRenderer = _previewGameObject.AddComponent<MeshRenderer>();
            // CC-074: re-resolve the palette default each regenerate so a palette
            // or config change is reflected without reopening the window.
            Material resolvedDefault = ResolveDefaultMaterial();
            if (resolvedDefault != null) previewRenderer.sharedMaterial = resolvedDefault;

            // MeshCollider needs sharedMesh reassigned (not just relying on the
            // same Mesh object being mutated) to pick up topology changes —
            // ToUnityMesh() always returns a brand-new Mesh each regenerate, so
            // this reassignment happens naturally every time.
            MeshCollider collider = _previewGameObject.GetComponent<MeshCollider>();
            if (collider == null) collider = _previewGameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void ApplyPreviewGeometry(GeneratedCreature generated)
        {
            ApplyPreviewMesh(generated.MainMesh);
            ClearPreviewGeometryChildren();

            for (int i = 1; i < generated.Geometry.Count; i++)
            {
                GeometryItem item = generated.Geometry[i];
                var child = new GameObject(PreviewGeometryChildPrefix + i);
                child.transform.SetParent(_previewGameObject.transform, worldPositionStays: false);
                child.AddComponent<MeshFilter>().sharedMesh = item.Mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                AssignPreviewItemMaterials(renderer, item);
                _previewGeometryObjects.Add(child);
            }
        }

        /// <summary>
        /// Assigns materials to a preview item's renderer. A mesh-asset item whose
        /// part carries a submaterial key (CC-028) resolves that key through the
        /// assigned material palette (<see cref="MaterialResolver"/> — a
        /// set-but-unresolvable key throws, so a missing palette entry is never
        /// silently ignored, matching the mesh-resolver contract; the throw is
        /// caught by RegeneratePreview and shown as a dialog). Items with no
        /// material region keep the palette's default surface material (CC-074);
        /// extra submeshes keep the default too.
        /// </summary>
        private void AssignPreviewItemMaterials(MeshRenderer renderer, GeometryItem item)
        {
            Material fallback = ResolveDefaultMaterial();
            if (item.MaterialRegions.Count == 0)
            {
                if (fallback != null) renderer.sharedMaterial = fallback;
                return;
            }

            Material resolved = MaterialResolver.Resolve(EffectiveMaterialPalette, item.MaterialRegions[0].MaterialKey);
            if (fallback == null && resolved == null) return;

            int subMeshCount = Mathf.Max(1, item.Mesh != null ? item.Mesh.subMeshCount : 1);
            var materials = new Material[subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = fallback;
            materials[0] = resolved != null ? resolved : fallback;
            renderer.sharedMaterials = materials;
        }

        private void ClearPreviewGeometryChildren()
        {
            for (int i = _previewGeometryObjects.Count - 1; i >= 0; i--)
            {
                if (_previewGeometryObjects[i] != null) Object.DestroyImmediate(_previewGeometryObjects[i]);
            }
            _previewGeometryObjects.Clear();

            if (_previewGameObject == null) return;
            for (int i = _previewGameObject.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _previewGameObject.transform.GetChild(i);
                if (child.name.StartsWith(PreviewGeometryChildPrefix, System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private Mesh ResolveMeshAsset(string key)
        {
            return EffectiveMeshPalette != null && EffectiveMeshPalette.TryResolve(key, out Mesh mesh) ? mesh : null;
        }

        private CreatureMeshPalette EffectiveMeshPalette =>
            _generationConfig != null ? _generationConfig.MeshPalette : null;

        private CreatureMaterialPalette EffectiveMaterialPalette =>
            _generationConfig != null ? _generationConfig.MaterialPalette : null;

        /// <summary>
        /// CC-074: the editor surface material is the material palette's default
        /// (for example the Body material). There is no standalone preview
        /// material override — the shared config's palette is the single source.
        /// A synthesized material is used only when the palette has no default.
        /// </summary>
        private Material ResolveDefaultMaterial()
        {
            Material material = MaterialResolver.ResolveDefault(EffectiveMaterialPalette);
            return material != null ? material : CreateDefaultPreviewMaterial();
        }

        private static Material _cachedFallbackMaterial;

        private static Material CreateDefaultPreviewMaterial()
        {
            // Cached so repeated regenerations do not leak a fresh Material each
            // time the palette has no resolvable default.
            if (_cachedFallbackMaterial != null) return _cachedFallbackMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Standard")
                             ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("[CreatureCreator] No default shader found; preview mesh will use Unity's fallback material.");
                return null;
            }
            _cachedFallbackMaterial = new Material(shader);
            return _cachedFallbackMaterial;
        }

        private static string GetPartLabel(CreaturePart part)
        {
            string displayName = string.IsNullOrWhiteSpace(part.DisplayName) ? part.Id : part.DisplayName;
            return $"{displayName} ({part.Id})";
        }

        private void ScheduleAutoRegeneration()
        {
            if (!_autoRegenerate) return;
            _transientPreviewDefinition = null;
            _autoRegenerateAt = EditorApplication.timeSinceStartup + _autoRegenerationDelaySeconds;
        }

        private void ScheduleLimbDragPreview(string partId, int jointIndex, Vector3 targetLocal)
        {
            if (!_autoRegenerate) return;

            CreatureDefinition previewDefinition = _definition.Clone();
            LimbChain chain = previewDefinition.FindPart(partId)?.Limb;
            if (chain == null || jointIndex <= 0 || jointIndex >= chain.Joints.Count) return;

            chain.Joints[jointIndex].Position = LimbAuthoring.ClampJointToBounds(
                targetLocal, jointIndex, previewDefinition.Bounds);
            _transientPreviewDefinition = previewDefinition;
            _autoRegenerateAt = EditorApplication.timeSinceStartup + _autoRegenerationDelaySeconds;
        }

        private void ProcessAutoRegeneration()
        {
            ProcessGenerationCompletions();
            if (!_autoRegenerate || _autoRegenerateAt < 0d) return;
            if (EditorApplication.timeSinceStartup < _autoRegenerateAt) return;

            _autoRegenerateAt = -1d;
            if (_transientPreviewDefinition != null)
            {
                CreatureDefinition previewDefinition = _transientPreviewDefinition;
                _transientPreviewDefinition = null;
                EnqueuePreviewGeneration(previewDefinition);
            }
            else
            {
                RegeneratePreview();
            }
        }
    }
}
