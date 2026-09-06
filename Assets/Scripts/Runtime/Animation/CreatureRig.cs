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
        private Transform[] _indexedBones = new Transform[0];
        private Quaternion[] _indexedRotations = new Quaternion[0];

        public IReadOnlyDictionary<string, Transform> Bones => _bones;

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
            _indexedBones = nextIndexedBones;
            _indexedRotations = new Quaternion[nextSkeleton.Count];
        }

        public void ApplyPose(PosedSkeleton pose)
        {
            if (_restSkeleton == null) throw new DomainException("Build must be called before ApplyPose.");
            if (pose == null) throw new DomainException("pose must not be null.");

            Ik.PoseRotationResolver.ResolveInto(_restSkeleton, pose, _indexedRotations);
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
