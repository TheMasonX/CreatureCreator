using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ProceduralCreature.Common;
using ProceduralCreature.Definition;
using UnityEngine;

namespace ProceduralCreature.Skeleton
{
    public readonly struct BoneSnapshot
    {
        public string Id { get; }
        public int Index { get; }
        public int ParentIndex { get; }
        public string SourcePartId { get; }
        public PartType PartType { get; }
        public bool IsMirrored { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool HasSegment { get; }
        public Vector3 EndPosition { get; }
        public bool HasChildAttachmentPosition { get; }
        public Vector3 ChildAttachmentPosition { get; }

        internal BoneSnapshot(Bone bone, int index, int parentIndex)
        {
            Id = bone.Id;
            Index = index;
            ParentIndex = parentIndex;
            SourcePartId = bone.SourcePartId;
            PartType = bone.PartType;
            IsMirrored = bone.IsMirrored;
            Position = bone.Position;
            Rotation = bone.Rotation;
            HasSegment = bone.HasSegment;
            EndPosition = bone.EndPosition;
            HasChildAttachmentPosition = bone.HasChildAttachmentPosition;
            ChildAttachmentPosition = bone.ChildAttachmentPosition;
        }
    }

    public sealed class SkeletonSnapshot
    {
        private readonly BoneSnapshot[] _bones;
        private readonly Dictionary<string, int> _indices;
        private readonly IReadOnlyList<int>[] _children;

        public int Count => _bones.Length;
        public BoneSnapshot this[int index] => _bones[index];

        private SkeletonSnapshot(BoneSnapshot[] bones, Dictionary<string, int> indices,
            IReadOnlyList<int>[] children)
        {
            _bones = bones;
            _indices = indices;
            _children = children;
        }

        public static SkeletonSnapshot Capture(Skeleton skeleton)
        {
            if (skeleton == null) throw new DomainException("skeleton must not be null.");

            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                Bone bone = skeleton.Bones[i];
                if (bone == null || string.IsNullOrEmpty(bone.Id))
                {
                    throw new DomainException("A skeleton snapshot cannot contain a null bone or empty bone id.");
                }
                if (!indices.TryAdd(bone.Id, i))
                {
                    throw new DomainException($"Skeleton contains duplicate bone id '{bone.Id}'.");
                }
            }

            var childrenById = new Dictionary<string, List<Bone>>(StringComparer.Ordinal);
            var roots = new List<Bone>();
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                Bone bone = skeleton.Bones[i];
                if (bone.ParentBoneId == null)
                {
                    roots.Add(bone);
                    continue;
                }

                if (!indices.ContainsKey(bone.ParentBoneId))
                {
                    throw new DomainException(
                        $"Bone '{bone.Id}' references missing parent '{bone.ParentBoneId}'.");
                }

                if (!childrenById.TryGetValue(bone.ParentBoneId, out List<Bone> childrenOfParent))
                {
                    childrenOfParent = new List<Bone>();
                    childrenById.Add(bone.ParentBoneId, childrenOfParent);
                }
                childrenOfParent.Add(bone);
            }

            roots.Sort(CompareBonesById);
            foreach (List<Bone> childrenList in childrenById.Values)
            {
                childrenList.Sort(CompareBonesById);
            }

            var orderedBones = new List<Bone>(skeleton.Bones.Count);
            var pending = new List<Bone>(roots);
            while (pending.Count > 0)
            {
                Bone bone = pending[0];
                pending.RemoveAt(0);
                orderedBones.Add(bone);

                if (childrenById.TryGetValue(bone.Id, out List<Bone> boneChildren))
                {
                    pending.AddRange(boneChildren);
                    pending.Sort(CompareBonesById);
                }
            }

            if (orderedBones.Count != skeleton.Bones.Count)
            {
                throw new DomainException("Skeleton contains a parent cycle.");
            }

            var orderedIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < orderedBones.Count; i++)
            {
                orderedIndices.Add(orderedBones[i].Id, i);
            }

            var bones = new BoneSnapshot[orderedBones.Count];
            var children = new List<int>[bones.Length];
            for (int i = 0; i < bones.Length; i++) children[i] = new List<int>();

            for (int i = 0; i < bones.Length; i++)
            {
                Bone bone = orderedBones[i];
                int parentIndex = -1;
                if (bone.ParentBoneId != null)
                {
                    parentIndex = orderedIndices[bone.ParentBoneId];
                    children[parentIndex].Add(i);
                }
                bones[i] = new BoneSnapshot(bone, i, parentIndex);
            }

            var readOnlyChildren = new IReadOnlyList<int>[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                readOnlyChildren[i] = new ReadOnlyCollection<int>(children[i]);
            }
            return new SkeletonSnapshot(bones, orderedIndices, readOnlyChildren);
        }

        private static int CompareBonesById(Bone left, Bone right)
        {
            return StringComparer.Ordinal.Compare(left.Id, right.Id);
        }

        public int GetIndex(string boneId)
        {
            if (boneId == null || !_indices.TryGetValue(boneId, out int index))
            {
                throw new DomainException($"Bone '{boneId}' was not found in the skeleton snapshot.");
            }
            return index;
        }

        public bool TryGetIndex(string boneId, out int index)
        {
            if (boneId == null)
            {
                index = -1;
                return false;
            }
            return _indices.TryGetValue(boneId, out index);
        }

        public IReadOnlyList<int> GetChildren(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _bones.Length)
            {
                throw new DomainException("boneIndex must identify a bone in the skeleton snapshot.");
            }
            return _children[boneIndex];
        }

        public bool HasSameBoneOrder(SkeletonSnapshot other)
        {
            if (other == null || other.Count != Count) return false;
            for (int i = 0; i < Count; i++)
            {
                if (!string.Equals(_bones[i].Id, other._bones[i].Id, StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}