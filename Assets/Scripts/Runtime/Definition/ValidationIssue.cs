namespace ProceduralCreature.Definition
{
    /// <summary>
    /// A single structured validation finding (implementation guide §2.4). The editor
    /// may render Message directly, but generation code must never depend on the
    /// editor to remain safe — Severity/Code alone must be enough to decide whether
    /// generation can proceed (design doc §4.3).
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public ValidationCode Code { get; }

        /// <summary>Null when the issue is definition-wide rather than part-specific (e.g. schema version).</summary>
        public string PartId { get; }

        public string Message { get; }

        public ValidationIssue(ValidationSeverity severity, ValidationCode code, string message, string partId = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            PartId = partId;
        }

        public override string ToString()
        {
            string location = PartId != null ? $" [part {PartId}]" : string.Empty;
            return $"{Severity} {Code}{location}: {Message}";
        }
    }
}
