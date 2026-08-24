using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Generation;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public class CreatureMeshPaletteTests
    {
        private CreatureMeshPalette _palette;
        private Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<CreatureMeshPalette>();
            _mesh = new Mesh { name = "Test Mesh" };
            _palette.Entries.Add(new CreatureMeshPalette.Entry { Key = "eye", Mesh = _mesh });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void TryResolve_UsesOrdinalStableKey()
        {
            Assert.IsTrue(_palette.TryResolve("eye", out Mesh resolved));
            Assert.AreSame(_mesh, resolved);
            Assert.IsFalse(_palette.TryResolve("Eye", out _));
        }

        [Test]
        public void TryResolve_RejectsBlankAndMissingEntries()
        {
            Assert.IsFalse(_palette.TryResolve("", out _));
            Assert.IsFalse(_palette.TryResolve("missing", out _));
        }

        [Test]
        public void GetUsableKeys_IsSortedAndDeduplicated()
        {
            _palette.Entries.Add(new CreatureMeshPalette.Entry { Key = "zeta", Mesh = _mesh });
            _palette.Entries.Add(new CreatureMeshPalette.Entry { Key = "eye", Mesh = _mesh });

            CollectionAssert.AreEqual(new[] { "eye", "zeta" }, _palette.GetUsableKeys());
        }

        [Test]
        public void HasDuplicateKeys_ReportsOrdinalDuplicate()
        {
            _palette.Entries.Add(new CreatureMeshPalette.Entry { Key = "eye", Mesh = _mesh });

            Assert.IsTrue(_palette.HasDuplicateKeys(out string duplicate));
            Assert.AreEqual("eye", duplicate);
        }
    }
}
