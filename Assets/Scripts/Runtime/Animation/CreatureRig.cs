using System.Collections.Generic;
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
    /// </summary>
    public sealed class CreatureRig : MonoBehaviour
    {
        private const string BoneObjectPrefix = "Bone_";
        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        private readonly List<GameObject> _generatedObjects = new List<GameObject>();
        private Skeleton.Skeleton _restSkeleton;

        public IReadOnlyDictionary<string, Transform> Bones => _bones;

        public void Build(Skeleton.Skeleton restSkeleton)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");

            Clear();
            _restSkeleton = restSkeleton;

            foreach (Bone bone in restSkeleton.Bones)
            {
                if (bone == null || string.IsNullOrEmpty(bone.Id))
                {
                    throw new DomainException("A runtime rig cannot contain a null bone or empty bone id.");
                }
                if (_bones.ContainsKey(bone.Id))
                {
                    throw new DomainException($"Skeleton contains duplicate bone id '{bone.Id}'.");
                }

                var boneObject = new GameObject(BoneObjectPrefix + bone.Id);
                Transform parent = bone.ParentBoneId == null ? transform : ResolveParent(bone.ParentBoneId);
                boneObject.transform.SetParent(parent, worldPositionStays: false);
                boneObject.transform.position = bone.Position;
                boneObject.transform.rotation = bone.Rotation;
                _bones.Add(bone.Id, boneObject.transform);
                _generatedObjects.Add(boneObject);
            }
        }

        public void ApplyPose(PosedSkeleton pose)
        {
            if (_restSkeleton == null) throw new DomainException("Build must be called before ApplyPose.");
            if (pose == null) throw new DomainException("pose must not be null.");

            Dictionary<string, Quaternion> rotations = Ik.PoseRotationResolver.Resolve(_restSkeleton, pose);
            foreach (Bone bone in _restSkeleton.Bones)
            {
                Transform boneTransform = _bones[bone.Id];
                boneTransform.position = pose.GetPosition(bone.Id);
                boneTransform.rotation = rotations[bone.Id];
            }
        }

        public void Clear()
        {
            for (int i = _generatedObjects.Count - 1; i >= 0; i--)
            {
                GameObject generatedObject = _generatedObjects[i];
                if (generatedObject == null) continue;
                if (Application.isPlaying) Destroy(generatedObject);
                else DestroyImmediate(generatedObject);
            }
            _generatedObjects.Clear();
            _bones.Clear();
            _restSkeleton = null;
        }

        private Transform ResolveParent(string parentBoneId)
        {
            if (!_bones.TryGetValue(parentBoneId, out Transform parent))
            {
                throw new DomainException($"Bone '{parentBoneId}' must be created before its child.");
            }
            return parent;
        }
    }
}
