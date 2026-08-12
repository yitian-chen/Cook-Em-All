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

        // 随机放置最多尝试次数（避开已有子物体）
        private const int MaxPlacementAttempts = 20;

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

        /// <summary>
        /// 把实体以编程方式放入暂存区（不依赖 drop 事件）。
        /// 用于"被置换弹开"的道具：自动放置到暂存区内随机且尽量不重叠的位置。
        /// 流程：ClearGridAssociation → reparent → ApplyRealSize → 随机非重叠位置。
        /// </summary>
        public void AcceptEntity(DraggableEntity entity)
        {
            if (entity == null) return;

            entity.ClearGridAssociation();
            entity.transform.SetParent(transform, false);
            entity.ApplyRealSize();

            RectTransform entityRect = entity.Rect;
            Vector2 entitySize = entityRect.rect.size;
            Rect stagingRect = rectTransform.rect;

            // 在暂存区本地矩形内，item 中心可放置的范围（保证整个 item 在暂存区内）
            float halfW = entitySize.x * 0.5f;
            float halfH = entitySize.y * 0.5f;
            float minX = stagingRect.xMin + halfW;
            float maxX = stagingRect.xMax - halfW;
            float minY = stagingRect.yMin + halfH;
            float maxY = stagingRect.yMax - halfH;

            // 范围为空（暂存区比道具还小）时直接放中心
            if (maxX < minX || maxY < minY)
            {
                entityRect.anchoredPosition = Vector2.zero;
                return;
            }

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float y = Random.Range(minY, maxY);
                Vector2 candidate = new Vector2(x, y);
                if (!OverlapsExisting(entityRect, candidate, entitySize))
                {
                    entityRect.anchoredPosition = candidate;
                    return;
                }
            }

            // 兜底：接受重叠，仍给一个随机位置
            entityRect.anchoredPosition = new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY));
        }

        /// <summary>
        /// 判定候选位置 + entitySize 形成的矩形是否与暂存区内已有子物体重叠。
        /// 坐标系：anchoredPosition（相对暂存区 pivot），与 rect 中心一致（pivot 0.5 假设）。
        /// </summary>
        private bool OverlapsExisting(RectTransform ignore, Vector2 candidateCenter, Vector2 candidateSize)
        {
            Rect candidate = new Rect(candidateCenter - candidateSize * 0.5f, candidateSize);
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = transform.GetChild(i) as RectTransform;
                if (child == null || child == ignore) continue;
                Vector2 childSize = child.rect.size;
                Rect childRect = new Rect((Vector2)child.anchoredPosition - childSize * 0.5f, childSize);
                if (candidate.Overlaps(childRect)) return true;
            }
            return false;
        }

        private static DraggableEntity GetEntityFromEvent(PointerEventData eventData)
        {
            return eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableEntity>()
                : null;
        }
    }
}
