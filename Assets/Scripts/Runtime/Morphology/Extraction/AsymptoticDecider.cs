using UnityEngine;

namespace ProceduralCreature.Morphology.Extraction
{
    /// <summary>
    /// The Asymptotic Decider (Nielson &amp; Hamann, 1991): resolves which of the two
    /// valid triangulations applies to an ambiguous cube face by testing the sign
    /// of the bilinear interpolant at its saddle point, rather than picking a fixed
    /// convention that can disagree between two cubes sharing that face (the root
    /// cause of the micro-holes in plain Marching Cubes — delta-audit item #1).
    ///
    /// A face with corners c0,c1,c2,c3 in cyclic order (see CubeTopology) is
    /// AMBIGUOUS iff the diagonal pairs (c0,c2) and (c1,c3) each share a sign but
    /// disagree with each other (a "checkerboard" pattern) — this is a pure sign
    /// comparison and can never be wrong regardless of the actual density values.
    ///
    /// DERIVATION of the saddle test: treating the face as a unit square with
    /// corner values v00=c0, v10=c1, v11=c2, v01=c3 (matching the cyclic order,
    /// which by construction traces (0,0)-&gt;(1,0)-&gt;(1,1)-&gt;(0,1)), the bilinear
    /// interpolant is
    ///   f(u,v) = (1-u)(1-v)v00 + u(1-v)v10 + (1-u)v*v01 + uv*v11.
    /// Setting both partial derivatives to zero and solving gives the saddle point
    ///   u* = (v00-v01)/D,  v* = (v00-v10)/D,  where D = v00 - v10 - v01 + v11,
    /// and substituting back yields a closed form for the value there:
    ///   f(u*,v*) = (v00*v11 - v01*v10) / D.
    /// If that saddle value has the SAME sign as v00 (== v11, the shared diagonal
    /// sign), the region around the saddle matches the diagonal corners, meaning
    /// the surface connects them THROUGH THE MIDDLE of the face (the two "minority"
    /// corners v10/v01 are each cut off separately). If it has the OPPOSITE sign,
    /// the diagonal corners are separated instead (each cut off on its own), and
    /// the middle of the face belongs to the other pair.
    /// </summary>
    public static class AsymptoticDecider
    {
        public static bool IsFaceAmbiguous(float v00, float v10, float v11, float v01)
        {
            bool diagonal1Positive = v00 >= 0f;
            bool diagonal1Agrees = (v11 >= 0f) == diagonal1Positive;
            bool diagonal2Positive = v10 >= 0f;
            bool diagonal2Agrees = (v01 >= 0f) == diagonal2Positive;

            return diagonal1Agrees && diagonal2Agrees && diagonal1Positive != diagonal2Positive;
        }

        /// <summary>
        /// True if the region around the diagonal corners (v00, v11) connects
        /// through the face's interior (the "B" pairing in CubeContourResolver);
        /// false if they are separated (the "A" pairing). Only meaningful when
        /// IsFaceAmbiguous is true for the same four values — callers must check
        /// that first (see CubeContourResolver, which always does).
        /// </summary>
        public static bool DiagonalConnectsThroughMiddle(float v00, float v10, float v11, float v01)
        {
            float denominator = v00 - v10 - v01 + v11;

            if (Mathf.Approximately(denominator, 0f))
            {
                // Degenerate saddle (the bilinear surface is exactly planar along
                // one direction here) — a zero-measure edge case in practice given
                // continuously varying SDF samples. Fall back to "separated"
                // (false) consistently; both cubes sharing this face compute the
                // same v00/v10/v11/v01 from the same shared corners, so the
                // fallback is still consistent between them, which is the only
                // property that actually matters for closing holes.
                return false;
            }

            float saddleValue = (v00 * v11 - v01 * v10) / denominator;
            bool diagonalPositive = v00 >= 0f;
            bool saddlePositive = saddleValue >= 0f;

            return saddlePositive == diagonalPositive;
        }
    }
}
