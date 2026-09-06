using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Orientation-correctness gate for the extraction pipeline (CCWIND).
    ///
    /// The production <see cref="MeshTopologyValidator"/> proves only boundary /
    /// non-manifold edge counts, because <c>CountEdge</c> canonicalizes each edge
    /// to an unordered (min,max) vertex pair and discards direction. It therefore
    /// cannot detect a local winding inversion: two adjacent triangles traversing
    /// a shared edge in the SAME direction still count as a clean 2-use edge.
    ///
    /// These tests add the directed-edge invariant the validator intentionally
    /// leaves out: for every undirected edge used by exactly two triangles, the
    /// two traversals must be opposite. This is test-only and cheap — it runs over
    /// an already-built MeshExtractionResult and adds no structure to the hot
    /// extraction path. A single locally-reversed triangle fails it.
    /// </summary>
    [TestFixture]
    public class MeshWindingOrientationTests
    {
        /// <summary>
        /// Counts undirected edges used by exactly two triangles whose two
        /// traversals run in the SAME direction (a local winding inversion).
        /// Returns 0 for a consistently-oriented closed surface.
        /// </summary>
        private static int CountCoDirectionalEdges(MeshExtractionResult mesh)
        {
            var directedUses = new Dictionary<(int, int), List<(int A, int B)>>();

            for (int i = 0; i < mesh.Triangles.Count; i += 3)
            {
                int a = mesh.Triangles[i];
                int b = mesh.Triangles[i + 1];
                int c = mesh.Triangles[i + 2];
                AddDirectedEdge(directedUses, a, b);
                AddDirectedEdge(directedUses, b, c);
                AddDirectedEdge(directedUses, c, a);
            }

            int violations = 0;
            foreach (KeyValuePair<(int, int), List<(int A, int B)>> kv in directedUses)
            {
                List<(int A, int B)> uses = kv.Value;
                if (uses.Count != 2) continue; // boundary/non-manifold are already covered by MeshTopologyValidator
                // Consistent: (A,B) and (B,A). A local inversion yields (A,B) and (A,B).
                if (!(uses[0].A == uses[1].B && uses[0].B == uses[1].A))
                {
                    violations++;
                }
            }
            return violations;
        }

        private static void AddDirectedEdge(
            Dictionary<(int, int), List<(int A, int B)>> directedUses, int a, int b)
        {
            (int lo, int hi) = a < b ? (a, b) : (b, a);
            if (!directedUses.TryGetValue((lo, hi), out List<(int A, int B)> list))
            {
                list = new List<(int A, int B)>(2);
                directedUses[(lo, hi)] = list;
            }
            list.Add((a, b));
        }

        /// <summary>
        /// For a sphere centered at the origin: every non-degenerate triangle's
        /// face normal must point outward from the surface, i.e.
        /// <c>dot(faceNormal, centroid) &gt; 0</c>.
        /// </summary>
        private static void AssertSphereOutward(MeshExtractionResult mesh)
        {
            int checkedTriangles = 0;
            for (int i = 0; i < mesh.Triangles.Count; i += 3)
            {
                int a = mesh.Triangles[i];
                int b = mesh.Triangles[i + 1];
                int c = mesh.Triangles[i + 2];
                Vector3 pa = mesh.Positions[a];
                Vector3 pb = mesh.Positions[b];
                Vector3 pc = mesh.Positions[c];
                Vector3 faceNormal = Vector3.Cross(pb - pa, pc - pa);
                if (faceNormal.sqrMagnitude < 1e-12f) continue; // skip degenerate
                Vector3 centroid = (pa + pb + pc) / 3f;
                float dot = Vector3.Dot(faceNormal, centroid);
                Assert.Greater(dot, 0f,
                    $"Triangle {i / 3} at centroid {centroid} faces inward (dot(faceNormal, centroid) = {dot}).");
                checkedTriangles++;
            }
            Assert.Greater(checkedTriangles, 0, "Sphere mesh should have non-degenerate triangles.");
        }

        private static void AssertConsistentAndWatertight(MeshExtractionResult mesh, string label)
        {
            Assert.AreEqual(0, CountCoDirectionalEdges(mesh),
                $"{label}: a shared edge is traversed twice in the same direction (local winding inversion).");

            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
            Assert.IsTrue(report.IsWatertight,
                $"{label}: expected watertight; boundary={report.BoundaryEdgeCount} nonManifold={report.NonManifoldEdgeCount}.");
        }

        private static CreatureDefinition Sphere(float radius)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = radius, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default });
            return definition;
        }

        private static CreatureDefinition TwoSpheres(float blendRadius)
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.AddPart(new CreaturePart { Id = "sphere_a", Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = blendRadius },
                Appearance = AppearanceDefinition.Default });
            definition.AddPart(new CreaturePart { Id = "sphere_b",
                Transform = new TransformData { Position = new Vector3(1.5f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 1f, SmoothBlendRadius = blendRadius },
                Appearance = AppearanceDefinition.Default });
            return definition;
        }

        private static MeshExtractionResult Extract(CreatureDefinition definition, BoundsDefinition bounds, float vpu)
        {
            var settings = new GenerationSettings { VoxelsPerUnit = vpu };
            MeshExtractionResult mesh;
            using (DensityGrid grid = DensityGrid.SamplePortable(SdfProgramBuilder.CompilePortable(definition), bounds, settings))
            {
                mesh = MarchingCubesExtractor.Extract(grid);
            }
            return mesh;
        }

        [Test]
        public void Sphere_IsOutwardAndConsistentlyWound()
        {
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            MeshExtractionResult mesh = Extract(Sphere(1f), bounds, 8f);

            AssertSphereOutward(mesh);
            AssertConsistentAndWatertight(mesh, "sphere @ VPU 8");
        }

        [Test]
        public void Sphere_CoarseResolution_StaysConsistent()
        {
            // Coarse resolutions make orientation marginal (resolution-dependent
            // winding flips, CCWIND W-09), so this is a sharper stress than a fine
            // sphere.
            var bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            MeshExtractionResult mesh = Extract(Sphere(1f), bounds, 4f);

            AssertSphereOutward(mesh);
            AssertConsistentAndWatertight(mesh, "sphere @ VPU 4");
        }

        [Test]
        public void OverlappingSpheres_AreConsistentlyWound()
        {
            var bounds = new BoundsDefinition { MaxX = 3f, MaxY = 1.5f, MaxZ = 1.5f };
            MeshExtractionResult mesh = Extract(TwoSpheres(0f), bounds, 5f);
            AssertConsistentAndWatertight(mesh, "overlapping spheres (hard union)");
        }

        [Test]
        public void SmoothUnionSpheres_AreConsistentlyWound()
        {
            // A smooth union produces a saddle/weak-gradient crease at the blend —
            // the region most prone to a marginal winding flip (CCWIND W-02/W-07).
            var bounds = new BoundsDefinition { MaxX = 3f, MaxY = 1.5f, MaxZ = 1.5f };
            MeshExtractionResult mesh = Extract(TwoSpheres(0.4f), bounds, 5f);
            AssertConsistentAndWatertight(mesh, "smooth-union spheres");
        }
    }
}
