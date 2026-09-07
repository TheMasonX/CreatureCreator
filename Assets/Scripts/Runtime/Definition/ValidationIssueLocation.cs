namespace ProceduralCreature.Definition
{
    /// <summary>
    /// The structural location kind a <see cref="ValidationIssue"/> can carry in
    /// addition to (never instead of) its free-text <see cref="ValidationIssue.Message"/>.
    /// A typed location lets tooling and tests navigate to the exact offending
    /// element without parsing Message text (audit F-203). Kinds are added only
    /// when the validator emits them; a null <see cref="ValidationIssue.Location"/>
    /// means the finding is definition- or part-wide and has no finer structural
    /// target.
    /// </summary>
    public enum ValidationLocationKind
    {
        /// <summary>An index into <c>CreatureDefinition.Body.Samples</c>.</summary>
        BodySample,

        /// <summary>An index into a part's <c>LimbChain.Joints</c>.</summary>
        LimbJoint,
    }

    /// <summary>
    /// Immutable, typed location for a validation finding (audit F-203). Carries
    /// the semantic index (body-sample or limb-joint) of the offending element.
    /// The corresponding validator Message keeps its human-readable text — this
    /// type supplements, never replaces or duplicates, the message content.
    /// </summary>
    public readonly struct ValidationIssueLocation
    {
        public ValidationLocationKind Kind { get; }

        public int Index { get; }

        public ValidationIssueLocation(ValidationLocationKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public override string ToString() => $"{Kind}[{Index}]";
    }
}
