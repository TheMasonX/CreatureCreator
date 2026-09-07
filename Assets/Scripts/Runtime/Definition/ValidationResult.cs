using System;
using System.Collections.Generic;
using System.Linq;

namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Aggregate validation outcome (§2.4). IsValid is true iff there are no
    /// Error-severity issues — Warning/Info issues do not block generation.
    ///
    /// Issues are sorted into a documented TOTAL order once at construction and
    /// exposed through a genuinely read-only view (callers cannot add, remove, or
    /// replace issues). This satisfies the Sprint 1.2 exit gate: "Validation
    /// failures are deterministic and order-independent" — i.e. running the
    /// validator twice, or running it against a definition whose Parts list has
    /// been reordered without semantic change, produces the same ordered issue
    /// list. IsValid is computed once at construction and cached.
    ///
    /// Documented total order (each key ascending; ordinal for strings, enum
    /// ordinal for Code/Severity):
    ///   1. PartId — null/empty sorts before any non-empty value, then ordinal.
    ///   2. Code (enum ordinal).
    ///   3. Severity (enum ordinal).
    ///   4. Message (ordinal) — the final tie-break. Any two distinct issues have
    ///      a fully defined relative order; only issues identical in every field
    ///      are unordered with respect to each other, which is immaterial because
    ///      they are indistinguishable.
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly IReadOnlyList<ValidationIssue> _issues;
        private readonly bool _isValid;

        /// <summary>True iff no Error-severity issue exists (computed once at construction).</summary>
        public bool IsValid => _isValid;

        /// <summary>
        /// Read-only view of the issues in the documented total order. The backing
        /// collection is a fixed-size read-only view; mutation attempts throw
        /// <see cref="NotSupportedException"/>.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public ValidationResult(IEnumerable<ValidationIssue> issues)
        {
            List<ValidationIssue> sorted = issues
                .OrderBy(i => i.PartId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(i => i.Code)
                .ThenBy(i => i.Severity)
                .ThenBy(i => i.Message, StringComparer.Ordinal)
                .ToList();

            _issues = sorted.AsReadOnly();
            _isValid = !sorted.Any(i => i.Severity == ValidationSeverity.Error);
        }

        public static ValidationResult Valid() => new ValidationResult(Array.Empty<ValidationIssue>());
    }
}
