using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ChessGameManager : MonoBehaviour
{
    public static ChessGameManager Instance { get; private set; }

    private const float BalancedStartingMoney = 70f;
    private const float BalancedMaxMoney = 350f;
    private const float BalancedMoneyPerSecond = 5.5f;
    private const float BalancedEnemySpawnInterval = 0.78f;
    private const float BalancedMinimumEnemySpawnInterval = 0.48f;
    private const float BalancedEnemyStartingMoney = 15f;
    private const float EnemyInitialSpawnDelay = 4f;
    private const float EnemyEarlyIncomeMultiplier = 0.78f;
    private const float PresentationLaneY = -2.10f;
    private const float PresentationBaseY = -1.35f;
    private const float PresentationCameraY = -0.25f;

    private struct SpawnDefinition
    {
        public readonly string label;
        public readonly float cost;
        public readonly KeyCode hotkey;
        public readonly ChessPieceKind kind;
        public readonly int unlockOutpost;

        public SpawnDefinition(string label, float cost, KeyCode hotkey, ChessPieceKind kind)
        {
            this.label = label;
            this.cost = cost;
            this.hotkey = hotkey;
            this.kind = kind;
            unlockOutpost = ChessBalance.GetUnlockOutpost(kind);
        }
    }

    private static readonly SpawnDefinition[] SpawnDefinitions =
    {
        new SpawnDefinition("Pawn", ChessBalance.PawnCost, KeyCode.Alpha1, ChessPieceKind.Pawn),
        new SpawnDefinition("Rook", ChessBalance.RookCost, KeyCode.Alpha2, ChessPieceKind.Rook),
        new SpawnDefinition("Knight", ChessBalance.KnightCost, KeyCode.Alpha3, ChessPieceKind.Knight),
        new SpawnDefinition("Bishop", ChessBalance.BishopCost, KeyCode.Alpha4, ChessPieceKind.Bishop),
        new SpawnDefinition("Queen", ChessBalance.QueenCost, KeyCode.Alpha5, ChessPieceKind.Queen),
        new SpawnDefinition("King", ChessBalance.KingCost, KeyCode.Alpha6, ChessPieceKind.King)
    };

    [Header("Mode")]
    public GameMode currentMode = GameMode.Endless;
    public GameState currentState = GameState.Menu;

    [Header("Money")]
    public float money = BalancedStartingMoney;
    public float maxMoney = BalancedMaxMoney;
    public float moneyPerSecond = BalancedMoneyPerSecond;

    [Header("Bases")]
    public ChessBase playerBase;
    public ChessBase enemyBase;
    public GameObject enemyBasePrefab;
    public float sharedBaseHp = ChessBalance.SharedInitialBaseHp;
    [HideInInspector] public float playerBaseHp = ChessBalance.SharedInitialBaseHp;

    [Header("Endless")]
    public int endlessScore = 0;
    public int endlessDifficulty = 0;
    public float endlessBaseSpacing = 10f;
    [HideInInspector] public float endlessStartEnemyBaseHp = ChessBalance.SharedInitialBaseHp;
    public float endlessHpPerBase = ChessBalance.EndlessBaseHpGrowth;
    [Range(0.05f, 1f)]
    public float capturedBaseHealthRatio = ChessBalance.CapturedBaseHpRatio;
    public float playerBaseOffset = 1.5f;
    public float enemyBaseOffset = 1.5f;

    [Header("Spawn Points")]
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;
    public float laneY = PresentationLaneY;
    public float baseY = PresentationBaseY;

    [Header("Player Unit Prefabs")]
    public GameObject playerPawnPrefab;
    public GameObject playerRookPrefab;
    public GameObject playerKnightPrefab;
    public GameObject playerBishopPrefab;
    public GameObject playerQueenPrefab;
    public GameObject playerKingPrefab;

    [Header("Enemy Unit Prefabs")]
    public GameObject enemyPawnPrefab;
    public GameObject enemyRookPrefab;
    public GameObject enemyKnightPrefab;
    public GameObject enemyBishopPrefab;
    public GameObject enemyQueenPrefab;
    public GameObject enemyKingPrefab;

    [Header("Enemy Spawning")]
    public float enemySpawnInterval = BalancedEnemySpawnInterval;
    public float minimumEnemySpawnInterval = BalancedMinimumEnemySpawnInterval;

    [Header("Player Spawning")]
    [Min(0f)] public float playerSpawnCooldown = 0.45f;

    [Header("Camera")]
    public Camera mainCamera;
    public float cameraFollowSpeed = 4.5f;
    public float cameraTargetY = PresentationCameraY;
    public float cameraTargetZ = -10f;
    public float cameraHorizontalPadding = 2.5f;
    public float minCameraSize = 5f;
    private float cameraTargetX;
    private float cameraTargetSize = 5f;
    private bool enemyBaseTemplateCaptured;
    private Vector3 enemyBaseTemplateScale = Vector3.one * 3f;
    private Quaternion enemyBaseTemplateRotation = Quaternion.identity;
    private Sprite enemyBaseTemplateSprite;
    private Color enemyBaseTemplateColor = Color.white;
    private int enemyBaseTemplateSortingLayerId;
    private int enemyBaseTemplateSortingOrder;
    private bool enemyBaseTemplateFlipX;
    private bool enemyBaseTemplateFlipY;
    private Sprite enemyBaseTemplatePlayerSprite;
    private Sprite enemyBaseTemplateEnemySprite;

    [Header("Background")]
    public string backgroundResourceName = "BattleBackground";
    public Vector2 backgroundWorldOffset = new Vector2(0f, 0f);
    public float backgroundDepth = 8f;
    public int backgroundSortingOrder = -100;
    private SpriteRenderer battleBackgroundRenderer;

    [Header("UI")]
    public GameObject cardsPanel;
    public GameObject startMenuPanel;
    public TMP_Text startTitleText;
    public TMP_Text startSubtitleText;
    public Button startEndlessButton;
    public TMP_Text modeText;
    public TMP_Text scoreText;
    public TMP_Text moneyText;
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button restartButton;

    [Header("UI Editing")]
    public bool useSceneAuthoredUi = true;
    public bool autoStyleRuntimeUi = false;
    public bool compactSceneCardPanel = false;
    public Vector2 sceneCardsPanelSize = new Vector2(1320f, 136f);
    public Vector2 sceneCardsPanelPosition = new Vector2(0f, 16f);
    public Vector2 sceneCardSize = new Vector2(190f, 112f);
    public float sceneCardSpacing = 22f;
    public Vector2 hudOffsetMin = new Vector2(34f, -144f);
    public Vector2 hudOffsetMax = new Vector2(-34f, -22f);
    public float hudFontSize = 45f;
    public Vector2 cardsPanelSize = new Vector2(1780f, 272f);
    public Vector2 cardsPanelPosition = new Vector2(0f, 18f);
    public Vector2 cardSize = new Vector2(270f, 190f);
    public Vector2 cardIconSize = new Vector2(118f, 118f);
    public float cardNameFontSize = 34f;
    public float cardCostFontSize = 32f;
    public float cardHotkeyFontSize = 28f;
    public float startTitleFontSize = 124f;
    public float startSubtitleFontSize = 44f;
    public Vector2 startButtonSize = new Vector2(760f, 150f);
    public float resultFontSize = 108f;

    [Header("Browser / WebGL Adaptation")]
    public bool enableResponsiveBrowserLayout = true;
    public Vector2 browserReferenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float wideScreenUiMatch = 1f;
    [Range(0f, 1f)] public float narrowScreenUiMatch = 0f;
    [Range(0f, 1f)] public float balancedScreenUiMatch = 0.5f;
    public float browserMinAspect = 1.25f;
    public float browserMaxAspect = 2.35f;
    public float narrowScreenExtraCameraPadding = 0.65f;

    private float moneyCarry;
    [SerializeField] private float enemyMoney = BalancedStartingMoney;
    private float enemyMoneyCarry;
    private float enemySpawnTimer;
    private float playerSpawnCooldownTimer;
    private float initialBaseSpacing;
    private int lastDisplayedMoney = -1;
    private int lastDisplayedScore = -1;
    private float nextSpawnCardRefreshTime;
    private const float SpawnCardRefreshInterval = 0.08f;
    private readonly Dictionary<string, SpawnCardUiCache> spawnCardUiCache = new Dictionary<string, SpawnCardUiCache>();
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float lastScreenAspect = -1f;

    private class SpawnCardUiCache
    {
        public GameObject card;
        public Button button;
        public TMP_Text nameText;
        public TMP_Text costText;
        public Image iconImage;
        public int lastCost = int.MinValue;
        public bool lastAffordable;
        public bool initialized;
    }

    public bool IsPlaying
    {
        get
        {
            return currentState == GameState.Playing;
        }
    }

    public float LaneY
    {
        get { return laneY; }
    }

    public ChessBase GetFrontlineBaseForTeam(Team requesterTeam)
    {
        return requesterTeam == Team.Player ? enemyBase : playerBase;
    }

    public int BattlefieldRevision { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureEnemyBaseTemplateIfNeeded();
    }

    private void OnValidate()
    {
        NormalizeBalanceSettings();
        ResolveReferences();
    }

    private void Start()
    {
        NormalizeBalanceSettings();
        ResolveReferences();
        CaptureEnemyBaseTemplateIfNeeded();
        EnsureSceneSetup();
        EnsureBattleBackground();
        ResolveReferences();
        ApplyBrowserResponsiveLayout(true);
        BindRuntimeButtons();
        CompactSceneAuthoredCardPanel();
        StyleRuntimeUi();
        RefreshSpawnCardVisuals(true);
        ShowStartMenu();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateBattleBackground();
        ApplyBrowserResponsiveLayout(false);

        if (!IsPlaying)
        {
            StopAllUnits();
            UpdateUI();
            return;
        }

        AccumulateMoney(Time.deltaTime);
        playerSpawnCooldownTimer = Mathf.Max(0f, playerSpawnCooldownTimer - Time.deltaTime);

        enemySpawnTimer -= Time.deltaTime;
        if (enemySpawnTimer <= 0f)
        {
            enemySpawnTimer = GetCurrentEnemySpawnInterval();
            TrySpawnEnemyFromBudget();
        }

        UpdateCameraFollow(Time.deltaTime);

        UpdateUI();
    }

    public void StartGame()
    {
        ResolveReferences();

        currentMode = GameMode.Endless;
        currentState = GameState.Playing;
        SetGameplayUiVisible(true);
        HideStartMenu();
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        money = BalancedStartingMoney;
        moneyCarry = 0f;
        enemyMoney = BalancedEnemyStartingMoney;
        enemyMoneyCarry = 0f;
        enemySpawnTimer = EnemyInitialSpawnDelay;
        playerSpawnCooldownTimer = 0f;

        SetupEndlessMode();
        RefreshCameraTarget();
        SnapCameraToFocus();
        UpdateBattleBackground();

        UpdateSpawnPointsForCurrentBases();
        RefreshSpawnCardVisuals(true);
        UpdateUI();
    }

    public void StartEndlessMode()
    {
        StartGame();
    }

    public void ShowStartMenu()
    {
        ResolveReferences();

        currentState = GameState.Menu;
        SetGameplayUiVisible(false);

        if (startMenuPanel != null)
        {
            startMenuPanel.SetActive(true);
            startMenuPanel.transform.SetAsLastSibling();
        }

        UpdateUI();
    }

    private void NormalizeBalanceSettings()
    {
        if (sharedBaseHp < 10f)
        {
            sharedBaseHp = ChessBalance.SharedInitialBaseHp;
        }

        playerBaseHp = sharedBaseHp;
        endlessStartEnemyBaseHp = sharedBaseHp;
        endlessHpPerBase = Mathf.Max(0f, endlessHpPerBase);
        capturedBaseHealthRatio = Mathf.Clamp(capturedBaseHealthRatio, 0.05f, 1f);
        maxMoney = BalancedMaxMoney;
        moneyPerSecond = BalancedMoneyPerSecond;
        enemySpawnInterval = BalancedEnemySpawnInterval;
        minimumEnemySpawnInterval = BalancedMinimumEnemySpawnInterval;
        laneY = PresentationLaneY;
        baseY = PresentationBaseY;
        cameraTargetY = PresentationCameraY;
    }

    private void SetupEndlessMode()
    {
        NormalizeBalanceSettings();
        CaptureEnemyBaseTemplateIfNeeded();
        endlessScore = 0;
        endlessDifficulty = 0;
        BattlefieldRevision += 1;

        if (playerBase != null)
        {
            playerBase.SetTeam(Team.Player);
            SetWorldY(playerBase.transform, baseY);
            playerBase.ResetHp(sharedBaseHp);
        }

        if (enemyBase != null)
        {
            enemyBase.SetTeam(Team.Enemy);
            SetWorldY(enemyBase.transform, baseY);
            enemyBase.ResetHp(sharedBaseHp);
        }

        RefreshInitialBaseSpacing();
    }

    public void HandleBaseDestroyed(ChessBase destroyedBase)
    {
        if (destroyedBase == null)
        {
            return;
        }

        if (destroyedBase.team == Team.Enemy)
        {
            CaptureEnemyBase(destroyedBase);
        }
        else
        {
            currentState = GameState.Defeat;
            StopAllUnits();
            ShowResult("Defeat");
        }
    }

    private void CaptureEnemyBase(ChessBase capturedBase)
    {
        endlessScore += 1;
        endlessDifficulty += 1;
        BattlefieldRevision += 1;

        Vector3 capturedPosition = capturedBase.transform.position;
        float capturedMaxHp = Mathf.Max(1f, capturedBase.maxHp);
        float spacing = GetCurrentBaseSpacing();
        Vector3 newEnemyPosition = new Vector3(capturedPosition.x + spacing, baseY, capturedPosition.z);
        ChessBase nextEnemyBase = CreateNewEnemyBase(newEnemyPosition);

        capturedBase.SetTeam(Team.Player);
        SetWorldY(capturedBase.transform, baseY);
        capturedBase.ResetHp(capturedMaxHp);
        playerBase = capturedBase;
        enemyBase = nextEnemyBase;
        CleanupEnemyStragglersAfterCapture();
        ChessCombatFx.SpawnBaseCaptureBurst(capturedBase.transform.position);

        if (nextEnemyBase != null)
        {
            ChessCombatFx.SpawnUnitSpawnBurst(nextEnemyBase.transform.position, Team.Enemy);
        }

        UpdateSpawnPointsForCurrentBases();
        RefreshCameraTarget();
        money += 45f + endlessScore * 8f;
        enemyMoney += 28f + endlessDifficulty * 10f;
        RefreshSpawnCardVisuals(true);
        UpdateUI();
    }

    private void CleanupEnemyStragglersAfterCapture()
    {
        if (playerBase == null)
        {
            return;
        }

        float protectedFrontX = playerBase.transform.position.x + Mathf.Abs(playerBaseOffset) + 0.35f;
        List<ChessUnit> units = ChessUnit.ActiveUnits;

        for (int i = units.Count - 1; i >= 0; i--)
        {
            ChessUnit unit = units[i];
            if (unit == null || unit.team != Team.Enemy)
            {
                continue;
            }

            if (unit.transform.position.x <= protectedFrontX)
            {
                Destroy(unit.gameObject);
            }
        }
    }

    private ChessBase CreateNewEnemyBase(Vector3 position)
    {
        GameObject newBaseObject;
        if (enemyBasePrefab != null)
        {
            newBaseObject = Instantiate(enemyBasePrefab, position, Quaternion.identity);
        }
        else if (enemyBaseTemplateCaptured)
        {
            newBaseObject = CreateCleanEnemyBaseObject(position);
        }
        else
        {
            Debug.LogError("No enemy base prefab or captured enemy base template found.");
            return null;
        }

        ChessBase newBase = newBaseObject.GetComponent<ChessBase>();
        if (newBase == null)
        {
            newBase = newBaseObject.AddComponent<ChessBase>();
        }

        float newHp = sharedBaseHp + endlessDifficulty * endlessHpPerBase;
        newBase.SetTeam(Team.Enemy);
        SetWorldY(newBase.transform, baseY);
        newBase.ResetHp(newHp);
        return newBase;
    }

    private GameObject CreateCleanEnemyBaseObject(Vector3 position)
    {
        GameObject newBaseObject = new GameObject("EnemyBase");
        newBaseObject.transform.position = position;
        newBaseObject.transform.rotation = enemyBaseTemplateRotation;
        newBaseObject.transform.localScale = enemyBaseTemplateScale;

        SpriteRenderer renderer = newBaseObject.AddComponent<SpriteRenderer>();
        renderer.sprite = enemyBaseTemplateSprite;
        renderer.color = enemyBaseTemplateColor;
        renderer.sortingLayerID = enemyBaseTemplateSortingLayerId;
        renderer.sortingOrder = enemyBaseTemplateSortingOrder;
        renderer.flipX = enemyBaseTemplateFlipX;
        renderer.flipY = enemyBaseTemplateFlipY;

        newBaseObject.AddComponent<BoxCollider2D>();
        ChessBase cleanBase = newBaseObject.AddComponent<ChessBase>();
        cleanBase.spriteRenderer = renderer;
        cleanBase.playerBaseSprite = enemyBaseTemplatePlayerSprite;
        cleanBase.enemyBaseSprite = enemyBaseTemplateEnemySprite;

        return newBaseObject;
    }

    private void CaptureEnemyBaseTemplateIfNeeded()
    {
        if (enemyBaseTemplateCaptured || enemyBase != null && enemyBasePrefab != null)
        {
            return;
        }

        if (enemyBase == null)
        {
            return;
        }

        SpriteRenderer renderer = enemyBase.spriteRenderer != null
            ? enemyBase.spriteRenderer
            : enemyBase.GetComponent<SpriteRenderer>();

        enemyBaseTemplateCaptured = true;
        enemyBaseTemplateScale = enemyBase.transform.localScale;
        enemyBaseTemplateRotation = enemyBase.transform.rotation;
        enemyBaseTemplatePlayerSprite = enemyBase.playerBaseSprite;
        enemyBaseTemplateEnemySprite = enemyBase.enemyBaseSprite;

        if (renderer != null)
        {
            enemyBaseTemplateSprite = enemyBase.enemyBaseSprite != null ? enemyBase.enemyBaseSprite : renderer.sprite;
            enemyBaseTemplateColor = renderer.color;
            enemyBaseTemplateSortingLayerId = renderer.sortingLayerID;
            enemyBaseTemplateSortingOrder = renderer.sortingOrder;
            enemyBaseTemplateFlipX = renderer.flipX;
            enemyBaseTemplateFlipY = renderer.flipY;
        }
    }

    private void RefreshInitialBaseSpacing()
    {
        if (playerBase == null || enemyBase == null)
        {
            initialBaseSpacing = Mathf.Max(1f, endlessBaseSpacing);
            return;
        }

        initialBaseSpacing = Mathf.Abs(enemyBase.transform.position.x - playerBase.transform.position.x);
        endlessBaseSpacing = initialBaseSpacing;
    }

    private float GetCurrentBaseSpacing()
    {
        if (initialBaseSpacing <= 0.01f)
        {
            RefreshInitialBaseSpacing();
        }

        return Mathf.Max(1f, initialBaseSpacing);
    }

    private void UpdateSpawnPointsForCurrentBases()
    {
        if (playerBase != null && playerSpawnPoint != null)
        {
            playerSpawnPoint.position = new Vector3(playerBase.transform.position.x + Mathf.Abs(playerBaseOffset), laneY, 0f);
        }

        if (enemyBase != null && enemySpawnPoint != null)
        {
            enemySpawnPoint.position = new Vector3(enemyBase.transform.position.x - Mathf.Abs(enemyBaseOffset), laneY, 0f);
        }
    }

    private void SetWorldY(Transform target, float y)
    {
        if (target == null)
        {
            return;
        }

        Vector3 position = target.position;
        position.y = y;
        target.position = position;
    }

    private void ApplyBrowserResponsiveLayout(bool force)
    {
        if (!enableResponsiveBrowserLayout)
        {
            return;
        }

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        float aspect = width / (float)height;

        if (!force
            && width == lastScreenWidth
            && height == lastScreenHeight
            && Mathf.Abs(aspect - lastScreenAspect) < 0.001f)
        {
            return;
        }

        lastScreenWidth = width;
        lastScreenHeight = height;
        lastScreenAspect = aspect;

        ConfigureCanvasForBrowser(aspect);
        RefreshCameraTarget();
        UpdateBattleBackground();
        RefreshSpawnCardVisuals(true);
    }

    private void ConfigureCanvasForBrowser(float aspect)
    {
        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            return;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = browserReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        if (aspect <= 1.45f)
        {
            scaler.matchWidthOrHeight = narrowScreenUiMatch;
        }
        else if (aspect >= 1.95f)
        {
            scaler.matchWidthOrHeight = wideScreenUiMatch;
        }
        else
        {
            scaler.matchWidthOrHeight = balancedScreenUiMatch;
        }

        canvas.pixelPerfect = false;
    }

    private void RefreshCameraTarget()
    {
        if (playerBase != null && enemyBase != null)
        {
            float playerX = playerBase.transform.position.x;
            float enemyX = enemyBase.transform.position.x;
            float halfDistance = Mathf.Abs(enemyX - playerX) * 0.5f;
            float aspect = mainCamera != null ? mainCamera.aspect : 16f / 9f;
            float safeAspect = Mathf.Clamp(aspect, Mathf.Max(0.1f, browserMinAspect), Mathf.Max(browserMinAspect, browserMaxAspect));
            float verticalPadding = aspect < 1.45f ? narrowScreenExtraCameraPadding : 0f;

            cameraTargetX = (playerX + enemyX) * 0.5f;
            cameraTargetSize = Mathf.Max(minCameraSize + verticalPadding, (halfDistance + cameraHorizontalPadding) / Mathf.Max(0.1f, safeAspect));
            return;
        }

        if (playerBase != null)
        {
            cameraTargetX = playerBase.transform.position.x;
            cameraTargetSize = minCameraSize;
            return;
        }

        if (enemyBase != null)
        {
            cameraTargetX = enemyBase.transform.position.x;
            cameraTargetSize = minCameraSize;
            return;
        }

        cameraTargetX = 0f;
        cameraTargetSize = minCameraSize;
    }

    private void SnapCameraToFocus()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        RefreshCameraTarget();

        Vector3 position = mainCamera.transform.position;
        position.x = cameraTargetX;
        position.y = cameraTargetY;
        position.z = cameraTargetZ;
        mainCamera.transform.position = position;

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = cameraTargetSize;
        }

        UpdateBattleBackground();
    }

    private void UpdateCameraFollow(float deltaTime)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }
        }

        RefreshCameraTarget();

        Vector3 position = mainCamera.transform.position;
        float lerpT = Mathf.Clamp01(deltaTime * cameraFollowSpeed);
        position.x = Mathf.Lerp(position.x, cameraTargetX, lerpT);
        position.y = cameraTargetY;
        position.z = cameraTargetZ;
        mainCamera.transform.position = position;

        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, cameraTargetSize, lerpT);
        }

        UpdateBattleBackground();
    }

    private void EnsureBattleBackground()
    {
        if (battleBackgroundRenderer == null)
        {
            GameObject existing = GameObject.Find("BattleBackground");
            if (existing == null)
            {
                existing = new GameObject("BattleBackground");
            }

            battleBackgroundRenderer = existing.GetComponent<SpriteRenderer>();
            if (battleBackgroundRenderer == null)
            {
                battleBackgroundRenderer = existing.AddComponent<SpriteRenderer>();
            }
        }

        if (battleBackgroundRenderer.sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(backgroundResourceName);
            if (texture != null)
            {
                battleBackgroundRenderer.sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                battleBackgroundRenderer.sprite.name = backgroundResourceName + "_Sprite";
            }
            else
            {
                battleBackgroundRenderer.sprite = ChessCombatFx.GetDefaultSprite();
                battleBackgroundRenderer.color = new Color(0.55f, 0.72f, 0.96f, 1f);
            }
        }

        battleBackgroundRenderer.sortingOrder = backgroundSortingOrder;
        UpdateBattleBackground();
    }

    private void UpdateBattleBackground()
    {
        if (battleBackgroundRenderer == null)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || battleBackgroundRenderer.sprite == null)
        {
            return;
        }

        float cameraHeight = mainCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * mainCamera.aspect;
        Vector3 cameraPosition = mainCamera.transform.position;
        battleBackgroundRenderer.transform.position = new Vector3(
            cameraPosition.x + backgroundWorldOffset.x,
            cameraPosition.y + backgroundWorldOffset.y,
            backgroundDepth);

        Vector2 spriteSize = battleBackgroundRenderer.sprite.bounds.size;
        float scale = Mathf.Max(cameraWidth / Mathf.Max(0.01f, spriteSize.x), cameraHeight / Mathf.Max(0.01f, spriteSize.y));
        battleBackgroundRenderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private float GetCurrentEnemySpawnInterval()
    {
        float earlyDelay = Mathf.Max(0f, 0.65f - endlessDifficulty * 0.13f);
        return Mathf.Max(minimumEnemySpawnInterval, enemySpawnInterval + earlyDelay);
    }

    private void AccumulateMoney(float deltaTime)
    {
        moneyCarry += moneyPerSecond * deltaTime;
        int gained = Mathf.FloorToInt(moneyCarry);
        if (gained > 0)
        {
            money += gained;
            moneyCarry -= gained;
            money = Mathf.Max(0f, money);
        }

        float enemyIncomeMultiplier = Mathf.Lerp(EnemyEarlyIncomeMultiplier, 1f, Mathf.Clamp01(endlessScore / 4f));
        enemyMoneyCarry += moneyPerSecond * enemyIncomeMultiplier * deltaTime;
        int enemyGained = Mathf.FloorToInt(enemyMoneyCarry);
        if (enemyGained > 0)
        {
            enemyMoney += enemyGained;
            enemyMoneyCarry -= enemyGained;
            enemyMoney = Mathf.Max(0f, enemyMoney);
        }
    }

    public void SpawnPawn()
    {
        SpawnPlayerUnit(playerPawnPrefab, "Pawn", ChessBalance.PawnCost);
    }

    public void SpawnRook()
    {
        SpawnPlayerUnit(playerRookPrefab, "Rook", ChessBalance.RookCost);
    }

    public void SpawnKnight()
    {
        SpawnPlayerUnit(playerKnightPrefab, "Knight", ChessBalance.KnightCost);
    }

    public void SpawnBishop()
    {
        SpawnPlayerUnit(playerBishopPrefab, "Bishop", ChessBalance.BishopCost);
    }

    public void SpawnQueen()
    {
        SpawnPlayerUnit(playerQueenPrefab, "Queen", ChessBalance.QueenCost);
    }

    public void SpawnKing()
    {
        SpawnPlayerUnit(playerKingPrefab, "King", ChessBalance.KingCost);
    }

    private void SpawnPlayerUnit(GameObject prefab, string label, float cost)
    {
        if (!Application.isPlaying || !IsPlaying)
        {
            return;
        }

        if (!CanPlayerSpawn(cost))
        {
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning("Player unit prefab missing.");
            return;
        }

        if (!SpendMoney(cost))
        {
            return;
        }

        playerSpawnCooldownTimer = playerSpawnCooldown;

        GameObject unitObject = Instantiate(prefab, GetSpawnPosition(Team.Player), Quaternion.identity);
        ChessUnit unit = unitObject.GetComponent<ChessUnit>();
        if (unit != null)
        {
            unit.Initialize(Team.Player);
        }

        RefreshSpawnCardVisuals(true);
        UpdateUI();
    }

    private bool CanPlayerSpawn(float cost)
    {
        return IsPlaying && playerSpawnCooldownTimer <= 0f && money >= cost;
    }

    public void SpawnRandomEnemy()
    {
        TrySpawnEnemyFromBudget();
    }

    private void TrySpawnEnemyFromBudget()
    {
        SpawnDefinition? selectedDefinition = ChooseEnemySpawnDefinition();
        if (!selectedDefinition.HasValue)
        {
            return;
        }

        SpawnDefinition definition = selectedDefinition.Value;
        GameObject prefab = GetEnemyPrefab(definition.kind);
        if (prefab == null)
        {
            Debug.LogWarning("Enemy unit prefab missing.");
            return;
        }

        enemyMoney = Mathf.Max(0f, enemyMoney - definition.cost);
        SpawnEnemyUnit(prefab);
    }

    private void SpawnEnemyUnit(GameObject prefab)
    {
        GameObject unitObject = Instantiate(prefab, GetSpawnPosition(Team.Enemy), Quaternion.identity);
        ChessUnit unit = unitObject.GetComponent<ChessUnit>();
        if (unit != null)
        {
            unit.Initialize(Team.Enemy);
        }
    }

    private SpawnDefinition? ChooseEnemySpawnDefinition()
    {
        int highestUnlocked = GetHighestUnlockedUnitTier();
        float totalWeight = 0f;
        float[] weights = new float[SpawnDefinitions.Length];

        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition definition = SpawnDefinitions[i];
            if (definition.cost > enemyMoney || GetEnemyPrefab(definition.kind) == null)
            {
                continue;
            }

            weights[i] = GetEnemySpawnWeight(definition.kind, highestUnlocked);
            totalWeight += weights[i];
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            if (weights[i] <= 0f)
            {
                continue;
            }

            roll -= weights[i];
            if (roll <= 0f)
            {
                return SpawnDefinitions[i];
            }
        }

        return SpawnDefinitions[0];
    }

    private float GetEnemySpawnWeight(ChessPieceKind kind, int highestUnlocked)
    {
        if (highestUnlocked <= 0)
        {
            return kind == ChessPieceKind.Pawn ? 1f : 0f;
        }

        switch (kind)
        {
            case ChessPieceKind.Pawn:
                return 34f;
            case ChessPieceKind.Knight:
                return 20f;
            case ChessPieceKind.Rook:
                return 18f;
            case ChessPieceKind.Bishop:
                return highestUnlocked >= 2 ? 16f : 0f;
            case ChessPieceKind.Queen:
                return highestUnlocked >= 3 ? 8f : 0f;
            case ChessPieceKind.King:
                return highestUnlocked >= 4 ? 5f : 0f;
            default:
                return 0f;
        }
    }

    private GameObject GetEnemyPrefab(ChessPieceKind kind)
    {
        switch (kind)
        {
            case ChessPieceKind.Rook:
                return enemyRookPrefab;
            case ChessPieceKind.Knight:
                return enemyKnightPrefab;
            case ChessPieceKind.Bishop:
                return enemyBishopPrefab;
            case ChessPieceKind.Queen:
                return enemyQueenPrefab;
            case ChessPieceKind.King:
                return enemyKingPrefab;
            default:
                return enemyPawnPrefab;
        }
    }

    private int GetHighestUnlockedUnitTier()
    {
        return 4;
    }

    private bool IsUnitUnlocked(string label)
    {
        return true;
    }

    private Vector3 GetSpawnPosition(Team team)
    {
        Transform anchor = team == Team.Player ? playerSpawnPoint : enemySpawnPoint;
        if (anchor != null)
        {
            return new Vector3(anchor.position.x, laneY, 0f);
        }

        ChessBase fallbackBase = team == Team.Player ? playerBase : enemyBase;
        float offset = team == Team.Player ? Mathf.Abs(playerBaseOffset) : -Mathf.Abs(enemyBaseOffset);
        if (fallbackBase != null)
        {
            return new Vector3(fallbackBase.transform.position.x + offset, laneY, 0f);
        }

        return new Vector3(0f, laneY, 0f);
    }

    private bool SpendMoney(float cost)
    {
        if (money < cost || !IsPlaying)
        {
            return false;
        }

        money -= cost;
        RefreshSpawnCardVisuals(true);
        UpdateUI();
        return true;
    }

    private void StopAllUnits()
    {
        List<ChessUnit> units = ChessUnit.ActiveUnits;
        for (int i = 0; i < units.Count; i++)
        {
            ChessUnit unit = units[i];
            if (unit == null)
            {
                continue;
            }

            Rigidbody2D rb = unit.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }
    }

    private void ShowResult(string message)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            resultPanel.transform.SetAsLastSibling();
        }

        if (resultText != null)
        {
            resultText.text = message;
        }
    }

    private void UpdateUI()
    {
        if (modeText != null)
        {
            modeText.text = "Chess Lane Clash";
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            scoreText.text = "Score: " + endlessScore;
            if (lastDisplayedScore != endlessScore)
            {
                lastDisplayedScore = endlessScore;
                GetOrAddComponent<ChessUiTextPopFx>(scoreText.gameObject).Pop(new Color(0.58f, 0.86f, 1f, 1f));
            }
        }

        if (moneyText != null)
        {
            int displayMoney = Mathf.FloorToInt(money);
            moneyText.text = "Gold: " + displayMoney;
            if (lastDisplayedMoney != displayMoney)
            {
                bool gained = lastDisplayedMoney < 0 || displayMoney >= lastDisplayedMoney;
                lastDisplayedMoney = displayMoney;
                GetOrAddComponent<ChessUiTextPopFx>(moneyText.gameObject).Pop(gained ? new Color(1f, 0.86f, 0.32f, 1f) : new Color(1f, 0.36f, 0.28f, 1f));
            }
        }

        if (restartButton != null)
        {
            restartButton.interactable = currentState != GameState.Playing;
            ApplyMenuButtonEffects(restartButton, new Color(1f, 0.72f, 0.32f, 0.9f));
        }

        ApplyMenuButtonEffects(startEndlessButton, new Color(0.36f, 0.78f, 1f, 0.95f));

        RefreshSpawnCardVisuals();
    }

    private void RefreshSpawnCardVisuals(bool force = false)
    {
        if (Application.isPlaying && !force && Time.unscaledTime < nextSpawnCardRefreshTime)
        {
            return;
        }

        if (Application.isPlaying && !force)
        {
            nextSpawnCardRefreshTime = Time.unscaledTime + SpawnCardRefreshInterval;
        }

        if (!useSceneAuthoredUi)
        {
            EnsureSpawnCards();
        }

        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition def = SpawnDefinitions[i];
            SpawnCardUiCache cache = GetSpawnCardUiCache(def.label);
            if (cache == null || cache.card == null)
            {
                continue;
            }

            GameObject card = cache.card;
            bool affordable = CanPlayerSpawn(def.cost);
            if (cache.button != null)
            {
                cache.button.interactable = affordable;
            }

            if (!cache.initialized || cache.lastAffordable != affordable)
            {
                ApplySpawnCardStyle(card, def, i);
                ApplySceneCardEffects(card, affordable, i);
                cache.lastAffordable = affordable;
            }

            if (cache.nameText != null && (!cache.initialized || cache.nameText.text != def.label))
            {
                cache.nameText.text = def.label;
            }

            int costValue = Mathf.FloorToInt(def.cost);
            if (cache.costText != null && (!cache.initialized || cache.lastCost != costValue))
            {
                cache.costText.text = "$" + costValue;
                cache.lastCost = costValue;
            }

            if (cache.iconImage != null && (!cache.initialized || cache.iconImage.sprite == null))
            {
                cache.iconImage.sprite = GetSpawnIconSprite(def.label);
                cache.iconImage.preserveAspect = true;
            }

            cache.initialized = true;
        }
    }

    private SpawnCardUiCache GetSpawnCardUiCache(string label)
    {
        if (!spawnCardUiCache.TryGetValue(label, out SpawnCardUiCache cache))
        {
            cache = new SpawnCardUiCache();
            spawnCardUiCache[label] = cache;
        }

        if (cache.card == null)
        {
            cache.card = FindSpawnCard(label);
            cache.initialized = false;
        }

        if (cache.card == null)
        {
            return null;
        }

        if (cache.button == null)
        {
            cache.button = cache.card.GetComponent<Button>();
        }

        if (cache.nameText == null)
        {
            cache.nameText = FindChildText(cache.card.transform, "Name");
        }

        if (cache.costText == null)
        {
            cache.costText = FindChildText(cache.card.transform, "Cost");
        }

        if (cache.iconImage == null)
        {
            cache.iconImage = FindChildImage(cache.card.transform, "Icon");
        }

        return cache;
    }

    private GameObject FindSpawnCard(string label)
    {
        if (cardsPanel != null)
        {
            Transform child = cardsPanel.transform.Find(label);
            if (child != null)
            {
                return child.gameObject;
            }
        }

        return GameObject.Find(label);
    }

    private void ApplySceneCardEffects(GameObject card, bool affordable, int index)
    {
        if (card == null)
        {
            return;
        }

        Button button = GetOrAddComponent<Button>(card);
        Image image = card.GetComponent<Image>();
        if (image != null)
        {
            button.targetGraphic = image;
            image.raycastTarget = true;
        }

        Color accent = affordable
            ? new Color(0.32f, 0.72f, 1f, 0.9f)
            : new Color(0.12f, 0.16f, 0.22f, 0.65f);

        Outline outline = GetOrAddComponent<Outline>(card);
        outline.effectColor = accent;
        outline.effectDistance = affordable ? new Vector2(4f, -4f) : new Vector2(2f, -2f);

        Shadow shadow = GetOrAddComponent<Shadow>(card);
        shadow.effectColor = affordable ? new Color(0f, 0.16f, 0.35f, 0.72f) : new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(6f, -6f);

        ChessUiPulseFx pulse = GetOrAddComponent<ChessUiPulseFx>(card);
        pulse.Configure(affordable ? 1f : 0.25f, 1.075f, affordable ? 0.014f : 0.004f);

        ChessUiGlowFx glow = GetOrAddComponent<ChessUiGlowFx>(card);
        glow.Configure(accent, affordable, index * 0.33f);
    }

    private TMP_Text FindChildText(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Image FindChildImage(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private Button FindButton(string name)
    {
        GameObject go = FindSpawnCard(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private void ResolveReferences()
    {
        if (playerBase == null)
        {
            GameObject playerBaseObject = GameObject.Find("PlayerBase");
            if (playerBaseObject != null)
            {
                playerBase = playerBaseObject.GetComponent<ChessBase>();
            }
        }

        if (enemyBase == null)
        {
            GameObject enemyBaseObject = GameObject.Find("EnemyBase");
            if (enemyBaseObject != null)
            {
                enemyBase = enemyBaseObject.GetComponent<ChessBase>();
            }
        }

        if (moneyText == null)
        {
            GameObject moneyObject = GameObject.Find("MoneyText");
            if (moneyObject == null)
            {
                moneyObject = GameObject.Find("Money");
            }

            if (moneyObject != null)
            {
                moneyText = moneyObject.GetComponent<TMP_Text>();
                if (moneyText != null && moneyText.gameObject.name == "Money")
                {
                    moneyText.gameObject.name = "MoneyText";
                }
            }
        }

        if (moneyText != null && moneyText.gameObject.name == "Money")
        {
            moneyText.gameObject.name = "MoneyText";
        }

        if (modeText == null)
        {
            GameObject modeObject = GameObject.Find("ModeText");
            if (modeObject != null)
            {
                modeText = modeObject.GetComponent<TMP_Text>();
            }
        }

        if (scoreText == null)
        {
            GameObject scoreObject = GameObject.Find("ScoreText");
            if (scoreObject != null)
            {
                scoreText = scoreObject.GetComponent<TMP_Text>();
            }
        }

        if (cardsPanel == null)
        {
            GameObject cardsObject = GameObject.Find("CardsPanel");
            if (cardsObject != null)
            {
                cardsPanel = cardsObject;
            }
        }

        if (resultPanel == null)
        {
            GameObject panelObject = GameObject.Find("ResultPanel");
            if (panelObject != null)
            {
                resultPanel = panelObject;
            }
        }

        if (resultText == null && resultPanel != null)
        {
            Transform resultTextTransform = resultPanel.transform.Find("ResultText");
            if (resultTextTransform != null)
            {
                resultText = resultTextTransform.GetComponent<TMP_Text>();
            }
        }

        if (restartButton == null && resultPanel != null)
        {
            Transform restartButtonTransform = resultPanel.transform.Find("RestartButton");
            if (restartButtonTransform != null)
            {
                restartButton = restartButtonTransform.GetComponent<Button>();
            }
        }

        if (playerSpawnPoint == null)
        {
            GameObject playerSpawnObject = GameObject.Find("PlayerSpawnPoint");
            if (playerSpawnObject != null)
            {
                playerSpawnPoint = playerSpawnObject.transform;
            }
        }

        if (enemySpawnPoint == null)
        {
            GameObject enemySpawnObject = GameObject.Find("EnemySpawnPoint");
            if (enemySpawnObject != null)
            {
                enemySpawnPoint = enemySpawnObject.transform;
            }
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras != null && cameras.Length > 0)
                {
                    mainCamera = cameras[0];
                }
            }
        }

        if (startMenuPanel == null)
        {
            GameObject startMenuObject = GameObject.Find("StartMenuPanel");
            if (startMenuObject != null)
            {
                startMenuPanel = startMenuObject;
            }
        }

        if (startTitleText == null && startMenuPanel != null)
        {
            Transform titleTransform = startMenuPanel.transform.Find("StartTitleText");
            if (titleTransform != null)
            {
                startTitleText = titleTransform.GetComponent<TMP_Text>();
            }
        }

        if (startSubtitleText == null && startMenuPanel != null)
        {
            Transform subtitleTransform = startMenuPanel.transform.Find("StartSubtitleText");
            if (subtitleTransform != null)
            {
                startSubtitleText = subtitleTransform.GetComponent<TMP_Text>();
            }
        }

        if (startEndlessButton == null && startMenuPanel != null)
        {
            Transform endlessButtonTransform = startMenuPanel.transform.Find("StartEndlessButton");
            if (endlessButtonTransform != null)
            {
                startEndlessButton = endlessButtonTransform.GetComponent<Button>();
            }
        }

        if (playerBase != null && playerSpawnPoint != null && playerSpawnPoint.IsChildOf(playerBase.transform))
        {
            playerSpawnPoint = CreateSpawnPoint("PlayerSpawnPoint", playerBase, playerSpawnPoint.position);
        }

        if (enemyBase != null && enemySpawnPoint != null && enemySpawnPoint.IsChildOf(enemyBase.transform))
        {
            enemySpawnPoint = CreateSpawnPoint("EnemySpawnPoint", enemyBase, enemySpawnPoint.position);
        }
    }

    private void BindRuntimeButtons()
    {
        BindSpawnButtons();
        BindStartMenuButtons();

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartScene);
            restartButton.onClick.AddListener(RestartScene);
        }

        ApplyMenuButtonEffects(startEndlessButton, new Color(0.36f, 0.78f, 1f, 0.95f));
        ApplyMenuButtonEffects(restartButton, new Color(1f, 0.72f, 0.32f, 0.9f));
    }

    private void ApplyMenuButtonEffects(Button button, Color accent)
    {
        if (button == null)
        {
            return;
        }

        ChessUiPulseFx pulse = GetOrAddComponent<ChessUiPulseFx>(button.gameObject);
        pulse.Configure(button.interactable ? 1f : 0.3f, 1.08f, 0.024f);

        ChessUiGlowFx glow = GetOrAddComponent<ChessUiGlowFx>(button.gameObject);
        glow.Configure(accent, button.interactable, 0f);

        Outline outline = GetOrAddComponent<Outline>(button.gameObject);
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(5f, -5f);

        Shadow shadow = GetOrAddComponent<Shadow>(button.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.74f);
        shadow.effectDistance = new Vector2(7f, -7f);
    }

    private void BindSpawnButtons()
    {
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition def = SpawnDefinitions[i];
            Button button = FindButton(def.label);
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            UnityEngine.Events.UnityAction action = GetSpawnAction(def.label);
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
        }
    }

    private UnityEngine.Events.UnityAction GetSpawnAction(string label)
    {
        switch (label)
        {
            case "Pawn":
                return SpawnPawn;
            case "Rook":
                return SpawnRook;
            case "Knight":
                return SpawnKnight;
            case "Bishop":
                return SpawnBishop;
            case "Queen":
                return SpawnQueen;
            case "King":
                return SpawnKing;
            default:
                return null;
        }
    }

    private void BindStartMenuButtons()
    {
        if (startEndlessButton != null)
        {
            startEndlessButton.onClick.RemoveListener(StartEndlessMode);
            startEndlessButton.onClick.AddListener(StartEndlessMode);
        }
    }

    private void EnsureSceneSetup()
    {
        ResolveReferences();

        Canvas canvas = FindCanvas();
        if (!useSceneAuthoredUi && canvas != null)
        {
            if (moneyText == null)
            {
                EnsureMoneyText(canvas.transform);
            }

            if (modeText == null)
            {
                modeText = CreateText(
                    "ModeText",
                    canvas.transform,
                    "Chess Lane Clash",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(24f, -24f),
                    new Vector2(280f, 42f),
                    Color.white,
                    26f);
            }

            if (cardsPanel == null)
            {
                cardsPanel = EnsureCardsPanel(canvas.transform);
            }

            EnsureSpawnCards();

            if (startMenuPanel == null)
            {
                startMenuPanel = CreateStartMenuPanel(canvas.transform);
            }

            if (scoreText == null)
            {
                scoreText = CreateText(
                    "ScoreText",
                    canvas.transform,
                    "Score: 0",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -24f),
                    new Vector2(280f, 42f),
                    new Color(1f, 0.92f, 0.55f, 1f),
                    26f);
            }

            if (resultPanel == null)
            {
                resultPanel = CreateResultPanel(canvas.transform);
            }

            if (resultText == null && resultPanel != null)
            {
                resultText = CreateText(
                    "ResultText",
                    resultPanel.transform,
                    "Defeat",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 54f),
                    new Vector2(520f, 120f),
                    Color.white,
                    54f);
                resultText.alignment = TextAlignmentOptions.Center;
            }

            if (restartButton == null && resultPanel != null)
            {
                restartButton = CreateRestartButton(resultPanel.transform);
            }
        }

        if (playerSpawnPoint == null)
        {
            playerSpawnPoint = CreateSpawnPoint("PlayerSpawnPoint", playerBase, new Vector3(playerBase != null ? playerBase.transform.position.x + playerBaseOffset : -7f, 0f, 0f));
        }

        if (enemySpawnPoint == null)
        {
            enemySpawnPoint = CreateSpawnPoint("EnemySpawnPoint", enemyBase, new Vector3(enemyBase != null ? enemyBase.transform.position.x - Mathf.Abs(enemyBaseOffset) : 7f, 0f, 0f));
        }

        ResolveReferences();
        if (!useSceneAuthoredUi)
        {
            StyleRuntimeUi();
        }
        BindRuntimeButtons();
        CompactSceneAuthoredCardPanel();
        RefreshSpawnCardVisuals();
        UpdateUI();
    }

    private void StyleRuntimeUi()
    {
        if (useSceneAuthoredUi)
        {
            return;
        }

        if (!autoStyleRuntimeUi)
        {
            return;
        }

        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            return;
        }

        GameObject hudBar = EnsurePanel(canvas.transform, "TopHudBar", new Color(0.018f, 0.025f, 0.045f, 0.74f));
        RectTransform hudRect = hudBar.GetComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0f, 1f);
        hudRect.anchorMax = new Vector2(1f, 1f);
        hudRect.pivot = new Vector2(0.5f, 1f);
        hudRect.offsetMin = hudOffsetMin;
        hudRect.offsetMax = hudOffsetMax;

        Outline hudOutline = GetOrAddComponent<Outline>(hudBar);
        hudOutline.effectColor = new Color(0.36f, 0.68f, 1f, 0.72f);
        hudOutline.effectDistance = new Vector2(5f, -5f);

        StyleHudText(modeText, hudBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(42f, 0f), new Vector2(470f, 86f), TextAlignmentOptions.Left, Color.white);
        StyleHudText(scoreText, hudBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 86f), TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.34f, 1f));
        StyleHudText(moneyText, hudBar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(540f, 86f), TextAlignmentOptions.Right, new Color(1f, 0.91f, 0.38f, 1f));

        cardsPanel = cardsPanel != null ? cardsPanel : EnsureCardsPanel(canvas.transform);
        StyleCardsPanel();
        StyleStartMenuPanel();
        StyleResultPanel();
    }

    private void StyleHudText(TMP_Text text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment, Color color)
    {
        if (text == null)
        {
            return;
        }

        text.transform.SetParent(parent, false);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        text.color = color;
        text.fontSize = hudFontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, hudFontSize * 0.65f);
        text.fontSizeMax = hudFontSize;

        Shadow shadow = GetOrAddComponent<Shadow>(text.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(4f, -4f);
    }

    private GameObject EnsureCardsPanel(Transform parent)
    {
        GameObject panel = GameObject.Find("CardsPanel");
        if (panel == null)
        {
            panel = new GameObject("CardsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }

        panel.transform.SetParent(parent, false);
        GetOrAddComponent<Image>(panel).sprite = ChessCombatFx.GetDefaultSprite();
        return panel;
    }

    private void StyleCardsPanel()
    {
        if (cardsPanel == null)
        {
            return;
        }

        RectTransform rect = cardsPanel.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = cardsPanel.AddComponent<RectTransform>();
        }

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = cardsPanelSize;
        rect.anchoredPosition = cardsPanelPosition;

        Image panelImage = GetOrAddComponent<Image>(cardsPanel);
        panelImage.sprite = ChessCombatFx.GetDefaultSprite();
        panelImage.type = Image.Type.Simple;
        panelImage.color = new Color(0.018f, 0.023f, 0.04f, 0.66f);
        panelImage.raycastTarget = false;

        HorizontalLayoutGroup layout = GetOrAddComponent<HorizontalLayoutGroup>(cardsPanel);
        layout.padding = new RectOffset(36, 36, 32, 32);
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        EnsureSpawnCards();
    }

    private void CompactSceneAuthoredCardPanel()
    {
        if (!useSceneAuthoredUi || !compactSceneCardPanel || cardsPanel == null)
        {
            return;
        }

        RectTransform panelRect = cardsPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            return;
        }

        HorizontalLayoutGroup horizontalLayout = cardsPanel.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
        {
            horizontalLayout.enabled = false;
        }

        ContentSizeFitter contentSizeFitter = cardsPanel.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.enabled = false;
        }

        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = sceneCardsPanelSize;
        panelRect.anchoredPosition = sceneCardsPanelPosition;

        int cardCount = 0;
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            if (FindSpawnCard(SpawnDefinitions[i].label) != null)
            {
                cardCount++;
            }
        }

        if (cardCount <= 0)
        {
            return;
        }

        float step = sceneCardSize.x + sceneCardSpacing;
        float startX = -step * (cardCount - 1) * 0.5f;
        int visibleIndex = 0;
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            GameObject card = FindSpawnCard(SpawnDefinitions[i].label);
            if (card == null)
            {
                continue;
            }

            float x = startX + visibleIndex * step;
            CompactSceneCard(card, SpawnDefinitions[i], i, new Vector2(x, 0f));
            visibleIndex++;
        }
    }

    private void CompactSceneCard(GameObject card, SpawnDefinition definition, int index, Vector2 anchoredPosition)
    {
        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect == null)
        {
            return;
        }

        LayoutElement layoutElement = card.GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.enabled = false;
        }

        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = sceneCardSize;
        cardRect.anchoredPosition = anchoredPosition;

        Button button = GetOrAddComponent<Button>(card);
        Image cardImage = card.GetComponent<Image>();
        if (cardImage != null)
        {
            button.targetGraphic = cardImage;
            cardImage.raycastTarget = true;
        }

        PlaceChildRect(card.transform, "Icon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(56f, 56f));
        PlaceChildRect(card.transform, "Name", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(170f, 26f));
        PlaceChildRect(card.transform, "Cost", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 8f), new Vector2(76f, 24f));
        PlaceChildRect(card.transform, "Hotkey", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(34f, 26f));
        PlaceChildRect(card.transform, "IconFrame", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(68f, 62f));
        PlaceChildRect(card.transform, "CostPlate", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(8f, 5f), new Vector2(86f, 30f));
        PlaceChildRect(card.transform, "HotkeyBadge", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(40f, 32f));

        TMP_Text nameText = FindChildText(card.transform, "Name");
        if (nameText != null)
        {
            nameText.text = definition.label;
            nameText.fontSize = 21f;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 13f;
            nameText.fontSizeMax = 21f;
            nameText.alignment = TextAlignmentOptions.Center;
        }

        TMP_Text costText = FindChildText(card.transform, "Cost");
        if (costText != null)
        {
            costText.text = "$" + Mathf.FloorToInt(definition.cost);
            costText.fontSize = 20f;
            costText.alignment = TextAlignmentOptions.Left;
        }

        TMP_Text hotkeyText = FindChildText(card.transform, "Hotkey");
        if (hotkeyText != null)
        {
            hotkeyText.text = (index + 1).ToString();
            hotkeyText.fontSize = 19f;
            hotkeyText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void PlaceChildRect(Transform parent, string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        Transform child = FindDirectChild(parent, childName);
        if (child == null)
        {
            return;
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private void EnsureSpawnCards()
    {
        if (cardsPanel == null)
        {
            return;
        }

        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition def = SpawnDefinitions[i];
            GameObject card = FindSpawnCard(def.label);
            if (card == null)
            {
                card = new GameObject(def.label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            }

            card.name = def.label;
            card.transform.SetParent(cardsPanel.transform, false);
            EnsureCardChildren(card.transform);
            ApplySpawnCardStyle(card, def, i);
        }
    }

    private void EnsureCardChildren(Transform card)
    {
        EnsureChildImage(card, "Icon");
        EnsureChildText(card, "Name");
        EnsureChildText(card, "Cost");
        EnsureChildText(card, "Hotkey");
        EnsureChildImage(card, "IconFrame");
        EnsureChildImage(card, "CostPlate");
        EnsureChildImage(card, "HotkeyBadge");
    }

    private void ApplySpawnCardStyle(GameObject card, SpawnDefinition def, int index)
    {
        if (card == null)
        {
            return;
        }

        if (!autoStyleRuntimeUi)
        {
            Button existingButton = GetOrAddComponent<Button>(card);
            Image existingImage = card.GetComponent<Image>();
            if (existingImage != null)
            {
                existingButton.targetGraphic = existingImage;
            }

            return;
        }

        RectTransform rect = card.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = card.AddComponent<RectTransform>();
        }

        rect.sizeDelta = cardSize;

        LayoutElement layoutElement = GetOrAddComponent<LayoutElement>(card);
        layoutElement.preferredWidth = cardSize.x;
        layoutElement.preferredHeight = cardSize.y;
        layoutElement.minWidth = Mathf.Max(1f, cardSize.x - 24f);
        layoutElement.minHeight = Mathf.Max(1f, cardSize.y - 16f);

        Image image = GetOrAddComponent<Image>(card);
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.type = Image.Type.Simple;
        bool unlocked = true;
        bool affordable = CanPlayerSpawn(def.cost);
        image.color = unlocked && affordable ? new Color(0.10f, 0.14f, 0.21f, 0.90f) : new Color(0.06f, 0.07f, 0.10f, 0.82f);
        image.raycastTarget = true;

        Outline outline = GetOrAddComponent<Outline>(card);
        outline.effectColor = unlocked && affordable ? new Color(0.34f, 0.74f, 1f, 0.92f) : new Color(0.08f, 0.10f, 0.14f, 0.9f);
        outline.effectDistance = new Vector2(5f, -5f);

        Shadow cardShadow = GetOrAddComponent<Shadow>(card);
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
        cardShadow.effectDistance = new Vector2(8f, -8f);

        ChessUiPulseFx pulseFx = GetOrAddComponent<ChessUiPulseFx>(card);
        pulseFx.Configure(unlocked && affordable ? 1f : 0.35f, 1.075f, 0.018f);

        Button button = GetOrAddComponent<Button>(card);
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.84f, 1f, 1f);
        colors.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.72f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Image iconFrame = EnsureChildImage(card.transform, "IconFrame");
        iconFrame.transform.SetAsFirstSibling();
        RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 1f);
        frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.sizeDelta = new Vector2(cardIconSize.x + 24f, cardIconSize.y + 18f);
        frameRect.anchoredPosition = new Vector2(0f, -14f);
        iconFrame.sprite = ChessCombatFx.GetDefaultSprite();
        iconFrame.color = affordable ? new Color(0.18f, 0.25f, 0.36f, 0.95f) : new Color(0.10f, 0.11f, 0.14f, 0.9f);
        iconFrame.raycastTarget = false;

        Image icon = EnsureChildImage(card.transform, "Icon");
        icon.transform.SetAsLastSibling();
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.sizeDelta = cardIconSize;
        iconRect.anchoredPosition = new Vector2(0f, -20f);
        icon.sprite = GetSpawnIconSprite(def.label);
        icon.color = affordable ? Color.white : new Color(0.62f, 0.66f, 0.74f, 0.82f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text name = EnsureChildText(card.transform, "Name");
        name.transform.SetAsLastSibling();
        RectTransform nameRect = name.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(1f, 0f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.offsetMin = new Vector2(12f, 52f);
        nameRect.offsetMax = new Vector2(-12f, 92f);
        name.text = def.label;
        name.fontSize = cardNameFontSize;
        name.fontStyle = FontStyles.Bold;
        name.color = Color.white;
        name.alignment = TextAlignmentOptions.Center;
        name.enableAutoSizing = true;
        name.fontSizeMin = Mathf.Max(10f, cardNameFontSize * 0.65f);
        name.fontSizeMax = cardNameFontSize;
        name.raycastTarget = false;

        Image costPlate = EnsureChildImage(card.transform, "CostPlate");
        costPlate.transform.SetAsLastSibling();
        RectTransform costPlateRect = costPlate.GetComponent<RectTransform>();
        costPlateRect.anchorMin = new Vector2(0f, 0f);
        costPlateRect.anchorMax = new Vector2(1f, 0f);
        costPlateRect.pivot = new Vector2(0.5f, 0f);
        costPlateRect.offsetMin = new Vector2(14f, 10f);
        costPlateRect.offsetMax = new Vector2(-14f, 48f);
        costPlate.sprite = ChessCombatFx.GetDefaultSprite();
        costPlate.color = affordable ? new Color(0.42f, 0.26f, 0.07f, 0.92f) : new Color(0.18f, 0.15f, 0.12f, 0.88f);
        costPlate.raycastTarget = false;

        TMP_Text cost = EnsureChildText(card.transform, "Cost");
        cost.transform.SetAsLastSibling();
        RectTransform costRect = cost.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(0f, 0f);
        costRect.anchorMax = new Vector2(1f, 0f);
        costRect.pivot = new Vector2(0f, 0f);
        costRect.offsetMin = new Vector2(26f, 9f);
        costRect.offsetMax = new Vector2(-20f, 49f);
        cost.text = "$" + Mathf.FloorToInt(def.cost);
        cost.fontSize = cardCostFontSize;
        cost.fontStyle = FontStyles.Bold;
        cost.color = new Color(1f, 0.83f, 0.30f, 1f);
        cost.alignment = TextAlignmentOptions.Left;
        cost.raycastTarget = false;

        Image hotkeyBadge = EnsureChildImage(card.transform, "HotkeyBadge");
        hotkeyBadge.transform.SetAsLastSibling();
        RectTransform badgeRect = hotkeyBadge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(1f, 1f);
        badgeRect.sizeDelta = new Vector2(54f, 48f);
        badgeRect.anchoredPosition = new Vector2(-12f, -12f);
        hotkeyBadge.sprite = ChessCombatFx.GetDefaultSprite();
        hotkeyBadge.color = affordable ? new Color(0.18f, 0.44f, 0.78f, 0.95f) : new Color(0.12f, 0.14f, 0.18f, 0.9f);
        hotkeyBadge.raycastTarget = false;

        TMP_Text hotkey = EnsureChildText(card.transform, "Hotkey");
        hotkey.transform.SetAsLastSibling();
        RectTransform hotkeyRect = hotkey.GetComponent<RectTransform>();
        hotkeyRect.anchorMin = new Vector2(1f, 1f);
        hotkeyRect.anchorMax = new Vector2(1f, 1f);
        hotkeyRect.pivot = new Vector2(1f, 1f);
        hotkeyRect.sizeDelta = new Vector2(54f, 48f);
        hotkeyRect.anchoredPosition = new Vector2(-12f, -12f);
        hotkey.text = (index + 1).ToString();
        hotkey.fontSize = cardHotkeyFontSize;
        hotkey.fontStyle = FontStyles.Bold;
        hotkey.color = new Color(0.72f, 0.84f, 1f, 1f);
        hotkey.alignment = TextAlignmentOptions.Center;
        hotkey.raycastTarget = false;
    }

    private void StyleStartMenuPanel()
    {
        if (startMenuPanel == null)
        {
            return;
        }

        RectTransform rect = startMenuPanel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        Image image = GetOrAddComponent<Image>(startMenuPanel);
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.color = new Color(0.025f, 0.035f, 0.055f, 0.78f);
        image.raycastTarget = true;

        if (startTitleText != null)
        {
            RectTransform titleRect = startTitleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -116f);
            titleRect.sizeDelta = new Vector2(1240f, 150f);
            startTitleText.text = "Chess Lane Clash";
            startTitleText.fontSize = startTitleFontSize;
            startTitleText.fontStyle = FontStyles.Bold;
            startTitleText.alignment = TextAlignmentOptions.Center;
            startTitleText.color = Color.white;
            Shadow titleShadow = GetOrAddComponent<Shadow>(startTitleText.gameObject);
            titleShadow.effectColor = new Color(0.08f, 0.18f, 0.34f, 0.9f);
            titleShadow.effectDistance = new Vector2(4f, -4f);
        }

        if (startSubtitleText != null)
        {
            RectTransform subtitleRect = startSubtitleText.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -270f);
            subtitleRect.sizeDelta = new Vector2(1320f, 92f);
            startSubtitleText.text = "Push the lane, capture outposts, and hold the line.";
            startSubtitleText.fontSize = startSubtitleFontSize;
            startSubtitleText.alignment = TextAlignmentOptions.Center;
            startSubtitleText.color = new Color(0.78f, 0.84f, 0.94f, 1f);
        }

        if (startEndlessButton != null)
        {
            RectTransform buttonRect = startEndlessButton.GetComponent<RectTransform>();
            buttonRect.sizeDelta = startButtonSize;
            buttonRect.anchoredPosition = new Vector2(0f, -24f);
            Image buttonImage = GetOrAddComponent<Image>(startEndlessButton.gameObject);
            buttonImage.color = new Color(0.10f, 0.18f, 0.30f, 1f);

            Outline buttonOutline = GetOrAddComponent<Outline>(startEndlessButton.gameObject);
            buttonOutline.effectColor = new Color(0.35f, 0.68f, 1f, 0.85f);
            buttonOutline.effectDistance = new Vector2(4f, -4f);

            ChessUiPulseFx buttonFx = GetOrAddComponent<ChessUiPulseFx>(startEndlessButton.gameObject);
            buttonFx.Configure(1f, 1.08f, 0.025f);

            TMP_Text buttonLabel = startEndlessButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = "Start Game";
                buttonLabel.fontSize = 48f;
                buttonLabel.fontStyle = FontStyles.Bold;
            }
        }

        TMP_Text footerText = FindChildText(startMenuPanel.transform, "StartFooterText");
        if (footerText != null)
        {
            footerText.text = "Build a line, push forward, and break the enemy base.";
            footerText.fontSize = 30f;
            footerText.color = new Color(0.76f, 0.82f, 0.92f, 1f);
        }
    }

    private void StyleResultPanel()
    {
        if (resultPanel == null)
        {
            return;
        }

        Image image = GetOrAddComponent<Image>(resultPanel);
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.color = new Color(0.02f, 0.025f, 0.035f, 0.76f);

        if (resultText != null)
        {
            resultText.fontSize = resultFontSize;
            resultText.fontStyle = FontStyles.Bold;
            resultText.alignment = TextAlignmentOptions.Center;
            Shadow resultShadow = GetOrAddComponent<Shadow>(resultText.gameObject);
            resultShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            resultShadow.effectDistance = new Vector2(4f, -4f);
        }
    }

    private GameObject EnsurePanel(Transform parent, string name, Color color)
    {
        GameObject panel = GameObject.Find(name);
        if (panel == null)
        {
            panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        }

        panel.transform.SetParent(parent, false);
        Image image = GetOrAddComponent<Image>(panel);
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        panel.transform.SetAsFirstSibling();
        return panel;
    }

    private Image EnsureChildImage(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject childObject;
        if (child == null)
        {
            childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(parent, false);
        }
        else
        {
            childObject = child.gameObject;
        }

        return GetOrAddComponent<Image>(childObject);
    }

    private TMP_Text EnsureChildText(Transform parent, string name)
    {
        Transform child = FindDirectChild(parent, name);
        GameObject childObject;
        if (child == null)
        {
            childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            childObject.transform.SetParent(parent, false);
        }
        else
        {
            childObject = child.gameObject;
        }

        TextMeshProUGUI text = GetOrAddComponent<TextMeshProUGUI>(childObject);
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    private Canvas FindCanvas()
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        return canvases != null && canvases.Length > 0 ? canvases[0] : null;
    }

    private TMP_Text EnsureMoneyText(Transform parent)
    {
        GameObject moneyObject = GameObject.Find("MoneyText");
        if (moneyObject == null)
        {
            moneyObject = GameObject.Find("Money");
        }

        if (moneyObject != null)
        {
            moneyText = moneyObject.GetComponent<TMP_Text>();
            if (moneyText != null)
            {
                moneyText.gameObject.name = "MoneyText";
                return moneyText;
            }
        }

        moneyText = CreateText(
            "MoneyText",
            parent,
            "Gold: 100",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-24f, -24f),
            new Vector2(300f, 42f),
            new Color(1f, 0.92f, 0.55f, 1f),
            26f);
        return moneyText;
    }

    private GameObject CreateResultPanel(Transform parent)
    {
        GameObject panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.type = Image.Type.Simple;
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;

        panel.SetActive(false);
        return panel;
    }

    private GameObject CreateStartMenuPanel(Transform parent)
    {
        GameObject panel = new GameObject("StartMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.GetComponent<Image>();
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.type = Image.Type.Simple;
        image.color = new Color(0.04f, 0.06f, 0.10f, 0.96f);
        image.raycastTarget = true;

        TMP_Text title = CreateText(
            "StartTitleText",
            panel.transform,
            "Chess Lane Clash",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -118f),
            new Vector2(760f, 90f),
            Color.white,
            62f);
        title.alignment = TextAlignmentOptions.Center;

        TMP_Text subtitle = CreateText(
            "StartSubtitleText",
            panel.transform,
            "Push the lane, capture outposts, and hold the line.",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -186f),
            new Vector2(780f, 60f),
            new Color(0.82f, 0.87f, 0.95f, 1f),
            24f);
        subtitle.alignment = TextAlignmentOptions.Center;

        startEndlessButton = CreateMenuButton(panel.transform, "StartEndlessButton", "Start Game", new Vector2(0f, -8f));

        TMP_Text footer = CreateText(
            "StartFooterText",
            panel.transform,
            "Build a line, push forward, and break the enemy base.",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 64f),
            new Vector2(820f, 44f),
            new Color(0.76f, 0.80f, 0.88f, 1f),
            20f);
        footer.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);
        return panel;
    }

    private Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(500f, 104f);
        rect.anchoredPosition = anchoredPosition;

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.color = new Color(0.18f, 0.22f, 0.30f, 0.96f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        TMP_Text labelText = CreateText(
            name + "_Label",
            buttonObject.transform,
            label,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(460f, 64f),
            Color.white,
            36f);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontStyle = FontStyles.Bold;

        return button;
    }

    private void SetGameplayUiVisible(bool visible)
    {
        if (cardsPanel != null)
        {
            cardsPanel.SetActive(visible);
        }

        if (modeText != null)
        {
            modeText.gameObject.SetActive(visible);
        }

        if (moneyText != null)
        {
            moneyText.gameObject.SetActive(visible);
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(visible);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(visible && currentState != GameState.Playing);
        }
    }

    private void HideStartMenu()
    {
        if (startMenuPanel != null)
        {
            startMenuPanel.SetActive(false);
        }
    }

    private Button CreateRestartButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(340f, 90f);
        rect.anchoredPosition = new Vector2(0f, -58f);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = ChessCombatFx.GetDefaultSprite();
        image.color = new Color(0.22f, 0.26f, 0.34f, 0.95f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(RestartScene);

        TMP_Text label = CreateText(
            "RestartLabel",
            buttonObject.transform,
            "Restart",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(300f, 60f),
            Color.white,
            34f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;

        return button;
    }

    private Transform CreateSpawnPoint(string name, ChessBase baseObject, Vector3 fallbackPosition)
    {
        GameObject spawnPoint = GameObject.Find(name);
        if (spawnPoint == null)
        {
            spawnPoint = new GameObject(name);
        }

        spawnPoint.transform.SetParent(null, true);
        spawnPoint.transform.position = fallbackPosition;

        if (baseObject != null)
        {
            float xOffset = name == "PlayerSpawnPoint" ? Mathf.Abs(playerBaseOffset) : -Mathf.Abs(enemyBaseOffset);
            spawnPoint.transform.position = baseObject.transform.position + new Vector3(xOffset, 0f, 0f);
        }

        return spawnPoint.transform;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Color color,
        float fontSize)
    {
        GameObject go = GameObject.Find(name);
        TextMeshProUGUI text;

        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
        }
        else if (go.transform.parent != parent)
        {
            go.transform.SetParent(parent, false);
        }

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        text = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        text.text = value;
        text.color = color;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(14f, fontSize * 0.7f);
        text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.raycastTarget = false;

        return text;
    }

    private void RefreshSpawnButtonInteractability()
    {
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition def = SpawnDefinitions[i];
            Button button = FindButton(def.label);
            if (button != null)
            {
                button.interactable = CanPlayerSpawn(def.cost);
            }
        }
    }

    private Sprite GetSpawnIconSprite(string label)
    {
        switch (label)
        {
            case "Pawn":
                return GetPrefabSprite(playerPawnPrefab);
            case "Rook":
                return GetPrefabSprite(playerRookPrefab);
            case "Knight":
                return GetPrefabSprite(playerKnightPrefab);
            case "Bishop":
                return GetPrefabSprite(playerBishopPrefab);
            case "Queen":
                return GetPrefabSprite(playerQueenPrefab);
            case "King":
                return GetPrefabSprite(playerKingPrefab);
            default:
                return ChessCombatFx.GetDefaultSprite();
        }
    }

    private Sprite GetPrefabSprite(GameObject prefab)
    {
        if (prefab == null)
        {
            return ChessCombatFx.GetDefaultSprite();
        }

        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null && renderer.sprite != null)
        {
            return renderer.sprite;
        }

        return ChessCombatFx.GetDefaultSprite();
    }

    public void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

