using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CreatureEditorSessionTests
    {
        [SetUp]
        [TearDown]
        public void ClearSessionState()
        {
            CreatureEditorSession.Clear();
        }

        [Test]
        public void TryLoad_ReturnsNullWhenNothingHasBeenSaved()
        {
            Assert.IsNull(CreatureEditorSession.TryLoad());
        }

        [Test]
        public void SaveThenTryLoad_RoundTripsTheDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.SymmetryMode = SymmetryMode.MirrorAcrossXAxis;
            definition.AddPart(new CreaturePart
            {
                Id = "part_a",
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere,
                Appearance = AppearanceDefinition.Default,
            });

            CreatureEditorSession.Save(definition);
            CreatureDefinition loaded = CreatureEditorSession.TryLoad();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(SymmetryMode.MirrorAcrossXAxis, loaded.SymmetryMode);
            Assert.AreEqual(1, loaded.Parts.Count);
            Assert.AreEqual("part_a", loaded.Parts[0].Id);
        }

        [Test]
        public void Save_OverwritesPreviouslySavedState()
        {
            var first = CreatureDefinition.CreateEmpty();
            first.AddPart(new CreaturePart
            {
                Id = "part_first", Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            CreatureEditorSession.Save(first);

            var second = CreatureDefinition.CreateEmpty();
            second.AddPart(new CreaturePart
            {
                Id = "part_second", Transform = TransformData.Identity,
                Shape = ShapeDefinition.DefaultSphere, Appearance = AppearanceDefinition.Default,
            });
            CreatureEditorSession.Save(second);

            CreatureDefinition loaded = CreatureEditorSession.TryLoad();
            Assert.AreEqual("part_second", loaded.Parts[0].Id);
        }

        [Test]
        public void Clear_RemovesSavedState()
        {
            CreatureEditorSession.Save(CreatureDefinition.CreateEmpty());
            CreatureEditorSession.Clear();
            Assert.IsNull(CreatureEditorSession.TryLoad());
        }

        [Test]
        public void Save_NullDefinition_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CreatureEditorSession.Save(null));
        }
    }
}
