using UnityEngine;
using UnityEngine.UI;

public class ChessBase : MonoBehaviour
{
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

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
            new Vector3(0f, 1.35f, 0f),
            1.6f,
            0.14f);
    }

    public void SetTeam(Team newTeam)
    {
        team = newTeam;
        RefreshVisual();
    }

    public void ResetHp(float hpValue)
    {
        maxHp = Mathf.Max(1f, hpValue);
        currentHp = maxHp;
        isBroken = false;
        UpdateUI();
    }

    public void HealToFull()
    {
        currentHp = maxHp;
        isBroken = false;
        UpdateUI();
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

        UpdateUI();

        if (currentHp <= 0f)
        {
            isBroken = true;

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
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHp / Mathf.Max(1f, maxHp);
        }
    }
}
