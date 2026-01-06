using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private LevelBlueprint levelBlueprint;
        [SerializeField] private Character playerCharacter;
        [SerializeField] private Character playerCharacter2; // Player 2 for local co-op
        [SerializeField] private EntityManager entityManager;
        [SerializeField] private AbilityManager abilityManager;
        private AbilityManager abilityManagerP2; // runtime-created for Player 2
        [SerializeField] private AbilitySelectionDialog abilitySelectionDialog;
        [SerializeField] private InfiniteBackground infiniteBackground;
        [SerializeField] private Inventory inventory;
        [SerializeField] private StatsManager statsManager;
        [SerializeField] private GameOverDialog gameOverDialog;
        [SerializeField] private GameTimer gameTimer;
        private float levelTime = 0;
        private float timeSinceLastMonsterSpawned;
        private float timeSinceLastChestSpawned;
        private bool miniBossSpawned = false;
        private bool finalBossSpawned = false;

        public void Init(LevelBlueprint levelBlueprint)
        {
            this.levelBlueprint = levelBlueprint;
            levelTime = 0;

            // Initialize the entity manager
            entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);
            // Initialize the ability manager
            abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
            abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);
            abilitySelectionDialog.OnAbilitySelected += MirrorAbilityToPlayer2;
            // Initialize the character
            playerCharacter.Init(entityManager, abilityManager, statsManager);
            playerCharacter.OnDeath.AddListener(GameOver);

            // Initialize Player 2 if present (local co-op)
            if (playerCharacter2 == null)
            {
                // Attempt to auto-discover a second Character in the scene
                var characters = FindObjectsOfType<Character>(includeInactive: true);
                foreach (var c in characters)
                {
                    if (c != null && c != playerCharacter)
                    {
                        playerCharacter2 = c;
                        Debug.Log("[LevelManager] Auto-assigned Player 2: " + playerCharacter2.gameObject.name);
                        break;
                    }
                }
            }

            if (playerCharacter2 != null)
            {
                // Create a dedicated AbilityManager for Player 2 so abilities spawn/upgrade independently
                if (abilityManagerP2 == null)
                {
                    var go = new GameObject("AbilityManager_P2");
                    go.transform.SetParent(transform);
                    abilityManagerP2 = go.AddComponent<AbilityManager>();
                }

                abilityManagerP2.Init(this.levelBlueprint, entityManager, playerCharacter2, abilityManagerP2);
                playerCharacter2.Init(entityManager, abilityManager, statsManager);
                playerCharacter2.OnDeath.AddListener(GameOver);
                Debug.Log("[LevelManager] Player 2 initialized");
            }

            // Ensure both players have world-space health bars; clone from the other if one is missing
            if (playerCharacter != null && playerCharacter2 != null)
            {
                if (!playerCharacter.HasHealthBar && playerCharacter2.HasHealthBar)
                {
                    CloneHealthBar(playerCharacter2, playerCharacter, "[LevelManager] Cloned Player 2 health bar onto Player 1");
                }
                if (!playerCharacter2.HasHealthBar && playerCharacter.HasHealthBar)
                {
                    CloneHealthBar(playerCharacter, playerCharacter2, "[LevelManager] Cloned Player 1 health bar onto Player 2");
                }
                if (!playerCharacter.HasHealthBar && !playerCharacter2.HasHealthBar)
                {
                    Debug.LogWarning("[LevelManager] Both players missing health bars; no source to clone.");
                }
            }
            else if (playerCharacter != null && !playerCharacter.HasHealthBar)
            {
                Debug.LogWarning("[LevelManager] Player 1 missing health bar and Player 2 not available to clone.");
            }
            else if (playerCharacter2 != null && !playerCharacter2.HasHealthBar)
            {
                Debug.LogWarning("[LevelManager] Player 2 missing health bar and Player 1 not available to clone.");
            }

            // Spawn initial gems
            entityManager.SpawnGemsAroundPlayer(this.levelBlueprint.initialExpGemCount, this.levelBlueprint.initialExpGemType);
            // Spawn a singular chest
            entityManager.SpawnChest(levelBlueprint.chestBlueprint);
            // Initialize the infinite background
            infiniteBackground.Init(this.levelBlueprint.backgroundTexture, playerCharacter.transform);
            // Initialize inventory
            inventory.Init();
        }

        // Start is called before the first frame update
        void Start()
        {
            Init(levelBlueprint);
        }

        // Update is called once per frame
        void Update()
        {
            // Time
            levelTime += Time.deltaTime;
            gameTimer.SetTime(levelTime);
            // Monster spawning timer
            if (levelTime < levelBlueprint.levelTime)
            {
                timeSinceLastMonsterSpawned += Time.deltaTime;
                float spawnRate = levelBlueprint.monsterSpawnTable.GetSpawnRate(levelTime / levelBlueprint.levelTime);
                float monsterSpawnDelay = spawnRate > 0 ? 1.0f / spawnRate : float.PositiveInfinity;
                if (timeSinceLastMonsterSpawned >= monsterSpawnDelay)
                {
                    (int monsterIndex, float hpMultiplier) = levelBlueprint.monsterSpawnTable.SelectMonsterWithHPMultiplier(levelTime / levelBlueprint.levelTime);
                    (int poolIndex, int blueprintIndex) = levelBlueprint.MonsterIndexMap[monsterIndex];
                    MonsterBlueprint monsterBlueprint = levelBlueprint.monsters[poolIndex].monsterBlueprints[blueprintIndex];
                    entityManager.SpawnMonsterRandomPosition(poolIndex, monsterBlueprint, monsterBlueprint.hp * hpMultiplier);
                    timeSinceLastMonsterSpawned = Mathf.Repeat(timeSinceLastMonsterSpawned, monsterSpawnDelay);
                }
            }
            // Boss spawning
            if (!miniBossSpawned && levelTime > levelBlueprint.miniBosses[0].spawnTime)
            {
                miniBossSpawned = true;
                entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.miniBosses[0].bossBlueprint);
            }
            // Boss spawning
            if (!finalBossSpawned && levelTime > levelBlueprint.levelTime)
            {
                //entityManager.KillAllMonsters();
                finalBossSpawned = true;
                Monster finalBoss = entityManager.SpawnMonsterRandomPosition(levelBlueprint.monsters.Length, levelBlueprint.finalBoss.bossBlueprint);
                finalBoss.OnKilled.AddListener(LevelPassed);
            }
            // Chest spawning timer
            timeSinceLastChestSpawned += Time.deltaTime;
            if (timeSinceLastChestSpawned >= levelBlueprint.chestSpawnDelay)
            {
                for (int i = 0; i < levelBlueprint.chestSpawnAmount; i++)
                {
                    entityManager.SpawnChest(levelBlueprint.chestBlueprint);
                }
                timeSinceLastChestSpawned = Mathf.Repeat(timeSinceLastChestSpawned, levelBlueprint.chestSpawnDelay);
            }
        }

        public void GameOver()
        {
            Time.timeScale = 0;
            int coinCount = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("Coins", coinCount + statsManager.CoinsGained);
            gameOverDialog.Open(false, statsManager);
        }

        public void LevelPassed(Monster finalBossKilled)
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

        private void MirrorAbilityToPlayer2(Ability ability)
        {
            if (abilityManagerP2 == null || ability == null)
                return;

            bool ok = abilityManagerP2.TrySelectAbilityByName(ability.Name);
            if (!ok)
            {
                Debug.LogWarning($"[LevelManager] Could not mirror ability '{ability.Name}' to Player 2");
            }
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }

        private void CloneHealthBar(Character source, Character target, string logMessage)
        {
            var sourceBar = source.HealthBar;
            if (sourceBar == null)
                return;

            var clonedBar = Instantiate(sourceBar, target.transform);
            clonedBar.transform.localPosition = sourceBar.transform.localPosition;
            clonedBar.transform.localRotation = sourceBar.transform.localRotation;
            clonedBar.transform.localScale = sourceBar.transform.localScale;
            target.SetHealthBar(clonedBar);
            Debug.Log(logMessage);
        }
    }
}
