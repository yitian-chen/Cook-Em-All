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

        /// <summary>
        /// 格子能否放在指定锚点：
        /// 1. footprint 每格在界内且无其他格子占用
        /// 2. 移动场景下（tile.HomeGrid == this）按"跟随/停留"分类处理道具：
        ///   - 完全位于旧 footprint 的道具（following）：随格子一起移动到新位置，
        ///     新位置必须在界内、在新 footprint 内、且不与非跟随/无关道具冲突。
        ///   - 部分位于旧 footprint 的道具（non-following）：保持原位，
        ///     其在旧 footprint 上的每一格必须仍被新 footprint 覆盖（否则道具会落底板 → 禁止）。
        /// 初次放置（HomeGrid != this）没有道具在格子上，跳过第 2 步。
        /// </summary>
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

            // 仅在移动已存在于本 grid 的格子时才需要处理道具跟随
            if (tile.HomeGrid != this) return true;

            int oldCol = tile.HomeAnchorCol;
            int oldRow = tile.HomeAnchorRow;
            int deltaCol = anchorCol - oldCol;
            int deltaRow = anchorRow - oldRow;

            // 收集旧 footprint 上的所有道具
            HashSet<DraggableItem> itemsOnOldTile = new HashSet<DraggableItem>();
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    int c = oldCol + dc;
                    int r = oldRow + dr;
                    if (c < 0 || c >= cols || r < 0 || r >= rows) continue;
                    if (itemMap[c, r] != null) itemsOnOldTile.Add(itemMap[c, r]);
                }
            }

            // 分类：完全在旧 footprint 上 = 跟随；否则 = 停留
            HashSet<DraggableItem> followingItems = new HashSet<DraggableItem>();
            HashSet<DraggableItem> nonFollowingItems = new HashSet<DraggableItem>();
            foreach (var item in itemsOnOldTile)
            {
                if (item == null) continue;
                if (IsItemFullyOnFootprint(item, oldCol, oldRow, w, h))
                    followingItems.Add(item);
                else
                    nonFollowingItems.Add(item);
            }

            // 非跟随道具：旧 footprint 上的每一格必须仍被新 footprint 覆盖
            // （该格原本只有当前格子占据，移走后若无新 footprint 覆盖就会落底板）
            foreach (var item in nonFollowingItems)
            {
                int iw = item.GridWidth;
                int ih = item.GridHeight;
                int ic = item.CurrentAnchorCol;
                int ir = item.CurrentAnchorRow;
                for (int idc = 0; idc < iw; idc++)
                {
                    for (int idr = 0; idr < ih; idr++)
                    {
                        int cc = ic + idc;
                        int rr = ir + idr;
                        bool onOldTile = cc >= oldCol && cc < oldCol + w
                                      && rr >= oldRow && rr < oldRow + h;
                        if (!onOldTile) continue;
                        bool inNewFootprint = cc >= anchorCol && cc < anchorCol + w
                                           && rr >= anchorRow && rr < anchorRow + h;
                        if (!inNewFootprint) return false;
                    }
                }
            }

            // 跟随道具：新位置必须在界内、在新 footprint 内、不与非跟随/无关道具冲突
            // （跟随道具之间同向移动，相对位置不变，不会互相冲突）
            foreach (var item in followingItems)
            {
                int iw = item.GridWidth;
                int ih = item.GridHeight;
                int ic = item.CurrentAnchorCol;
                int ir = item.CurrentAnchorRow;
                for (int idc = 0; idc < iw; idc++)
                {
                    for (int idr = 0; idr < ih; idr++)
                    {
                        int nc = ic + idc + deltaCol;
                        int nr = ir + idr + deltaRow;
                        if (nc < 0 || nc >= cols || nr < 0 || nr >= rows) return false;
                        if (!(nc >= anchorCol && nc < anchorCol + w
                              && nr >= anchorRow && nr < anchorRow + h)) return false;
                        DraggableItem occ = itemMap[nc, nr];
                        if (occ != null && !followingItems.Contains(occ)) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 判断 item 的所有占用格是否完全落在 [footCol, footCol+footW) × [footRow, footRow+footH) 内。
        /// </summary>
        private bool IsItemFullyOnFootprint(DraggableItem item, int footCol, int footRow, int footW, int footH)
        {
            int iw = item.GridWidth;
            int ih = item.GridHeight;
            int ic = item.CurrentAnchorCol;
            int ir = item.CurrentAnchorRow;
            for (int dc = 0; dc < iw; dc++)
            {
                for (int dr = 0; dr < ih; dr++)
                {
                    int c = ic + dc;
                    int r = ir + dr;
                    if (!(c >= footCol && c < footCol + footW
                          && r >= footRow && r < footRow + footH))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 返回完全位于 tile 旧 footprint（HomeAnchor/HomeGrid）上的道具列表。
        /// 用于 GridTile 拖动时把这些道具作为视觉子物体一起带走。
        /// 调用时机：OnBeginDrag 已记录 HomeGrid/HomeAnchor 并从 gridTileMap 移除 tile，
        /// 但 itemMap 仍保留道具占用，因此可按旧 footprint 扫描 itemMap。
        /// </summary>
        public List<DraggableItem> GetFollowingItemsForTile(GridTile tile)
        {
            var result = new List<DraggableItem>();
            if (tile == null || tile.HomeGrid != this) return result;
            int oldCol = tile.HomeAnchorCol;
            int oldRow = tile.HomeAnchorRow;
            int w = tile.GridWidth;
            int h = tile.GridHeight;
            HashSet<DraggableItem> seen = new HashSet<DraggableItem>();
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    int c = oldCol + dc;
                    int r = oldRow + dr;
                    if (c < 0 || c >= cols || r < 0 || r >= rows) continue;
                    DraggableItem item = itemMap[c, r];
                    if (item != null && IsItemFullyOnFootprint(item, oldCol, oldRow, w, h) && seen.Add(item))
                    {
                        result.Add(item);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 放置格子到指定锚点。调用前应已通过 CanPlaceGridTile 验证。
        /// 若为移动场景（tile.HomeGrid == this），完全位于旧 footprint 的道具会跟随移动到新位置；
        /// 部分位于旧 footprint 的道具保持原位。
        /// </summary>
        public void PlaceGridTile(GridTile tile, int anchorCol, int anchorRow)
        {
            int w = tile.GridWidth;
            int h = tile.GridHeight;

            // 移动场景下先处理跟随道具：从旧位置移除，稍后放到新位置
            List<DraggableItem> followingItems = null;
            int deltaCol = 0;
            int deltaRow = 0;
            if (tile.HomeGrid == this)
            {
                int oldCol = tile.HomeAnchorCol;
                int oldRow = tile.HomeAnchorRow;
                deltaCol = anchorCol - oldCol;
                deltaRow = anchorRow - oldRow;

                followingItems = new List<DraggableItem>();
                for (int dc = 0; dc < w; dc++)
                {
                    for (int dr = 0; dr < h; dr++)
                    {
                        int c = oldCol + dc;
                        int r = oldRow + dr;
                        if (c < 0 || c >= cols || r < 0 || r >= rows) continue;
                        DraggableItem occ = itemMap[c, r];
                        if (occ != null && IsItemFullyOnFootprint(occ, oldCol, oldRow, w, h))
                        {
                            if (!followingItems.Contains(occ)) followingItems.Add(occ);
                        }
                    }
                }

                // 先把跟随道具从 itemMap 移除（视觉与 currentAnchor 稍后由 PlaceItem 重置）
                foreach (var item in followingItems) RemoveItem(item);
            }

            // 写入格子占用
            for (int dc = 0; dc < w; dc++)
            {
                for (int dr = 0; dr < h; dr++)
                {
                    gridTileMap[anchorCol + dc, anchorRow + dr] = tile;
                }
            }

            RegisterEntity(tile);
            ApplyEntitySize(tile);
            PositionEntity(tile, anchorCol, anchorRow, gridTileContainer);
            tile.SetPlacement(this, anchorCol, anchorRow);

            // 跟随道具放到新位置（偏移 delta）
            if (followingItems != null)
            {
                foreach (var item in followingItems)
                {
                    int newCol = item.CurrentAnchorCol + deltaCol;
                    int newRow = item.CurrentAnchorRow + deltaRow;
                    PlaceItem(item, newCol, newRow);
                }
            }
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
            ApplyEntitySize(item);
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

            // 钳制锚点到网格可容纳范围内，缩窄边缘"死区"——
            // 光标落在边缘格附近时，物品吸附到能放下的最近位置，而不是直接弹回。
            int maxCol = Mathf.Max(0, cols - entity.GridWidth);
            int maxRow = Mathf.Max(0, rows - entity.GridHeight);
            anchorCol = Mathf.Clamp(anchorCol, 0, maxCol);
            anchorRow = Mathf.Clamp(anchorRow, 0, maxRow);

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
