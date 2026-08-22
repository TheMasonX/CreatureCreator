using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Editor;

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
    }
}