public class ChessUiPulseFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rectTransform;
    private float enabledAmount = 1f;
    private float hoverScale = 1.06f;
    private float pulseAmount = 0.015f;
    private bool hovering;
    private bool pressing;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            baseScale = rectTransform.localScale;
        }
    }

    public void Configure(float enabledAmount, float hoverScale, float pulseAmount)
    {
        this.enabledAmount = Mathf.Clamp01(enabledAmount);
        this.hoverScale = Mathf.Max(1f, hoverScale);
        this.pulseAmount = Mathf.Max(0f, pulseAmount);

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (rectTransform != null && baseScale == Vector3.zero)
        {
            baseScale = Vector3.one;
        }
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return;
            }
        }

        float hover = hovering ? hoverScale : 1f;
        float press = pressing ? 0.94f : 1f;
        float pulse = 1f + Mathf.Sin((Time.unscaledTime * 3.6f) + transform.GetSiblingIndex()) * pulseAmount * enabledAmount;
        float target = hover * press * pulse;
        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, baseScale * target, Time.unscaledDeltaTime * 12f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressing = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }
}

public class ChessUiGlowFx : MonoBehaviour
{
    private Image glowImage;
    private RectTransform glowRect;
    private Color color = Color.white;
    private bool active;
    private float phase;

