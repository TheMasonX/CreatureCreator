using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology.Extraction;
using ProceduralCreature.Morphology.Sdf;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// Extraction-side regression gate for the DensityGrid gradient policy
    /// (TSK-0065 / S01).
    ///
    /// CC-064 defines a sampled scalar field's non-finite contract: fast samples
    /// may read <c>+inf</c> (outside/culled), never NaN; consumers must treat
    /// <c>+inf</c> as absent, not as a giant finite distance. The S01 audit
    /// concern was that a gradient consumer could subtract an absent <c>+inf</c>
    /// sample as if it were finite, producing an invalid/NaN winding reference.
    ///
    /// Current production already guards this on both paths:
    ///   * <see cref="DensityGrid.TryEstimateGradient"/> returns false when the
    ///     containing cell has any non-finite (+inf/NaN) corner, so the winding
    ///     caller never trusts loop order there; and
    ///   * the winding fallback <see cref="DensityGrid.EstimateGradient"/>
    ///     (per-axis <c>EstimateAxis</c>) is finite-aware: a non-finite center
    ///     yields 0, and a non-finite neighbor is skipped in favor of a one-sided
    ///     finite difference, never <c>+inf - +inf</c> as if finite.
    ///
    /// These tests lock that policy across finite, one-sided (field present on
    /// one side of an absent boundary), and invalid/absent (+inf / NaN corner)
    /// cases, and assert the end-to-end winding decision never consumes
    /// NaN/Inf — no emitted triangle has a non-finite winding reference and no
    /// NaN/Inf appears in output vertex positions.
    ///
    /// Deterministic fixtures: the unit tests hand-write a controlled linear
    /// ramp field (v(x,y,z) = world x) into an otherwise-sampled grid via the
    /// internal <see cref="DensityGrid.MutableSamples"/> seam so individual
    /// corners can be forced to +inf / NaN exactly. The winding test runs real
    /// extraction over a cull-boundary-bearing creature.
    /// </summary>
    [TestFixture]
    public class DensityGridGradientPolicyTests
    {
        // ---- deterministic field + grid construction ---------------------

        /// <summary>
        /// Corner world-x position, the value used for the linear ramp field
        /// v(x,y,z) = world x, whose gradient is exactly +1 in x.
        /// </summary>
        private static float RampValue(DensityGrid grid, int x, int y, int z)
        {
            return grid.CornerPosition(x, 0, 0).x;
        }

        /// <summary>
        /// Grid-corner flat index matching DensityGrid's internal Index layout
        /// (corners-per-axis = cells + 1).
        /// </summary>
        private static int CornerIndex(DensityGrid grid, int x, int y, int z)
        {
            int cx = grid.CellsX + 1;
            int cy = grid.CellsY + 1;
            return (z * cy + y) * cx + x;
        }

        /// <summary>
        /// Samples a small real grid, then overwrites every corner with the
        /// linear ramp field so tests can control each corner exactly. All 8
        /// corners of a given cell share the same y,z and differ only in x, so
        /// the field is a pure +x ramp. Caller must dispose the returned grid.
        /// </summary>
        private static DensityGrid BuildRampGrid()
        {
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1f, MaxY = 1f, MaxZ = 1f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 2f };
            definition.AddPart(new CreaturePart
            {
                Id = "sphere",
                PartType = PartType.Body,
                Transform = TransformData.Identity,
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.5f, SmoothBlendRadius = 0f },
                Appearance = AppearanceDefinition.Default,
            });

            DensityGrid grid;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            {
                grid = DensityGrid.SamplePortable(program, definition.Bounds, definition.Generation);
            }

            NativeArray<float> samples = grid.MutableSamples;
            for (int z = 0; z <= grid.CellsZ; z++)
            for (int y = 0; y <= grid.CellsY; y++)
            for (int x = 0; x <= grid.CellsX; x++)
            {
                samples[CornerIndex(grid, x, y, z)] = RampValue(grid, x, y, z);
            }
            return grid;
        }

        /// <summary>
        /// World-space center of grid cell (cx, cy, cz) — an interior point whose
        /// containing cell is exactly (cx, cy, cz).
        /// </summary>
        private static Vector3 CellCenter(DensityGrid grid, int cx, int cy, int cz)
        {
            return grid.CornerPosition(cx, cy, cz) + new Vector3(0.5f, 0.5f, 0.5f) * grid.CellSize;
        }

        private static void AssertFinite(Vector3 v, string label)
        {
            Assert.IsFalse(float.IsNaN(v.x) || float.IsInfinity(v.x), $"{label}: x non-finite ({v.x}).");
            Assert.IsFalse(float.IsNaN(v.y) || float.IsInfinity(v.y), $"{label}: y non-finite ({v.y}).");
            Assert.IsFalse(float.IsNaN(v.z) || float.IsInfinity(v.z), $"{label}: z non-finite ({v.z}).");
        }

        // ---- gradient policy: finite cell ---------------------------------

        [Test]
        public void FiniteCell_TryEstimateGradient_ReturnsFiniteIncreasingGradient()
        {
            using (DensityGrid grid = BuildRampGrid())
            {
                // Cell (2,2,2) spans x world [0, 0.5]; all 8 corners finite.
                Vector3 point = CellCenter(grid, 2, 2, 2);

                Assert.IsTrue(grid.TryEstimateGradient(point, out Vector3 gradient),
                    "An all-finite cell must produce a valid local gradient.");
                AssertFinite(gradient, "finite-cell gradient");
                Assert.Greater(gradient.x, 0.5f, "The ramp field's gradient must point +x.");
                Assert.AreEqual(0f, gradient.y, 1e-3f);
                Assert.AreEqual(0f, gradient.z, 1e-3f);
            }
        }

        // ---- gradient policy: one-sided (field present on one side) --------

        [Test]
        public void OneSidedAbsentBoundary_TryEstimateFalse_EstimateFiniteOutward()
        {
            using (DensityGrid grid = BuildRampGrid())
            {
                // Model a cull boundary: the far x slice (index 4) reads +inf
                // (absent) while the surface-side corners remain finite. Evaluate
                // at the finite corner adjacent to the absent slice.
                NativeArray<float> samples = grid.MutableSamples;
                for (int y = 0; y <= grid.CellsY; y++)
                for (int z = 0; z <= grid.CellsZ; z++)
                {
                    samples[CornerIndex(grid, 4, y, z)] = float.PositiveInfinity;
                }

                Vector3 point = grid.CornerPosition(3, 2, 2); // finite center, +inf neighbor beyond

                // The cell containing this corner has an absent corner, so the
                // analytic trilinear gradient is not trustworthy -> false.
                Assert.IsFalse(grid.TryEstimateGradient(point, out _),
                    "A cell touching an absent (+inf) corner must not confirm a local gradient.");

                // The finite-aware fallback must be finite and still point outward
                // (into the finite present side) so winding stays decidable.
                Vector3 fallback = grid.EstimateGradient(point);
                AssertFinite(fallback, "one-sided fallback gradient");
                Assert.Greater(fallback.x, 0f, "The one-sided finite difference must preserve the outward +x direction.");
                Assert.AreEqual(0f, fallback.y, 1e-3f);
                Assert.AreEqual(0f, fallback.z, 1e-3f);
            }
        }

        // ---- gradient policy: invalid / absent (+inf corner) ---------------

        [Test]
        public void AbsentInfCorner_TryEstimateFalse_EstimateFallbackFinite()
        {
            using (DensityGrid grid = BuildRampGrid())
            {
                // Force a single interior corner of cell (2,2,2) to +inf (absent).
                NativeArray<float> samples = grid.MutableSamples;
                samples[CornerIndex(grid, 3, 2, 2)] = float.PositiveInfinity;

                Vector3 point = CellCenter(grid, 2, 2, 2); // containing cell has the +inf corner

                Assert.IsFalse(grid.TryEstimateGradient(point, out _),
                    "A cell with an absent (+inf) corner must not confirm a local gradient.");

                Vector3 fallback = grid.EstimateGradient(point);
                AssertFinite(fallback, "fallback on absent corner");
            }
        }

        // ---- gradient policy: invalid / NaN corner ------------------------

        [Test]
        public void NanCorner_TryEstimateFalse_EstimateFallbackFinite()
        {
            using (DensityGrid grid = BuildRampGrid())
            {
                // NaN is outside the CC-064 contract (fast samples are +inf, never
                // NaN), but the policy must still never let a NaN reference reach
                // winding. Force a NaN corner into cell (2,2,2).
                NativeArray<float> samples = grid.MutableSamples;
                samples[CornerIndex(grid, 3, 2, 2)] = float.NaN;

                Vector3 point = CellCenter(grid, 2, 2, 2);

                Assert.IsFalse(grid.TryEstimateGradient(point, out _),
                    "A cell with a NaN corner must not confirm a local gradient.");

                Vector3 fallback = grid.EstimateGradient(point);
                AssertFinite(fallback, "fallback on NaN corner");
            }
        }

        // ---- gradient policy: absent center corner ------------------------

        [Test]
        public void AbsentCenterCorner_EstimateFallbackFinite_NoLocalInfo()
        {
            using (DensityGrid grid = BuildRampGrid())
            {
                // Force the corner at world (0,0,0) (grid corner index 2 on each
                // axis) to +inf and evaluate exactly there, so the rounded center
                // of EstimateGradient is itself absent.
                NativeArray<float> samples = grid.MutableSamples;
                samples[CornerIndex(grid, 2, 2, 2)] = float.PositiveInfinity;

                Vector3 point = grid.CornerPosition(2, 2, 2); // == (0,0,0)

                Assert.IsFalse(grid.TryEstimateGradient(point, out _),
                    "A cell whose center corner is absent must not confirm a local gradient.");

                Vector3 fallback = grid.EstimateGradient(point);
                AssertFinite(fallback, "fallback on absent center");
                Assert.AreEqual(0f, fallback.x, 1e-4f);
                Assert.AreEqual(0f, fallback.y, 1e-4f);
                Assert.AreEqual(0f, fallback.z, 1e-4f);
            }
        }

        // ---- winding: never consumes NaN/Inf ------------------------------

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
                if (uses.Count != 2) continue;
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

        [Test]
        public void Winding_NeverConsumesNaNOrInf_OnCullBoundarySurface()
        {
            // A body + part at coarse resolution whose far region is culled to
            // +inf, so the mesh is produced adjacent to absent corners. The grid
            // must genuinely contain absent corners for the test not to be vacuous.
            var definition = CreatureDefinition.CreateEmpty();
            definition.Bounds = new BoundsDefinition { MaxX = 1.5f, MaxY = 1.5f, MaxZ = 1.5f };
            definition.Generation = new GenerationSettings { VoxelsPerUnit = 4f };
            definition.Body.Samples.Add(new BodySample { Id = 1, Position = new Vector3(0f, 0f, 0f), Radius = 0.6f });
            definition.AddPart(new CreaturePart
            {
                Id = "part",
                PartType = PartType.Part,
                ParentId = CreatureDefinition.BodyId,
                Transform = new TransformData { Position = new Vector3(1.1f, 0f, 0f), Rotation = Quaternion.identity, Scale = Vector3.one },
                Shape = new ShapeDefinition { Type = ShapeType.Sphere, PrimarySize = 0.4f, SmoothBlendRadius = 0.1f },
                Appearance = AppearanceDefinition.Default,
            });

            MeshExtractionResult mesh;
            int absentCornerCount;
            using (SdfProgram program = SdfProgramBuilder.CompilePortable(definition))
            using (DensityGrid grid = DensityGrid.SamplePortable(
                program, definition.Bounds, definition.Generation))
            {
                absentCornerCount = CountAbsentCorners(grid);
                mesh = MarchingCubesExtractor.Extract(grid);

                // Direct winding gate: for every emitted triangle, re-derive the
                // winding reference exactly as production does and assert it is
                // finite (never NaN/Inf) and consistent with the emitted order.
                // A NaN/Inf reference would make dot(faceNormal, ref) non-finite
                // and could never be >= 0, so this catches any future leak.
                int checkedTriangles = 0;
                for (int i = 0; i < mesh.Triangles.Count; i += 3)
                {
                    int a = mesh.Triangles[i];
                    int b = mesh.Triangles[i + 1];
                    int c = mesh.Triangles[i + 2];
                    Vector3 pa = mesh.Positions[a];
                    Vector3 pb = mesh.Positions[b];
                    Vector3 pc = mesh.Positions[c];
                    AssertFinite(pa, $"vertex {a}");
                    AssertFinite(pb, $"vertex {b}");
                    AssertFinite(pc, $"vertex {c}");

                    Vector3 faceNormal = Vector3.Cross(pb - pa, pc - pa);
                    if (faceNormal.sqrMagnitude < 1e-12f) continue;

                    Vector3 centroid = (pa + pb + pc) / 3f;
                    Vector3 reference;
                    if (!grid.TryEstimateGradient(centroid, out reference))
                    {
                        reference = grid.EstimateGradient(centroid);
                    }
                    AssertFinite(reference, $"triangle {i / 3} winding reference");
                    Assert.GreaterOrEqual(
                        Vector3.Dot(faceNormal, reference), 0f,
                        $"Triangle {i / 3} winding disagrees with its (finite) gradient reference.");
                    checkedTriangles++;
                }

                Assert.Greater(checkedTriangles, 0, "Mesh should have non-degenerate triangles.");
            }

            Assert.Greater(absentCornerCount, 0,
                "Fixture must contain absent (+inf) corners so the winding gate is not vacuous.");
            Assert.Greater(mesh.Triangles.Count / 3, 0, "Cull-boundary mesh must emit triangles.");
            Assert.AreEqual(0, CountCoDirectionalEdges(mesh),
                "A shared edge is traversed twice in the same direction (local winding inversion).");
            MeshTopologyReport report = MeshTopologyValidator.Validate(mesh);
            Assert.IsTrue(report.IsWatertight,
                $"Expected watertight; boundary={report.BoundaryEdgeCount} nonManifold={report.NonManifoldEdgeCount}.");
        }

        private static int CountAbsentCorners(DensityGrid grid)
        {
            int absent = 0;
            for (int z = 0; z <= grid.CellsZ; z++)
            for (int y = 0; y <= grid.CellsY; y++)
            for (int x = 0; x <= grid.CellsX; x++)
            {
                if (float.IsPositiveInfinity(grid.GetSample(x, y, z))) absent++;
            }
            return absent;
        }
    }
}
