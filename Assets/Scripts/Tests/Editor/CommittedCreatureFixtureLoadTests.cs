using System;
using System.Linq;
using NUnit.Framework;
using ProceduralCreature.Serialization;
using UnityEditor;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CommittedCreatureFixtureLoadTests
    {
        private const string FixtureFolder = "Assets/Creatures";
        private static readonly string[] IntentionallyUnsupportedFixtures =
        {
            "Assets/Creatures/first_creature.json",
            "Assets/Creatures/first_creature_bak.json",
        };

        [Test]
        public void AllCommittedCreatureFixtures_LoadUnderStrictJsonGrammar()
        {
            string[] fixturePaths = AssetDatabase.FindAssets("t:TextAsset", new[] { FixtureFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.That(fixturePaths, Is.Not.Empty,
                $"No committed creature JSON fixtures were discovered under {FixtureFolder}.");
            TestContext.WriteLine($"Discovered {fixturePaths.Length} committed creature JSON fixtures.");

            var serializer = new JsonDnaSerializer();
            foreach (string fixturePath in fixturePaths)
            {
                TextAsset fixture = AssetDatabase.LoadAssetAtPath<TextAsset>(fixturePath);
                Assert.IsNotNull(fixture, $"Could not load fixture asset {fixturePath}.");

                if (IntentionallyUnsupportedFixtures.Contains(fixturePath))
                {
                    DnaDeserializationException exception = Assert.Throws<DnaDeserializationException>(
                        () => serializer.Deserialize(fixture.text),
                        $"Expected intentionally unsupported fixture to be rejected: {fixturePath}");
                    StringAssert.Contains("Schema version 1 is unsupported", exception.Message);
                    continue;
                }

                Assert.DoesNotThrow(
                    () => serializer.Deserialize(fixture.text),
                    $"Committed creature fixture failed strict JSON/DNA deserialization: {fixturePath}");
            }
        }
    }
}