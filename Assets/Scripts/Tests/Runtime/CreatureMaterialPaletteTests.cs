using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Appearance;
using ProceduralCreature.Common;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-028: the material palette asset (ordinal stable-key lookup, display
    /// names, duplicate detection) and the MaterialResolver policy (explicit key →
    /// palette → material; blank key → null fallback; set-but-unresolvable key →
    /// DomainException, never a silent drop). The palette lives in the Runtime
    /// assembly so the editor preview and runtime preview share one abstraction.
    ///
    /// Runtime assembly — per project convention this fixture is NOT discovered by
    /// the MCP runner; invoke its methods directly via execute_code for evidence.
    /// </summary>
    [TestFixture]
    public class CreatureMaterialPaletteTests
    {
        private CreatureMaterialPalette _palette;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<CreatureMaterialPalette>();
            _material = new Material(Shader.Find("Unlit/Color"));
            _palette.Entries.Add(new CreatureMaterialPalette.Entry { Key = "eye", DisplayName = "Eye White", Material = _material });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void TryResolve_UsesOrdinalStableKey()
        {
            Assert.IsTrue(_palette.TryResolve("eye", out Material resolved));
            Assert.AreSame(_material, resolved);
            Assert.IsFalse(_palette.TryResolve("Eye", out _));
        }

        [Test]
        public void TryResolve_RejectsBlankAndMissingEntries()
        {
            Assert.IsFalse(_palette.TryResolve("", out _));
            Assert.IsFalse(_palette.TryResolve("missing", out _));
        }

        [Test]
        public void TryResolve_IgnoresEntriesWithNullMaterial()
        {
            _palette.Entries.Add(new CreatureMaterialPalette.Entry { Key = "null_mat", Material = null });
            Assert.IsFalse(_palette.TryResolve("null_mat", out _));
        }

        [Test]
        public void GetUsableKeys_IsSortedAndDeduplicated()
        {
            _palette.Entries.Add(new CreatureMaterialPalette.Entry { Key = "zeta", Material = _material });
            _palette.Entries.Add(new CreatureMaterialPalette.Entry { Key = "eye", Material = _material });

            CollectionAssert.AreEqual(new[] { "eye", "zeta" }, _palette.GetUsableKeys());
        }

        [Test]
        public void GetDisplayName_FallsBackToKey()
        {
            Assert.AreEqual("Eye White", _palette.GetDisplayName("eye"));
            Assert.AreEqual("unknown", _palette.GetDisplayName("unknown"));
        }

        [Test]
        public void HasDuplicateKeys_ReportsOrdinalDuplicate()
        {
            _palette.Entries.Add(new CreatureMaterialPalette.Entry { Key = "eye", Material = _material });

            Assert.IsTrue(_palette.HasDuplicateKeys(out string duplicate));
            Assert.AreEqual("eye", duplicate);
        }

        [Test]
        public void Resolve_BlankKey_ReturnsNullForNearestPartFallback()
        {
            Assert.IsNull(MaterialResolver.Resolve(_palette, null));
            Assert.IsNull(MaterialResolver.Resolve(_palette, "   "));
        }

        [Test]
        public void Resolve_ResolvesUsableKey()
        {
            Assert.AreSame(_material, MaterialResolver.Resolve(_palette, "eye"));
        }

        [Test]
        public void Resolve_SetKeyWithoutPalette_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => MaterialResolver.Resolve(null, "eye"));
        }

        [Test]
        public void Resolve_UnresolvableSetKey_ThrowsDomainException()
        {
            Assert.Throws<DomainException>(() => MaterialResolver.Resolve(_palette, "missing"));
        }

        [Test]
        public void TryResolveDefault_ReturnsFalseWhenKeyBlank()
        {
            Assert.IsFalse(_palette.TryResolveDefault(out _));
        }

        [Test]
        public void TryResolveDefault_ResolvesConfiguredKey()
        {
            SetDefaultMaterialKey(_palette, "eye");
            Assert.IsTrue(_palette.TryResolveDefault(out Material resolved));
            Assert.AreSame(_material, resolved);
        }

        [Test]
        public void TryResolveDefault_ReturnsFalseForUnresolvableKey()
        {
            SetDefaultMaterialKey(_palette, "missing");
            Assert.IsFalse(_palette.TryResolveDefault(out _));
        }

        [Test]
        public void ResolveDefault_NullPalette_ReturnsNull()
        {
            Assert.IsNull(MaterialResolver.ResolveDefault(null));
        }

        [Test]
        public void ResolveDefault_BlankKey_ReturnsNull()
        {
            Assert.IsNull(MaterialResolver.ResolveDefault(_palette));
        }

        [Test]
        public void ResolveDefault_ResolvesConfiguredKey()
        {
            SetDefaultMaterialKey(_palette, "eye");
            Assert.AreSame(_material, MaterialResolver.ResolveDefault(_palette));
        }

        [Test]
        public void ResolveDefault_UnresolvableKey_ReturnsNullWithoutThrowing()
        {
            SetDefaultMaterialKey(_palette, "missing");
            Assert.IsNull(MaterialResolver.ResolveDefault(_palette));
        }

        private static void SetDefaultMaterialKey(CreatureMaterialPalette palette, string key)
        {
            var field = typeof(CreatureMaterialPalette).GetField(
                "defaultMaterialKey",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, "defaultMaterialKey field must exist.");
            field.SetValue(palette, key);
        }
    }
}
