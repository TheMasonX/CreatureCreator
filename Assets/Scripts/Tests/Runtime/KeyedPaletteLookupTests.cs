using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Common;

namespace ProceduralCreature.Tests.Runtime
{
    [TestFixture]
    public sealed class KeyedPaletteLookupTests
    {
        private sealed class Entry
        {
            public string Key;
            public bool Usable;
        }

        [Test]
        public void TryResolve_NullCollection_ReturnsFalse()
        {
            bool resolved = KeyedPaletteLookup.TryResolve<Entry>(
                null,
                "mesh",
                entry => entry.Key,
                entry => entry.Usable,
                out Entry match);

            Assert.IsFalse(resolved);
            Assert.IsNull(match);
        }

        [Test]
        public void TryResolve_NullEntriesAreIgnored()
        {
            var entries = new List<Entry>
            {
                null,
                new Entry { Key = "mesh", Usable = true },
            };

            bool resolved = KeyedPaletteLookup.TryResolve(
                entries,
                "mesh",
                entry => entry.Key,
                entry => entry.Usable,
                out Entry match);

            Assert.IsTrue(resolved);
            Assert.AreEqual("mesh", match.Key);
        }

        [Test]
        public void GetUsableKeys_NullCollectionReturnsEmpty()
        {
            string[] keys = KeyedPaletteLookup.GetUsableKeys<Entry>(
                null,
                entry => entry.Key,
                entry => entry.Usable);

            Assert.IsNotNull(keys);
            Assert.AreEqual(0, keys.Length);
        }

        [Test]
        public void GetUsableKeys_DropsBlankKeysAndSortsOrdinalDistinct()
        {
            var entries = new List<Entry>
            {
                new Entry { Key = "z", Usable = true },
                new Entry { Key = "a", Usable = true },
                new Entry { Key = "z", Usable = true },
                new Entry { Key = "", Usable = true },
                new Entry { Key = "ignored", Usable = false },
                null,
            };

            string[] keys = KeyedPaletteLookup.GetUsableKeys(
                entries,
                entry => entry.Key,
                entry => entry.Usable);

            CollectionAssert.AreEqual(new[] { "a", "z" }, keys);
        }

        [Test]
        public void HasDuplicateKeys_NullCollectionReturnsFalse()
        {
            bool duplicate = KeyedPaletteLookup.HasDuplicateKeys<Entry>(
                null,
                entry => entry.Key,
                out string duplicateKey);

            Assert.IsFalse(duplicate);
            Assert.IsNull(duplicateKey);
        }

        [Test]
        public void HasDuplicateKeys_IgnoresBlankKeysAndUsesOrdinalMatching()
        {
            var entries = new List<Entry>
            {
                new Entry { Key = "mesh", Usable = true },
                new Entry { Key = "mesh", Usable = true },
                new Entry { Key = "MESH", Usable = true },
                new Entry { Key = "", Usable = true },
                null,
            };

            bool duplicate = KeyedPaletteLookup.HasDuplicateKeys(
                entries,
                entry => entry.Key,
                out string duplicateKey);

            Assert.IsTrue(duplicate);
            Assert.AreEqual("mesh", duplicateKey);
        }
    }
}
