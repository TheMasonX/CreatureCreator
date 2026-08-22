using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// A minimal ScriptableObject whose sole purpose is to give
    /// CreatureEditorWindow something Unity's native Undo system can snapshot.
    /// CreatureDefinition itself is a plain C# object with no Unity
    /// serialization/scene dependency by design (§2.1), so it can't be the
    /// direct target of Undo.RecordObject — Unity's built-in Undo stack only
    /// knows how to diff serialized UnityEngine.Object fields. This wrapper
    /// holds the canonical JSON representation instead (the same
    /// JsonDnaSerializer output already used for disk save/load and session
    /// persistence), and lets Unity's own serialization-diffing Undo system do
    /// the actual snapshotting/restoring — no custom undo-stack code needed.
    ///
    /// Created via ScriptableObject.CreateInstance with HideFlags.HideAndDontSave
    /// (see CreatureEditorWindow.OnEnable) — never saved as an asset, never shown
    /// in the Project window. It exists only as an in-memory undo anchor for the
    /// lifetime of one CreatureEditorWindow instance and is destroyed in OnDisable.
    /// </summary>
    public sealed class CreatureUndoState : ScriptableObject
    {
        [SerializeField]
        private string _json = string.Empty;

        public string Json
        {
            get => _json;
            set => _json = value;
        }
    }
}
