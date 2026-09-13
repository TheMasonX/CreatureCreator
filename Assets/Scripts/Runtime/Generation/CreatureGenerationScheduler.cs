using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Generation
{
    public sealed class CreatureGenerationScheduler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ConcurrentQueue<CreatureGenerationResult> _completed = new ConcurrentQueue<CreatureGenerationResult>();
        private long _latestSequence;
        private CancellationTokenSource _latestCancellation;
        private bool _disposed;

        public long LatestSequence
        {
            get { lock (_gate) return _latestSequence; }
        }

        /// <summary>
        /// Public standalone scheduling boundary. The scheduler captures its input
        /// exactly once so callers cannot race a mutable definition after enqueue.
        /// Preview callers that already own a detached capture use
        /// <see cref="EnqueueCaptured"/> to avoid a second deep clone.
        /// </summary>
        public long Enqueue(CreatureDefinition definition, GenerationDiagnostics diagnostics = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return EnqueueCaptured(definition.Clone(), diagnostics);
        }

        /// <summary>
        /// Queues an already-detached definition without cloning it again. The caller
        /// transfers ownership of this private request snapshot to the scheduler.
        /// This entry point is public because the editor preview lives in a separate
        /// assembly; callers must pass an already-detached definition and therefore
        /// take responsibility for establishing that ownership boundary. The public
        /// <see cref="Enqueue"/> method remains the safe cloning boundary for ordinary
        /// callers (TSK-0104).
        /// </summary>
        public long EnqueueCaptured(CreatureDefinition capturedDefinition, GenerationDiagnostics diagnostics = null)
        {
            if (capturedDefinition == null) throw new ArgumentNullException(nameof(capturedDefinition));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(CreatureGenerationScheduler));

                // Newest-request-wins is now cooperative at the scheduler boundary:
                // cancel any queued/running predecessor before publishing the next
                // sequence. Generation itself remains synchronous within a worker,
                // so an already-running request may finish; its result is discarded
                // instead of entering the completion queue.
                _latestCancellation?.Cancel();
                _latestCancellation?.Dispose();
                _latestCancellation = new CancellationTokenSource();
                CancellationToken cancellationToken = _latestCancellation.Token;

                long sequence = ++_latestSequence;
                Task.Run(() => RunAndPublish(sequence, capturedDefinition, diagnostics, cancellationToken), cancellationToken);
                return sequence;
            }
        }

        public bool TryTakeCompleted(out CreatureGenerationResult result)
        {
            lock (_gate)
            {
                result = null;
                if (!_completed.TryDequeue(out result)) return false;
                result.IsStale = result.Sequence != _latestSequence;
                return true;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _latestSequence++;
                _latestCancellation?.Cancel();
                _latestCancellation?.Dispose();
                _latestCancellation = null;
            }
        }

        private void RunAndPublish(
            long sequence,
            CreatureDefinition definition,
            GenerationDiagnostics diagnostics,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            CreatureGenerationResult result = Run(sequence, definition, diagnostics);
            if (cancellationToken.IsCancellationRequested) return;

            _completed.Enqueue(result);
        }

        private static CreatureGenerationResult Run(long sequence, CreatureDefinition definition, GenerationDiagnostics diagnostics)
        {
            try
            {
                return CreatureGenerationResult.Success(sequence, CreatureMeshGenerator.GenerateData(definition, diagnostics), diagnostics);
            }
            catch (Exception exception)
            {
                return CreatureGenerationResult.Failure(sequence, exception, diagnostics);
            }
        }
    }

    public sealed class CreatureGenerationResult
    {
        private CreatureGenerationResult(long sequence, GeneratedCreatureData data, Exception exception, GenerationDiagnostics diagnostics)
        {
            Sequence = sequence;
            Data = data;
            Exception = exception;
            Diagnostics = diagnostics;
        }

        public long Sequence { get; }
        public GeneratedCreatureData Data { get; }
        public Exception Exception { get; }
        public GenerationDiagnostics Diagnostics { get; }
        public bool IsStale { get; internal set; }
        public bool Succeeded => Exception == null;

        internal static CreatureGenerationResult Success(long sequence, GeneratedCreatureData data, GenerationDiagnostics diagnostics)
        {
            return new CreatureGenerationResult(sequence, data, null, diagnostics);
        }

        internal static CreatureGenerationResult Failure(long sequence, Exception exception, GenerationDiagnostics diagnostics)
        {
            return new CreatureGenerationResult(sequence, null, exception, diagnostics);
        }
    }
}
