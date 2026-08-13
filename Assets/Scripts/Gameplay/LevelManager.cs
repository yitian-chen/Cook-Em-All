using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelBlueprint levelBlueprint;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private AbilityManager abilityManager;
        [SerializeField] private AbilitySelectionDialog abilitySelectionDialog;
        [SerializeField] private InfiniteBackground infiniteBackground;
        [SerializeField] private Inventory inventory;
        [SerializeField] private StatsManager statsManager;
        [SerializeField] private GameOverDialog gameOverDialog;
        [SerializeField] private GameTimer gameTimer;
        [SerializeField] private WaveManager waveManager;
        private float levelTime = 0;
        private float timeSinceLastMonsterSpawned;

        public void Init(LevelBlueprint levelBlueprint)
        {
            this.levelBlueprint = levelBlueprint;
            levelTime = 0;

            // Initialize the entity manager
            entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);
            // Initialize the ability manager (MVP: 起始武器由 WaveManager 通过背包重建)
            abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
            abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);
            // Initialize the character
            playerCharacter.Init(entityManager, abilityManager, statsManager);
            playerCharacter.OnDeath.AddListener(GameOver);
            // 经验钻石已移除（跑通流程期间）
            // entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount, this.levelBlueprint.initialExpGemType);
            // Initialize the infinite background
            infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);
            // Initialize inventory (legacy system, may be null — backpack grid supersedes it)
            if (inventory != null) inventory.Init();
            // Initialize wave manager (种子化背包、进入初始整备阶段、订阅击杀事件)
            if (waveManager != null)
            {
                waveManager.AllWavesCleared.AddListener(LevelPassed);
                waveManager.Init();
            }
        }

        // Start is called before the first update
        void Start()
        {
            Init(levelBlueprint);
        }

        // Update is called once per frame
        void Update()
        {
            // 仅在战斗阶段推进时间与刷怪；整备阶段 Time.timeScale=0 已冻结 Update 的有效推进
            if (waveManager == null || waveManager.Phase != WaveManager.WavePhase.Combat) return;

            // Time
            levelTime += Time.deltaTime;
            gameTimer.SetTime(levelTime);

            // Monster spawning timer（基于 levelTime 进度的曲线，沿用现有 LevelBlueprint 配置）
            if (levelTime < levelBlueprint.levelTime)
            {
                timeSinceLastMonsterSpawned += Time.deltaTime;
                float spawnRate = levelBlueprint.monsterSpawnTable.GetSpawnRate(levelTime/levelBlueprint.levelTime);
                float monsterSpawnDelay = spawnRate > 0 ? 1.0f/spawnRate : float.PositiveInfinity;
                if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
                {
                    (int monsterIndex, float hpMultiplier) = levelBlueprint.monsterSpawnTable.SelectMonsterWithHPMultiplier(levelTime/levelBlueprint.levelTime);
                    (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];
                    MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];
                    entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, monsterBlueprint.hp * hpMultiplier);
                    timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
                }
            }
        }

        public void GameOver()
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            gameOverDialog.Open(false, statsManager);
        }

        public void LevelPassed()
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            gameOverDialog.Open(true, statsManager);
        }

        public void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }
    }
}
