using UnityEngine;
using UnityEngine.Events;
using Vampire.Backpack;

namespace Vampire
{
    /// <summary>
    /// 波次状态机。协调战斗阶段与整备阶段的切换。
    /// 流程：
    ///   Init() → 订阅击杀事件 → SeedBackpack → 进入初始 Prep（玩家先看背包）→ 等待玩家点"开始波次"
    ///   玩家点按钮 → StartNextWave() → 满血 + RebuildFromBackpack + 进入 Combat
    ///   杀够 killsRequiredPerWave 只怪 → StartPrepPhase() → 冻结时间 + 清场 + 销毁 Abilities + 显示背包
    ///   玩家点按钮 → 若 currentWave &gt; totalWaves 触发 AllWavesCleared；否则进入下一波
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public enum WavePhase { Combat, Prep }

        [Header("Wave Config (MVP hardcoded)")]
        [SerializeField] private int totalWaves = 2;
        [SerializeField] private int killsRequiredPerWave = 10;

        [Header("Dependencies")]
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private PreparationController preparationController;

        public WavePhase Phase { get; private set; } = WavePhase.Prep;
        public int CurrentWave { get; private set; } = 1;
        public int KillsThisWave { get; private set; } = 0;
        public int KillsRequiredPerWave => killsRequiredPerWave;
        public int TotalWaves => totalWaves;

        /// <summary>所有波次完成时触发（LevelManager 监听调用 LevelPassed）。</summary>
        public UnityEvent AllWavesCleared = new UnityEvent();

        private bool backpackSeeded = false;

        /// <summary>由 LevelManager.Init 调用。注入依赖、订阅事件、进入初始整备阶段。</summary>
        public void Init()
        {
            if (entityManager != null)
            {
                entityManager.OnMonsterKilledByPlayer.AddListener(OnMonsterKilled);
            }

            // 一次性初始化整备界面（创建底板与商店槽）
            if (preparationController != null)
            {
                preparationController.Init();
            }

            // 种子化初始背包
            EnsureBackpackSeeded();

            // 进入初始整备阶段：玩家先看到背包，点"开始波次 1"才开打
            EnterPrepPhase(initial: true);
        }

        private void OnMonsterKilled(Monster monster)
        {
            if (Phase != WavePhase.Combat) return;  // 防御：prep 期间残留事件不计
            KillsThisWave++;
            if (KillsThisWave >= killsRequiredPerWave)
            {
                EnterPrepPhase(initial: false);
            }
        }

        /// <summary>进入整备阶段：冻结时间、清场、销毁 Abilities、显示背包。</summary>
        private void EnterPrepPhase(bool initial)
        {
            Phase = WavePhase.Prep;
            Time.timeScale = 0;

            if (!initial)
            {
                // 非初始整备：清场残留怪物、销毁当前波次的 Abilities、推进波次计数
                if (entityManager != null) entityManager.KillAllMonsters();
                if (abilityManager != null) abilityManager.DestroyActiveAbilities();
                CurrentWave++;
            }

            if (preparationController != null) preparationController.Show();
        }

        /// <summary>开始下一波（绑到"开始/下一波"按钮）。</summary>
        public void StartNextWave()
        {
            if (preparationController != null) preparationController.Hide();

            // 所有波次完成 → 触发通关
            if (CurrentWave > totalWaves)
            {
                AllWavesCleared.Invoke();
                return;
            }

            // 满血回复
            if (playerCharacter != null && playerCharacter.Blueprint != null)
            {
                playerCharacter.GainHealth(playerCharacter.Blueprint.hp);
            }

            // 从背包重建 Abilities
            BackpackGrid backpackGrid = preparationController != null ? preparationController.BackpackGrid : null;
            if (abilityManager != null && backpackGrid != null)
            {
                abilityManager.RebuildFromBackpack(backpackGrid);
            }

            KillsThisWave = 0;
            Time.timeScale = 1;
            Phase = WavePhase.Combat;
        }

        private void EnsureBackpackSeeded()
        {
            if (backpackSeeded) return;
            if (preparationController != null) preparationController.SeedBackpack();
            backpackSeeded = true;
        }
    }
}
