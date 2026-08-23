namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Semantic classification of a CreaturePart, consumed by skeleton inference and
    /// locomotion. Design doc §4.1 is explicit that this vocabulary should stay small
    /// for the MVP — resist adding entries speculatively; every new value is a new
    /// case every downstream consumer (skeleton inferer, gait system, editor palette)
    /// must handle deliberately.
    /// </summary>
    public enum PartType
    {
        Body = 0,
        Limb = 1,
        Leg = 2,
        Arm = 3,
        Tail = 4,
        Foot = 5,
        Root = 6,
        /// <summary>Generic part with no special locomotion or skeleton meaning.</summary>
        Part = 7,
        /// <summary>An eye, typically authored on a head or the Body.</summary>
        Eye = 8,
    }
}
