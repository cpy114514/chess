using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class ChessCombatFx
{
    private static Sprite defaultSprite;
    private static readonly Dictionary<Transform, CombatHealthBar> activeHealthBars = new Dictionary<Transform, CombatHealthBar>();

    public static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null)
        {
            return defaultSprite;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.name = "ChessVfxTexture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(new[]
        {
            Color.white,
            Color.white,
            Color.white,
            Color.white
        });
        texture.Apply();

        defaultSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        defaultSprite.name = "ChessVfxSprite";
        return defaultSprite;
    }

    public static Color TeamAccent(Team team)
    {
        return team == Team.Player
            ? new Color(0.34f, 0.85f, 1f, 1f)
            : new Color(1f, 0.44f, 0.48f, 1f);
    }

    public static Color KingBuffColor()
    {
        return new Color(1f, 0.84f, 0.28f, 0.55f);
    }

    public static Color HealColor()
    {
        return new Color(0.45f, 1f, 0.62f, 0.8f);
    }

    public static DamageFlashFx AttachDamageFlash(Transform host)
    {
        if (host == null)
        {
            return null;
        }

        DamageFlashFx flash = host.GetComponent<DamageFlashFx>();
        if (flash == null)
        {
            flash = host.gameObject.AddComponent<DamageFlashFx>();
        }

        return flash;
    }

    public static CombatHealthBar AttachHealthBar(
        Transform host,
        Func<float> currentGetter,
        Func<float> maxGetter,
        Vector3 offset,
        float width,
        float height)
    {
        if (host == null)
        {
            return null;
        }

        if (activeHealthBars.TryGetValue(host, out CombatHealthBar existingBar) && existingBar != null)
        {
            existingBar.Initialize(host, currentGetter, maxGetter, offset, width, height);
            return existingBar;
        }

        GameObject go = new GameObject(host.name + "_HealthBar");
        go.transform.position = host.position + offset;

        CombatHealthBar bar = go.AddComponent<CombatHealthBar>();
        activeHealthBars[host] = bar;
        bar.Initialize(host, currentGetter, maxGetter, offset, width, height);
        return bar;
    }

    public static void ReleaseHealthBar(Transform host, CombatHealthBar bar)
    {
        if (host == null)
        {
            return;
        }

        if (activeHealthBars.TryGetValue(host, out CombatHealthBar existingBar) && existingBar == bar)
        {
            activeHealthBars.Remove(host);
        }
    }

    public static SpriteRenderer CreateAttachedGlow(
        Transform parent,
        Sprite sourceSprite,
        string name,
        Color color,
        float scaleMultiplier,
        int sortingOrder,
        float pulseAmount,
        float pulseSpeed)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scaleMultiplier;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sourceSprite != null ? sourceSprite : GetDefaultSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        ChessSpriteFx fx = go.AddComponent<ChessSpriteFx>();
        fx.ConfigurePersistent(color, pulseAmount, pulseSpeed);

        return renderer;
    }

    public static ChessSpriteFx SpawnPulse(
        Vector3 position,
        Color color,
        float startScale,
        float endScale,
        float duration,
        int sortingOrder = 25)
    {
        return SpawnPulse(position, null, color, startScale, endScale, duration, sortingOrder, 0.12f, 6.5f);
    }

    public static ChessSpriteFx SpawnPulse(
        Vector3 position,
        Sprite sprite,
        Color color,
        float startScale,
        float endScale,
        float duration,
        int sortingOrder,
        float pulseAmount,
        float pulseSpeed)
    {
        GameObject go = new GameObject("PulseFx");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * startScale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : GetDefaultSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        ChessSpriteFx fx = go.AddComponent<ChessSpriteFx>();
        fx.ConfigureTimed(color, startScale, endScale, duration, pulseAmount, pulseSpeed);
        return fx;
    }

    public static void SpawnUnitSpawnBurst(Vector3 center, Team team)
    {
        Color accent = TeamAccent(team);
        SpawnPulse(center + new Vector3(0f, 0.05f, 0f), new Color(accent.r, accent.g, accent.b, 0.42f), 0.24f, 1.35f, 0.24f, 22);
        SpawnSparkBurst(center, accent, 7, 0.18f, 0.54f, 0.34f, 24, 35f, 145f);
    }

    public static void SpawnHitSpark(Vector3 center, Team damagedTeam)
    {
        Color color = damagedTeam == Team.Player
            ? new Color(1f, 0.46f, 0.38f, 0.9f)
            : new Color(1f, 0.62f, 0.2f, 0.9f);

        SpawnPulse(center + new Vector3(0f, 0.18f, 0f), color, 0.16f, 0.72f, 0.16f, 32);
        SpawnSparkBurst(center + new Vector3(0f, 0.2f, 0f), color, 5, 0.16f, 0.62f, 0.24f, 34, 45f, 135f);
    }

    public static void SpawnDeathBurst(Vector3 center, Team team)
    {
        Color accent = TeamAccent(team);
        Color smoke = new Color(0.92f, 0.86f, 0.74f, 0.55f);
        SpawnPulse(center + new Vector3(0f, 0.18f, 0f), smoke, 0.36f, 1.55f, 0.32f, 29);
        SpawnSparkBurst(center + new Vector3(0f, 0.2f, 0f), accent, 11, 0.22f, 0.92f, 0.46f, 36, 15f, 165f);
    }

    public static void SpawnBaseImpact(Vector3 center, Team damagedTeam)
    {
        Color color = damagedTeam == Team.Player
            ? new Color(1f, 0.35f, 0.28f, 0.88f)
            : new Color(1f, 0.68f, 0.22f, 0.88f);

        SpawnPulse(center + new Vector3(0f, 0.42f, 0f), color, 0.28f, 1.25f, 0.2f, 35);
        SpawnSparkBurst(center + new Vector3(0f, 0.46f, 0f), color, 9, 0.22f, 0.86f, 0.32f, 38, 35f, 145f);
    }

    public static void SpawnBaseCaptureBurst(Vector3 center)
    {
        Color gold = new Color(1f, 0.82f, 0.28f, 0.92f);
        Color blue = new Color(0.34f, 0.85f, 1f, 0.78f);
        SpawnPulse(center + new Vector3(0f, 0.4f, 0f), gold, 0.55f, 2.45f, 0.52f, 42);
        SpawnPulse(center + new Vector3(0f, 0.4f, 0f), blue, 0.35f, 1.75f, 0.42f, 43);
        SpawnSparkBurst(center + new Vector3(0f, 0.5f, 0f), gold, 16, 0.28f, 1.08f, 0.62f, 45, 20f, 160f);
    }

    private static void SpawnSparkBurst(
        Vector3 center,
        Color color,
        int count,
        float minScale,
        float maxScale,
        float duration,
        int sortingOrder,
        float minAngle,
        float maxAngle)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.Range(minAngle, maxAngle) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(1.0f, 2.6f);
            Vector3 velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed;
            float scale = UnityEngine.Random.Range(minScale, maxScale);
            SpawnSpark(center, color, velocity, scale, duration * UnityEngine.Random.Range(0.78f, 1.18f), sortingOrder);
        }
    }

    private static void SpawnSpark(Vector3 position, Color color, Vector3 velocity, float scale, float duration, int sortingOrder)
    {
        GameObject go = new GameObject("SparkFx");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetDefaultSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        ChessSparkFx spark = go.AddComponent<ChessSparkFx>();
        spark.Configure(color, velocity, scale, duration);
    }

    public static void SpawnHealingPlusBurst(Vector3 center, int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-0.24f, 0.24f),
                UnityEngine.Random.Range(0.12f, 0.33f) + (i * 0.09f),
                0f);

            float scale = UnityEngine.Random.Range(1.28f, 1.65f);
            float riseSpeed = UnityEngine.Random.Range(0.48f, 0.72f);
            float duration = UnityEngine.Random.Range(0.7f, 0.95f);

            SpawnFloatingText(
                "+",
                center + offset,
                HealColor(),
                scale,
                scale * 1.08f,
                duration,
                riseSpeed,
                31);
        }
    }

    public static ChessFloatingTextFx SpawnFloatingText(
        string text,
        Vector3 position,
        Color color,
        float startScale,
        float endScale,
        float duration,
        float riseSpeed,
        int sortingOrder)
    {
        GameObject go = new GameObject("FloatingTextFx");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * startScale;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.richText = false;
        tmp.fontStyle = FontStyles.Bold;

        MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = sortingOrder;
        }

        ChessFloatingTextFx fx = go.AddComponent<ChessFloatingTextFx>();
        fx.Configure(text, color, startScale, endScale, duration, riseSpeed, sortingOrder);
        return fx;
    }
}

