using System;
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
    /// Space contract: pose coordinates are creature-space and are applied directly
    /// as world positions/rotations on the generated bone Transforms. The generated
    /// hierarchy therefore remains independent of the host's local transform; a
    /// non-identity host is supported and does not offset the requested bone world
    /// pose. A future explicit root-motion layer can replace this contract.
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
        private IReadOnlyList<Transform> _indexedBonesView = Array.AsReadOnly(new Transform[0]);
        private Quaternion[] _indexedRotations = new Quaternion[0];

        public IReadOnlyDictionary<string, Transform> Bones =>
            _readOnlyBones ?? (_readOnlyBones = new ReadOnlyDictionary<string, Transform>(_bones));

        public SkeletonSnapshot RestSkeleton => _restSkeleton;
        public IReadOnlyList<Transform> IndexedBones => _indexedBonesView;

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
            Build(SkeletonSnapshot.Capture(restSkeleton));
        }

        public void Build(SkeletonSnapshot restSkeleton)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");

            SkeletonSnapshot nextSkeleton = restSkeleton;
            var nextBones = new Dictionary<string, Transform>(nextSkeleton.Count);
            var nextIndexedBones = new Transform[nextSkeleton.Count];
            var nextGeneratedObjects = new List<GameObject>(nextSkeleton.Count);

            // Capture the previous hierarchy before the new one is created. Cleanup
            // runs only after a successful build so a failed build still preserves the
            // previous valid rig, while reload orphans are still removed.
            List<GameObject> previousRoots = CaptureGeneratedBoneRoots();

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
            _generatedObjects.Clear();
            DestroyGeneratedObjects(previousRoots);

            _bones.Clear();
            foreach (KeyValuePair<string, Transform> bone in nextBones)
            {
                _bones.Add(bone.Key, bone.Value);
            }
            _generatedObjects.AddRange(nextGeneratedObjects);
            _restSkeleton = nextSkeleton;
            _validatedPoseSkeleton = null;
            _indexedBones = nextIndexedBones;
            _indexedBonesView = Array.AsReadOnly(nextIndexedBones);
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
            DestroyGeneratedObjects(CaptureGeneratedBoneRoots());

            _bones.Clear();
            _restSkeleton = null;
            _validatedPoseSkeleton = null;
            _indexedBones = new Transform[0];
            _indexedBonesView = Array.AsReadOnly(_indexedBones);
            _indexedRotations = new Quaternion[0];
        }

        /// <summary>
        /// Direct children that look like generated bone roots. The tracked list is
        /// not serialized, so after a domain reload it can be empty while a previous
        /// hierarchy still exists in the scene; this snapshot lets the post-swap
        /// cleanup remove those reload orphans without a prefix sweep that would also
        /// match the freshly created bones. Every bone is a descendant of the root
        /// bone, so removing matching direct children removes the whole hierarchy.
        /// </summary>
        private List<GameObject> CaptureGeneratedBoneRoots()
        {
            var roots = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith(BoneObjectPrefix, StringComparison.Ordinal))
                    roots.Add(child.gameObject);
            }
            return roots;
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