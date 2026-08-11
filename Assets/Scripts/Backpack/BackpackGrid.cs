using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 背包底板协调器。5×7 最大范围，所有 slot 初始为空（暗色底板）。
    /// 两层系统：
    ///   1. 格子层（GridTile）：玩家拼排在底板上的单元格，占用 gridTileMap
    ///   2. 道具层（DraggableItem）：放在格子上方，占用 itemMap，要求下方有格子
    /// BackpackSlot 只负责接收 drop 事件与底板视觉，所有放置决策在此处。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BackpackGrid : MonoBehaviour
    {
        private RectTransform rectTransform;
        private GridLayoutGroup layoutGroup;

        private int cols;
        private int rows;
        private BackpackSlot[,] slots;
        private GridTile[,] gridTileMap;       // 格子占用：哪格被哪个 GridTile 占
        private DraggableItem[,] itemMap;      // 道具占用：哪格被哪个 DraggableItem 占

        // 布局参数
        private Vector2 cellSize;
        private Vector2 spacing;
        private RectOffset padding;

        // 层级容器：gridTileContainer 在下，itemContainer 在上，确保道具渲染在格子上方
        private RectTransform gridTileContainer;
        private RectTransform itemContainer;

        // 注册的所有实体（用于拖拽时切换 raycast）
        private readonly HashSet<DraggableEntity> allEntities = new HashSet<DraggableEntity>();

        // 预览状态
        private readonly List<BackpackSlot> previewedSlots = new List<BackpackSlot>();

        public RectTransform Rect => rectTransform;
        public int Cols => cols;
        public int Rows => rows;
        public Vector2 CellSize => cellSize;
        public Vector2 Spacing => spacing;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            layoutGroup = GetComponent<GridLayoutGroup>();
        }

        /// <summary>
        /// 初始化底板：生成 cols×rows 个 slot（全部底板状态），创建层级容器。
        /// </summary>
        public void Init(int cols, int rows, BackpackSlot slotPrefab)
        {
            this.cols = cols;
            this.rows = rows;

            if (layoutGroup != null)
            {
                cellSize = layoutGroup.cellSize;
                spacing = layoutGroup.spacing;
                padding = layoutGroup.padding;
            }
            else
            {
                cellSize = new Vector2(100, 100);
                spacing = Vector2.zero;
                padding = new RectOffset(10, 10, 10, 10);
            }

            slots = new BackpackSlot[cols, rows];
            gridTileMap = new GridTile[cols, rows];
            itemMap = new DraggableItem[cols, rows];

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    BackpackSlot slot = Instantiate(slotPrefab, transform);
                    slot.gameObject.SetActive(true);
                    slot.Init(col, row, this);
                    slots[col, row] = slot;
                }
            }

            CreateContainers();

            // 强制 GridLayoutGroup 立即计算 slot 位置，避免布局时序问题
            // （Start 中调用 Place 时 slot.anchoredPosition 可能还是 0,0）
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        /// <summary>
        /// 创建层级容器：gridTileContainer（下）和 itemContainer（上）。
        /// 容器是 grid 的兄弟节点（非子节点），避免被 GridLayoutGroup 当作 slot 处理。
        /// 复制 grid 的 RectTransform 设置使其完全重叠 grid。
        /// </summary>
        private void CreateContainers()
        {
            Transform parent = transform.parent;
            gridTileContainer = CreateContainer("GridTileContainer", parent, rectTransform);
            itemContainer = CreateContainer("ItemContainer", parent, rectTransform);
            // 确保 container 渲染在 grid 之上（siblingIndex 靠后）
            gridTileContainer.SetAsLastSibling();
            itemContainer.SetAsLastSibling();
        }

        private RectTransform CreateContainer(string name, Transform parent, RectTransform reference)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            // 复制 grid 的 rect 设置，使 container 与 grid 完全重叠且坐标系一致
            rt.anchorMin = reference.anchorMin;
            rt.anchorMax = reference.anchorMax;
            rt.sizeDelta = reference.sizeDelta;
            rt.pivot = reference.pivot;
            rt.anchoredPosition = reference.anchoredPosition;
            return rt;
        }

        public BackpackSlot GetSlot(int col, int row)
        {
            if (col < 0 || col >= cols || row < 0 || row >= rows) return null;
            return slots[col, row];
        }

        public bool HasGridTileAt(int col, int row)
        {
            return col >= 0 && col < cols && row >= 0 && row < rows && gridTileMap[col, row] != null;
        }

        // ===================== 格子层 =====================

        /// <summary>格子能否放在指定锚点：footprint 每格在界内且无其他格子占用。</summary>
        public bool CanPlaceGridTile(GridTile tile, int anchorCol, int anchorRow)
        {
            if (tile == null) return false;
            int w = tile.GridWidth;
            int h = tile.GridHeight;
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    int c = anchorCol + dc;
                    int r = anchorRow + dr;
                    if (c < 0 || c >= cols || r < 0 || r >= rows) return false;
                    if (gridTileMap[c, r] != null && gridTileMap[c, r] != tile) return false;
                }
            }
            return true;
        }

        /// <summary>放置格子到指定锚点。调用前应已通过 CanPlaceGridTile 验证。</summary>
        public void PlaceGridTile(GridTile tile, int anchorCol, int anchorRow)
        {
            int w = tile.GridWidth;
            int h = tile.GridHeight;
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    gridTileMap[anchorCol + dc, anchorRow + dr] = tile;
                }
            }

            RegisterEntity(tile);
            PositionEntity(tile, anchorCol, anchorRow, gridTileContainer);
            tile.SetPlacement(this, anchorCol, anchorRow);
        }

        public void RemoveGridTile(GridTile tile)
        {
            if (tile == null) return;
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (gridTileMap[c, r] == tile) gridTileMap[c, r] = null;
                }
            }
        }

        // ===================== 道具层 =====================

        /// <summary>道具能否放在指定锚点：footprint 每格在界内、下方有格子、无其他道具占用。</summary>
        public bool CanPlaceItem(DraggableItem item, int anchorCol, int anchorRow)
        {
            if (item == null) return false;
            int w = item.GridWidth;
            int h = item.GridHeight;
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    int c = anchorCol + dc;
                    int r = anchorRow + dr;
                    if (c < 0 || c >= cols || r < 0 || r >= rows) return false;
                    if (gridTileMap[c, r] == null) return false;       // 下方必须有格子
                    if (itemMap[c, r] != null && itemMap[c, r] != item) return false;
                }
            }
            return true;
        }

        public void PlaceItem(DraggableItem item, int anchorCol, int anchorRow)
        {
            int w = item.GridWidth;
            int h = item.GridHeight;
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    itemMap[anchorCol + dc, anchorRow + dr] = item;
                }
            }

            RegisterEntity(item);
            PositionEntity(item, anchorCol, anchorRow, itemContainer);
            item.SetPlacement(this, anchorCol, anchorRow);
        }

        public void RemoveItem(DraggableItem item)
        {
            if (item == null) return;
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (itemMap[c, r] == item) itemMap[c, r] = null;
                }
            }
        }

        // ===================== 定位与放置入口 =====================

        /// <summary>把实体放到对应容器中，位置用数学计算对齐到锚点 slot 中心 + 多格偏移。</summary>
        private void PositionEntity(DraggableEntity entity, int anchorCol, int anchorRow, RectTransform container)
        {
            entity.transform.SetParent(container, false);

            // 数学计算锚点 slot 中心在 grid 局部坐标中的位置（不依赖 layout 已完成）
            Vector2 rectSize = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            float slotX = padding.left + anchorCol * (cellSize.x + spacing.x) + cellSize.x * 0.5f - pivot.x * rectSize.x;
            float slotY = (1f - pivot.y) * rectSize.y - (padding.top + anchorRow * (cellSize.y + spacing.y) + cellSize.y * 0.5f);

            float cellPitchX = cellSize.x + spacing.x;
            float cellPitchY = cellSize.y + spacing.y;
            Vector2 offset = new Vector2(
                (entity.GridWidth - 1) * 0.5f * cellPitchX,
                -(entity.GridHeight - 1) * 0.5f * cellPitchY
            );
            entity.Rect.anchoredPosition = new Vector2(slotX, slotY) + offset;
        }

        /// <summary>由 BackpackSlot.OnDrop 调用：根据 cursor 推算锚点并尝试放置。</summary>
        public bool TryPlaceAtCursor(DraggableEntity entity, Vector2 screenPos, Camera cam)
        {
            if (entity == null) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, cam)) return false;

            Vector2Int anchor = ComputeAnchorFromCursor(entity, screenPos, cam);
            if (entity.CanPlaceAt(this, anchor.x, anchor.y))
            {
                entity.PlaceAt(this, anchor.x, anchor.y);
                return true;
            }
            return false;
        }

        private Vector2Int ComputeAnchorFromCursor(DraggableEntity entity, Vector2 screenPos, Camera cam)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPos, cam, out Vector2 cursorLocal))
            {
                return new Vector2Int(-1, -1);
            }

            Vector2 rectSize = rectTransform.rect.size;
            Vector2 pivot = rectTransform.pivot;
            float leftTopX = -pivot.x * rectSize.x;
            float leftTopY = (1f - pivot.y) * rectSize.y;
            float dx = cursorLocal.x - leftTopX;
            float dy = leftTopY - cursorLocal.y;

            float itemW = entity.GridWidth * cellSize.x + (entity.GridWidth - 1) * spacing.x;
            float itemH = entity.GridHeight * cellSize.y + (entity.GridHeight - 1) * spacing.y;
            float topLeftX = dx - itemW * 0.5f;
            float topLeftY = dy - itemH * 0.5f;

            float pitchX = cellSize.x + spacing.x;
            float pitchY = cellSize.y + spacing.y;
            int anchorCol = Mathf.RoundToInt((topLeftX - padding.left) / pitchX);
            int anchorRow = Mathf.RoundToInt((topLeftY - padding.top) / pitchY);
            return new Vector2Int(anchorCol, anchorRow);
        }

        // ===================== 预览 =====================

        public void UpdatePreview(DraggableEntity entity, Vector2 screenPos, Camera cam)
        {
            ClearPreview();
            if (entity == null) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, cam)) return;

            Vector2Int anchor = ComputeAnchorFromCursor(entity, screenPos, cam);
            bool valid = entity.CanPlaceAt(this, anchor.x, anchor.y);

            for (int dc = 0; dc < entity.GridWidth; dc++)
            {
                for (int dr = 0; dr < entity.GridHeight; dr++)
                {
                    BackpackSlot slot = GetSlot(anchor.x + dc, anchor.y + dr);
                    if (slot != null)
                    {
                        slot.SetHighlight(valid ? HighlightState.Valid : HighlightState.Invalid);
                        previewedSlots.Add(slot);
                    }
                }
            }
        }

        public void ClearPreview()
        {
            foreach (var slot in previewedSlots)
            {
                if (slot != null) slot.ClearHighlight();
            }
            previewedSlots.Clear();
        }

        // ===================== Raycast 管理 =====================

        private void RegisterEntity(DraggableEntity entity)
        {
            allEntities.Add(entity);
        }

        /// <summary>拖拽开始时，让其他实体不阻挡 raycast，确保 raycast 穿透到 baseplate slot。</summary>
        public void SetOthersNonRaycasting(DraggableEntity dragging)
        {
            foreach (var e in allEntities)
            {
                if (e == dragging || e == null) continue;
                var cg = e.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = false;
            }
        }

        public void RestoreAllRaycasting()
        {
            allEntities.RemoveWhere(e => e == null);
            foreach (var e in allEntities)
            {
                var cg = e.GetComponent<CanvasGroup>();
                if (cg != null) cg.blocksRaycasts = true;
            }
        }

        /// <summary>用本 grid 的 cellSize/spacing 更新实体视觉尺寸（旋转后调用）。</summary>
        public void ApplyEntitySize(DraggableEntity entity)
        {
            entity.UpdateSize(cellSize, spacing);
        }
    }
}
