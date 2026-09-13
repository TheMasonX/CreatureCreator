using NUnit.Framework;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class CreatureDefinitionCloneContractTests
    {
        [Test]
        public void Clone_NullPartsCollection_PreservesNullState()
        {
            CreatureDefinition definition = CreatureDefinition.CreateEmpty();
            definition.Parts = null;

            CreatureDefinition clone = definition.Clone();

            Assert.That(clone.Parts, Is.Null,
                "Cloning malformed authoring data must not silently normalize Parts to an empty collection.");
        }
    }
}
