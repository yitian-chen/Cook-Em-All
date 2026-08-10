using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 暂存区组件。物品拖到暂存区后停在松手位置（自由坐标，无格子）。
    /// 注意：暂存区不能挂 LayoutGroup，否则会强制重排覆盖自由位置。
    /// </summary>
    public class StagingArea : MonoBehaviour, IDropHandler
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

            // reparent 到暂存区，保留松手时的局部坐标（DraggableItem.OnDrag 已设置好位置）
            // 关键：reparent 不能重置 localPosition。我们手动转换一次坐标确保正确。
            RectTransform itemRect = item.GetComponent<RectTransform>();
            item.transform.SetParent(transform);

            // 把松手位置（屏幕坐标）转换到暂存区局部坐标
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                itemRect.anchoredPosition = localPoint;
            }

            // 限制在暂存区范围内（可选：超界则回到原父级）
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    rectTransform, eventData.position, eventData.pressEventCamera))
            {
                item.ReturnToOriginal();
            }
        }

        private static DraggableItem GetDraggableFromEvent(PointerEventData eventData)
        {
            return eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableItem>()
                : null;
        }
    }
}
