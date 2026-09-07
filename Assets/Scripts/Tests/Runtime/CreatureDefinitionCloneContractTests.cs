using NUnit.Framework;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class CreatureDefinitionCloneContractTests
    {
        [Test]
        public void Clone_NullPartsCollection_PreservesNullState()
        {
            var definition = CreatureDefinition.CreateEmpty
            {
                Parts = null,
            };

            CreatureDefinition clone = definition.Clone();

            Assert.IsNull(clone.Parts,
                "Cloning malformed authoring data must not silently normalize Parts to an empty collection.");
        }
    }
}
