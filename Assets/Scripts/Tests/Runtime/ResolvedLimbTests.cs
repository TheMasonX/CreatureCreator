using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using ProceduralCreature.Morphology;

namespace ProceduralCreature.Tests.Runtime
{
    /// <summary>
    /// CC-056A resolved limb geometry contract (canonical derived morphology,
    /// increment A). Runtime assembly — invoke via the PlayMode runner or
    /// execute_code, not the EditMode MCP runner.
    /// </summary>
    [TestFixture]
    public class ResolvedLimbTests
    {
        private static LimbChain StraightChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            return chain;
        }

        private static LimbChain BentChain()
        {
            var chain = new LimbChain();
            chain.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            chain.Joints.Add(new LimbJoint { Id = 2, Position = new Vector3(0f, -1f, 0f) });
            chain.Joints.Add(new LimbJoint { Id = 3, Position = new Vector3(1f, -1f, 0f) });
            return chain;
        }

        [Test]
        public void Resolve_StraightChain_ComputesSegmentsArcAndSockets()
        {
            ResolvedLimb resolved = ResolvedLimb.Resolve(StraightChain());

            Assert.AreEqual(2, resolved.JointPositions.Count);
            Assert.AreEqual(1, resolved.SegmentLengths.Count);
            Assert.AreEqual(1f, resolved.SegmentLengths[0], 1e-6f);
            Assert.AreEqual(1f, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolved.NormalizedArcLengthAtJoint[0], 1e-6f);
            Assert.AreEqual(1f, resolved.NormalizedArcLengthAtJoint[1], 1e-6f);
            Assert.AreEqual(Vector3.zero, resolved.RootSocket);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), resolved.TerminalSocket);
            Assert.AreSame(resolved.Centerline, resolved.JointPositions,
                "The v1 centerline IS the joint polyline (CC-055 decision pending).");
            Assert.IsNotNull(resolved.Thickness, "Default tapering profile must be attached.");
        }

        [Test]
        public void Resolve_BentChain_NormalizedArcLengthMatchesCumulative()
        {
            ResolvedLimb resolved = ResolvedLimb.Resolve(BentChain()); // two 1.0 segments

            Assert.AreEqual(3, resolved.JointPositions.Count);
            Assert.AreEqual(2, resolved.SegmentLengths.Count);
            Assert.AreEqual(1f, resolved.SegmentLengths[0], 1e-6f);
            Assert.AreEqual(1f, resolved.SegmentLengths[1], 1e-6f);
            Assert.AreEqual(2f, resolved.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolved.NormalizedArcLengthAtJoint[0], 1e-6f);
            Assert.AreEqual(0.5f, resolved.NormalizedArcLengthAtJoint[1], 1e-6f);
            Assert.AreEqual(1f, resolved.NormalizedArcLengthAtJoint[2], 1e-6f);
            Assert.AreEqual(Vector3.zero, resolved.RootSocket);
            Assert.AreEqual(new Vector3(1f, -1f, 0f), resolved.TerminalSocket);
        }

        [Test]
        public void Resolve_IsDeterministicAndDoesNotMutateChain()
        {
            LimbChain chain = BentChain();
            ResolvedLimb first = ResolvedLimb.Resolve(chain);
            ResolvedLimb second = ResolvedLimb.Resolve(chain);

            Assert.AreEqual(first.TotalLength, second.TotalLength, 1e-6f);
            for (int i = 0; i < first.JointPositions.Count; i++)
            {
                Assert.AreEqual(first.JointPositions[i], second.JointPositions[i]);
                Assert.AreEqual(first.NormalizedArcLengthAtJoint[i], second.NormalizedArcLengthAtJoint[i], 1e-6f);
            }
            Assert.AreEqual(first.SegmentLengths.Count, second.SegmentLengths.Count);

            Assert.AreEqual(3, chain.Joints.Count, "Resolution must never write back to the chain.");
            Assert.AreEqual(Vector3.zero, chain.Joints[0].Position);
        }

        [Test]
        public void Resolve_IsAnImmutableSnapshot()
        {
            LimbChain chain = StraightChain();
            ResolvedLimb resolved = ResolvedLimb.Resolve(chain);

            // Mutating the source after resolution must not change the snapshot.
            chain.Joints[0].Position = new Vector3(5f, 5f, 5f);
            chain.Joints[1].Position = new Vector3(5f, 4f, 5f);

            Assert.AreEqual(Vector3.zero, resolved.JointPositions[0]);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), resolved.JointPositions[1]);
            Assert.AreEqual(1f, resolved.TotalLength, 1e-6f);
        }

        [Test]
        public void Resolve_ImmutableSnapshot_IgnoresLaterThicknessMutation()
        {
            LimbChain chain = StraightChain();
            chain.Thickness = ThicknessProfile.CreateDefault();
            ResolvedLimb resolved = ResolvedLimb.Resolve(chain);

            chain.Thickness.Keys[0].Value = 99f;

            Assert.AreEqual(0.30f, resolved.Thickness.Evaluate(0f), 1e-4f,
                "Snapshot thickness is immune to later source mutation.");
        }

        [Test]
        public void Resolve_ExposesReadOnlyCollections()
        {
            ResolvedLimb resolved = ResolvedLimb.Resolve(StraightChain());

            IList<Vector3> positions = resolved.JointPositions as IList<Vector3>;
            IList<float> lengths = resolved.SegmentLengths as IList<float>;

            Assert.IsNotNull(positions);
            Assert.IsNotNull(lengths);
            Assert.IsTrue(positions.IsReadOnly);
            Assert.IsTrue(lengths.IsReadOnly);
            Assert.Throws<System.NotSupportedException>(() => positions[0] = Vector3.one);
            Assert.Throws<System.NotSupportedException>(() => lengths[0] = 99f);
        }

        [Test]
        public void Resolve_SingleJointAndCoincidentJoints_HandleDegenerate()
        {
            var single = new LimbChain();
            single.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });

            ResolvedLimb resolvedSingle = ResolvedLimb.Resolve(single);
            Assert.AreEqual(1, resolvedSingle.JointPositions.Count);
            Assert.AreEqual(0, resolvedSingle.SegmentLengths.Count);
            Assert.AreEqual(0f, resolvedSingle.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolvedSingle.NormalizedArcLengthAtJoint[0], 1e-6f);
            Assert.AreEqual(Vector3.zero, resolvedSingle.RootSocket);
            Assert.AreEqual(Vector3.zero, resolvedSingle.TerminalSocket);

            var coincident = new LimbChain();
            coincident.Joints.Add(new LimbJoint { Id = 1, Position = Vector3.zero });
            coincident.Joints.Add(new LimbJoint { Id = 2, Position = Vector3.zero });

            ResolvedLimb resolvedCoincident = ResolvedLimb.Resolve(coincident);
            Assert.AreEqual(0f, resolvedCoincident.TotalLength, 1e-6f);
            Assert.AreEqual(0f, resolvedCoincident.NormalizedArcLengthAtJoint[0], 1e-6f);
            Assert.AreEqual(0f, resolvedCoincident.NormalizedArcLengthAtJoint[1], 1e-6f);
        }

        [Test]
        public void Resolve_NullChainOrEmptyJointsOrNullJoint_Throws()
        {
            Assert.Throws<DomainException>(() => ResolvedLimb.Resolve(null));

            var empty = new LimbChain();
            Assert.Throws<DomainException>(() => ResolvedLimb.Resolve(empty));

            var withNull = StraightChain();
            withNull.Joints.Add(null);
            Assert.Throws<DomainException>(() => ResolvedLimb.Resolve(withNull));
        }

        [Test]
        public void Resolve_NullThickness_FallsBackToDefaultProfile()
        {
            LimbChain chain = StraightChain();
            chain.Thickness = null;

            ResolvedLimb resolved = ResolvedLimb.Resolve(chain);

            Assert.IsNotNull(resolved.Thickness);
            Assert.AreEqual(0.30f, resolved.Thickness.Evaluate(0f), 1e-4f, "Default profile root.");
            Assert.AreEqual(0.12f, resolved.Thickness.Evaluate(1f), 1e-4f, "Default profile tip.");
        }

    }
}
