using UnityEngine;
using UnityEngine.EventSystems;

namespace Vampire.Backpack
{
    /// <summary>
    /// 可拖拽实体基类。提取共享的拖拽/旋转/尺寸逻辑。
    /// 子类（GridTile、DraggableItem）实现放置规则。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class DraggableEntity : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Tooltip("在网格中的宽度（格数）")]
        [SerializeField] protected int gridWidth = 1;
        [Tooltip("在网格中的高度（格数）")]
        [SerializeField] protected int gridHeight = 1;

        // 真实尺寸（背包格的 cellSize/spacing），由 SetGridSize 缓存。
        // 用于 ApplyRealSize：拖拽中、暂存区、放回背包时还原真实大小。
        private Vector2 realCellSize = new Vector2(100, 100);
        private Vector2 realSpacing = Vector2.zero;

        protected RectTransform rectTransform;
        protected CanvasGroup canvasGroup;
        protected Transform originalParent;
        protected Vector2 originalAnchoredPosition;
        protected Canvas rootCanvas;
        protected Transform dragLayer;

        // 当前所在 grid 与锚点（null=在 shop/staging/拖拽中）
        protected BackpackGrid currentGrid;
        protected int currentAnchorCol;
        protected int currentAnchorRow;

        // 拖拽开始时记录的原始位置（用于 ReturnToOriginal）
        protected BackpackGrid homeGrid;
        protected int homeAnchorCol;
        protected int homeAnchorRow;
        protected bool wasInGrid;

        private BackpackGrid cachedPreviewGrid;
        private bool previewGridCached;

