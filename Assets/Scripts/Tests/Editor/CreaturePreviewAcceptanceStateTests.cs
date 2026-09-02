using NUnit.Framework;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CreaturePreviewAcceptanceStateTests
    {
        [Test]
        public void UnknownPreviewIsStale()
        {
            var state = new CreaturePreviewAcceptanceState();

            Assert.IsFalse(state.HasAcceptedPreview);
            Assert.IsTrue(state.IsStale("revision", "body"));
        }

        [Test]
        public void AcceptedRevisionAndPlacementFingerprintAreFresh()
        {
            var state = new CreaturePreviewAcceptanceState();
            state.Accept("revision", "body");

            Assert.IsTrue(state.HasAcceptedPreview);
            Assert.IsFalse(state.IsStale("revision", "body"));
        }

        [Test]
        public void RevisionChangeMakesPreviewStale()
        {
            var state = new CreaturePreviewAcceptanceState();
            state.Accept("revision-1", "body");

            Assert.IsTrue(state.IsStale("revision-2", "body"));
        }

        [Test]
        public void PlacementFingerprintChangeMakesPreviewStale()
        {
            var state = new CreaturePreviewAcceptanceState();
            state.Accept("revision", "body-1");

            Assert.IsTrue(state.IsStale("revision", "body-2"));
        }

        [Test]
        public void ClearReturnsToUnknownStaleState()
        {
            var state = new CreaturePreviewAcceptanceState();
            state.Accept("revision", "body");
            state.Clear();

            Assert.IsFalse(state.HasAcceptedPreview);
            Assert.IsTrue(state.IsStale("revision", "body"));
        }
    }
}