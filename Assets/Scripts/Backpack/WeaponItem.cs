using UnityEngine;

namespace Vampire.Backpack
{
    /// <summary>
    /// 武器物品。继承 DraggableItem 的拖拽/放置逻辑，
    /// 额外持有该武器对应的 Ability prefab 引用，供 AbilityManager.RebuildFromBackpack 实例化。
    /// 调味料等其他道具继续使用 DraggableItem 基类（不带 abilityPrefab）。
    /// </summary>
    public class WeaponItem : DraggableItem
    {
        [Tooltip("该武器对应的 Ability prefab（挂 Ability 子类的 GameObject）")]
        [SerializeField] private GameObject abilityPrefab;

        public GameObject AbilityPrefab => abilityPrefab;

        public void SetAbilityPrefab(GameObject prefab)
        {
            abilityPrefab = prefab;
        }
    }
}
