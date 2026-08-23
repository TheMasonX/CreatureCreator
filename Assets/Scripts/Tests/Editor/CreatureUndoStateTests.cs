using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;
using ProceduralCreature.Serialization;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CreatureUndoStateTests
    {
        [Test]
        public void Json_DefaultsToEmptyString()
        {
            var state = ScriptableObject.CreateInstance<CreatureUndoState>();
            try
            {
                Assert.AreEqual(string.Empty, state.Json);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void Json_RoundTripsThroughTheProperty()
        {
            var state = ScriptableObject.CreateInstance<CreatureUndoState>();
            try
            {
                state.Json = "{\"schemaVersion\":1}";
                Assert.AreEqual("{\"schemaVersion\":1}", state.Json);
            }
            finally
            {
                Object.DestroyImmediate(state);
            }
        }

        [Test]
        public void OnUndoRedoPerformed_SchedulesAutoRegenerationWhenEnabled()
        {
            var window = ScriptableObject.CreateInstance<CreatureEditorWindow>();
            var undoState = ScriptableObject.CreateInstance<CreatureUndoState>();
            try
            {
                var autoRegenerateField = typeof(CreatureEditorWindow)
                    .GetField("_autoRegenerate", BindingFlags.Instance | BindingFlags.NonPublic);
                var autoRegenerateAtField = typeof(CreatureEditorWindow)
                    .GetField("_autoRegenerateAt", BindingFlags.Instance | BindingFlags.NonPublic);
                var undoStateField = typeof(CreatureEditorWindow)
                    .GetField("_undoState", BindingFlags.Instance | BindingFlags.NonPublic);

                autoRegenerateField.SetValue(window, true);
                autoRegenerateAtField.SetValue(window, -1d);
                undoStateField.SetValue(window, undoState);

                var definition = CreatureDefinition.CreateEmpty();
                definition.Forward = Vector3.forward;
                undoState.Json = new JsonDnaSerializer().Serialize(definition);

                var method = typeof(CreatureEditorWindow)
                    .GetMethod("OnUndoRedoPerformed", BindingFlags.Instance | BindingFlags.NonPublic);
                method.Invoke(window, null);

                Assert.Greater((double)autoRegenerateAtField.GetValue(window), EditorApplication.timeSinceStartup - 1d);
            }
            finally
            {
                Object.DestroyImmediate(undoState);
                Object.DestroyImmediate(window);
            }
        }
    }
}
