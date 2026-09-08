using System;
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
            Assert.IsTrue(synchronous.TryGetImplicitSurface(out GeometryItem syncImplicit),
                "synchronous output must include the implicit mesh");

            using (var scheduler = new CreatureGenerationScheduler())
            {
                scheduler.Enqueue(definition);
                CreatureGenerationResult result = WaitForResult(scheduler);
                Assert.IsTrue(result.Succeeded, result.Exception?.ToString());

                GeneratedCreature asynchronous = CreatureMeshGenerator.Assemble(result.Data);
                Assert.IsTrue(asynchronous.TryGetImplicitSurface(out GeometryItem asyncImplicit),
                    "asynchronous output must include the implicit mesh");
                AssertMeshEqual(syncImplicit.Mesh, asyncImplicit.Mesh);
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

        [Test]
        public void GenerationFailure_IsReturnedAsFailedResultInsteadOfEscapingWorker()
        {
            CreatureDefinition invalid = CreateDefinition();
            invalid.Forward = Vector3.zero;

            using (var scheduler = new CreatureGenerationScheduler())
            {
                scheduler.Enqueue(invalid);
                CreatureGenerationResult result = WaitForResult(scheduler);

                Assert.IsFalse(result.Succeeded);
                Assert.IsNotNull(result.Exception);
                Assert.IsNull(result.Data);
            }
        }

        [Test]
        public void Enqueue_AfterDispose_ThrowsObjectDisposedException()
        {
            var scheduler = new CreatureGenerationScheduler();
            scheduler.Dispose();

            Assert.Throws<ObjectDisposedException>(() => scheduler.Enqueue(CreateDefinition()));
        }

        [Test]
        public void ResultCompletedAfterDispose_IsMarkedStale()
        {
            var scheduler = new CreatureGenerationScheduler();
            scheduler.Enqueue(CreateDefinition());
            scheduler.Dispose();

            CreatureGenerationResult result = WaitForResult(scheduler);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsStale,
                "disposing the scheduler advances the latest sequence so already-running work cannot become current");
            scheduler.Dispose();
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