    public void Configure(Color color, bool active, float phase)
    {
        this.color = color;
        this.active = active;
        this.phase = phase;
        EnsureGlow();
    }

    private void Update()
    {
        EnsureGlow();
        if (glowImage == null)
        {
            return;
        }

        float pulse = active ? 0.11f + Mathf.Sin(Time.unscaledTime * 4.2f + phase) * 0.045f : 0.025f;
        Color target = color;
        target.a = Mathf.Max(0f, pulse);
        glowImage.color = Color.Lerp(glowImage.color, target, Time.unscaledDeltaTime * 10f);

        if (glowRect != null)
        {
            float inflate = active ? 8f + Mathf.Sin(Time.unscaledTime * 3.4f + phase) * 4f : 3f;
            glowRect.offsetMin = new Vector2(-inflate, -inflate);
            glowRect.offsetMax = new Vector2(inflate, inflate);
        }
    }

    private void EnsureGlow()
    {
        if (glowImage != null)
        {
            return;
        }

        Transform existing = transform.Find("UiGlowFx");
        GameObject glowObject;
        if (existing != null)
        {
            glowObject = existing.gameObject;
        }
        else
        {
            glowObject = new GameObject("UiGlowFx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowObject.transform.SetParent(transform, false);
        }

        glowRect = glowObject.GetComponent<RectTransform>();
        if (glowRect != null)
        {
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
        }

        glowImage = glowObject.GetComponent<Image>();
        glowImage.sprite = ChessCombatFx.GetDefaultSprite();
        glowImage.type = Image.Type.Simple;
        glowImage.raycastTarget = false;
        glowImage.color = new Color(color.r, color.g, color.b, 0f);
        glowObject.transform.SetAsFirstSibling();
    }
}

public class ChessUiTextPopFx : MonoBehaviour
{
    private RectTransform rectTransform;
    private TMP_Text text;
    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;
    private Color flashColor = Color.white;
    private float timer;
    private const float Duration = 0.22f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        text = GetComponent<TMP_Text>();
        if (rectTransform != null)
        {
            baseScale = rectTransform.localScale;
        }

        if (text != null)
        {
            baseColor = text.color;
        }
    }

    public void Pop(Color color)
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        flashColor = color;
        timer = Duration;
    }

    private void Update()
    {
        if (timer <= 0f)
        {
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, baseScale, Time.unscaledDeltaTime * 10f);
            }

            if (text != null)
            {
                text.color = Color.Lerp(text.color, baseColor, Time.unscaledDeltaTime * 10f);
            }

            return;
        }

        timer -= Time.unscaledDeltaTime;
        float t = 1f - Mathf.Clamp01(timer / Duration);
        float wave = Mathf.Sin(t * Mathf.PI);

        if (rectTransform != null)
        {
            rectTransform.localScale = baseScale * (1f + wave * 0.13f);
        }

        if (text != null)
        {
            text.color = Color.Lerp(baseColor, flashColor, wave);
        }
    }
}
