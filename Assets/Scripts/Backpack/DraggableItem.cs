using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 可拖拽物品组件。挂到任何需要在背包/商店/暂存区之间拖动的物品上。
    /// 实现 uGUI 的拖拽接口，处理跨区域移动与归位逻辑。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("物品显示标签，仅用于原型识别")]
        [SerializeField] private string itemLabel = "Item";

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Transform originalParent;
        private Vector2 originalAnchoredPosition;
        private Canvas rootCanvas;
        private Transform dragLayer;

        /// <summary>
        /// 拖拽进行中标志，供 Drop 目标判断当前事件是否有效。
        /// </summary>
        public bool IsDragging { get; private set; }

        /// <summary>
        /// 物品拖拽开始时的父级，未命中任何 Drop 目标时回到这里。
        /// </summary>
        public Transform OriginalParent => originalParent;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            // 向上查找根 Canvas，拖拽时把物品临时挂到它的 transform 下
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            IsDragging = true;
            originalParent = transform.parent;
            originalAnchoredPosition = rectTransform.anchoredPosition;

            // 临时挂到根 Canvas 下，使其能跨越原父级的层级跟随鼠标
            dragLayer = rootCanvas.transform;
            transform.SetParent(dragLayer);
            transform.SetAsLastSibling();

            // 关闭 raycast，让 OnDrag 期间的 raycast 能穿透物品命中下方 drop 目标
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.7f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            // 用 RectTransformUtility 把屏幕鼠标坐标转换到 Canvas 局部坐标，
            // 这样在 Scale With Screen Size 模式下也不会偏移
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                rectTransform.localPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            IsDragging = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            // 若 OnDrop 已经 reparent 了物品，originalParent 不会被使用；
            // 若没命中任何 IDropHandler，回到原父级原位置。
            if (transform.parent == dragLayer)
            {
                ReturnToOriginal();
            }
        }

        /// <summary>
        /// 把物品归还到拖拽开始时的父级和位置。
        /// </summary>
        public void ReturnToOriginal()
        {
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }
}
