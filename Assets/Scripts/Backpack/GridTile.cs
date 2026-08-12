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
