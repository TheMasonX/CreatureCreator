using System.Collections.Generic;
using NUnit.Framework;
using ProceduralCreature.Animation.Binding;
using ProceduralCreature.Generation;
using UnityEngine;

namespace ProceduralCreature.Tests.Runtime
{
    public sealed class GeneratedCreatureOwnershipTests
    {
        [Test]
        public void GeometryItem_CopiesMaterialRegionCollectionAtConstruction()
        {
            Mesh mesh = CreateTriangleMesh();
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

        [Test]
        public void GeometryItem_DeeplyProtectsVertexInfluencesFromCallerMutation()
        {
            Mesh mesh = CreateTriangleMesh();
            VertexInfluence[][] influences =
            {
                new[] { new VertexInfluence(0, 1f) },
            };
            var item = new GeometryItem(
                "part",
                GeometryType.MeshAsset,
                mesh,
                mesh,
                Matrix4x4.identity,
                null,
                new RigBindingMetadata("part", "body", false),
                influences);

            influences[0][0] = new VertexInfluence(1, 0f);

            Assert.AreEqual(1, item.VertexInfluences.Count);
            Assert.AreEqual(1, item.VertexInfluences[0].Count);
            Assert.AreEqual(0, item.VertexInfluences[0][0].BoneIndex);
            Assert.AreEqual(1f, item.VertexInfluences[0][0].Weight);
            Assert.IsTrue(item.VertexInfluences[0] is IList<VertexInfluence>);
            Assert.IsTrue(((IList<VertexInfluence>)item.VertexInfluences[0]).IsReadOnly);
            Assert.IsTrue(item.VertexInfluences is IList<IReadOnlyList<VertexInfluence>>);
            Assert.IsTrue(((IList<IReadOnlyList<VertexInfluence>>)item.VertexInfluences).IsReadOnly);
        }

        [Test]
        public void GeneratedCreature_GeometryViewIsReadOnly()
        {
            Mesh mesh = CreateTriangleMesh();
            var generated = new GeneratedCreature();
            generated.AddGeometry(new GeometryItem(
                "part",
                GeometryType.MeshAsset,
                mesh,
                mesh,
                Matrix4x4.identity,
                null,
                new RigBindingMetadata("part", "body", false)));

            Assert.AreEqual(1, generated.Geometry.Count);
            Assert.IsTrue(generated.Geometry is IList<GeometryItem>);
            Assert.IsTrue(((IList<GeometryItem>)generated.Geometry).IsReadOnly);
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
