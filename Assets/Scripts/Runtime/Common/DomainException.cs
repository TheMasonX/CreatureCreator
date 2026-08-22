using System;

namespace ProceduralCreature.Common
{
    /// <summary>
    /// Thrown only for programmer errors — contract violations that indicate a bug in
    /// calling code, not a problem with user-authored data. Anything derived from
    /// user-authored CreatureDefinition content (bad transforms, missing parents, etc.)
    /// must never throw; it must become a ValidationIssue/GenerationDiagnostics entry
    /// instead (implementation guide, Sprint 0.2 exception policy).
    ///
    /// Example correct use: calling a solver with a null chain reference, or asking the
    /// canonicalizer to quantize a definition that failed validation without checking
    /// the result first.
    ///
    /// Example incorrect use: a NaN transform in loaded DNA. That is user data and must
    /// surface as a ValidationIssue, not a DomainException.
    /// </summary>
    public sealed class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }

        public DomainException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