public class ChessSpriteFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale = Vector3.one;
    private float age;
    private float duration = -1f;
    private float startScale = 1f;
    private float endScale = 1f;
    private float pulseAmount;
    private float pulseSpeed = 4f;
    private bool persistent;
    private bool fade;
    private float fadeOutStartAlpha = 1f;
    private float fadeOutEndAlpha = 0f;
    private Color initialColor = Color.white;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;

        if (spriteRenderer != null)
        {
            initialColor = spriteRenderer.color;
        }
    }

    public void ConfigurePersistent(Color color, float pulseAmount, float pulseSpeed)
    {
        EnsureSpriteRenderer();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            initialColor = color;
        }

        this.pulseAmount = pulseAmount;
        this.pulseSpeed = pulseSpeed;
        persistent = true;
        fade = false;
        duration = -1f;
        baseScale = transform.localScale;
    }

    public void ConfigureTimed(Color color, float startScale, float endScale, float duration, float pulseAmount, float pulseSpeed)
    {
        EnsureSpriteRenderer();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            initialColor = color;
        }

        this.startScale = startScale;
        this.endScale = endScale;
        this.duration = duration;
        this.pulseAmount = pulseAmount;
        this.pulseSpeed = pulseSpeed;
        persistent = false;
        fade = true;
        fadeOutStartAlpha = color.a;
        fadeOutEndAlpha = 0f;
        age = 0f;
        transform.localScale = Vector3.one * startScale;
        baseScale = transform.localScale;
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        if (persistent)
        {
            transform.localScale = baseScale * pulse;
            return;
        }

        age += Time.deltaTime;
        float t = duration <= 0f ? 1f : Mathf.Clamp01(age / duration);
        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = Vector3.one * scale * pulse;

        if (fade)
        {
            Color color = initialColor;
            color.a = Mathf.Lerp(fadeOutStartAlpha, fadeOutEndAlpha, t);
            spriteRenderer.color = color;
        }

        if (duration > 0f && age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureSpriteRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
}

