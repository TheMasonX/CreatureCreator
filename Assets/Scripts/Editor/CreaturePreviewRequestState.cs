using System;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// A7.1 (CC-094): viewport-free, scene-object-free preview request/state
    /// coordinator. It records preview generation requests and decides whether a
    /// completed result is the <b>current</b> request so stale results can be
    /// discarded. It has no dependency on Unity scene objects, viewport rendering,
    /// or the editor window, so it is deterministically unit-testable in EditMode.
    ///
    /// Ownership boundaries (no duplication of existing responsibilities):
    /// <list type="bullet">
    /// <item><see cref="CreatureGenerationScheduler"/> owns EXECUTION: it runs
    /// generation and returns each result carrying the request sequence it was
    /// enqueued with. Its own <c>IsStale</c> remains in use for the runtime
    /// preview path.</item>
    /// <item>This coordinator owns the EDITOR preview request lifecycle and the
    /// "is this completed result the current request" decision.</item>
    /// <item><see cref="CreaturePreviewAcceptanceState"/> owns accepted-result
    /// identity (the revision/fingerprint of the last applied preview) and is a
    /// separate, later concern.</item>
    /// </list>
    ///
    /// Request ids are the monotonic sequence <see cref="CreatureGenerationScheduler.Enqueue"/>
    /// returns, so this coordinator is always consistent with the runtime
    /// scheduler and introduces no second revision scheme.
    /// </summary>
    internal sealed class CreaturePreviewRequestState
    {
        private const long NoRequest = -1L;
        private long _currentRequestId = NoRequest;

        /// <summary>True while a preview request is outstanding (not yet completed and not cleared).</summary>
        public bool HasPendingRequest => _currentRequestId != NoRequest;

        /// <summary>The id of the current request, or -1 when none is pending.</summary>
        public long CurrentRequestId => _currentRequestId;

        /// <summary>
        /// Records a new preview request as the only current request. Any earlier
        /// request becomes stale; when its result later completes it is not current.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">When <paramref name="requestId"/> is negative.</exception>
        public void BeginRequest(long requestId)
        {
            if (requestId < 0) throw new ArgumentOutOfRangeException(nameof(requestId), "A preview request id must be non-negative.");
            _currentRequestId = requestId;
        }

        /// <summary>
        /// True only when <paramref name="requestId"/> is the current request and a
        /// request is pending. An id that is stale (superseded by a newer request),
        /// default/never-issued, negative, or delivered after <see cref="Clear"/> is
        /// treated as not current.
        /// </summary>
        public bool IsCurrentRequest(long requestId)
        {
            return HasPendingRequest && requestId == _currentRequestId;
        }

        /// <summary>
        /// Cancels/clears the current request: no completed result is current until
        /// a new <see cref="BeginRequest"/>. Used on teardown and exposed for the
        /// cancel scenario.
        /// </summary>
        public void Clear()
        {
            _currentRequestId = NoRequest;
        }
    }
}
