using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 底板格子组件。5×7 底板上的每个位置一个，视觉为暗色底板。
    /// 接收 drop 事件并委托给 BackpackGrid 处理（grid 区分格子/道具两层放置规则）。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BackpackSlot : MonoBehaviour, IDropHandler
    {
        private int col;
        private int row;
        private BackpackGrid grid;
        private Image backgroundImage;
        private static readonly Color BaseplateColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

        public int Col => col;
        public int Row => row;
        public RectTransform Rect => GetComponent<RectTransform>();

        void Awake()
        {
            backgroundImage = GetComponent<Image>();
        }

        public void Init(int col, int row, BackpackGrid grid)
        {
            this.col = col;
            this.row = row;
            this.grid = grid;
            if (backgroundImage != null)
            {
                backgroundImage.color = BaseplateColor;
            }
        }

        /// <summary>高亮预览：Valid=绿色，Invalid=红色，None=恢复底板色。</summary>
        public void SetHighlight(HighlightState state)
        {
            if (backgroundImage == null) return;
            switch (state)
            {
                case HighlightState.Valid:
                    backgroundImage.color = new Color(0.3f, 0.9f, 0.4f, 0.5f);
                    break;
                case HighlightState.Invalid:
                    backgroundImage.color = new Color(0.9f, 0.3f, 0.3f, 0.5f);
                    break;
                default:
                    backgroundImage.color = BaseplateColor;
                    break;
            }
        }

        public void ClearHighlight()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = BaseplateColor;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (grid == null || eventData.pointerDrag == null) return;
            DraggableEntity entity = eventData.pointerDrag.GetComponent<DraggableEntity>();
            if (entity == null) return;

            grid.TryPlaceAtCursor(entity, eventData.position, eventData.pressEventCamera);
        }
    }

    public enum HighlightState
    {
        None,
        Valid,
        Invalid
    }
}
