using System;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class GeneratedCreatureDataContractTests
    {
        private static CreatureDefinition DefinitionWithBody()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 8f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = Vector3.zero, Radius = 1f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.9f });
            return definition;
        }

        [Test]
        public void Constructor_RejectsMissingRequiredInputs()
        {
            GeneratedCreatureData valid = CreatureMeshGenerator.GenerateData(DefinitionWithBody());
            Color[] colors = { Color.white };

            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                null, valid.Snapshot, valid.MeshResult, colors, valid.TopologyReport));
            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                valid.Definition, null, valid.MeshResult, colors, valid.TopologyReport));
            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                valid.Definition, valid.Snapshot, null, colors, valid.TopologyReport));
            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                valid.Definition, valid.Snapshot, valid.MeshResult, null, valid.TopologyReport));
            Assert.Throws<ArgumentNullException>(() => new GeneratedCreatureData(
                valid.Definition, valid.Snapshot, valid.MeshResult, colors, null));
        }

        [Test]
        public void Constructor_DefensivelyCopiesColors()
        {
            GeneratedCreatureData valid = CreatureMeshGenerator.GenerateData(DefinitionWithBody());
            Color[] colors = { Color.red, Color.green };

            var data = new GeneratedCreatureData(
                valid.Definition, valid.Snapshot, valid.MeshResult, colors, valid.TopologyReport);
            colors[0] = Color.blue;

            Assert.AreEqual(Color.red, data.Colors[0]);
            Assert.AreEqual(2, data.Colors.Count);
        }
    }
}
