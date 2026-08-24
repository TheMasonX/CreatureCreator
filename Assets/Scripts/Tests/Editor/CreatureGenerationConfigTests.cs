using NUnit.Framework;
using ProceduralCreature.Appearance;
using ProceduralCreature.Generation;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    [TestFixture]
    public sealed class CreatureGenerationConfigTests
    {
        [Test]
        public void ConfigReferencesSharedPaletteTypes()
        {
            CreatureGenerationConfig config = ScriptableObject.CreateInstance<CreatureGenerationConfig>();
            CreatureMeshPalette meshPalette = ScriptableObject.CreateInstance<CreatureMeshPalette>();
            CreatureMaterialPalette materialPalette = ScriptableObject.CreateInstance<CreatureMaterialPalette>();

            try
            {
                SerializedObjectUtility.SetPrivateField(config, "meshPalette", meshPalette);
                SerializedObjectUtility.SetPrivateField(config, "materialPalette", materialPalette);

                Assert.AreSame(meshPalette, config.MeshPalette);
                Assert.AreSame(materialPalette, config.MaterialPalette);
                Assert.Greater(config.DefaultVoxelsPerUnit, 0f);
            }
            finally
            {
                Object.DestroyImmediate(materialPalette);
                Object.DestroyImmediate(meshPalette);
                Object.DestroyImmediate(config);
            }
        }
    }

    internal static class SerializedObjectUtility
    {
        public static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var serialized = new UnityEditor.SerializedObject(target);
            serialized.FindProperty(fieldName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
