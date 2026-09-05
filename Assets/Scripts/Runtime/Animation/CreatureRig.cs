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
        private SkeletonSnapshot _restSkeleton;

        public IReadOnlyDictionary<string, Transform> Bones => _bones;

        public void Build(Skeleton.Skeleton restSkeleton)
        {
            if (restSkeleton == null) throw new DomainException("restSkeleton must not be null.");

            SkeletonSnapshot nextSkeleton = SkeletonSnapshot.Capture(restSkeleton);
            var nextBones = new Dictionary<string, Transform>(nextSkeleton.Count);
            var nextGeneratedObjects = new List<GameObject>(nextSkeleton.Count);
            try
            {
                for (int i = 0; i < nextSkeleton.Count; i++)
                {
                    BoneSnapshot bone = nextSkeleton[i];
                    var boneObject = new GameObject(BoneObjectPrefix + bone.Id);
                    Transform parent = bone.ParentIndex < 0
                        ? transform
                        : nextBones[nextSkeleton[bone.ParentIndex].Id];
                    boneObject.transform.SetParent(parent, worldPositionStays: false);
                    boneObject.transform.position = bone.Position;
                    boneObject.transform.rotation = bone.Rotation;
                    nextBones.Add(bone.Id, boneObject.transform);
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
        }

        public void ApplyPose(PosedSkeleton pose)
        {
            if (_restSkeleton == null) throw new DomainException("Build must be called before ApplyPose.");
            if (pose == null) throw new DomainException("pose must not be null.");

            Dictionary<string, Quaternion> rotations = Ik.PoseRotationResolver.Resolve(_restSkeleton, pose);
            for (int i = 0; i < _restSkeleton.Count; i++)
            {
                BoneSnapshot bone = _restSkeleton[i];
                Transform boneTransform = _bones[bone.Id];
                boneTransform.position = pose.GetPosition(i);
                boneTransform.rotation = rotations[bone.Id];
            }
        }

        public void Clear()
        {
            DestroyGeneratedObjects(_generatedObjects);
            _generatedObjects.Clear();
            _bones.Clear();
            _restSkeleton = null;
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
