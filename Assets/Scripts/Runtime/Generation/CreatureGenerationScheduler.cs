using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ProceduralCreature.Definition;

namespace ProceduralCreature.Generation
{
    public sealed class CreatureGenerationScheduler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ConcurrentQueue<CreatureGenerationResult> _completed = new ConcurrentQueue<CreatureGenerationResult>();
        private long _latestSequence;
        private bool _disposed;

        public long LatestSequence
        {
            get { lock (_gate) return _latestSequence; }
        }

        public long Enqueue(CreatureDefinition definition, GenerationDiagnostics diagnostics = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(CreatureGenerationScheduler));
                long sequence = ++_latestSequence;
                CreatureDefinition captured = definition.Clone();
                Task.Run(() => _completed.Enqueue(Run(sequence, captured, diagnostics)));
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
                _disposed = true;
                _latestSequence++;
            }
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