using UnityEngine;

namespace Vampire.Backpack
{
    /// <summary>
    /// 格子实体。代表玩家购买并拼排在底板上的"格子"。
    /// 放在底板空位上，道具只能放在格子上。
    /// 视觉：半透明单元格（让底板高亮预览能透出）。
    /// </summary>
    public class GridTile : DraggableEntity
    {
        /// <summary>
        /// 身上承载道具时禁止拖拽，避免移动后道具落到底板上。
        /// 在商店/暂存区（currentGrid == null）时允许拖拽。
        /// </summary>
        public override bool CanDrag()
        {
            return currentGrid == null || !currentGrid.HasItemsOnTile(this);
        }

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
    }
}