public class ChessFloatingTextFx : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Color baseColor;
    private float age;
    private float duration = 0.8f;
    private float startScale = 1f;
    private float endScale = 1.1f;
    private Vector3 drift;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Configure(
        string text,
        Color color,
        float startScale,
        float endScale,
        float duration,
        float riseSpeed,
        int sortingOrder)
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
        }

        if (textMesh != null)
        {
            textMesh.text = text;
            textMesh.color = color;
            textMesh.fontSize = 5f;
            textMesh.alignment = TextAlignmentOptions.Center;

            MeshRenderer meshRenderer = textMesh.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = sortingOrder;
            }
        }

        baseColor = color;
        this.startScale = startScale;
        this.endScale = endScale;
        this.duration = duration;
        drift = CreateRandomFlyDirection(riseSpeed);
        transform.localScale = Vector3.one * startScale;
    }

    private Vector3 CreateRandomFlyDirection(float speed)
    {
        float angle = UnityEngine.Random.Range(68f, 112f) * Mathf.Deg2Rad;
        float speedVariance = UnityEngine.Random.Range(0.88f, 1.16f);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed * speedVariance;
    }

    private void Update()
    {
        age += Time.deltaTime;

        float t = duration <= 0f ? 1f : Mathf.Clamp01(age / duration);
        transform.position += drift * Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

        if (textMesh != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(baseColor.a, 0f, t);
            textMesh.color = color;
        }

        if (duration > 0f && age >= duration)
        {
            Destroy(gameObject);
        }
    }
}

public class ChessSparkFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private Vector3 velocity;
    private float age;
    private float duration = 0.35f;
    private float baseScale = 1f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Configure(Color color, Vector3 velocity, float scale, float duration)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        baseColor = color;
        this.velocity = velocity;
        baseScale = Mathf.Max(0.01f, scale);
        this.duration = Mathf.Max(0.05f, duration);
        transform.localScale = Vector3.one * baseScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);

        transform.position += velocity * Time.deltaTime;
        velocity = Vector3.Lerp(velocity, Vector3.down * 0.24f, Time.deltaTime * 2.2f);
        transform.localScale = Vector3.one * Mathf.Lerp(baseScale, baseScale * 0.25f, t);

        if (spriteRenderer != null)
        {
            Color color = baseColor;
            color.a = Mathf.Lerp(baseColor.a, 0f, t);
            spriteRenderer.color = color;
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }
}

