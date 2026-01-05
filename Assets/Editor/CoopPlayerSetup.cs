#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Vampire;
using Vampire.Gameplay;

/// <summary>
/// Editor utility to setup Co-op player prefabs with Character component
/// </summary>
public class CoopPlayerSetup : EditorWindow
{
    private GameObject playerPrefab;
    private Transform[] spawnPoints;

    [MenuItem("Vampire/Setup Co-op Players")]
    public static void ShowWindow()
    {
        GetWindow<CoopPlayerSetup>("Co-op Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("Co-op Player Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool helps you create a player prefab with Character component for co-op play.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Player Prefab with Character", GUILayout.Height(40)))
        {
            CreatePlayerPrefab();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Setup CoopPlayerManager in Scene", GUILayout.Height(40)))
        {
            SetupCoopPlayerManager();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Player Prefab", GUILayout.Height(40)))
        {
            ValidatePlayerPrefab();
        }
    }

    private void CreatePlayerPrefab()
    {
        // Create a new GameObject for the player
        GameObject playerGO = new GameObject("CoopPlayer");

        // Add Character component (or MainCharacter)
        var character = playerGO.AddComponent<MainCharacter>();

        // Add PlayerInput component for Input System
        var playerInput = playerGO.AddComponent<PlayerInput>();

        // Add Rigidbody2D
        var rb = playerGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.linearDamping = 5f; // Typical for top-down movement
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Add colliders
        var collectableCollider = playerGO.AddComponent<CircleCollider2D>();
        collectableCollider.radius = 0.5f;
        collectableCollider.isTrigger = true;

        var meleeHitboxCollider = playerGO.AddComponent<CircleCollider2D>();
        meleeHitboxCollider.radius = 0.3f;

        // Create visual hierarchy
        GameObject visualGO = new GameObject("Visual");
        visualGO.transform.SetParent(playerGO.transform);
        visualGO.transform.localPosition = Vector3.zero;

        var spriteRenderer = visualGO.AddComponent<SpriteRenderer>();
        var spriteAnimator = visualGO.AddComponent<SpriteAnimator>();

        // Create center transform
        GameObject centerGO = new GameObject("Center");
        centerGO.transform.SetParent(playerGO.transform);
        centerGO.transform.localPosition = Vector3.zero;

        // Create look indicator
        GameObject lookIndicatorGO = new GameObject("LookIndicator");
        lookIndicatorGO.transform.SetParent(playerGO.transform);
        lookIndicatorGO.transform.localPosition = Vector3.zero;
        var lookIndicatorSprite = lookIndicatorGO.AddComponent<SpriteRenderer>();
        lookIndicatorSprite.color = new Color(1, 1, 1, 0.5f);

        // Create UI Canvas for health/exp bars
        GameObject canvasGO = new GameObject("PlayerUI");
        canvasGO.transform.SetParent(playerGO.transform);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var canvasScaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var rectTransform = canvasGO.GetComponent<RectTransform>();
        rectTransform.localPosition = new Vector3(0, 1, 0);
        rectTransform.sizeDelta = new Vector2(200, 100);
        rectTransform.localScale = Vector3.one * 0.01f;

        // Try to find and configure assets
        ConfigureCharacterAssets(character, playerGO, spriteRenderer, centerGO.transform, lookIndicatorGO.transform);

        // Try to find Input Actions asset
        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            playerInput.actions = inputActions;
            Debug.Log($"✓ Assigned Input Actions: {inputActions.name}");
        }
        else
        {
            Debug.LogWarning("No InputActionAsset found. Please assign manually.");
        }

        // Create prefab
        string prefabPath = "Assets/Prefabs/CoopPlayer.prefab";

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Save as prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(playerGO, prefabPath);

        // Clean up
        DestroyImmediate(playerGO);

        // Select the prefab
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log($"✓ Created player prefab with Character component at: {prefabPath}");

        EditorUtility.DisplayDialog("Success",
            "Player prefab created with auto-configured assets!\n\n" +
            "✓ Sprites and animations\n" +
            "✓ Materials (default, hit, death)\n" +
            "✓ UI (health/exp bars, level text)\n" +
            "✓ Colliders\n\n" +
            "Note: Particle systems and AbilitySelectionDialog\n" +
            "need to be assigned from scene objects.",
            "OK");
    }

    private void ConfigureCharacterAssets(MainCharacter character, GameObject playerGO, SpriteRenderer spriteRenderer, Transform centerTransform, Transform lookIndicator)
    {
        // Use SerializedObject to assign protected fields
        SerializedObject so = new SerializedObject(character);

        // Find and assign sprites for animation
        var characterSprites = AssetDatabase.FindAssets("MainCharacter t:Texture2D");
        if (characterSprites.Length > 0)
        {
            string spritePath = AssetDatabase.GUIDToAssetPath(characterSprites[0]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
                Debug.Log($"✓ Assigned sprite: {sprite.name}");
            }
        }

        // Assign transforms
        so.FindProperty("centerTransform").objectReferenceValue = centerTransform;
        so.FindProperty("lookIndicator").objectReferenceValue = lookIndicator;
        so.FindProperty("lookIndicatorRadius").floatValue = 0.5f;

        // Find and assign materials
        var redMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Red Sprite.mat");
        var whiteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/White Sprite.mat");
        var deathMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player Death.mat");

        if (redMat != null)
        {
            so.FindProperty("hitMaterial").objectReferenceValue = redMat;
            Debug.Log("✓ Assigned hit material");
        }
        if (whiteMat != null)
        {
            so.FindProperty("defaultMaterial").objectReferenceValue = whiteMat;
            Debug.Log("✓ Assigned default material");
        }
        if (deathMat != null)
        {
            so.FindProperty("deathMaterial").objectReferenceValue = deathMat;
            Debug.Log("✓ Assigned death material");
        }

        // Assign colliders
        var colliders = playerGO.GetComponents<CircleCollider2D>();
        if (colliders.Length >= 2)
        {
            so.FindProperty("collectableCollider").objectReferenceValue = colliders[0];
            so.FindProperty("meleeHitboxCollider").objectReferenceValue = colliders[1];
            Debug.Log("✓ Assigned colliders");
        }

        // Create and assign UI elements (PointBars)
        var canvas = playerGO.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            // Create Health Bar
            GameObject healthBarGO = CreatePointBar(canvas.transform, "HealthBar", new Vector2(0, 30), new Color(0.8f, 0.2f, 0.2f));
            var healthBar = healthBarGO.GetComponent<PointBar>();
            so.FindProperty("healthBar").objectReferenceValue = healthBar;

            // Create Exp Bar
            GameObject expBarGO = CreatePointBar(canvas.transform, "ExpBar", new Vector2(0, 20), new Color(0.3f, 0.6f, 1f));
            var expBar = expBarGO.GetComponent<PointBar>();
            so.FindProperty("expBar").objectReferenceValue = expBar;

            // Create Level Text
            GameObject levelTextGO = new GameObject("LevelText");
            levelTextGO.transform.SetParent(canvas.transform);
            var levelText = levelTextGO.AddComponent<TMPro.TextMeshProUGUI>();
            levelText.text = "1";
            levelText.fontSize = 24;
            levelText.alignment = TMPro.TextAlignmentOptions.Center;
            levelText.color = Color.white;
            var levelRect = levelTextGO.GetComponent<RectTransform>();
            levelRect.anchoredPosition = new Vector2(0, 50);
            levelRect.sizeDelta = new Vector2(50, 30);
            so.FindProperty("levelText").objectReferenceValue = levelText;

            Debug.Log("✓ Created UI elements (health bar, exp bar, level text)");
        }

        so.ApplyModifiedProperties();
    }

    private GameObject CreatePointBar(Transform parent, string name, Vector2 position, Color fillColor)
    {
        GameObject barGO = new GameObject(name);
        barGO.transform.SetParent(parent);
        barGO.transform.localPosition = Vector3.zero;
        barGO.transform.localScale = Vector3.one;

        var pointBar = barGO.AddComponent<PointBar>();

        // Create background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(barGO.transform);
        var bgImage = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchoredPosition = position;
        bgRect.sizeDelta = new Vector2(100, 10);

        // Create fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(bgGO.transform);
        var fillImage = fillGO.AddComponent<UnityEngine.UI.Image>();
        fillImage.color = fillColor;
        var fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(100, 0);

        // Assign to PointBar via SerializedObject
        SerializedObject barSO = new SerializedObject(pointBar);
        barSO.FindProperty("barBackground").objectReferenceValue = bgRect;
        barSO.FindProperty("barFill").objectReferenceValue = fillRect;
        barSO.ApplyModifiedProperties();

        return barGO;
    }

    private void SetupCoopPlayerManager()
    {
        // Find or create CoopPlayerManager
        var existing = FindObjectOfType<CoopPlayerManager>();
        if (existing != null)
        {
            Debug.Log("CoopPlayerManager already exists in scene");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        GameObject managerGO = new GameObject("CoopPlayerManager");
        var coopManager = managerGO.AddComponent<CoopPlayerManager>();

        // PlayerInputManager is auto-added by [RequireComponent]
        var inputManager = managerGO.GetComponent<PlayerInputManager>();

        // Try to find the player prefab
        string[] prefabGuids = AssetDatabase.FindAssets("CoopPlayer t:Prefab");
        if (prefabGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            inputManager.playerPrefab = prefab;
            Debug.Log($"✓ Assigned player prefab: {prefab.name}");
        }
        else
        {
            Debug.LogWarning("CoopPlayer prefab not found. Please assign manually.");
        }

        // Configure PlayerInputManager
        inputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
        inputManager.notificationBehavior = PlayerNotifications.InvokeUnityEvents;

        // Create spawn points
        GameObject spawnPointsParent = new GameObject("SpawnPoints");
        spawnPointsParent.transform.SetParent(managerGO.transform);

        Transform[] points = new Transform[4];
        Vector3[] positions = new Vector3[]
        {
            new Vector3(0, 0, 0),
            new Vector3(2, 0, 0),
            new Vector3(-2, 0, 0),
            new Vector3(0, 2, 0)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject spawnPoint = new GameObject($"SpawnPoint_{i + 1}");
            spawnPoint.transform.SetParent(spawnPointsParent.transform);
            spawnPoint.transform.position = positions[i];
            points[i] = spawnPoint.transform;
        }

        Debug.Log("✓ Created CoopPlayerManager with spawn points");
        Selection.activeGameObject = managerGO;

        EditorUtility.DisplayDialog("Success",
            "CoopPlayerManager setup complete!\n\n" +
            "The manager is ready. Adjust spawn point positions as needed.",
            "OK");
    }

    private void ValidatePlayerPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int validCount = 0;
        int invalidCount = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null) continue;

            var playerInput = prefab.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                var character = prefab.GetComponent<Character>();
                if (character != null)
                {
                    Debug.Log($"✓ Valid player prefab: {prefab.name} (has Character component)");
                    validCount++;
                }
                else
                {
                    Debug.LogWarning($"✗ Invalid player prefab: {prefab.name} (missing Character component)");
                    invalidCount++;
                }
            }
        }

        string message;
        if (validCount > 0)
        {
            message = $"Found {validCount} valid player prefab(s) with Character component.";
            if (invalidCount > 0)
            {
                message += $"\n{invalidCount} prefab(s) with PlayerInput are missing Character component.";
            }
        }
        else if (invalidCount > 0)
        {
            message = $"Found {invalidCount} prefab(s) with PlayerInput but missing Character component.\n\n" +
                      "Use 'Create Player Prefab with Character' to fix this.";
        }
        else
        {
            message = "No player prefabs found.\n\nUse 'Create Player Prefab with Character' to create one.";
        }

        EditorUtility.DisplayDialog("Validation Results", message, "OK");
    }
}
#endif
