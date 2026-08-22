using System.Collections.Generic;
using System.Linq;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Aggregate validation outcome (§2.4). IsValid is true iff there are no
    /// Error-severity issues — Warning/Info issues do not block generation.
    ///
    /// Issues are sorted deterministically (by PartId then Code) before being
    /// exposed, satisfying the Sprint 1.2 exit gate: "Validation failures are
    /// deterministic and order-independent" — i.e. running the validator twice, or
    /// running it against a definition whose Parts list has been reordered without
    /// semantic change, produces the same ordered issue list.
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly List<ValidationIssue> _issues;

        public bool IsValid => !_issues.Any(i => i.Severity == ValidationSeverity.Error);

        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public ValidationResult(IEnumerable<ValidationIssue> issues)
        {
            _issues = issues
                .OrderBy(i => i.PartId ?? string.Empty, System.StringComparer.Ordinal)
                .ThenBy(i => i.Code)
                .ThenBy(i => i.Severity)
                .ToList();
        }

        public static ValidationResult Valid() => new ValidationResult(System.Array.Empty<ValidationIssue>());
    }
}
