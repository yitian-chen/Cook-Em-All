using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 商店槽位组件。接收拖入的物品，对齐到槽位中心。
    /// 原型阶段无购买逻辑，允许物品拖回商店槽。
    /// </summary>
    public class ShopSlot : MonoBehaviour, IDropHandler
    {
        private RectTransform rectTransform;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public void OnDrop(PointerEventData eventData)
        {
            DraggableItem item = GetDraggableFromEvent(eventData);
            if (item == null) return;

            // 若该槽已有物品，把原物品推回到拖拽者原父级
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
