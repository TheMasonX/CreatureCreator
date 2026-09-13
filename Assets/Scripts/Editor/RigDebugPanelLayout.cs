using UnityEngine;

namespace ProceduralCreature.Editor
{
    /// <summary>
    /// Pure, testable layout math for the <see cref="RigDebugView"/> SceneView
    /// overlay panel. It owns the panel's row geometry, its content height, and
    /// its on-screen clamping so the overlay cannot draw one control on top of
    /// another (the former "Bone width" label overdraw) or drag itself off the
    /// SceneView.
    /// This type has no dependency beyond UnityEngine math types, so EditMode
    /// tests cover every computation the overlay renders.
    /// </summary>
    internal static class RigDebugPanelLayout
    {
        /// <summary>Height of the drag/collapse header.</summary>
        public const float HeaderHeight = 20f;

        /// <summary>Height of one stacked control row.</summary>
        public const float RowHeight = 18f;

        /// <summary>Vertical gap between stacked control rows.</summary>
        public const float RowSpacing = 4f;

        /// <summary>Horizontal and vertical padding inside the panel body.</summary>
        public const float Padding = 8f;

        /// <summary>Edge length of the bottom-right resize grip.</summary>
        public const float ResizeGripSize = 16f;

        /// <summary>Narrowest supported panel width.</summary>
        public const float MinPanelWidth = 240f;

        /// <summary>Widest supported panel width.</summary>
        public const float MaxPanelWidth = 600f;

        /// <summary>
        /// Stable body-row indices in draw order. The width value label owns its
        /// own row between the slider and the focus buttons, so it can never
        /// overlap the controls that follow it.
        /// </summary>
        public static class Row
        {
            public const int Enable = 0;
            public const int AlwaysOnTop = 1;
            public const int Labels = 2;
            public const int Selectable = 3;
            public const int ShowRawMesh = 4;
            public const int LineWidthSlider = 5;
            public const int LineWidthLabel = 6;
            public const int FrameButtons = 7;
            public const int FocusButtons = 8;
            public const int FocusSelectedBone = 9;

            /// <summary>Rows drawn while the rig overlay is enabled.</summary>
            public const int EnabledCount = 10;
        }

        /// <summary>Number of body rows drawn for the given overlay state.</summary>
        public static int CountBodyRows(bool overlayEnabled)
        {
            return overlayEnabled ? Row.EnabledCount : 1;
        }

        /// <summary>Exact height needed to draw the panel without clipping any row.</summary>
        public static float ContentHeight(bool overlayEnabled)
        {
            int rows = CountBodyRows(overlayEnabled);
            return HeaderHeight + Padding * 2f + rows * RowHeight + (rows - 1) * RowSpacing;
        }

        /// <summary>
        /// Panel height: the header alone when collapsed, otherwise the stored
        /// height grown to fit the content so the panel never clips a row.
        /// </summary>
        public static float EffectiveHeight(bool collapsed, bool overlayEnabled, float storedHeight)
        {
            if (collapsed) return HeaderHeight;
            return Mathf.Max(storedHeight, ContentHeight(overlayEnabled));
        }

        /// <summary>
        /// Collapsed state that results from a header foldout interaction.
        /// <c>EditorGUI.Foldout</c> returns the new expanded value, so the panel
        /// collapses exactly when the foldout reports itself collapsed.
        /// </summary>
        public static bool CollapsedFromFoldout(bool expanded)
        {
            return !expanded;
        }

        /// <summary>Screen rect of a body row for the given panel rect and overlay state.</summary>
        public static Rect BodyRow(int index, Rect panel, bool overlayEnabled)
        {
            if (index < 0 || index >= CountBodyRows(overlayEnabled))
            {
                return new Rect(panel.x + Padding, panel.y + HeaderHeight + Padding, 0f, 0f);
            }

            float y = panel.y + HeaderHeight + Padding + index * (RowHeight + RowSpacing);
            return new Rect(panel.x + Padding, y, panel.width - Padding * 2f, RowHeight);
        }

        /// <summary>Screen rect of the header.</summary>
        public static Rect HeaderRect(Rect panel)
        {
            return new Rect(panel.x, panel.y, panel.width, HeaderHeight);
        }

        /// <summary>Screen rect of the bottom-right resize grip.</summary>
        public static Rect ResizeGripRect(Rect panel)
        {
            return new Rect(panel.xMax - ResizeGripSize, panel.yMax - ResizeGripSize, ResizeGripSize, ResizeGripSize);
        }

        /// <summary>
        /// Clamp a panel origin so the whole panel stays inside the view when it
        /// fits; a panel taller than the view keeps its header visible.
        /// </summary>
        public static Vector2 ClampPosition(Vector2 position, Vector2 panelSize, Vector2 viewSize)
        {
            float maxX = Mathf.Max(0f, viewSize.x - panelSize.x);
            float maxY = Mathf.Max(0f, viewSize.y - HeaderHeight);
            return new Vector2(Mathf.Clamp(position.x, 0f, maxX), Mathf.Clamp(position.y, 0f, maxY));
        }

        /// <summary>Clamp a resize drag to the supported width range and to the minimum content height.</summary>
        public static Vector2 ClampSize(Vector2 size, bool overlayEnabled)
        {
            float width = Mathf.Clamp(size.x, MinPanelWidth, MaxPanelWidth);
            float height = Mathf.Max(size.y, ContentHeight(overlayEnabled));
            return new Vector2(width, height);
        }
    }
}