public class DamageFlashFx : MonoBehaviour
{
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer flashRenderer;
    private float timer;
    private float duration = 0.12f;
    private float intensity = 0.7f;

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        EnsureOverlay();
    }

    public void Flash(float flashDuration = 0.12f, float flashIntensity = 0.7f)
    {
        duration = Mathf.Max(0.05f, flashDuration);
        intensity = Mathf.Clamp01(flashIntensity);
        timer = duration;

        EnsureOverlay();
        if (flashRenderer != null)
        {
            flashRenderer.gameObject.SetActive(true);
        }
    }

    public void RefreshSourceSprite()
    {
        EnsureOverlay();
        UpdateOverlayRenderer();
    }

    private void Update()
    {
        if (flashRenderer == null)
        {
            return;
        }

        if (timer <= 0f)
        {
            if (flashRenderer.gameObject.activeSelf)
            {
                flashRenderer.gameObject.SetActive(false);
            }

            return;
        }

        timer -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(timer / duration);
        float alpha = Mathf.Lerp(intensity, 0f, t);
        flashRenderer.color = new Color(1f, 0.14f, 0.14f, alpha);

        if (timer <= 0f)
        {
            flashRenderer.gameObject.SetActive(false);
        }
    }

    private void EnsureOverlay()
    {
        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        if (flashRenderer != null)
        {
            UpdateOverlayRenderer();
            return;
        }

        GameObject overlay = new GameObject("DamageFlashOverlay");
        overlay.transform.SetParent(transform, false);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localScale = Vector3.one * 1.02f;

        flashRenderer = overlay.AddComponent<SpriteRenderer>();
        flashRenderer.color = new Color(1f, 0.14f, 0.14f, 0f);
        UpdateOverlayRenderer();
        overlay.SetActive(false);
    }

    private void UpdateOverlayRenderer()
    {
        if (flashRenderer == null)
        {
            return;
        }

        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }

        flashRenderer.sprite = sourceRenderer != null && sourceRenderer.sprite != null
            ? sourceRenderer.sprite
            : ChessCombatFx.GetDefaultSprite();
        flashRenderer.sortingOrder = (sourceRenderer != null ? sourceRenderer.sortingOrder : 0) + 12;
        flashRenderer.flipX = sourceRenderer != null && sourceRenderer.flipX;
        flashRenderer.flipY = sourceRenderer != null && sourceRenderer.flipY;
    }
}

public class CombatHealthBar : MonoBehaviour
{
    private Transform target;
    private Func<float> currentGetter;
    private Func<float> maxGetter;
    private Vector3 worldOffset;
    private float width;
    private float height;
    private SpriteRenderer backgroundRenderer;
    private SpriteRenderer fillRenderer;
    private Transform registeredTarget;

    public void Initialize(
        Transform target,
        Func<float> currentGetter,
        Func<float> maxGetter,
        Vector3 offset,
        float width,
        float height)
    {
        if (registeredTarget != null && registeredTarget != target)
        {
            ChessCombatFx.ReleaseHealthBar(registeredTarget, this);
        }

        this.target = target;
        registeredTarget = target;
        this.currentGetter = currentGetter;
        this.maxGetter = maxGetter;
        worldOffset = offset;
        this.width = width;
        this.height = height;

        if (fillRenderer == null)
        {
            CreateVisuals();
        }

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        ChessCombatFx.ReleaseHealthBar(registeredTarget, this);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + worldOffset;
        UpdateVisuals();
    }

    private void CreateVisuals()
    {
        GameObject background = new GameObject("Background");
        background.transform.SetParent(transform, false);
        background.transform.localPosition = Vector3.zero;
        background.transform.localScale = new Vector3(width, height, 1f);

        backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        backgroundRenderer.color = new Color(0f, 0f, 0f, 0.34f);
        backgroundRenderer.sortingOrder = 120;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = Vector3.zero;
        fill.transform.localScale = new Vector3(width * 0.96f, height * 0.62f, 1f);

        fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        fillRenderer.color = Color.green;
        fillRenderer.sortingOrder = 121;

    }

    private void UpdateVisuals()
    {
        if (fillRenderer == null)
        {
            return;
        }

        float max = maxGetter != null ? maxGetter.Invoke() : 1f;
        float current = currentGetter != null ? currentGetter.Invoke() : 0f;
        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        int lowHealthPriority = Mathf.RoundToInt((1f - ratio) * 80f);
        int sortBase = 120 + lowHealthPriority * 3;

        if (backgroundRenderer != null)
        {
            backgroundRenderer.sortingOrder = sortBase;
        }

        fillRenderer.sortingOrder = sortBase + 1;

        if (ratio <= 0.001f)
        {
            fillRenderer.gameObject.SetActive(false);
            return;
        }

        if (!fillRenderer.gameObject.activeSelf)
        {
            fillRenderer.gameObject.SetActive(true);
        }

        float fillWidth = width * 0.96f * ratio;
        fillRenderer.transform.localScale = new Vector3(fillWidth, height * 0.62f, 1f);
        fillRenderer.transform.localPosition = new Vector3(((ratio - 1f) * width * 0.48f), 0f, 0f);
        fillRenderer.color = Color.Lerp(
            new Color(1f, 0.22f, 0.22f, 0.95f),
            new Color(0.25f, 0.95f, 0.35f, 0.95f),
            ratio);
    }
}
