using System;

namespace ProceduralCreature.Serialization
{
    /// <summary>
    /// Thrown when DNA JSON is structurally malformed — not valid JSON, wrong types,
    /// or missing a required field. This is a data-format problem, not a
    /// user-authoring-content problem, so it is intentionally NOT a DomainException
    /// (which is reserved for programmer errors) and NOT a ValidationIssue (which is
    /// reserved for semantically-parseable-but-invalid creature content). Callers
    /// should catch this specifically around load operations and present it as
    /// "this file isn't a valid DNA document" rather than routing it through the
    /// creature-content validation UI.
    /// </summary>
    public sealed class DnaDeserializationException : Exception
    {
        public DnaDeserializationException(string message) : base(message) { }

        public DnaDeserializationException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
