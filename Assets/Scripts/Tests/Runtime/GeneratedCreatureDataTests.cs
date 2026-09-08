using NUnit.Framework;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using UnityEngine;
using System.Collections.Generic;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class GeneratedCreatureDataTests
    {
        [Test]
        public void Constructor_DefensivelyCopiesColors_AndExposesReadOnlyView()
        {
            Color[] source =
            {
                Color.red,
                Color.green,
            };
            var data = new GeneratedCreatureData(
                CreatureDefinition.CreateEmpty(),
                snapshot: null,
                meshResult: null,
                colors: source,
                topologyReport: null);

            source[0] = Color.blue;

            Assert.IsTrue(data.Colors is IReadOnlyList<Color>);
            Assert.AreEqual(Color.red, data.Colors[0], "generated data must not alias caller-owned color storage");
            Assert.AreEqual(Color.green, data.Colors[1]);
        }

        [Test]
        public void Constructor_DefensivelyCopiesDefinitionOwnership()
        {
            CreatureDefinition source = CreatureDefinition.CreateEmpty();
            source.Forward = Vector3.right;
            source.Generation = GenerationSettings.Default;

            var data = new GeneratedCreatureData(
                source,
                snapshot: null,
                meshResult: null,
                colors: new[] { Color.white },
                topologyReport: null);

            source.Forward = Vector3.up;
            source.Generation.VoxelPerUnit = 99;

            Assert.AreEqual(Vector3.right, data.Definition.Forward);
            Assert.AreNotEqual(99, data.Definition.Generation.VoxelPerUnit);
            Assert.AreNotSame(source, data.Definition);
        }

        [Test]
        public void Constructor_NullColors_ThrowsDomainException()
        {
            Assert.Throws<ProceduralCreature.Common.DomainException>(() => new GeneratedCreatureData(
                CreatureDefinition.CreateEmpty(),
                snapshot: null,
                meshResult: null,
                colors: null,
                topologyReport: null));
        }
    }
}
