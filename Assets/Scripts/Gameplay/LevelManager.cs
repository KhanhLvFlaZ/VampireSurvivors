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

            // If Player 2 not assigned, try auto-discover early (needed for health bar clone)
            if (playerCharacter2 == null)
            {
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

            // Ensure both players have visible, child health bars before they Init()
            if (playerCharacter != null && playerCharacter2 != null)
            {
                bool p1Has = playerCharacter.HasHealthBar;
                bool p2Has = playerCharacter2.HasHealthBar;
                bool p1Visible = p1Has && playerCharacter.HealthBar.gameObject.activeInHierarchy;
                bool p2Visible = p2Has && playerCharacter2.HealthBar.gameObject.activeInHierarchy;
                bool p1Attached = p1Has && playerCharacter.HealthBar.transform.IsChildOf(playerCharacter.transform);
                bool p2Attached = p2Has && playerCharacter2.HealthBar.transform.IsChildOf(playerCharacter2.transform);

                Debug.Log($"[LevelManager] HealthBar status before init => P1:has={p1Has}, visible={p1Visible}, attached={p1Attached}; P2:has={p2Has}, visible={p2Visible}, attached={p2Attached}");

                bool p1Needs = !p1Has || !p1Visible || !p1Attached;
                bool p2Needs = !p2Has || !p2Visible || !p2Attached;

                // If bar exists but is not attached, attach a cloned copy to self to ensure positioning follows the player
                if (p1Has && !p1Attached)
                {
                    AttachHealthBarToSelf(playerCharacter, "[LevelManager] Attached (self-clone) health bar onto Player 1");
                    // recompute flags
                    p1Has = playerCharacter.HasHealthBar;
                    p1Visible = p1Has && playerCharacter.HealthBar.gameObject.activeInHierarchy;
                    p1Attached = p1Has && playerCharacter.HealthBar.transform.IsChildOf(playerCharacter.transform);
                    p1Needs = !p1Has || !p1Visible || !p1Attached;
                }

                if (p2Has && !p2Attached)
                {
                    AttachHealthBarToSelf(playerCharacter2, "[LevelManager] Attached (self-clone) health bar onto Player 2");
                    p2Has = playerCharacter2.HasHealthBar;
                    p2Visible = p2Has && playerCharacter2.HealthBar.gameObject.activeInHierarchy;
                    p2Attached = p2Has && playerCharacter2.HealthBar.transform.IsChildOf(playerCharacter2.transform);
                    p2Needs = !p2Has || !p2Visible || !p2Attached;
                }

                if (p1Needs && !p2Needs && playerCharacter2.HealthBar != null)
                {
                    CloneHealthBar(playerCharacter2, playerCharacter, "[LevelManager] Cloned Player 2 health bar onto Player 1");
                }
                else if (p2Needs && !p1Needs && playerCharacter.HealthBar != null)
                {
                    CloneHealthBar(playerCharacter, playerCharacter2, "[LevelManager] Cloned Player 1 health bar onto Player 2");
                }
                else if (p1Needs && p2Needs)
                {
                    Debug.LogWarning("[LevelManager] Both players missing usable health bars; no source to clone.");
                }
            }
            else if (playerCharacter != null && (!playerCharacter.HasHealthBar || !playerCharacter.HealthBar.gameObject.activeInHierarchy))
            {
                Debug.LogWarning("[LevelManager] Player 1 missing/hidden health bar and Player 2 not available to clone (pre-init).");
            }
            else if (playerCharacter2 != null && (!playerCharacter2.HasHealthBar || !playerCharacter2.HealthBar.gameObject.activeInHierarchy))
            {
                Debug.LogWarning("[LevelManager] Player 2 missing/hidden health bar and Player 1 not available to clone (pre-init).");
            }

            // Initialize the entity manager
            entityManager.Init(this.levelBlueprint, playerCharacter, inventory, statsManager, infiniteBackground, abilitySelectionDialog);
            // Initialize the ability manager
            abilityManager.Init(this.levelBlueprint, entityManager, playerCharacter, abilityManager);
            abilitySelectionDialog.Init(abilityManager, entityManager, playerCharacter);
            abilitySelectionDialog.OnAbilitySelected += MirrorAbilityToPlayer2;
            // Initialize the character
            playerCharacter.Init(entityManager, abilityManager, statsManager);
            playerCharacter.OnDeath.AddListener(GameOver);

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

            if (target.HealthBar != null && target.HealthBar != sourceBar)
            {
                Destroy(target.HealthBar.gameObject);
            }

            var clonedBar = Instantiate(sourceBar, target.transform);
            ApplyBarTransformDefaults(clonedBar, sourceBar);
            ConfigureHealthBarCanvas(clonedBar);
            target.SetHealthBar(clonedBar);
            Debug.Log(logMessage);
        }

        private void AttachHealthBarToSelf(Character target, string logMessage)
        {
            var sourceBar = target.HealthBar;
            if (sourceBar == null)
                return;

            if (target.HealthBar != null)
            {
                Destroy(target.HealthBar.gameObject);
            }

            var clonedBar = Instantiate(sourceBar, target.transform);
            ApplyBarTransformDefaults(clonedBar, sourceBar);
            clonedBar.gameObject.SetActive(true);
            ConfigureHealthBarCanvas(clonedBar);
            target.SetHealthBar(clonedBar);
            Debug.Log(logMessage);
        }

        private void ApplyBarTransformDefaults(PointBar bar, PointBar source)
        {
            // Force sane local transform relative to player
            var t = bar.transform;
            t.localPosition = Vector3.zero; // reset; will be nudged in ConfigureHealthBarCanvas
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            if (source != null)
                bar.gameObject.layer = source.gameObject.layer;
        }

        private void ConfigureHealthBarCanvas(PointBar bar)
        {
            var canvas = bar.GetComponentInChildren<Canvas>();
            if (canvas == null)
                canvas = bar.gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200; // force above sprites and map
            canvas.sortingLayerName = "Default"; // keep default sorting layer for compatibility
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null)
                scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.scaleFactor = 1f;
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
                canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // If bar has a RectTransform root, ensure reasonable size (world units)
            var rt = bar.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (rt.sizeDelta == Vector2.zero || rt.sizeDelta.x > 5f || rt.sizeDelta.y > 5f)
                    rt.sizeDelta = new Vector2(1.2f, 0.24f);
                rt.localPosition = Vector3.zero;
                rt.localScale = Vector3.one;
            }
            var canvasRt = canvas.transform as RectTransform;
            if (canvasRt != null)
            {
                // Compact rectangular health bar
                canvasRt.sizeDelta = new Vector2(0.4f, 0.18f);
                canvasRt.localPosition = new Vector3(-0.4f, 0.6f, 0f); // shifted left
                canvasRt.localRotation = Quaternion.identity;
                canvasRt.localScale = Vector3.one;
            }

            // Ensure bar children are active
            bar.gameObject.SetActive(true);
            foreach (var g in bar.GetComponentsInChildren<Transform>(true))
            {
                g.gameObject.SetActive(true);
                // ensure children use the same layer as bar (expected UI)
                g.gameObject.layer = bar.gameObject.layer;
            }

            // Force visible size and colors in case prefab data is off
            if (rt != null && (rt.sizeDelta.x == 0 || rt.sizeDelta.y == 0 || rt.sizeDelta.x > 5f || rt.sizeDelta.y > 5f))
                rt.sizeDelta = new Vector2(0.4f, 0.18f);

            var images = bar.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                if (img.rectTransform.sizeDelta == Vector2.zero || img.rectTransform.sizeDelta.x > 5f || img.rectTransform.sizeDelta.y > 5f)
                    img.rectTransform.sizeDelta = new Vector2(0.36f, 0.14f);
                img.rectTransform.localPosition = Vector3.zero;
                img.rectTransform.localRotation = Quaternion.identity;
                var c = img.color;
                if (c.a < 0.9f) c.a = 1f;
                if (c.r == 0 && c.g == 0 && c.b == 0) c = Color.white;
                img.color = c;
            }

            // If PointBar has explicit background/fill RectTransforms, tighten their sizes
            var pb = bar.GetComponent<PointBar>();
            if (pb != null)
            {
                var bg = pb.GetType().GetField("barBackground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(pb) as RectTransform;
                var fill = pb.GetType().GetField("barFill", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(pb) as RectTransform;
                if (bg != null)
                {
                    bg.sizeDelta = new Vector2(0.4f, 0.15f);
                    bg.localPosition = Vector3.zero;
                }
                if (fill != null)
                {
                    fill.sizeDelta = new Vector2(0.36f, 0.11f);
                    fill.localPosition = Vector3.zero;
                }
            }

            // Prevent culling if materials are partially transparent
            var cr = bar.GetComponent<CanvasRenderer>();
            if (cr != null)
                cr.cullTransparentMesh = false;
        }
    }
}
