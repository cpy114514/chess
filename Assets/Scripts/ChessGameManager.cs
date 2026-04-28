using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class ChessGameManager : MonoBehaviour
{
    public static ChessGameManager Instance { get; private set; }

    private struct SpawnDefinition
    {
        public readonly string label;
        public readonly float cost;
        public readonly KeyCode hotkey;

        public SpawnDefinition(string label, float cost, KeyCode hotkey)
        {
            this.label = label;
            this.cost = cost;
            this.hotkey = hotkey;
        }
    }

    private static readonly SpawnDefinition[] SpawnDefinitions =
    {
        new SpawnDefinition("Pawn", 50f, KeyCode.Alpha1),
        new SpawnDefinition("Rook", 150f, KeyCode.Alpha2),
        new SpawnDefinition("Knight", 120f, KeyCode.Alpha3),
        new SpawnDefinition("Bishop", 130f, KeyCode.Alpha4),
        new SpawnDefinition("Queen", 300f, KeyCode.Alpha5),
        new SpawnDefinition("King", 250f, KeyCode.Alpha6)
    };

    [Header("Mode")]
    public GameMode currentMode = GameMode.Level;
    public GameState currentState = GameState.Menu;

    [Header("Money")]
    public float money = 100f;
    public float maxMoney = 500f;
    public float moneyPerSecond = 20f;

    [Header("Bases")]
    public ChessBase playerBase;
    public ChessBase enemyBase;
    public GameObject enemyBasePrefab;
    public float levelEnemyBaseHp = 1000f;
    public float levelPlayerBaseHp = 1000f;

    [Header("Endless")]
    public int endlessScore = 0;
    public int endlessDifficulty = 0;
    public float endlessBaseSpacing = 10f;
    public float endlessStartEnemyBaseHp = 700f;
    public float endlessHpPerBase = 180f;
    public float playerBaseOffset = 1.5f;
    public float enemyBaseOffset = 1.5f;

    [Header("Spawn Points")]
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;

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
    public float enemySpawnInterval = 3f;
    public float minimumEnemySpawnInterval = 1f;

    [Header("UI")]
    public GameObject cardsPanel;
    public GameObject startMenuPanel;
    public TMP_Text startTitleText;
    public TMP_Text startSubtitleText;
    public Button startLevelButton;
    public Button startEndlessButton;
    public TMP_Text modeText;
    public TMP_Text scoreText;
    public TMP_Text moneyText;
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button restartButton;

    private float moneyCarry;
    private float enemySpawnTimer;

    public bool IsPlaying
    {
        get
        {
            if (currentMode == GameMode.Endless)
            {
                return true;
            }

            return currentState == GameState.Playing;
        }
    }

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
        if (!Application.isPlaying)
        {
            EnsureSceneSetup();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureSceneSetup();
        }
    }

    private void Start()
    {
        ResolveReferences();
        EnsureSceneSetup();
        ResolveReferences();
        BindRuntimeButtons();
        ShowStartMenu();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!IsPlaying)
        {
            StopAllUnits();
            UpdateUI();
            return;
        }

        moneyCarry += moneyPerSecond * Time.deltaTime;
        int gained = Mathf.FloorToInt(moneyCarry);
        if (gained > 0)
        {
            money += gained;
            moneyCarry -= gained;
            money = Mathf.Clamp(money, 0f, maxMoney);
        }

        enemySpawnTimer -= Time.deltaTime;
        if (enemySpawnTimer <= 0f)
        {
            enemySpawnTimer = GetCurrentEnemySpawnInterval();
            SpawnRandomEnemy();
        }

        UpdateUI();
    }

    public void StartGame()
    {
        ResolveReferences();

        currentState = GameState.Playing;
        SetGameplayUiVisible(true);
        HideStartMenu();
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        money = 100f;
        moneyCarry = 0f;
        enemySpawnTimer = 1f;

        if (currentMode == GameMode.Level)
        {
            SetupLevelMode();
        }
        else
        {
            SetupEndlessMode();
        }

        UpdateSpawnPointsForCurrentBases();
        RefreshSpawnCardVisuals();
        UpdateUI();
    }

    public void StartLevelMode()
    {
        currentMode = GameMode.Level;
        StartGame();
    }

    public void StartEndlessMode()
    {
        currentMode = GameMode.Endless;
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
        }

        UpdateUI();
    }

    private void SetupLevelMode()
    {
        endlessScore = 0;
        endlessDifficulty = 0;

        if (playerBase != null)
        {
            playerBase.SetTeam(Team.Player);
            playerBase.ResetHp(levelPlayerBaseHp);
        }

        if (enemyBase != null)
        {
            enemyBase.SetTeam(Team.Enemy);
            enemyBase.ResetHp(levelEnemyBaseHp);
        }
    }

    private void SetupEndlessMode()
    {
        endlessScore = 0;
        endlessDifficulty = 0;

        if (playerBase != null)
        {
            playerBase.SetTeam(Team.Player);
            playerBase.ResetHp(levelPlayerBaseHp);
        }

        if (enemyBase != null)
        {
            enemyBase.SetTeam(Team.Enemy);
            enemyBase.ResetHp(endlessStartEnemyBaseHp);
        }
    }

    public void HandleBaseDestroyed(ChessBase destroyedBase)
    {
        if (destroyedBase == null)
        {
            return;
        }

        if (currentMode == GameMode.Level)
        {
            if (destroyedBase.team == Team.Enemy)
            {
                LevelVictory();
            }
            else
            {
                LevelDefeat();
            }

            return;
        }

        if (destroyedBase.team == Team.Enemy)
        {
            CaptureEnemyBase(destroyedBase);
        }
        else
        {
            destroyedBase.HealToFull();
        }
    }

    private void LevelVictory()
    {
        currentState = GameState.Victory;
        StopAllUnits();
        ShowResult("Victory");
    }

    private void LevelDefeat()
    {
        currentState = GameState.Defeat;
        StopAllUnits();
        ShowResult("Defeat");
    }

    private void CaptureEnemyBase(ChessBase capturedBase)
    {
        endlessScore += 1;
        endlessDifficulty += 1;

        Vector3 capturedPosition = capturedBase.transform.position;

        if (playerBase != null && playerBase != capturedBase)
        {
            Destroy(playerBase.gameObject);
        }

        capturedBase.SetTeam(Team.Player);
        capturedBase.ResetHp(levelPlayerBaseHp);
        playerBase = capturedBase;

        Vector3 newEnemyPosition = capturedPosition + new Vector3(endlessBaseSpacing, 0f, 0f);
        enemyBase = CreateNewEnemyBase(newEnemyPosition);

        UpdateSpawnPointsForCurrentBases();
        money = Mathf.Min(maxMoney, money + 100f + endlessScore * 20f);
        RefreshSpawnCardVisuals();
        UpdateUI();
    }

    private ChessBase CreateNewEnemyBase(Vector3 position)
    {
        GameObject newBaseObject;
        if (enemyBasePrefab != null)
        {
            newBaseObject = Instantiate(enemyBasePrefab, position, Quaternion.identity);
        }
        else if (enemyBase != null)
        {
            newBaseObject = Instantiate(enemyBase.gameObject, position, Quaternion.identity);
        }
        else
        {
            Debug.LogError("No enemy base prefab or enemyBase reference found.");
            return null;
        }

        ChessBase newBase = newBaseObject.GetComponent<ChessBase>();
        if (newBase == null)
        {
            newBase = newBaseObject.AddComponent<ChessBase>();
        }

        float newHp = endlessStartEnemyBaseHp + endlessDifficulty * endlessHpPerBase;
        newBase.SetTeam(Team.Enemy);
        newBase.ResetHp(newHp);
        return newBase;
    }

    private void UpdateSpawnPointsForCurrentBases()
    {
        if (playerBase != null && playerSpawnPoint != null)
        {
            playerSpawnPoint.position = playerBase.transform.position + new Vector3(playerBaseOffset, 0f, 0f);
        }

        if (enemyBase != null && enemySpawnPoint != null)
        {
            enemySpawnPoint.position = enemyBase.transform.position + new Vector3(-enemyBaseOffset, 0f, 0f);
        }
    }

    private float GetCurrentEnemySpawnInterval()
    {
        if (currentMode == GameMode.Endless)
        {
            return Mathf.Max(minimumEnemySpawnInterval, enemySpawnInterval - endlessDifficulty * 0.15f);
        }

        return enemySpawnInterval;
    }

    private float EnemyHpMultiplier()
    {
        if (currentMode != GameMode.Endless)
        {
            return 1f;
        }

        return 1f + endlessDifficulty * 0.10f;
    }

    private float EnemyDamageMultiplier()
    {
        if (currentMode != GameMode.Endless)
        {
            return 1f;
        }

        return 1f + endlessDifficulty * 0.08f;
    }

    public void SpawnPawn()
    {
        SpawnPlayerUnit(playerPawnPrefab, 50f);
    }

    public void SpawnRook()
    {
        SpawnPlayerUnit(playerRookPrefab, 150f);
    }

    public void SpawnKnight()
    {
        SpawnPlayerUnit(playerKnightPrefab, 120f);
    }

    public void SpawnBishop()
    {
        SpawnPlayerUnit(playerBishopPrefab, 130f);
    }

    public void SpawnQueen()
    {
        SpawnPlayerUnit(playerQueenPrefab, 300f);
    }

    public void SpawnKing()
    {
        SpawnPlayerUnit(playerKingPrefab, 250f);
    }

    private void SpawnPlayerUnit(GameObject prefab, float cost)
    {
        if (!Application.isPlaying || !IsPlaying)
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

        GameObject unitObject = Instantiate(prefab, GetSpawnPosition(Team.Player), Quaternion.identity);
        ChessUnit unit = unitObject.GetComponent<ChessUnit>();
        if (unit != null)
        {
            unit.Initialize(Team.Player);
        }

        RefreshSpawnCardVisuals();
        UpdateUI();
    }

    public void SpawnRandomEnemy()
    {
        GameObject prefab = GetRandomEnemyPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("Enemy unit prefab missing.");
            return;
        }

        GameObject unitObject = Instantiate(prefab, GetSpawnPosition(Team.Enemy), Quaternion.identity);
        ChessUnit unit = unitObject.GetComponent<ChessUnit>();
        if (unit != null)
        {
            unit.Initialize(Team.Enemy, EnemyHpMultiplier(), EnemyDamageMultiplier(), 1f);
        }
    }

    private GameObject GetRandomEnemyPrefab()
    {
        int roll = Random.Range(0, 100);

        if (currentMode == GameMode.Endless)
        {
            int d = endlessDifficulty;
            if (d < 2)
            {
                return enemyPawnPrefab;
            }

            if (d < 4)
            {
                return roll < 70 ? enemyPawnPrefab : enemyKnightPrefab;
            }

            if (d < 7)
            {
                if (roll < 50) return enemyPawnPrefab;
                if (roll < 70) return enemyKnightPrefab;
                if (roll < 85) return enemyRookPrefab;
                return enemyBishopPrefab;
            }

            if (roll < 35) return enemyPawnPrefab;
            if (roll < 55) return enemyKnightPrefab;
            if (roll < 70) return enemyRookPrefab;
            if (roll < 85) return enemyBishopPrefab;
            if (roll < 95) return enemyQueenPrefab;
            return enemyKingPrefab;
        }

        if (roll < 45) return enemyPawnPrefab;
        if (roll < 60) return enemyKnightPrefab;
        if (roll < 75) return enemyBishopPrefab;
        if (roll < 88) return enemyRookPrefab;
        if (roll < 96) return enemyKingPrefab;
        return enemyQueenPrefab;
    }

    private Vector3 GetSpawnPosition(Team team)
    {
        Transform anchor = team == Team.Player ? playerSpawnPoint : enemySpawnPoint;
        if (anchor != null)
        {
            return anchor.position;
        }

        ChessBase fallbackBase = team == Team.Player ? playerBase : enemyBase;
        float offset = team == Team.Player ? playerBaseOffset : -enemyBaseOffset;
        if (fallbackBase != null)
        {
            return fallbackBase.transform.position + new Vector3(offset, 0f, 0f);
        }

        return Vector3.zero;
    }

    private bool SpendMoney(float cost)
    {
        if (money < cost || !IsPlaying)
        {
            return false;
        }

        money -= cost;
        RefreshSpawnCardVisuals();
        UpdateUI();
        return true;
    }

    private void StopAllUnits()
    {
        ChessUnit[] units = FindObjectsByType<ChessUnit>(FindObjectsSortMode.None);
        foreach (ChessUnit unit in units)
        {
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
            modeText.text = "Mode: " + currentMode;
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(currentMode == GameMode.Endless);
            scoreText.text = "Score: " + endlessScore;
        }

        if (moneyText != null)
        {
            moneyText.text = "Gold: " + Mathf.FloorToInt(money) + " / " + Mathf.FloorToInt(maxMoney);
        }

        if (restartButton != null)
        {
            restartButton.interactable = currentMode == GameMode.Level && currentState != GameState.Playing;
        }

        RefreshSpawnCardVisuals();
    }

    private void RefreshSpawnCardVisuals()
    {
        for (int i = 0; i < SpawnDefinitions.Length; i++)
        {
            SpawnDefinition def = SpawnDefinitions[i];
            GameObject card = FindSpawnCard(def.label);
            if (card == null)
            {
                continue;
            }

            Button button = card.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = IsPlaying && money >= def.cost;
            }

            TMP_Text nameText = FindChildText(card.transform, "Name");
            if (nameText != null)
            {
                nameText.text = def.label;
            }

            TMP_Text costText = FindChildText(card.transform, "Cost");
            if (costText != null)
            {
                costText.text = "$" + Mathf.FloorToInt(def.cost);
            }

            Image iconImage = FindChildImage(card.transform, "Icon");
            if (iconImage != null)
            {
                iconImage.sprite = GetSpawnIconSprite(def.label);
                iconImage.preserveAspect = true;
            }
        }
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

        if (startLevelButton == null && startMenuPanel != null)
        {
            Transform startButtonTransform = startMenuPanel.transform.Find("StartLevelButton");
            if (startButtonTransform != null)
            {
                startLevelButton = startButtonTransform.GetComponent<Button>();
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
        if (startLevelButton != null)
        {
            startLevelButton.onClick.RemoveListener(StartLevelMode);
            startLevelButton.onClick.AddListener(StartLevelMode);
        }

        if (startEndlessButton != null)
        {
            startEndlessButton.onClick.RemoveListener(StartEndlessMode);
            startEndlessButton.onClick.AddListener(StartEndlessMode);
        }
    }

    private void EnsureSceneSetup()
    {
        ResolveReferences();

        if (moneyText == null || modeText == null || scoreText == null || resultPanel == null || resultText == null || restartButton == null || playerSpawnPoint == null || enemySpawnPoint == null || cardsPanel == null || startMenuPanel == null || startLevelButton == null || startEndlessButton == null)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                return;
            }

            if (moneyText == null)
            {
                EnsureMoneyText(canvas.transform);
            }

            if (modeText == null)
            {
                modeText = CreateText(
                    "ModeText",
                    canvas.transform,
                    "Mode: Level",
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
                cardsPanel = GameObject.Find("CardsPanel");
            }

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
                    "Victory",
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

            if (playerSpawnPoint == null)
            {
                playerSpawnPoint = CreateSpawnPoint("PlayerSpawnPoint", playerBase, new Vector3(playerBase != null ? playerBase.transform.position.x + playerBaseOffset : -7f, 0f, 0f));
            }

            if (enemySpawnPoint == null)
            {
                enemySpawnPoint = CreateSpawnPoint("EnemySpawnPoint", enemyBase, new Vector3(enemyBase != null ? enemyBase.transform.position.x - enemyBaseOffset : 7f, 0f, 0f));
            }
        }

        ResolveReferences();
        BindRuntimeButtons();
        RefreshSpawnCardVisuals();
        UpdateUI();
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
            "Gold: 100 / 500",
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
            "All six pieces are available from the start.",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -186f),
            new Vector2(780f, 60f),
            new Color(0.82f, 0.87f, 0.95f, 1f),
            24f);
        subtitle.alignment = TextAlignmentOptions.Center;

        startLevelButton = CreateMenuButton(panel.transform, "StartLevelButton", "Stage Mode", new Vector2(0f, 24f));
        startEndlessButton = CreateMenuButton(panel.transform, "StartEndlessButton", "Endless Outposts", new Vector2(0f, -68f));

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
        rect.sizeDelta = new Vector2(360f, 76f);
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
            new Vector2(320f, 48f),
            Color.white,
            28f);
        labelText.alignment = TextAlignmentOptions.Center;

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
            scoreText.gameObject.SetActive(visible && currentMode == GameMode.Endless);
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
        rect.sizeDelta = new Vector2(260f, 72f);
        rect.anchoredPosition = new Vector2(0f, -42f);

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
            new Vector2(240f, 50f),
            Color.white,
            28f);
        label.alignment = TextAlignmentOptions.Center;

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
            float xOffset = name == "PlayerSpawnPoint" ? playerBaseOffset : -enemyBaseOffset;
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
                button.interactable = IsPlaying && money >= def.cost;
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
