using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ChessGameManager : MonoBehaviour
{
    [Header("Economy")]
    public int money = 100;
    public float moneyPerSecond = 20f;
    public TMP_Text moneyText;

    [Header("Spawn Points")]
    public Transform playerSpawnPoint;
    public Transform enemySpawnPoint;

    [Header("Player Prefabs")]
    public GameObject playerPawnPrefab;
    public GameObject playerRookPrefab;
    public GameObject playerKnightPrefab;
    public GameObject playerBishopPrefab;
    public GameObject playerQueenPrefab;
    public GameObject playerKingPrefab;

    [Header("Enemy Prefabs")]
    public GameObject enemyPawnPrefab;
    public GameObject enemyRookPrefab;
    public GameObject enemyKnightPrefab;
    public GameObject enemyBishopPrefab;
    public GameObject enemyQueenPrefab;
    public GameObject enemyKingPrefab;

    [Header("Enemy Flow")]
    public float enemySpawnInterval = 1f;

    private struct CardDefinition
    {
        public string label;
        public int cost;
        public KeyCode hotkey;
        public Color tint;

        public CardDefinition(string label, int cost, KeyCode hotkey, Color tint)
        {
            this.label = label;
            this.cost = cost;
            this.hotkey = hotkey;
            this.tint = tint;
        }
    }

    private class SpawnButtonSpec
    {
        public CardDefinition definition;
        public Action action;
        public Button button;
        public Image backgroundImage;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text costText;
    }

    private static readonly CardDefinition[] CardDefinitions =
    {
        new CardDefinition("Pawn", 20, KeyCode.Alpha1, new Color(0.84f, 0.84f, 0.84f, 1f)),
        new CardDefinition("Rook", 45, KeyCode.Alpha2, new Color(0.66f, 0.78f, 0.98f, 1f)),
        new CardDefinition("Knight", 35, KeyCode.Alpha3, new Color(0.98f, 0.72f, 0.45f, 1f)),
        new CardDefinition("Bishop", 50, KeyCode.Alpha4, new Color(0.73f, 0.55f, 0.98f, 1f)),
        new CardDefinition("Queen", 60, KeyCode.Alpha5, new Color(0.46f, 0.92f, 0.64f, 1f)),
        new CardDefinition("King", 80, KeyCode.Alpha6, new Color(0.98f, 0.84f, 0.36f, 1f))
    };

    private static Sprite cardBackgroundSprite;
    private static Texture2D cardBackgroundTexture;

    private readonly List<SpawnButtonSpec> spawnButtons = new List<SpawnButtonSpec>();
    private RectTransform buttonRow;
    private Button templateButton;
    private TMP_Text templateText;
    private float moneyCarry;
    private float enemySpawnTimer;
    private float battleClock;
    private bool battleEnded;
    private bool syncingSceneCards;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            SyncSceneCards();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SyncSceneCards();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        SyncSceneCards();
        ResolveReferences();
        BuildSpawnButtons();
        RefreshMoneyUI();
        RefreshButtonStates();

        enemySpawnTimer = Mathf.Max(0.25f, enemySpawnInterval);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!battleEnded)
        {
            TickMoney();
            TickEnemySpawner();
            battleClock += Time.deltaTime;
            CheckBattleState();
        }

        HandleHotkeys();
        RefreshButtonStates();
        ApplyButtonTransforms();
    }

    public void SpawnPawn()
    {
        SpawnPlayerUnit(playerPawnPrefab, 20);
    }

    public void SpawnRook()
    {
        SpawnPlayerUnit(playerRookPrefab, 45);
    }

    public void SpawnKnight()
    {
        SpawnPlayerUnit(playerKnightPrefab, 35);
    }

    public void SpawnBishop()
    {
        SpawnPlayerUnit(playerBishopPrefab, 50);
    }

    public void SpawnQueen()
    {
        SpawnPlayerUnit(playerQueenPrefab, 60);
    }

    public void SpawnKing()
    {
        SpawnPlayerUnit(playerKingPrefab, 80);
    }

    public void SpawnRandomEnemy()
    {
        GameObject prefab = ChooseEnemyPrefab();
        SpawnEnemyUnit(prefab);
    }

    private void ResolveReferences()
    {
        if (moneyText == null)
        {
            GameObject moneyObject = GameObject.Find("Money");
            if (moneyObject != null)
            {
                moneyText = moneyObject.GetComponent<TMP_Text>();
            }
        }

        if (playerSpawnPoint == null)
        {
            GameObject playerBase = GameObject.Find("PlayerBase");
            if (playerBase != null)
            {
                playerSpawnPoint = playerBase.transform;
            }
        }

        if (enemySpawnPoint == null)
        {
            GameObject enemyBase = GameObject.Find("EnemyBase");
            if (enemyBase != null)
            {
                enemySpawnPoint = enemyBase.transform;
            }
        }

        if (buttonRow == null)
        {
            GameObject cardsPanelObject = GameObject.Find("CardsPanel");
            if (cardsPanelObject != null)
            {
                buttonRow = cardsPanelObject.GetComponent<RectTransform>();
            }
        }

        if (templateButton == null)
        {
            Button pawnButton = FindCardButton("Pawn");
            if (pawnButton != null)
            {
                templateButton = pawnButton;
            }
        }

        if (templateText == null)
        {
            if (templateButton != null)
            {
                templateText = templateButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (templateText == null)
            {
                templateText = moneyText;
            }
        }
    }

    private void SyncSceneCards()
    {
        if (syncingSceneCards)
        {
            return;
        }

        syncingSceneCards = true;

        try
        {
            ResolveReferences();
            if (buttonRow == null)
            {
                return;
            }

            for (int i = 0; i < CardDefinitions.Length; i++)
            {
                EnsureSceneCard(CardDefinitions[i], i);
            }

            ApplySceneCardLayout();
        }
        finally
        {
            syncingSceneCards = false;
        }
    }

    private void EnsureSceneCard(CardDefinition definition, int siblingIndex)
    {
        Button button = FindCardButton(definition.label);
        if (button == null)
        {
            button = CreateCardButton(definition.label);
        }

        if (button == null)
        {
            return;
        }

        button.name = definition.label;
        button.transform.SetSiblingIndex(siblingIndex);

        Image backgroundImage = button.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = button.gameObject.AddComponent<Image>();
        }

        backgroundImage.sprite = GetCardBackgroundSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = GetCardBackgroundColor(definition.tint);
        backgroundImage.preserveAspect = false;
        backgroundImage.raycastTarget = true;

        button.targetGraphic = backgroundImage;
        button.transition = Selectable.Transition.ColorTint;

        Image iconImage = EnsureIconChild(button.transform);
        TMP_Text nameText = EnsureTextChild(button.transform, "Name", true);
        TMP_Text costText = EnsureTextChild(button.transform, "Cost", false);

        if (iconImage != null)
        {
            iconImage.sprite = GetSpawnIconSprite(definition.label);
            iconImage.type = Image.Type.Simple;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        if (nameText != null)
        {
            nameText.text = definition.label;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 18f;
            nameText.fontSizeMax = 28f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
        }

        if (costText != null)
        {
            costText.text = "$" + definition.cost;
            costText.alignment = TextAlignmentOptions.TopRight;
            costText.enableAutoSizing = true;
            costText.fontSizeMin = 18f;
            costText.fontSizeMax = 26f;
            costText.fontStyle = FontStyles.Bold;
            costText.color = new Color(1f, 0.92f, 0.55f, 1f);
            costText.raycastTarget = false;
        }
    }

    private Button CreateCardButton(string label)
    {
        if (buttonRow == null)
        {
            return null;
        }

        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(buttonRow, false);

        Button button = go.GetComponent<Button>();
        Image image = go.GetComponent<Image>();
        image.sprite = GetCardBackgroundSprite();
        image.type = Image.Type.Simple;
        image.raycastTarget = true;

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        if (templateButton != null)
        {
            button.navigation = templateButton.navigation;
        }

        return button;
    }

    private Image EnsureIconChild(Transform parent)
    {
        Transform iconTransform = FindDirectChild(parent, "Icon");
        if (iconTransform != null)
        {
            Image existingImage = iconTransform.GetComponent<Image>();
            if (existingImage != null)
            {
                return existingImage;
            }
        }

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(parent, false);
        return iconObject.GetComponent<Image>();
    }

    private TMP_Text EnsureTextChild(Transform parent, string childName, bool reuseAnyDirectText)
    {
        Transform childTransform = FindDirectChild(parent, childName);
        if (childTransform != null)
        {
            TMP_Text existingText = childTransform.GetComponent<TMP_Text>();
            if (existingText != null)
            {
                return existingText;
            }
        }

        if (reuseAnyDirectText)
        {
            TMP_Text reusableText = FindFirstDirectTextChild(parent);
            if (reusableText != null)
            {
                reusableText.name = childName;
                return reusableText;
            }
        }

        GameObject textObject;
        TMP_Text textComponent;

        if (templateText != null)
        {
            textObject = Instantiate(templateText.gameObject);
            textObject.name = childName;
            textObject.transform.SetParent(parent, false);
            textComponent = textObject.GetComponent<TMP_Text>();
        }
        else
        {
            textObject = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            textComponent = textObject.GetComponent<TextMeshProUGUI>();
        }

        return textComponent;
    }

    private TMP_Text FindFirstDirectTextChild(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            TMP_Text text = parent.GetChild(i).GetComponent<TMP_Text>();
            if (text != null)
            {
                return text;
            }
        }

        return null;
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

    private void BuildSpawnButtons()
    {
        SyncSceneCards();
        spawnButtons.Clear();

        for (int i = 0; i < CardDefinitions.Length; i++)
        {
            CardDefinition definition = CardDefinitions[i];
            Button button = FindCardButton(definition.label);
            if (button == null)
            {
                continue;
            }

            SpawnButtonSpec spec = CreateSpec(button, definition, GetSpawnAction(definition.label));
            spawnButtons.Add(spec);
        }

        ApplyButtonTransforms();
    }

    private SpawnButtonSpec CreateSpec(Button button, CardDefinition definition, Action action)
    {
        SpawnButtonSpec spec = new SpawnButtonSpec();
        spec.definition = definition;
        spec.action = action;
        spec.button = button;
        spec.backgroundImage = button != null ? button.GetComponent<Image>() : null;
        spec.iconImage = FindImageChild(button != null ? button.transform : null, "Icon");
        spec.nameText = FindTextChild(button != null ? button.transform : null, "Name");
        spec.costText = FindTextChild(button != null ? button.transform : null, "Cost");

        if (spec.button != null)
        {
            spec.button.onClick = new Button.ButtonClickedEvent();
            spec.button.onClick.AddListener(() => spec.action?.Invoke());
        }

        ApplySpecVisuals(spec);
        return spec;
    }

    private Image FindImageChild(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindTextChild(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Action GetSpawnAction(string label)
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

    private void ApplySpecVisuals(SpawnButtonSpec spec)
    {
        if (spec.button != null)
        {
            spec.button.interactable = money >= spec.definition.cost && !battleEnded;

            ColorBlock colors = spec.button.colors;
            colors.normalColor = spec.definition.tint;
            colors.highlightedColor = Color.Lerp(spec.definition.tint, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(spec.definition.tint, Color.black, 0.12f);
            colors.disabledColor = new Color(spec.definition.tint.r * 0.45f, spec.definition.tint.g * 0.45f, spec.definition.tint.b * 0.45f, 0.5f);
            colors.selectedColor = colors.highlightedColor;
            spec.button.colors = colors;
        }

        if (spec.backgroundImage != null)
        {
            spec.backgroundImage.sprite = GetCardBackgroundSprite();
            spec.backgroundImage.type = Image.Type.Simple;
            spec.backgroundImage.color = GetCardBackgroundColor(spec.definition.tint);
            spec.backgroundImage.preserveAspect = false;
        }

        if (spec.iconImage != null)
        {
            spec.iconImage.sprite = GetSpawnIconSprite(spec.definition.label);
            spec.iconImage.type = Image.Type.Simple;
            spec.iconImage.preserveAspect = true;
            spec.iconImage.color = Color.white;
        }

        if (spec.nameText != null)
        {
            spec.nameText.text = spec.definition.label;
            spec.nameText.alignment = TextAlignmentOptions.Center;
            spec.nameText.enableAutoSizing = true;
            spec.nameText.fontSizeMin = 18f;
            spec.nameText.fontSizeMax = 28f;
            spec.nameText.fontStyle = FontStyles.Bold;
            spec.nameText.color = Color.white;
        }

        if (spec.costText != null)
        {
            spec.costText.text = "$" + spec.definition.cost;
            spec.costText.alignment = TextAlignmentOptions.TopRight;
            spec.costText.enableAutoSizing = true;
            spec.costText.fontSizeMin = 18f;
            spec.costText.fontSizeMax = 26f;
            spec.costText.fontStyle = FontStyles.Bold;
            spec.costText.color = new Color(1f, 0.92f, 0.55f, 1f);
        }
    }

    private Color GetCardBackgroundColor(Color tint)
    {
        Color background = Color.Lerp(tint, new Color(0.08f, 0.1f, 0.14f, 1f), 0.55f);
        background.a = 0.96f;
        return background;
    }

    private Sprite GetCardBackgroundSprite()
    {
        if (cardBackgroundSprite != null)
        {
            return cardBackgroundSprite;
        }

        if (cardBackgroundTexture == null)
        {
            cardBackgroundTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            cardBackgroundTexture.hideFlags = HideFlags.HideAndDontSave;
            cardBackgroundTexture.SetPixels(new[]
            {
                Color.white, Color.white,
                Color.white, Color.white
            });
            cardBackgroundTexture.Apply();
        }

        cardBackgroundSprite = Sprite.Create(cardBackgroundTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        cardBackgroundSprite.hideFlags = HideFlags.HideAndDontSave;
        return cardBackgroundSprite;
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

    private void ApplySceneCardLayout()
    {
        if (buttonRow == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        float panelWidth = buttonRow.rect.width;
        if (panelWidth <= 1f)
        {
            panelWidth = 1920f;
        }

        float count = CardDefinitions.Length;
        float minMargin = 80f;
        float usableWidth = Mathf.Max(760f, panelWidth - minMargin * 2f);
        float buttonWidth = Mathf.Clamp((usableWidth - 16f * (count - 1f)) / count, 118f, 180f);
        float gap = count > 1f ? Mathf.Clamp((usableWidth - buttonWidth * count) / (count - 1f), 12f, 32f) : 0f;
        float totalWidth = buttonWidth * count + gap * (count - 1f);
        float startX = -totalWidth * 0.5f + buttonWidth * 0.5f;
        float buttonHeight = 182f;

        for (int i = 0; i < CardDefinitions.Length; i++)
        {
            Button button = FindCardButton(CardDefinitions[i].label);
            if (button == null)
            {
                continue;
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            rect.anchoredPosition = new Vector2(startX + i * (buttonWidth + gap), 0f);

            ApplyCardChildLayout(button.transform, buttonWidth, buttonHeight);
        }
    }

    private void ApplyButtonTransforms()
    {
        if (buttonRow == null || spawnButtons.Count == 0)
        {
            return;
        }

        ApplySceneCardLayout();
    }

    private void ApplyCardChildLayout(Transform cardTransform, float width, float height)
    {
        Image iconImage = FindImageChild(cardTransform, "Icon");
        TMP_Text nameText = FindTextChild(cardTransform, "Name");
        TMP_Text costText = FindTextChild(cardTransform, "Cost");

        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            float iconSize = Mathf.Clamp(width * 0.56f, 72f, 102f);
            iconRect.anchorMin = new Vector2(0.5f, 0.62f);
            iconRect.anchorMax = new Vector2(0.5f, 0.62f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(0f, height * 0.04f);
        }

        if (nameText != null)
        {
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0.5f, 0f);
            nameRect.anchorMax = new Vector2(0.5f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(width - 22f, 42f);
            nameRect.anchoredPosition = new Vector2(0f, 12f);

            nameText.fontSize = Mathf.Clamp(width * 0.18f, 18f, 28f);
        }

        if (costText != null)
        {
            RectTransform costRect = costText.rectTransform;
            costRect.anchorMin = new Vector2(1f, 1f);
            costRect.anchorMax = new Vector2(1f, 1f);
            costRect.pivot = new Vector2(1f, 1f);
            costRect.sizeDelta = new Vector2(width * 0.38f, 34f);
            costRect.anchoredPosition = new Vector2(-10f, -8f);

            costText.fontSize = Mathf.Clamp(width * 0.19f, 18f, 26f);
        }
    }

    private Button FindCardButton(string label)
    {
        if (buttonRow == null)
        {
            return null;
        }

        Transform cardTransform = FindDirectChild(buttonRow, label);
        return cardTransform != null ? cardTransform.GetComponent<Button>() : null;
    }

    private void TickMoney()
    {
        moneyCarry += moneyPerSecond * Time.deltaTime;
        int gained = Mathf.FloorToInt(moneyCarry);

        if (gained > 0)
        {
            money += gained;
            moneyCarry -= gained;
            RefreshMoneyUI();
        }
    }

    private void TickEnemySpawner()
    {
        enemySpawnTimer -= Time.deltaTime;
        if (enemySpawnTimer > 0f)
        {
            return;
        }

        SpawnRandomEnemy();
        enemySpawnTimer = Mathf.Max(0.3f, enemySpawnInterval);
    }

    private void HandleHotkeys()
    {
        if (battleEnded)
        {
            return;
        }

        for (int i = 0; i < spawnButtons.Count; i++)
        {
            SpawnButtonSpec spec = spawnButtons[i];
            if (Input.GetKeyDown(spec.definition.hotkey))
            {
                spec.action?.Invoke();
            }
        }
    }

    private void RefreshButtonStates()
    {
        for (int i = 0; i < spawnButtons.Count; i++)
        {
            SpawnButtonSpec spec = spawnButtons[i];
            if (spec.button == null)
            {
                continue;
            }

            spec.button.interactable = !battleEnded && money >= spec.definition.cost;
        }
    }

    private void RefreshMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = "$" + money;
        }
    }

    private void CheckBattleState()
    {
        ChessBase[] bases = FindObjectsByType<ChessBase>(FindObjectsSortMode.None);
        bool playerDead = false;
        bool enemyDead = false;

        for (int i = 0; i < bases.Length; i++)
        {
            ChessBase chessBase = bases[i];
            if (chessBase == null)
            {
                continue;
            }

            if (chessBase.team == Team.Player && chessBase.currentHp <= 0f)
            {
                playerDead = true;
            }

            if (chessBase.team == Team.Enemy && chessBase.currentHp <= 0f)
            {
                enemyDead = true;
            }
        }

        if (playerDead || enemyDead)
        {
            battleEnded = true;
        }
    }

    private void SpawnPlayerUnit(GameObject prefab, int cost)
    {
        if (!SpendMoney(cost))
        {
            return;
        }

        SpawnUnit(prefab, Team.Player);
    }

    private void SpawnEnemyUnit(GameObject prefab)
    {
        SpawnUnit(prefab, Team.Enemy);
    }

    private GameObject SpawnUnit(GameObject prefab, Team team)
    {
        if (prefab == null)
        {
            return null;
        }

        Vector3 spawnPosition = GetSpawnPosition(team);
        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity);

        ChessUnit unit = instance.GetComponent<ChessUnit>();
        if (unit != null)
        {
            unit.team = team;
        }

        return instance;
    }

    private Vector3 GetSpawnPosition(Team team)
    {
        Transform anchor = team == Team.Player ? playerSpawnPoint : enemySpawnPoint;
        Vector3 position = anchor != null ? anchor.position : Vector3.zero;
        float direction = team == Team.Player ? 1f : -1f;
        return position + new Vector3(direction * 0.85f, 0f, 0f);
    }

    private bool SpendMoney(int cost)
    {
        if (money < cost || battleEnded)
        {
            return false;
        }

        money -= cost;
        RefreshMoneyUI();
        RefreshButtonStates();
        return true;
    }

    private GameObject ChooseEnemyPrefab()
    {
        float progress = Mathf.Clamp01(battleClock / 90f);
        float roll = UnityEngine.Random.value;
        float pawnChance = Mathf.Lerp(0.52f, 0.28f, progress);
        float rookChance = Mathf.Lerp(0.72f, 0.52f, progress);
        float knightChance = Mathf.Lerp(0.86f, 0.68f, progress);
        float bishopChance = Mathf.Lerp(0.94f, 0.82f, progress);
        float queenChance = Mathf.Lerp(0.985f, 0.93f, progress);

        if (roll < pawnChance && enemyPawnPrefab != null)
        {
            return enemyPawnPrefab;
        }

        if (roll < rookChance && enemyRookPrefab != null)
        {
            return enemyRookPrefab;
        }

        if (roll < knightChance && enemyKnightPrefab != null)
        {
            return enemyKnightPrefab;
        }

        if (roll < bishopChance && enemyBishopPrefab != null)
        {
            return enemyBishopPrefab;
        }

        if (roll < queenChance && enemyQueenPrefab != null)
        {
            return enemyQueenPrefab;
        }

        if (enemyKingPrefab != null)
        {
            return enemyKingPrefab;
        }

        return enemyPawnPrefab;
    }
}
