using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Animation.Ik;
using ProceduralCreature.Common;
using ProceduralCreature.Skeleton;
using UnityEngine;

namespace ProceduralCreature.Animation
{
    /// <summary>
    /// Unity adapter for an inferred creature skeleton. The semantic skeleton and
    /// pose remain pure data; this component owns only its generated Transform
    /// hierarchy and applies poses in world space.
    ///
    /// Space contract: pose coordinates are creature-space. This adapter applies
    /// them directly as world positions/rotations on the generated bone Transforms.
    /// The host GameObject must remain at identity (position zero, rotation identity,
    /// scale one) for generated hierarchy placement and pose application to be
    /// predictable. A future root-motion layer can replace this deliberately.
    /// </summary>
    public sealed class CreatureRig : MonoBehaviour
    {
        private const string BoneObjectPrefix = "Bone_";
        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        private ReadOnlyDictionary<string, Transform> _readOnlyBones;
        private readonly List<GameObject> _generatedObjects = new List<GameObject>();
        private SkeletonSnapshot _restSkeleton;
        private SkeletonSnapshot _validatedPoseSkeleton;
        private Transform[] _indexedBones = new Transform[0];
        private Quaternion[] _indexedRotations = new Quaternion[0];

        public IReadOnlyDictionary<string, Transform> Bones =>
            _readOnlyBones ?? (_readOnlyBones = new ReadOnlyDictionary<string, Transform>(_bones));

        public SkeletonSnapshot RestSkeleton => _restSkeleton;
        public IReadOnlyList<Transform> IndexedBones => _indexedBones;

        public bool TryGetBone(string boneId, out Transform bone)
        {
            if (boneId == null)
            {
                bone = null;
                return false;
            }
            return _bones.TryGetValue(boneId, out bone);
        }

        public void Build(Skeleton.Skeleton restSkeleton)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");

            SkeletonSnapshot nextSkeleton = SkeletonSnapshot.Capture(restSkeleton);
            var nextBones = new Dictionary<string, Transform>(nextSkeleton.Count);
            var nextIndexedBones = new Transform[nextSkeleton.Count];
            var nextGeneratedObjects = new List<GameObject>(nextSkeleton.Count);
            try
            {
                for (int i = 0; i < nextSkeleton.Count; i++)
                {
                    BoneSnapshot bone = nextSkeleton[i];
                    var boneObject = new GameObject(BoneObjectPrefix + bone.Id);
                    Transform parent = bone.ParentIndex < 0
                        ? transform
                        : nextIndexedBones[bone.ParentIndex];
                    boneObject.transform.SetParent(parent, worldPositionStays: false);
                    boneObject.transform.position = bone.Position;
                    boneObject.transform.rotation = bone.Rotation;
                    nextBones.Add(bone.Id, boneObject.transform);
                    nextIndexedBones[i] = boneObject.transform;
                    nextGeneratedObjects.Add(boneObject);
                }
            }
            catch
            {
                DestroyGeneratedObjects(nextGeneratedObjects);
                throw;
            }

            DestroyGeneratedObjects(_generatedObjects);
            _bones.Clear();
            _generatedObjects.Clear();
            foreach (KeyValuePair<string, Transform> bone in nextBones)
            {
                _bones.Add(bone.Key, bone.Value);
            }
            _generatedObjects.AddRange(nextGeneratedObjects);
            _restSkeleton = nextSkeleton;
            _validatedPoseSkeleton = null;
            _indexedBones = nextIndexedBones;
            _indexedRotations = new Quaternion[nextSkeleton.Count];
        }

        public void ApplyPose(PosedSkeleton pose)
        {
            if (_restSkeleton == null) throw new DomainException("Build must be called before ApplyPose.");
            if (pose == null) throw new DomainException("pose must not be null.");

            // Compatibility is a structural contract, but it does not change while
            // a PosedSkeleton is derived from its immutable snapshot. Validate a new
            // snapshot once, then keep the steady-state animation loop indexed and
            // allocation-free.
            if (!ReferenceEquals(_validatedPoseSkeleton, pose.Skeleton))
            {
                if (!_restSkeleton.HasSameBoneOrder(pose.Skeleton))
                {
                    throw new DomainException("pose must use the same bone structure as the built rest skeleton.");
                }
                _validatedPoseSkeleton = pose.Skeleton;
            }

            PoseRotationResolver.ResolveIntoCompatible(_restSkeleton, pose, _indexedRotations);
            for (int i = 0; i < _restSkeleton.Count; i++)
            {
                Transform boneTransform = _indexedBones[i];
                boneTransform.position = pose.GetPosition(i);
                boneTransform.rotation = _indexedRotations[i];
            }
        }

        public void Clear()
        {
            DestroyGeneratedObjects(_generatedObjects);
            _generatedObjects.Clear();
            _bones.Clear();
            _restSkeleton = null;
            _validatedPoseSkeleton = null;
            _indexedBones = new Transform[0];
            _indexedRotations = new Quaternion[0];
        }

        private static void DestroyGeneratedObjects(List<GameObject> generatedObjects)
        {
            for (int i = generatedObjects.Count - 1; i >= 0; i--)
            {
                GameObject generatedObject = generatedObjects[i];
                if (generatedObject == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(generatedObject);
                else UnityEngine.Object.DestroyImmediate(generatedObject);
            }
        }
    }
}
