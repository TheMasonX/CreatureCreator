using NUnit.Framework;
using UnityEngine;
using ProceduralCreature.Editor;

namespace ProceduralCreature.Tests.Editor
{
    /// <summary>
    /// TSK-0254: layout math for the RigDebugView overlay panel. The SceneView
    /// overlay drawing itself is a manual residual check, but the row geometry is
    /// pure. These tests pin the property that regressed before: the
    /// "Bone width" label owns a reserved row, so every control after it is
    /// pushed down and can never overlap the label.
    /// </summary>
    [TestFixture]
    public class RigDebugPanelLayoutTests
    {
        [Test]
        public void CountBodyRows_DisabledOverlay_IsOnlyEnableToggle()
        {
            Assert.AreEqual(1, RigDebugPanelLayout.CountBodyRows(false));
        }

        [Test]
        public void CountBodyRows_EnabledOverlay_CoversHighestRowIndex()
        {
            Assert.AreEqual(RigDebugPanelLayout.Row.EnabledCount, RigDebugPanelLayout.CountBodyRows(true));
            Assert.AreEqual(
                RigDebugPanelLayout.Row.EnabledCount - 1,
                RigDebugPanelLayout.Row.FocusSelectedBone,
                "The highest row index must be the last row drawn.");
        }

        [Test]
        public void RowIndices_WidthLabelSitsBetweenSliderAndButtons()
        {
            Assert.Less(RigDebugPanelLayout.Row.LineWidthSlider, RigDebugPanelLayout.Row.LineWidthLabel);
            Assert.Less(RigDebugPanelLayout.Row.LineWidthLabel, RigDebugPanelLayout.Row.FrameButtons);
            Assert.Less(RigDebugPanelLayout.Row.FrameButtons, RigDebugPanelLayout.Row.FocusButtons);
            Assert.Less(RigDebugPanelLayout.Row.FocusButtons, RigDebugPanelLayout.Row.FocusSelectedBone);
        }

        [Test]
        public void BodyRows_EnabledOverlay_NeverOverlap()
        {
            var panel = new Rect(10f, 10f, 300f, RigDebugPanelLayout.ContentHeight(true));
            float previousBottom = RigDebugPanelLayout.HeaderRect(panel).yMax;

            for (int i = 0; i < RigDebugPanelLayout.CountBodyRows(true); i++)
            {
                Rect row = RigDebugPanelLayout.BodyRow(i, panel, true);
                Assert.GreaterOrEqual(
                    row.y,
                    previousBottom,
                    $"Body row {i} must start at or below the previous row so rows cannot overlap.");
                Assert.AreEqual(RigDebugPanelLayout.RowHeight, row.height, 0.001f);
                previousBottom = row.yMax;
            }
        }

        [Test]
        public void ContentHeight_EnabledOverlay_CoversEveryRowPlusPadding()
        {
            var panel = new Rect(10f, 10f, 300f, RigDebugPanelLayout.ContentHeight(true));

            for (int i = 0; i < RigDebugPanelLayout.CountBodyRows(true); i++)
            {
                Rect row = RigDebugPanelLayout.BodyRow(i, panel, true);
                Assert.LessOrEqual(
                    row.yMax,
                    panel.yMax - RigDebugPanelLayout.Padding + 0.001f,
                    $"Body row {i} must fit inside the panel body above the bottom padding.");
            }
        }

        [Test]
        public void BodyRow_IndexOutsideBody_ReturnsEmptyRow()
        {
            var panel = new Rect(10f, 10f, 300f, RigDebugPanelLayout.ContentHeight(true));

            Assert.AreEqual(0f, RigDebugPanelLayout.BodyRow(-1, panel, true).width, 0.001f);
            Assert.AreEqual(0f, RigDebugPanelLayout.BodyRow(RigDebugPanelLayout.Row.EnabledCount, panel, true).width, 0.001f);
            Assert.AreEqual(0f, RigDebugPanelLayout.BodyRow(1, panel, false).width, 0.001f);
        }

        [Test]
        public void EffectiveHeight_Collapsed_IsHeaderOnly()
        {
            Assert.AreEqual(
                RigDebugPanelLayout.HeaderHeight,
                RigDebugPanelLayout.EffectiveHeight(true, true, 500f),
                0.001f);
        }

        [Test]
        public void CollapsedFromFoldout_IsTheInverseOfTheReportedExpandedState()
        {
            Assert.IsTrue(
                RigDebugPanelLayout.CollapsedFromFoldout(false),
                "A collapsed foldout must collapse the panel.");
            Assert.IsFalse(
                RigDebugPanelLayout.CollapsedFromFoldout(true),
                "An expanded foldout must expand the panel.");
        }

        [Test]
        public void EffectiveHeight_Expanded_GrowsStoredHeightToFitContent()
        {
            float content = RigDebugPanelLayout.ContentHeight(true);

            Assert.AreEqual(content, RigDebugPanelLayout.EffectiveHeight(false, true, 0f), 0.001f);
            Assert.AreEqual(content, RigDebugPanelLayout.EffectiveHeight(false, true, content - 20f), 0.001f);
            Assert.AreEqual(content + 40f, RigDebugPanelLayout.EffectiveHeight(false, true, content + 40f), 0.001f);
        }

        [Test]
        public void ClampSize_ClampsWidthAndKeepsAtLeastContentHeight()
        {
            Vector2 clamped = RigDebugPanelLayout.ClampSize(new Vector2(10f, 10f), true);
            Assert.AreEqual(RigDebugPanelLayout.MinPanelWidth, clamped.x, 0.001f);
            Assert.AreEqual(RigDebugPanelLayout.ContentHeight(true), clamped.y, 0.001f);

            Vector2 wide = RigDebugPanelLayout.ClampSize(new Vector2(9999f, 9999f), true);
            Assert.AreEqual(RigDebugPanelLayout.MaxPanelWidth, wide.x, 0.001f);
            Assert.AreEqual(9999f, wide.y, 0.001f);
        }

        [Test]
        public void ClampPosition_KeepsPanelInsideView()
        {
            var viewSize = new Vector2(800f, 600f);
            var panelSize = new Vector2(300f, 200f);

            Vector2 inside = RigDebugPanelLayout.ClampPosition(new Vector2(50f, 60f), panelSize, viewSize);
            Assert.AreEqual(new Vector2(50f, 60f), inside);

            Vector2 beyondRightBottom = RigDebugPanelLayout.ClampPosition(new Vector2(5000f, 5000f), panelSize, viewSize);
            Assert.AreEqual(viewSize.x - panelSize.x, beyondRightBottom.x, 0.001f);
            Assert.AreEqual(viewSize.y - RigDebugPanelLayout.HeaderHeight, beyondRightBottom.y, 0.001f);

            Vector2 beyondLeftTop = RigDebugPanelLayout.ClampPosition(new Vector2(-500f, -500f), panelSize, viewSize);
            Assert.AreEqual(new Vector2(0f, 0f), beyondLeftTop);
        }

        [Test]
        public void ResizeGripRect_AnchoredToBottomRightCorner()
        {
            var panel = new Rect(10f, 20f, 300f, 200f);
            Rect grip = RigDebugPanelLayout.ResizeGripRect(panel);

            Assert.AreEqual(panel.xMax - RigDebugPanelLayout.ResizeGripSize, grip.x, 0.001f);
            Assert.AreEqual(panel.yMax - RigDebugPanelLayout.ResizeGripSize, grip.y, 0.001f);
            Assert.AreEqual(panel.xMax, grip.xMax, 0.001f);
            Assert.AreEqual(panel.yMax, grip.yMax, 0.001f);
        }
    }
}
