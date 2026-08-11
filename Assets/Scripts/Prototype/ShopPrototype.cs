using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 原型场景启动脚本。挂在 Canvas 上。
    /// 初始化 5×7 底板，放置两个 2×3 格子在底板中间，商店放道具和格子出售。
    /// 两层系统：底板（暗）→ 格子（半透明）→ 道具（实体）。
    /// </summary>
    public class ShopPrototype : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private BackpackSlot backpackSlotPrefab;
        [SerializeField] private ShopSlot shopSlotPrefab;
        [SerializeField] private DraggableItem itemPrefab;
        [SerializeField] private GridTile gridTilePrefab;

        [Header("Container References")]
        [SerializeField] private Transform backpackGridParent;
        [SerializeField] private Transform shopGridParent;
        [SerializeField] private Transform stagingAreaParent;

        [Header("Baseplate Settings")]
        [SerializeField] private int backpackColumns = 5;
        [SerializeField] private int backpackRows = 7;

        [Header("Shop Settings")]
        [SerializeField] private int shopSlotCount = 6;

        [Header("Placeholder Colors")]
        [SerializeField] private Color weaponColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] private Color seasoningColor = new Color(0.95f, 0.8f, 0.25f);
        [SerializeField] private Color gridTileColor = new Color(0.7f, 0.85f, 0.95f, 0.6f);

        private BackpackGrid backpackGrid;

        private void Start()
        {
            InitBackpackGrid();
            GenerateShop();
            SeedInitialEntities();
        }

        private void InitBackpackGrid()
        {
            backpackGrid = backpackGridParent.GetComponent<BackpackGrid>();
            if (backpackGrid == null)
            {
                backpackGrid = backpackGridParent.gameObject.AddComponent<BackpackGrid>();
            }
            backpackGrid.Init(backpackColumns, backpackRows, backpackSlotPrefab);
        }

        private void GenerateShop()
        {
            for (int i = 0; i < shopSlotCount; i++)
            {
                ShopSlot slot = Instantiate(shopSlotPrefab, shopGridParent);
                slot.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 初始实体：
        /// - 底板中间放两个 2×3 格子（并排，cols 0-3, rows 2-4）
        /// - 第一个格子上放一个 1×1 道具
        /// - 商店放几个道具和一个 1×2 格子出售
        /// </summary>
        private void SeedInitialEntities()
        {
            // 两个 2×3 格子并排放底板中间
            SpawnGridTile(0, 2, 2, 3);  // cols 0-1, rows 2-4
            SpawnGridTile(2, 2, 2, 3);  // cols 2-3, rows 2-4

            // 在第一个格子上放一个 1×1 道具
            SpawnItem(0, 2, "W1", 1, 1, weaponColor);

            // 商店：道具 + 格子出售
            SpawnItemInShop(0, "S1", 1, 1, seasoningColor);
            SpawnItemInShop(1, "W2", 1, 2, weaponColor);
            SpawnGridTileInShop(2, 1, 2);
            SpawnItemInShop(3, "S2", 1, 3, seasoningColor);
            SpawnGridTileInShop(4, 2, 2);
            SpawnItemInShop(5, "W3", 1, 1, weaponColor);
        }

        // —— 背包内生成 ——
        private void SpawnGridTile(int col, int row, int w, int h)
        {
            GridTile tile = Instantiate(gridTilePrefab, backpackGridParent);
            tile.gameObject.SetActive(true);
            ConfigureGridTile(tile, w, h, gridTileColor);
            backpackGrid.PlaceGridTile(tile, col, row);
        }

        private void SpawnItem(int col, int row, string label, int w, int h, Color color)
        {
            DraggableItem item = Instantiate(itemPrefab, backpackGridParent);
            item.gameObject.SetActive(true);
            ConfigureItem(item, label, w, h, color);
            backpackGrid.PlaceItem(item, col, row);
        }

        // —— 商店内生成 ——
        private void SpawnItemInShop(int slotIndex, string label, int w, int h, Color color)
        {
            if (slotIndex < 0 || slotIndex >= shopGridParent.childCount) return;
            Transform slot = shopGridParent.GetChild(slotIndex);
            DraggableItem item = Instantiate(itemPrefab, slot);
            item.gameObject.SetActive(true);
            ConfigureItem(item, label, w, h, color);
            item.ClearGridAssociation();
            RectTransform rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            // 商店内以 fit 尺寸显示
            item.ApplyFitSize(slot.GetComponent<ShopSlot>().GetFitSize());
        }

        private void SpawnGridTileInShop(int slotIndex, int w, int h)
        {
            if (slotIndex < 0 || slotIndex >= shopGridParent.childCount) return;
            Transform slot = shopGridParent.GetChild(slotIndex);
            GridTile tile = Instantiate(gridTilePrefab, slot);
            tile.gameObject.SetActive(true);
            ConfigureGridTile(tile, w, h, gridTileColor);
            tile.ClearGridAssociation();
            RectTransform rt = tile.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
            // 商店内以 fit 尺寸显示
            tile.ApplyFitSize(slot.GetComponent<ShopSlot>().GetFitSize());
        }

        // —— 配置 ——
        private void ConfigureItem(DraggableItem item, string label, int w, int h, Color color)
        {
            Image bg = item.GetComponent<Image>();
            if (bg != null) bg.color = color;

            item.SetLabel(label);
            var txt = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null) txt.text = label;

            item.SetGridSize(w, h, backpackGrid.CellSize, backpackGrid.Spacing);
        }

        private void ConfigureGridTile(GridTile tile, int w, int h, Color color)
        {
            Image bg = tile.GetComponent<Image>();
            if (bg != null) bg.color = color;

            var txt = tile.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null) txt.text = w + "x" + h;

            tile.SetGridSize(w, h, backpackGrid.CellSize, backpackGrid.Spacing);
        }
    }
}
