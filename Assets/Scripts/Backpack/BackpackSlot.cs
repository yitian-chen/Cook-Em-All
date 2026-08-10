using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 背包格子组件。挂到背包网格的每个格子上。
    /// 接收拖入的物品，对齐到格子中心。
    /// 原型阶段不限制每格放几个物品，也不处理物品尺寸占多格的逻辑。
    /// </summary>
    public class BackpackSlot : MonoBehaviour, IDropHandler
    {
        [Tooltip("是否已解锁。锁定格拒绝接收物品。")]
        [SerializeField] private bool unlocked = true;

        private RectTransform rectTransform;
        private Image backgroundImage;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            backgroundImage = GetComponent<Image>();
        }

        /// <summary>
        /// 运行时设置格子的解锁状态，并同步视觉（锁定格变深灰半透明）。
        /// </summary>
        public void SetUnlocked(bool isUnlocked)
        {
            unlocked = isUnlocked;
            if (backgroundImage != null)
            {
                backgroundImage.color = isUnlocked
                    ? new Color(1f, 1f, 1f, 0.3f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.5f);
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!unlocked) return;

            DraggableItem item = GetDraggableFromEvent(eventData);
            if (item == null) return;

            // 若该格已有物品，把原物品推回到拖拽者原父级（简单交换占位）
            if (transform.childCount > 0)
            {
                Transform existing = transform.GetChild(0);
                DraggableItem existingItem = existing.GetComponent<DraggableItem>();
                if (existingItem != null && existingItem != item)
                {
                    existingItem.ReturnToOriginal();
                }
            }

            item.transform.SetParent(transform);
            CenterItem(item);
        }

        /// <summary>
        /// 把物品在本格内居中。
        /// </summary>
        private void CenterItem(DraggableItem item)
        {
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchoredPosition = Vector2.zero;
        }

        private static DraggableItem GetDraggableFromEvent(PointerEventData eventData)
        {
            return eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableItem>()
                : null;
        }
    }
}
