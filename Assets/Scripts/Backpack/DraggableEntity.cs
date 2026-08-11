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
        /// </summary>
        public void SetGridSize(int width, int height, Vector2 cellSize, Vector2 spacing)
        {
            gridWidth = width;
            gridHeight = height;
            UpdateSize(cellSize, spacing);
        }

        public void UpdateSize(Vector2 cellSize, Vector2 spacing)
        {
            float w = gridWidth * cellSize.x + Mathf.Max(0, gridWidth - 1) * spacing.x;
            float h = gridHeight * cellSize.y + Mathf.Max(0, gridHeight - 1) * spacing.y;
            rectTransform.sizeDelta = new Vector2(w, h);
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

            BackpackGrid grid = homeGrid ?? currentGrid ?? GetPreviewGrid();
            if (grid != null)
            {
                grid.ApplyEntitySize(this);
            }
        }

        void Update()
        {
            if (IsDragging && Input.GetKeyDown(KeyCode.R))
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
