using System;
namespace ProceduralCreature.Editor
{
    internal sealed class CreaturePreviewAcceptanceState
    {
        private string _acceptedRevisionId;
        private string _acceptedPlacementFingerprint;

        public bool HasAcceptedPreview => _acceptedRevisionId != null && _acceptedPlacementFingerprint != null;

        public void Accept(string revisionId, string placementFingerprint)
        {
            if (string.IsNullOrEmpty(revisionId)) throw new ArgumentException("A preview revision is required.", nameof(revisionId));
            if (placementFingerprint == null) throw new ArgumentNullException(nameof(placementFingerprint));

            _acceptedRevisionId = revisionId;
            _acceptedPlacementFingerprint = placementFingerprint;
        }

        public bool IsStale(string currentRevisionId, string currentPlacementFingerprint)
        {
            if (!HasAcceptedPreview || string.IsNullOrEmpty(currentRevisionId) || currentPlacementFingerprint == null)
            {
                return true;
            }

            return currentRevisionId != _acceptedRevisionId
                || currentPlacementFingerprint != _acceptedPlacementFingerprint;
        }

        public void Clear()
        {
            _acceptedRevisionId = null;
            _acceptedPlacementFingerprint = null;
        }
    }
}