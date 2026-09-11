using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;
using ProceduralCreature.Morphology.Extraction;
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
            CreatureDefinition definition = CreateValidDefinition();
            var data = new GeneratedCreatureData(
                definition,
                ResolvedCreatureSnapshot.Resolve(definition),
                new MeshExtractionResult(),
                source,
                new MeshTopologyReport());

            source[0] = Color.blue;

            Assert.IsTrue(data.Colors is IReadOnlyList<Color>);
            Assert.AreEqual(Color.red, data.Colors[0], "generated data must not alias caller-owned color storage");
            Assert.AreEqual(Color.green, data.Colors[1]);
        }

        [Test]
        public void Constructor_DefensivelyCopiesDefinitionOwnership()
        {
            CreatureDefinition source = CreateValidDefinition();
            source.Forward = Vector3.right;
            source.Generation = GenerationSettings.Default;

            var data = new GeneratedCreatureData(
                source,
                ResolvedCreatureSnapshot.Resolve(source),
                new MeshExtractionResult(),
                new[] { Color.white },
                new MeshTopologyReport());

            source.Forward = Vector3.up;
            source.Generation.VoxelsPerUnit = 99;

            Assert.AreEqual(Vector3.right, data.Definition.Forward);
            Assert.AreNotEqual(99, data.Definition.Generation.VoxelsPerUnit);
            Assert.AreNotSame(source, data.Definition);
        }

        [Test]
        public void Constructor_NullColors_ThrowsDomainException()
        {
            CreatureDefinition definition = CreateValidDefinition();
            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                definition,
                ResolvedCreatureSnapshot.Resolve(definition),
                new MeshExtractionResult(),
                colors: null,
                topologyReport: new MeshTopologyReport()));
        }

        private static CreatureDefinition CreateValidDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Body.Samples.Clear();
            definition.Body.Samples.Add(new BodySample
            {
                Id = 1,
                Position = new Vector3(0f, 0f, -1f),
                Radius = 0.5f,
            });
            definition.Body.Samples.Add(new BodySample
            {
                Id = 2,
                Position = new Vector3(0f, 0f, 1f),
                Radius = 0.5f,
            });
            return definition;
        }
    }
}