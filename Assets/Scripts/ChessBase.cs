using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChessBase : MonoBehaviour
{
    public static readonly List<ChessBase> ActiveBases = new List<ChessBase>();

    [Header("Team")]
    public Team team = Team.Player;

    [Header("Health")]
    public float maxHp = 1000f;
    public float currentHp;

    [Header("Visuals")]
    public Sprite playerBaseSprite;
    public Sprite enemyBaseSprite;
    public SpriteRenderer spriteRenderer;

    [Header("UI")]
    public Slider hpSlider;

    private bool isBroken;
    private DamageFlashFx damageFlashFx;
    private BoxCollider2D baseCollider;

    private void Awake()
    {
        DisableLegacySlider();
        RemoveRuntimeFxChildren();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        LoadDefaultBaseSprites();

        baseCollider = GetComponent<BoxCollider2D>();
        if (baseCollider == null)
        {
            baseCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        currentHp = maxHp;
        isBroken = false;
        RefreshVisual();
        UpdateUI();

        damageFlashFx = ChessCombatFx.AttachDamageFlash(transform);
        ChessCombatFx.AttachHealthBar(
            transform,
            () => currentHp,
            () => maxHp,
            new Vector3(0f, 2.03f, 0f),
            2.4f,
            0.21f);

        UpdateColliderShape();
    }

    private void OnEnable()
    {
        if (!ActiveBases.Contains(this))
        {
            ActiveBases.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveBases.Remove(this);
    }

    public bool IsAlive
    {
        get { return !isBroken && currentHp > 0f; }
    }

    public void SetTeam(Team newTeam)
    {
        team = newTeam;
        LoadDefaultBaseSprites();
        RefreshVisual();
    }

    public void ResetHp(float hpValue)
    {
        SetHp(hpValue, hpValue);
    }

    public void SetHp(float maxHpValue, float currentHpValue)
    {
        maxHp = Mathf.Max(1f, maxHpValue);
        currentHp = Mathf.Clamp(currentHpValue, 0f, maxHp);
        isBroken = currentHp <= 0f;
        UpdateUI();
    }

    public void HealToFull()
    {
        currentHp = maxHp;
        isBroken = false;
        UpdateUI();
    }

    public float GetCombatHalfWidth()
    {
        if (baseCollider != null)
        {
            return Mathf.Clamp(baseCollider.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f, 0.35f, 0.85f);
        }

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return Mathf.Clamp(spriteRenderer.bounds.extents.x, 0.35f, 0.85f);
        }

        return 0.65f;
    }

    public void TakeDamage(float damage)
    {
        if (isBroken)
        {
            return;
        }

        currentHp -= damage;
        currentHp = Mathf.Max(currentHp, 0f);

        if (damageFlashFx == null)
        {
            damageFlashFx = ChessCombatFx.AttachDamageFlash(transform);
        }

        if (damageFlashFx != null)
        {
            damageFlashFx.Flash();
        }

        ChessCombatFx.SpawnBaseImpact(transform.position, team);
        ChessCombatFx.SpawnFloatingText(
            "-" + Mathf.CeilToInt(damage),
            transform.position + new Vector3(0f, 1.05f, 0f),
            new Color(1f, 0.28f, 0.22f, 1f),
            1.23f,
            1.53f,
            0.65f,
            0.42f,
            130);

        UpdateUI();

        if (currentHp <= 0f)
        {
            isBroken = true;
            ChessCombatFx.SpawnBaseCaptureBurst(transform.position);

            if (ChessGameManager.Instance != null)
            {
                ChessGameManager.Instance.HandleBaseDestroyed(this);
            }
            else if (team == Team.Player)
            {
                Debug.Log("You Lose!");
            }
            else
            {
                Debug.Log("You Win!");
            }
        }
    }

    private void RemoveRuntimeFxChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != "DamageFlashOverlay")
            {
                continue;
            }

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (team == Team.Player && playerBaseSprite != null)
        {
            spriteRenderer.sprite = playerBaseSprite;
        }
        else if (team == Team.Enemy && enemyBaseSprite != null)
        {
            spriteRenderer.sprite = enemyBaseSprite;
        }

        if (damageFlashFx != null)
        {
            damageFlashFx.RefreshSourceSprite();
        }

        UpdateColliderShape();
    }

    private void LoadDefaultBaseSprites()
    {
        if (playerBaseSprite == null)
        {
            playerBaseSprite = Resources.Load<Sprite>("PlayerBase");
        }

        if (enemyBaseSprite == null)
        {
            enemyBaseSprite = Resources.Load<Sprite>("EnemyBase");
        }

#if UNITY_EDITOR
        if (playerBaseSprite == null)
        {
            playerBaseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/PlayerBase.png");
        }

        if (enemyBaseSprite == null)
        {
            enemyBaseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Image/EnemyBase.png");
        }
#endif
    }

    private void UpdateColliderShape()
    {
        if (baseCollider == null)
        {
            return;
        }

        Sprite baseSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        if (baseSprite != null)
        {
            baseCollider.size = new Vector2(
                Mathf.Max(1.2f, baseSprite.bounds.size.x * 0.9f),
                Mathf.Max(1.2f, baseSprite.bounds.size.y * 0.9f));
        }
        else
        {
            baseCollider.size = new Vector2(1.8f, 1.8f);
        }

        baseCollider.offset = Vector2.zero;
        baseCollider.isTrigger = true;
    }

    private void UpdateUI()
    {
        DisableLegacySlider();
    }

    private void DisableLegacySlider()
    {
        if (hpSlider == null)
        {
            return;
        }

        hpSlider.gameObject.SetActive(false);
        hpSlider = null;
    }
}
