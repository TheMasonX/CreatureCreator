using NUnit.Framework;
using ProceduralCreature.Animation.Skinned;
using UnityEngine;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0215: CreatureSkinnedMeshRenderer tracks its generated presentation child
    /// in a non-serialized list. After a domain reload that list is empty while the
    /// previous "SkinnedMesh" child still exists, so without a name-based sweep a
    /// rebind would accumulate duplicate skinned objects (observed in the live Test
    /// scene alongside duplicate bone hierarchies).
    /// </summary>
    [TestFixture]
    public class CreatureSkinnedMeshRendererReloadCleanupTests
    {
        [Test]
        public void Clear_RemovesUntrackedSkinnedObjectFromPreviousSession()
        {
            var host = new GameObject("SkinnedHost");
            try
            {
                var adapter = host.AddComponent<CreatureSkinnedMeshRenderer>();
                var orphan = new GameObject("SkinnedMesh");
                orphan.transform.SetParent(host.transform, worldPositionStays: false);

                adapter.Clear();

                Assert.IsNull(host.transform.Find("SkinnedMesh"),
                    "a reload orphan must be removed so a rebind cannot duplicate it");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Clear_PreservesUnrelatedChildrenAndNestedLookalikes()
        {
            var host = new GameObject("SkinnedHost");
            try
            {
                var adapter = host.AddComponent<CreatureSkinnedMeshRenderer>();
                var unrelated = new GameObject("Unrelated");
                unrelated.transform.SetParent(host.transform, worldPositionStays: false);
                var nestedLookalike = new GameObject("SkinnedMesh");
                nestedLookalike.transform.SetParent(unrelated.transform, worldPositionStays: false);

                adapter.Clear();

                Assert.IsNotNull(host.transform.Find("Unrelated"),
                    "unowned children must survive");
                Assert.IsNotNull(host.transform.Find("Unrelated/SkinnedMesh"),
                    "only direct children are swept, not nested lookalikes");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
