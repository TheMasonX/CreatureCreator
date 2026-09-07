using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Common;
using ProceduralCreature.Generation;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class GeneratedCreatureOwnershipTests
    {
        [Test]
        public void GeometryItem_CopiesMaterialRegionCollectionAtConstruction()
        {
            using (var mesh = CreateTriangleMesh())
            {
                var regions = new List<MaterialRegion>
                {
                    new MaterialRegion(0, 0, 3, "body"),
                };
                var item = new GeometryItem(
                    "part",
                    GeometryType.MeshAsset,
                    mesh,
                    mesh,
                    Matrix4x4.identity,
                    regions,
                    new RigBindingMetadata("part", "body", false));

                regions.Add(new MaterialRegion(0, 0, 0, "other"));

                Assert.AreEqual(1, item.MaterialRegions.Count);
                Assert.AreEqual("body", item.MaterialRegions[0].MaterialKey);
                Assert.IsTrue(item.MaterialRegions is IList<MaterialRegion>);
                Assert.IsTrue(((IList<MaterialRegion>)item.MaterialRegions).IsReadOnly);
            }
        }

        private static Mesh CreateTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
                triangles = new[] { 0, 1, 2 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
