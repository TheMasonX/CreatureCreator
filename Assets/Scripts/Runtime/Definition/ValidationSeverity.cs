namespace ProceduralCreature.Definition
{
    public enum ValidationSeverity
    {
        /// <summary>Does not block generation; surfaced for awareness (e.g. an unusually thin blend radius).</summary>
        Info = 0,

        /// <summary>Does not block generation but flags something likely unintended.</summary>
        Warning = 1,

        /// <summary>Blocks generation. A definition with any Error-severity issue must not proceed to generation (§2.4).</summary>
        Error = 2,
    }
}
