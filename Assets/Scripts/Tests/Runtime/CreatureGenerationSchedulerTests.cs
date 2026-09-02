using System.Threading;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Generation;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class CreatureGenerationSchedulerTests
    {
        [Test]
        public void AsyncGeneration_MatchesSynchronousMeshAndColors()
        {
            CreatureDefinition definition = CreateDefinition();
            GeneratedCreature synchronous = CreatureMeshGenerator.Generate(definition, out _);

            using (var scheduler = new CreatureGenerationScheduler())
            {
                scheduler.Enqueue(definition);
                CreatureGenerationResult result = WaitForResult(scheduler);
                Assert.IsTrue(result.Succeeded, result.Exception?.ToString());

                GeneratedCreature asynchronous = CreatureMeshGenerator.Assemble(result.Data);
                AssertMeshEqual(synchronous.MainMesh, asynchronous.MainMesh);
            }
        }

        [Test]
        public void NewerRequest_MakesOlderCompletedResultStale()
        {
            using (var scheduler = new CreatureGenerationScheduler())
            {
                scheduler.Enqueue(CreateDefinition());
                scheduler.Enqueue(CreateDefinition());

                CreatureGenerationResult first = WaitForResult(scheduler);
                CreatureGenerationResult second = WaitForResult(scheduler);
                Assert.AreNotEqual(first.Sequence, second.Sequence);
                Assert.IsTrue(first.IsStale || second.IsStale);
                Assert.IsFalse(first.IsStale && second.IsStale);
            }
        }

        private static CreatureGenerationResult WaitForResult(CreatureGenerationScheduler scheduler)
        {
            for (int attempt = 0; attempt < 600; attempt++)
            {
                if (scheduler.TryTakeCompleted(out CreatureGenerationResult result)) return result;
                Thread.Sleep(50);
            }
            Assert.Fail("Timed out waiting for asynchronous generation.");
            return null;
        }

        private static CreatureDefinition CreateDefinition()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Forward = Vector3.forward;
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 4f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, -1f), Radius = 0.8f });
            definition.Body.Samples.Add(new BodySample { Id = 2, Position = new Vector3(0f, 0f, 0f), Radius = 0.9f });
            definition.Body.Samples.Add(new BodySample { Id = 3, Position = new Vector3(0f, 0f, 1f), Radius = 0.8f });
            return definition;
        }

        private static void AssertMeshEqual(Mesh expected, Mesh actual)
        {
            Assert.AreEqual(expected.vertices, actual.vertices);
            Assert.AreEqual(expected.triangles, actual.triangles);
            Assert.AreEqual(expected.colors, actual.colors);
        }
    }
}