using UnityEditor;
using ProceduralCreature.Definition;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Persists the editor's in-progress CreatureDefinition across Unity domain
    /// reloads (script recompilation, entering/exiting play mode) — resolves
    /// delta-audit item #5: "editor working state must survive domain reload
    /// without data loss."
    ///
    /// Uses SessionState deliberately, not EditorPrefs or a ScriptableObject
    /// asset: SessionState is per-editor-process key/value storage that survives
    /// domain reload but NOT an editor restart. That's exactly the scope of the
    /// problem being solved here — an accidental script-recompile shouldn't
    /// silently discard unsaved work — not a substitute for the explicit
    /// Save/Load-to-disk workflow CreatureEditorWindow's toolbar provides
    /// separately for actually keeping work across sessions.
    /// </summary>
    public static class CreatureEditorSession
    {
        private const string SessionKey = "ProceduralCreature.WorkingDefinitionJson";
        private static readonly IDnaSerializer Serializer = new JsonDnaSerializer();

        public static void Save(CreatureDefinition definition)
        {
            if (definition == null) return;
            string json = Serializer.Serialize(definition);
            SessionState.SetString(SessionKey, json);
        }

        /// <summary>
        /// Returns the persisted definition, or null if none exists yet or the
        /// persisted JSON is corrupted. Corrupted session data falls back to null
        /// rather than throwing: losing an unsaved edit to corrupted session
        /// state is unfortunate but recoverable (the user can undo their last
        /// disk save, or start over); crashing CreatureEditorWindow.OnEnable and
        /// making the window impossible to open again is worse.
        /// </summary>
        public static CreatureDefinition TryLoad()
        {
            string json = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return Serializer.Deserialize(json);
            }
            catch (DnaDeserializationException)
            {
                return null;
            }
        }

        public static void Clear()
        {
            SessionState.EraseString(SessionKey);
        }
    }
}
