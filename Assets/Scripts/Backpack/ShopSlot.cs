using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 商店槽位组件。接收拖入的实体（格子或道具），对齐到槽位中心。
    /// 原型阶段无购买逻辑，允许实体拖回商店槽。
    /// </summary>
    public class ShopSlot : MonoBehaviour, IDropHandler
    {
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
