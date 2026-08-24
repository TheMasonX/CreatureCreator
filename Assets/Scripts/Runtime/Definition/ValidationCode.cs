namespace ProceduralCreature.Definition
{
    /// <summary>
    /// Stable, structured codes for every validation failure the MVP is required to
    /// detect (implementation guide §2.4). Stable so tooling/tests can assert on the
    /// code rather than parsing message strings.
    /// </summary>
    public enum ValidationCode
    {
        DuplicatePartId,
        MissingParent,
        ParentCycle,
        UnsupportedPartType,
        NonFiniteTransform,
        InvalidScale,
        OutOfBoundsTransform,
        InvalidShapeParameter,
        NonFiniteAppearance,
        GenerationBudgetExceeded,
        UnsupportedSchemaVersion,
        InvalidBounds,
        MissingBody,
        InvalidBodySampleCount,
        DuplicateBodySampleId,
        InvalidBodySample,
        UnevenBodySpacing,
        InvalidForward,
        InvalidBodyParent,
        InvalidAttachmentAnchor,
        InvalidBodyAppearance,
        NonFiniteBodyAppearance,
        InvalidLimbChain,
        LimbJointCountOutOfRange,
        DuplicateLimbJointId,
        LimbJointOrderNotDeterministic,
        NonFiniteLimbJoint,
        LimbSegmentTooShort,
        LimbJointOutOfBounds,
        LimbRootNotAtOrigin,
        InvalidThicknessProfile,
        NonFiniteThickness,
        InvalidMeshGeometry,
        NonFiniteMeshGeometryAttachment,
        InvalidMeshGeometryScale,
    }
}
