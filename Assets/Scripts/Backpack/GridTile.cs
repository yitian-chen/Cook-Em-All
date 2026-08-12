using System.Collections.Generic;
using UnityEngine;

namespace Vampire.Backpack
{
    /// <summary>
    /// 格子实体。代表玩家购买并拼排在底板上的"格子"。
    /// 放在底板空位上，道具只能放在格子上。
    /// 视觉：半透明单元格（让底板高亮预览能透出）。
    /// 拖动时，完全位于其上的道具会作为视觉子物体一起被带走（跟随预览）。
    /// 放下时由 BackpackGrid.PlaceGridTile 重新把它们 reparent 到 itemContainer。
    /// </summary>
    public class GridTile : DraggableEntity
    {
        // 拖动期间视觉跟随的道具列表（reparent 到本 tile 下）。放下后会由 PlaceGridTile 移回 itemContainer。
        private readonly List<DraggableItem> draggingItems = new List<DraggableItem>();

        public override bool CanPlaceAt(BackpackGrid grid, int col, int row)
        {
            return grid != null && grid.CanPlaceGridTile(this, col, row);
        }

        public override void PlaceAt(BackpackGrid grid, int col, int row)
        {
            if (grid != null) grid.PlaceGridTile(this, col, row);
        }

        public override void RemoveFromGrid()
        {
            if (currentGrid != null) currentGrid.RemoveGridTile(this);
        }

        /// <summary>
        /// 拖拽开始后：把完全位于旧 footprint 上的道具 reparent 到本 tile 下，
        /// 并按格子坐标偏移设置它们的 anchoredPosition，使视觉上跟随 tile 一起移动。
        /// 这些道具的 itemMap 占用保留不变（仍记录在旧位置），由 PlaceGridTile 在放下时统一迁移。
        /// </summary>
        protected override void OnDragStarted()
        {
            draggingItems.Clear();
            if (homeGrid == null) return;

            var following = homeGrid.GetFollowingItemsForTile(this);
            if (following.Count == 0) return;

            Vector2 cell = homeGrid.CellSize;
            Vector2 sp = homeGrid.Spacing;
            float pitchX = cell.x + sp.x;
            float pitchY = cell.y + sp.y;
            foreach (var item in following)
            {
                if (item == null) continue;
                // 道具中心相对格子中心的像素偏移：
                //   锚点差 × pitch + 尺寸差 × 0.5 × pitch
                // 后者修正多格道具/格子的中心不在锚点格中心的问题（与 BackpackGrid.PositionEntity 一致）。
                float dx = (item.CurrentAnchorCol - homeAnchorCol) * pitchX
                         + (item.GridWidth - gridWidth) * 0.5f * pitchX;
                float dy = (item.CurrentAnchorRow - homeAnchorRow) * pitchY
                         + (item.GridHeight - gridHeight) * 0.5f * pitchY;
                // y 向下为负
                Vector2 offset = new Vector2(dx, -dy);
                item.transform.SetParent(transform, false);
                item.Rect.anchoredPosition = offset;
                draggingItems.Add(item);
            }
        }

        /// <summary>
        /// 拖拽结束后的清理：若有跟随道具仍挂在本 tile 下（理论上 PlaceGridTile 已移走，
        /// 但兜底场景下可能残留），强制 reparent 回 homeGrid 的 itemContainer 原位。
        /// </summary>
        protected override void OnDragEnded()
        {
            if (draggingItems.Count == 0) return;
            // 只清理仍然挂在本 tile 下的（PlaceGridTile 成功路径已 reparent 走）
            for (int i = draggingItems.Count - 1; i >= 0; i--)
            {
                var item = draggingItems[i];
                if (item == null) continue;
                if (item.transform.parent == transform)
                {
                    // 仍挂在 tile 下，说明 PlaceGridTile 没走，手动移回 homeGrid 原位
                    if (homeGrid != null)
                    {
                        homeGrid.PlaceItem(item, item.CurrentAnchorCol, item.CurrentAnchorRow);
                    }
                    else
                    {
                        // 兜底：挂回 itemContainer（若可访问），否则保持不动
                        item.transform.SetParent(item.transform.root, false);
                    }
                }
            }
            draggingItems.Clear();
        }
    }
}
