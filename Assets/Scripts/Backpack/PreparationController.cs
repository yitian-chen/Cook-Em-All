using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vampire.Backpack
{
    /// <summary>
    /// 整备阶段控制器。挂在整备 Canvas 上。
    /// 由 WaveManager 在波次切换时调用 Init/SeedBackpack/Show/Hide。
    /// 流程：Init() 创建底板与商店槽 → SeedBackpack() 放置初始格子和起始武器 →
    /// Show() 显示 Canvas（战斗时 Hide）。
    /// 两层系统：底板（暗）→ 格子（半透明）→ 道具（实体）。
    /// </summary>
    public class PreparationController : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private BackpackSlot backpackSlotPrefab;
        [SerializeField] private ShopSlot shopSlotPrefab;
        [SerializeField] private DraggableItem itemPrefab;
        [SerializeField] private WeaponItem weaponItemPrefab;
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

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI coinsDisplay;
        [SerializeField] private Vampire.StatsManager statsManager;

        private BackpackGrid backpackGrid;
        private bool initialized = false;

        public BackpackGrid BackpackGrid => backpackGrid;

        /// <summary>一次性初始化底板与商店槽。由 WaveManager 调用。
        /// Canvas 在场景中默认 inactive，子对象（如 BackpackGrid）的 Awake 不会运行，
        /// 因此这里激活 Canvas 完成初始化。初始化后保持激活——紧接着 WaveManager 会
        /// 进入初始整备阶段并调用 Show()，无需还原为 inactive。</summary>
        public void Init()
        {
            if (initialized) return;
            initialized = true;

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            InitBackpackGrid();
            GenerateShop();
            SeedInitialShopItems();
        }

        /// <summary>显示整备界面并刷新金币显示。</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            UpdateCoinsDisplay();
        }

        /// <summary>隐藏整备界面（战斗阶段）。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 种子化初始背包：两个 2×3 格子并排放底板中间，
        /// 起始武器（读 CrossSceneData.CharacterBlueprint.startingAbilities[0]）放在第一个格子上。
        /// 由 WaveManager 在游戏开始时调用一次。
        /// </summary>
        public void SeedBackpack()
        {
            // 两个 2×3 格子并排放底板中间（cols 0-1 与 2-3, rows 2-4）
            SpawnGridTile(0, 2, 2, 3);
            SpawnGridTile(2, 2, 2, 3);

            // 起始武器：从 CharacterBlueprint 读 startingAbilities[0]
            CharacterBlueprint blueprint = CrossSceneData.CharacterBlueprint;
            if (blueprint != null && blueprint.startingAbilities != null && blueprint.startingAbilities.Length > 0)
            {
                GameObject abilityPrefab = blueprint.startingAbilities[0];
                SpawnWeaponItem(0, 2, "W1", 3, 1, weaponColor, abilityPrefab);
            }
        }

        private void UpdateCoinsDisplay()
        {
            if (coinsDisplay != null && statsManager != null)
            {
                coinsDisplay.text = statsManager.CoinsGained.ToString();
            }
        }

        // —— 初始化 ——
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
        /// 商店武器道具（MVP：拖拽免费，无购买逻辑）。
        /// 从 CharacterBlueprint.startingAbilities 读取武器 prefab：
        /// [0] 作为起始背包武器（见 SeedBackpack），[0]/[1] 同时上架商店供玩家拖入背包。
        /// 后续接入购买系统时替换为数据驱动的商品配置。
        /// </summary>
        private void SeedInitialShopItems()
        {
            CharacterBlueprint blueprint = CrossSceneData.CharacterBlueprint;
            if (blueprint == null || blueprint.startingAbilities == null) return;
            if (blueprint.startingAbilities.Length > 0)
                SpawnWeaponInShop(0, "菜刀", 3, 1, weaponColor, blueprint.startingAbilities[0]);
            if (blueprint.startingAbilities.Length > 1)
                SpawnWeaponInShop(1, "平底锅", 2, 1, weaponColor, blueprint.startingAbilities[1]);
        }

        // —— 背包内生成 ——
        private void SpawnGridTile(int col, int row, int w, int h)
        {
            GridTile tile = Instantiate(gridTilePrefab, backpackGridParent);
            tile.gameObject.SetActive(true);
            ConfigureGridTile(tile, w, h, gridTileColor);
            backpackGrid.PlaceGridTile(tile, col, row);
        }

        private void SpawnWeaponItem(int col, int row, string label, int w, int h, Color color, GameObject abilityPrefab)
        {
            WeaponItem item = Instantiate(weaponItemPrefab, backpackGridParent);
            item.gameObject.SetActive(true);
            ConfigureWeaponItem(item, label, w, h, color, abilityPrefab);
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
            item.ApplyFitSize(slot.GetComponent<ShopSlot>().GetFitSize());
        }

        private void SpawnWeaponInShop(int slotIndex, string label, int w, int h, Color color, GameObject abilityPrefab)
        {
            if (slotIndex < 0 || slotIndex >= shopGridParent.childCount) return;
            Transform slot = shopGridParent.GetChild(slotIndex);
            WeaponItem item = Instantiate(weaponItemPrefab, slot);
            item.gameObject.SetActive(true);
            ConfigureWeaponItem(item, label, w, h, color, abilityPrefab);
            item.ClearGridAssociation();
            RectTransform rt = item.GetComponent<RectTransform>();
            rt.anchoredPosition = Vector2.zero;
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
            tile.ApplyFitSize(slot.GetComponent<ShopSlot>().GetFitSize());
        }

        // —— 配置 ——
        private void ConfigureItem(DraggableItem item, string label, int w, int h, Color color)
        {
            Image bg = item.GetComponent<Image>();
            if (bg != null) bg.color = color;

            item.SetLabel(label);
            var txt = item.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = label;

            item.SetGridSize(w, h, backpackGrid.CellSize, backpackGrid.Spacing);
        }

        private void ConfigureWeaponItem(WeaponItem item, string label, int w, int h, Color color, GameObject abilityPrefab)
        {
            Image bg = item.GetComponent<Image>();
            if (bg != null) bg.color = color;

            item.SetLabel(label);
            item.SetAbilityPrefab(abilityPrefab);
            var txt = item.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = label;

            item.SetGridSize(w, h, backpackGrid.CellSize, backpackGrid.Spacing);
        }

        private void ConfigureGridTile(GridTile tile, int w, int h, Color color)
        {
            Image bg = tile.GetComponent<Image>();
            if (bg != null) bg.color = color;

            var txt = tile.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = w + "x" + h;

            tile.SetGridSize(w, h, backpackGrid.CellSize, backpackGrid.Spacing);
        }
    }
}
