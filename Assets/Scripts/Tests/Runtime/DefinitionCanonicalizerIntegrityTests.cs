using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class DefinitionCanonicalizerIntegrityTests
    {
        [Test]
        public void Canonicalize_RejectsInvalidCapsuleAxisInsteadOfRepairingIt()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = UnityEngine.Vector3.zero,
                Radius = 1f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = UnityEngine.Vector3.up,
                Radius = 1f,
            });
            definition.Parts.Add(new CreaturePart
            {
                Id = "part",
                PartType = PartType.Eye,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition
                {
                    Type = ShapeType.Capsule,
                    PrimarySize = 0.5f,
                    Radius = 0.5f,
                    CapsuleAxis = (ShapeAxis)999,
                    CapsuleHeight = 1f,
                    EllipsoidRadii = UnityEngine.Vector3.one,
                    BoxHalfExtents = UnityEngine.Vector3.one,
                    SmoothBlendRadius = 0.1f,
                },
                Appearance = AppearanceDefinition.Default,
            });

            Assert.Throws<DomainException>(() => DefinitionCanonicalizer.Canonicalize(definition));
            Assert.AreEqual((ShapeAxis)999, definition.Parts[0].Shape.CapsuleAxis,
                "Canonicalization must not mutate or silently repair the authored value.");
        }
    }
}
