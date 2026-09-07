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

        /// <summary>
        /// Optional typed structural location (body-sample or limb-joint index) for
        /// findings whose target the validator knows structurally (audit F-203).
        /// Supplements — never replaces — <see cref="Message"/>, whose human-readable
        /// text is unchanged. Null for definition-wide, part-wide, or otherwise
        /// unscoped findings.
        /// </summary>
        public ValidationIssueLocation? Location { get; }

        public ValidationIssue(
            ValidationSeverity severity,
            ValidationCode code,
            string message,
            string partId = null,
            ValidationIssueLocation? location = null)
        {
            Severity = severity;
            Code = code;
            Message = message;
            PartId = partId;
            Location = location;
        }

        public override string ToString()
        {
            string location = PartId != null ? $" [part {PartId}]" : string.Empty;
            return $"{Severity} {Code}{location}: {Message}";
        }
    }
}
