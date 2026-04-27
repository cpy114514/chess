using UnityEngine;
using UnityEngine.UI;

public class ChessBase : MonoBehaviour
{
    public Team team;
    public float maxHp = 1000f;
    public float currentHp;

    public Slider hpSlider;
    private DamageFlashFx damageFlashFx;

    private void Awake()
    {
        currentHp = maxHp;
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

    public void TakeDamage(float damage)
    {
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
            if (team == Team.Player)
            {
                Debug.Log("You Lose!");
            }
            else
            {
                Debug.Log("You Win!");
            }
        }
    }

    private void UpdateUI()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHp / maxHp;
        }
    }
}