        public bool IsDragging { get; protected set; }
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public BackpackGrid CurrentGrid => currentGrid;
        public RectTransform Rect => rectTransform;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
        }

        /// <summary>
        /// 设置网格尺寸并更新 RectTransform sizeDelta。
        /// 同时缓存真实 cellSize/spacing，供 ApplyRealSize 还原。
        /// </summary>
        public void SetGridSize(int width, int height, Vector2 cellSize, Vector2 spacing)
        {
            gridWidth = width;
            gridHeight = height;
            realCellSize = cellSize;
            realSpacing = spacing;
            UpdateSize(cellSize, spacing);
        }

        public void UpdateSize(Vector2 cellSize, Vector2 spacing)
        {
            float w = gridWidth * cellSize.x + Mathf.Max(0, gridWidth - 1) * spacing.x;
            float h = gridHeight * cellSize.y + Mathf.Max(0, gridHeight - 1) * spacing.y;
            rectTransform.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// 还原为真实背包尺寸（拖拽中、暂存区、放入背包时使用）。
        /// </summary>
        public void ApplyRealSize()
        {
            UpdateSize(realCellSize, realSpacing);
        }

        /// <summary>
        /// 等比缩放到 available 内（商店槽位使用）。
        /// 维持 gridWidth×gridHeight（含 spacing）的宽高比，居中后刚好不超出 available。
        /// </summary>
        public void ApplyFitSize(Vector2 available)
        {
            if (gridWidth <= 0 || gridHeight <= 0) return;
            float realW = gridWidth * realCellSize.x + Mathf.Max(0, gridWidth - 1) * realSpacing.x;
            float realH = gridHeight * realCellSize.y + Mathf.Max(0, gridHeight - 1) * realSpacing.y;
            if (realW <= 0f || realH <= 0f) return;
            if (available.x <= 0f || available.y <= 0f) return;
            float scale = Mathf.Min(available.x / realW, available.y / realH);
            rectTransform.sizeDelta = new Vector2(realW * scale, realH * scale);
        }

        /// <summary>由 BackpackGrid 调用，记录当前所在 grid 与锚点。</summary>
        public virtual void SetPlacement(BackpackGrid grid, int anchorCol, int anchorRow)
        {
            currentGrid = grid;
            currentAnchorCol = anchorCol;
            currentAnchorRow = anchorRow;
        }

        /// <summary>由 ShopSlot/StagingArea 调用，标记已离开 grid。</summary>
        public virtual void ClearGridAssociation()
        {
            currentGrid = null;
        }

        // —— 子类实现的放置规则 ——
        public abstract bool CanPlaceAt(BackpackGrid grid, int col, int row);
        public abstract void PlaceAt(BackpackGrid grid, int col, int row);
        public abstract void RemoveFromGrid();

        // —— 共享拖拽逻辑 ——
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            IsDragging = true;
            originalParent = transform.parent;
            originalAnchoredPosition = rectTransform.anchoredPosition;

            homeGrid = currentGrid;
            homeAnchorCol = currentAnchorCol;
            homeAnchorRow = currentAnchorRow;
            wasInGrid = currentGrid != null;
            if (currentGrid != null)
            {
                RemoveFromGrid();
                currentGrid = null;
            }

            // 临时挂到根 Canvas
            dragLayer = rootCanvas.transform;
            transform.SetParent(dragLayer);
            transform.SetAsLastSibling();

            // 拖拽中以真实大小显示（从商店拖出时从 fit 尺寸还原）
            ApplyRealSize();

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.7f;

            // 让其他实体不阻挡 raycast，确保 raycast 穿透到 baseplate slot
            BackpackGrid grid = GetPreviewGrid();
            if (grid != null) grid.SetOthersNonRaycasting(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                rectTransform.localPosition = localPoint;
            }

            BackpackGrid grid = GetPreviewGrid();
            if (grid == null) return;

            if (RectTransformUtility.RectangleContainsScreenPoint(grid.Rect, eventData.position, eventData.pressEventCamera))
            {
                grid.UpdatePreview(this, eventData.position, eventData.pressEventCamera);
            }
            else
            {
                grid.ClearPreview();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            IsDragging = false;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;

            BackpackGrid grid = GetPreviewGrid();
            if (grid != null)
            {
                grid.ClearPreview();
                grid.RestoreAllRaycasting();
            }

            // 若 OnDrop 已 reparent（grid/shop/staging 接管），transform.parent != dragLayer
            // 若没命中任何 IDropHandler，回到原位
            if (transform.parent == dragLayer)
            {
                ReturnToOriginal();
            }
        }

        /// <summary>
        /// 回到拖拽开始时的位置。
        /// 若原在 grid 中：尝试放回原锚点；旋转后原位放不下则回滚旋转再试。
        /// 若原在 shop/staging：回到原父级原位置。
        /// </summary>
        public void ReturnToOriginal()
        {
            if (wasInGrid && homeGrid != null)
            {
                if (CanPlaceAt(homeGrid, homeAnchorCol, homeAnchorRow))
                {
                    PlaceAt(homeGrid, homeAnchorCol, homeAnchorRow);
                    return;
                }
                // 旋转后原位放不下，回滚旋转再试
                if (gridWidth != gridHeight)
                {
                    RotateInternal();
                    if (CanPlaceAt(homeGrid, homeAnchorCol, homeAnchorRow))
                    {
                        PlaceAt(homeGrid, homeAnchorCol, homeAnchorRow);
                        return;
                    }
                }
            }
            // 兜底：回到原父级原位置
            transform.SetParent(originalParent);
            rectTransform.anchoredPosition = originalAnchoredPosition;
            ApplySizeForParent(originalParent);
        }

        /// <summary>
        /// 根据目标父级应用对应尺寸：
        /// - ShopSlot：fit 到槽位
        /// - 其他（暂存区/根）：真实尺寸
        /// </summary>
        private void ApplySizeForParent(Transform parent)
        {
            if (parent == null) return;
            ShopSlot shop = parent.GetComponent<ShopSlot>();
            if (shop != null)
            {
                ApplyFitSize(shop.GetFitSize());
                return;
            }
            ApplyRealSize();
        }

        public void Rotate()
        {
            RotateInternal();
        }

        private void RotateInternal()
        {
            int tmp = gridWidth;
            gridWidth = gridHeight;
            gridHeight = tmp;

            // 旋转只在拖拽中触发，直接用真实尺寸
            ApplyRealSize();
        }

        void Update()
        {
            if (!IsDragging) return;
            if (Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(1))
            {
                Rotate();
            }
        }

        protected BackpackGrid GetPreviewGrid()
        {
            if (!previewGridCached)
            {
                cachedPreviewGrid = FindObjectOfType<BackpackGrid>();
                previewGridCached = true;
            }
            return cachedPreviewGrid;
        }
    }
}
