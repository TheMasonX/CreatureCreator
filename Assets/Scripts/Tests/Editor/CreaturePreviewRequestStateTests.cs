using System;
using NUnit.Framework;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// A7.1 (CC-094) EditMode matrix for the preview request/state coordinator.
    /// These tests drive the coordinator directly with request ids and assert the
    /// current-vs-stale decision; they involve no viewport rendering and no Unity
    /// scene objects.
    /// </summary>
    [TestFixture]
    public sealed class CreaturePreviewRequestStateTests
    {
        [Test]
        public void FreshStateHasNoPendingRequest()
        {
            var state = new CreaturePreviewRequestState();

            Assert.IsFalse(state.HasPendingRequest);
            Assert.AreEqual(-1, state.CurrentRequestId);
        }

        [Test]
        public void ARequestedThenBRequested_MakesAStale()
        {
            // A requested -> B requested -> A completes: A must be treated stale.
            var state = new CreaturePreviewRequestState();
            long a = 1;
            long b = 2;

            state.BeginRequest(a);
            state.BeginRequest(b);

            Assert.IsTrue(state.HasPendingRequest);
            Assert.AreEqual(b, state.CurrentRequestId);
            Assert.IsFalse(state.IsCurrentRequest(a), "Superseded request A must not be current.");
            Assert.IsTrue(state.IsCurrentRequest(b), "Latest request B must be current.");
        }

        [Test]
        public void ARequestedThenBRequested_BCompletesIsAccepted()
        {
            // A requested -> B requested -> B completes: B is accepted and current.
            var state = new CreaturePreviewRequestState();
            long b = 2;

            state.BeginRequest(1);
            state.BeginRequest(b);

            Assert.IsTrue(state.IsCurrentRequest(b), "Latest request B is the current, accepted request.");
        }

        [Test]
        public void CurrentCompletionClearsInFlight_LeavesOlderCompletionsStale()
        {
            // A requested -> B requested -> B completes (accepted) -> A completes late.
            var state = new CreaturePreviewRequestState();
            state.BeginRequest(1);
            state.BeginRequest(2);

            // The controller clears in-flight once the current request's result is
            // delivered, so a late older completion is no longer current.
            state.Clear();
            Assert.IsFalse(state.HasPendingRequest);
            Assert.IsFalse(state.IsCurrentRequest(1), "Late older completion A must not be current.");
            Assert.IsFalse(state.IsCurrentRequest(2), "Already-delivered request B is no longer pending.");
        }

        [Test]
        public void ClearCancels_SoNoCompletedResultIsCurrent()
        {
            // clear/cancel: after Clear, an in-flight result for the cancelled
            // request must not be treated as current.
            var state = new CreaturePreviewRequestState();
            long a = 1;

            state.BeginRequest(a);
            Assert.IsTrue(state.IsCurrentRequest(a));

            state.Clear();

            Assert.IsFalse(state.HasPendingRequest);
            Assert.IsFalse(state.IsCurrentRequest(a), "Cancelled request A must not be current after Clear.");
        }

        [Test]
        public void DefaultAndNeverIssuedIdsAreNotCurrent()
        {
            // invalid request: a default/never-issued id is never current.
            var state = new CreaturePreviewRequestState();

            Assert.IsFalse(state.IsCurrentRequest(0));
            Assert.IsFalse(state.IsCurrentRequest(42));
        }

        [Test]
        public void InvalidIdDeliveredBeforeAnyRequestIsNotCurrent()
        {
            // invalid request: a negative/default id reported as completing is stale.
            var state = new CreaturePreviewRequestState();
            Assert.IsFalse(state.IsCurrentRequest(-1));
            Assert.IsFalse(state.IsCurrentRequest(long.MinValue));
        }

        [Test]
        public void BeginRequestRejectsNegativeId()
        {
            var state = new CreaturePreviewRequestState();

            Assert.Throws<ArgumentOutOfRangeException>(() => state.BeginRequest(-1));
            Assert.IsFalse(state.HasPendingRequest, "A rejected request must not leave a pending request.");
        }
    }
}
