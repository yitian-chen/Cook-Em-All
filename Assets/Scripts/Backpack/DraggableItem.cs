using UnityEngine;

namespace Vampire.Backpack
{
    /// <summary>
    /// 道具实体（厨具/调味料等）。放在格子（GridTile）上方。
    /// 继承 DraggableEntity 的拖拽/旋转/尺寸逻辑，仅实现放置规则。
    /// </summary>
    public class DraggableItem : DraggableEntity
    {
        [Tooltip("物品显示标签，仅用于原型识别")]
        [SerializeField] private string itemLabel = "Item";

        public string ItemLabel => itemLabel;

        public void SetLabel(string label)
        {
            itemLabel = label;
        }

        public override bool CanPlaceAt(BackpackGrid grid, int col, int row)
        {
            return grid != null && grid.CanPlaceItem(this, col, row);
        }

        public override void PlaceAt(BackpackGrid grid, int col, int row)
        {
            if (grid != null) grid.PlaceItem(this, col, row);
        }

        public override void RemoveFromGrid()
        {
            if (currentGrid != null) currentGrid.RemoveItem(this);
        }
    }
}
