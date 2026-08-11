using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 暂存区组件。实体拖到暂存区后停在松手位置（自由坐标，无格子）。
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
            DraggableEntity entity = GetEntityFromEvent(eventData);
            if (entity == null) return;

            // 离开背包（占用已在 OnBeginDrag 中清除）
            entity.ClearGridAssociation();

            RectTransform entityRect = entity.GetComponent<RectTransform>();
            entity.transform.SetParent(transform);

            // 暂存区在商店外，以真实大小显示
            entity.ApplyRealSize();

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                entityRect.anchoredPosition = localPoint;
            }

            if (!RectTransformUtility.RectangleContainsScreenPoint(
                    rectTransform, eventData.position, eventData.pressEventCamera))
            {
                entity.ReturnToOriginal();
            }
        }

        private static DraggableEntity GetEntityFromEvent(PointerEventData eventData)
        {
            return eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableEntity>()
                : null;
        }
    }
}
