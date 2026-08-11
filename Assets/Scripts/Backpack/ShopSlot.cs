using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 商店槽位组件。接收拖入的实体（格子或道具），对齐到槽位中心。
    /// 实体在商店内以 fit 尺寸显示（缩放至槽位内），拖出后还原真实大小。
    /// 原型阶段无购买逻辑，允许实体拖回商店槽。
    /// </summary>
    public class ShopSlot : MonoBehaviour, IDropHandler
    {
        [Tooltip("实体与槽位边缘的留白（像素）")]
        [SerializeField] private float padding = 10f;

        /// <summary>返回实体在该槽位内可用的最大尺寸（已减去 padding）。</summary>
        public Vector2 GetFitSize()
        {
            RectTransform rt = GetComponent<RectTransform>();
            Vector2 size = rt.rect.size;
            return new Vector2(
                Mathf.Max(0f, size.x - padding * 2f),
                Mathf.Max(0f, size.y - padding * 2f));
        }

        public void OnDrop(PointerEventData eventData)
        {
            DraggableEntity entity = GetEntityFromEvent(eventData);
            if (entity == null) return;

            // 离开背包（占用已在 OnBeginDrag 中清除）
            entity.ClearGridAssociation();

            // 若该槽已有实体，把原实体推回到拖拽者原父级
            if (transform.childCount > 0)
            {
                Transform existing = transform.GetChild(0);
                DraggableEntity existingEntity = existing.GetComponent<DraggableEntity>();
                if (existingEntity != null && existingEntity != entity)
                {
                    existingEntity.ReturnToOriginal();
                }
            }

            entity.transform.SetParent(transform);
            CenterEntity(entity);
            entity.ApplyFitSize(GetFitSize());
        }

        private void CenterEntity(DraggableEntity entity)
        {
            RectTransform rt = entity.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
        }

        private static DraggableEntity GetEntityFromEvent(PointerEventData eventData)
        {
            return eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableEntity>()
                : null;
        }
    }
}
