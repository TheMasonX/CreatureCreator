using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CreatureEditorWindowPartTypeTests
    {
        [Test]
        public void DefaultPartNameFor_MatchesPartTypeName()
        {
            Assert.AreEqual("Part", CreatureEditorWindow.DefaultPartNameFor(PartType.Part));
            Assert.AreEqual("Eye", CreatureEditorWindow.DefaultPartNameFor(PartType.Eye));
            Assert.AreEqual("Limb", CreatureEditorWindow.DefaultPartNameFor(PartType.Limb));
            Assert.AreEqual("Leg", CreatureEditorWindow.DefaultPartNameFor(PartType.Leg));
        }

        [Test]
        public void ResolveDisplayName_DefaultNameFollowsNewType()
        {
            // A fresh "Part" part switched to Eye becomes "Eye".
            string next = CreatureEditorWindow.ResolveDisplayNameAfterTypeChange(
                "Part", PartType.Part, PartType.Eye);
            Assert.AreEqual("Eye", next);
        }

        [Test]
        public void ResolveDisplayName_DefaultNameFollowsWhenSwitchingAway()
        {
            string next = CreatureEditorWindow.ResolveDisplayNameAfterTypeChange(
                "Eye", PartType.Eye, PartType.Part);
            Assert.AreEqual("Part", next);
        }

        [Test]
        public void ResolveDisplayName_CustomNameIsPreserved()
        {
            string next = CreatureEditorWindow.ResolveDisplayNameAfterTypeChange(
                "MyEyeball", PartType.Part, PartType.Eye);
            Assert.AreEqual("MyEyeball", next);
        }

        [Test]
        public void ResolveDisplayName_NullOrEmptyNameIsPreserved()
        {
            Assert.IsNull(CreatureEditorWindow.ResolveDisplayNameAfterTypeChange(
                null, PartType.Part, PartType.Eye));
            Assert.AreEqual("", CreatureEditorWindow.ResolveDisplayNameAfterTypeChange(
                "", PartType.Part, PartType.Eye));
        }
    }
}
