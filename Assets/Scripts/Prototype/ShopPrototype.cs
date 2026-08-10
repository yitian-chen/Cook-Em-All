using UnityEngine;
using UnityEngine.UI;

namespace Vampire.Backpack
{
    /// <summary>
    /// 原型场景启动脚本。挂在 Canvas 上，Start() 里生成背包格、商店槽和占位物品。
    /// 不含任何游戏逻辑，仅用于验证背包+商店页面的布局与拖拽交互。
    /// </summary>
    public class ShopPrototype : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private BackpackSlot backpackSlotPrefab;
        [SerializeField] private ShopSlot shopSlotPrefab;
        [SerializeField] private DraggableItem itemPrefab;

        [Header("Container References")]
        [SerializeField] private Transform backpackGridParent;
        [SerializeField] private Transform shopGridParent;
        [SerializeField] private Transform stagingAreaParent;

        [Header("Backpack Settings")]
        [SerializeField] private int backpackColumns = 5;
        [SerializeField] private int backpackRows = 6;
        [Tooltip("前 N 个格子解锁，其余锁定（按行优先顺序）")]
        [SerializeField] private int unlockedSlotCount = 12;

        [Header("Shop Settings")]
        [SerializeField] private int shopSlotCount = 6;

        [Header("Placeholder Item Colors")]
        [SerializeField] private Color weaponColor = new Color(0.85f, 0.25f, 0.25f);
        [SerializeField] private Color seasoningColor = new Color(0.95f, 0.8f, 0.25f);
        [SerializeField] private Color gridExpansionColor = new Color(0.3f, 0.8f, 0.4f);

        private void Start()
        {
            GenerateBackpackGrid();
            GenerateShop();
            SeedInitialItems();
        }

        /// <summary>
        /// 生成背包网格：前 unlockedSlotCount 个解锁，其余锁定。
        /// </summary>
        private void GenerateBackpackGrid()
        {
            int total = backpackColumns * backpackRows;
            for (int i = 0; i < total; i++)
            {
                BackpackSlot slot = Instantiate(backpackSlotPrefab, backpackGridParent);
                slot.gameObject.SetActive(true);
                slot.SetUnlocked(i < unlockedSlotCount);
            }
        }

        /// <summary>
        /// 生成商店槽位（2×3，由 GridLayoutGroup 自动排布）。
        /// </summary>
        private void GenerateShop()
        {
            for (int i = 0; i < shopSlotCount; i++)
            {
                ShopSlot slot = Instantiate(shopSlotPrefab, shopGridParent);
                slot.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 放置初始占位物品：背包前 2 格各放一个，商店 6 个槽各放一个，暂存区放 1 个。
        /// </summary>
        private void SeedInitialItems()
        {
            // 背包前 2 格放武器
            SpawnItemIn(backpackGridParent.GetChild(0), "W1", weaponColor);
            SpawnItemIn(backpackGridParent.GetChild(1), "S1", seasoningColor);

            // 商店 6 个槽各放一个物品，类型循环
            for (int i = 0; i < shopSlotCount; i++)
            {
                Transform slot = shopGridParent.GetChild(i);
                Color c = i % 3 == 0 ? weaponColor : (i % 3 == 1 ? seasoningColor : gridExpansionColor);
                string label = i % 3 == 0 ? "W" : (i % 3 == 1 ? "S" : "G");
                SpawnItemIn(slot, label + (i + 1), c);
            }

            // 暂存区放一个物品在左上角附近
            DraggableItem stagingItem = Instantiate(itemPrefab, stagingAreaParent);
            stagingItem.gameObject.SetActive(true);
            ConfigureItem(stagingItem, "G2", gridExpansionColor);
            RectTransform stagingRect = stagingItem.GetComponent<RectTransform>();
            stagingRect.anchoredPosition = new Vector2(-200, 50);
        }

        private void SpawnItemIn(Transform parent, string label, Color color)
        {
            DraggableItem item = Instantiate(itemPrefab, parent);
            item.gameObject.SetActive(true);
            ConfigureItem(item, label, color);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchoredPosition = Vector2.zero;
        }

        private void ConfigureItem(DraggableItem item, string label, Color color)
        {
            // 设置物品背景颜色（Item prefab 上应有 Image 组件）
            Image bg = item.GetComponent<Image>();
            if (bg != null) bg.color = color;

            // 设置标签文本（Item prefab 上应有子物体 TMP Text）
            TMPro.TextMeshProUGUI txt = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null) txt.text = label;
        }
    }
}
