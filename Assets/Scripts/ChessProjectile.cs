using UnityEngine;

public class ChessProjectile : MonoBehaviour
{
    private Team team;
    private float damage;
    private float healAmount;
    private float speed;
    private float lifetime = 4f;
    private float hitDistance = 0.16f;
    private ChessUnit targetUnit;
    private ChessBase targetBase;
    private Vector3 travelDirection;
    private Color projectileColor = Color.white;
    private bool initialized;
    private bool healMode;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Initialize(
        Team ownerTeam,
        float projectileDamage,
        float projectileSpeed,
        ChessUnit unitTarget,
        ChessBase baseTarget,
        Color color)
    {
        team = ownerTeam;
        damage = projectileDamage;
        healAmount = 0f;
        speed = projectileSpeed;
        targetUnit = unitTarget;
        targetBase = baseTarget;
        projectileColor = color;
        travelDirection = ownerTeam == Team.Player ? Vector3.right : Vector3.left;
        healMode = false;
        initialized = true;

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 26;

        transform.localScale = Vector3.one * 0.24f;

        ChessCombatFx.CreateAttachedGlow(
            transform,
            ChessCombatFx.GetDefaultSprite(),
            "ProjectileGlow",
            new Color(color.r, color.g, color.b, 0.28f),
            1.8f,
            25,
            0.05f,
            8f);

        ChessSpriteFx pulse = GetComponent<ChessSpriteFx>();
        if (pulse == null)
        {
            pulse = gameObject.AddComponent<ChessSpriteFx>();
        }

        pulse.ConfigurePersistent(color, 0.06f, 9f);
    }

    public void InitializeHeal(
        Team ownerTeam,
        float healValue,
        float projectileSpeed,
        ChessUnit allyTarget,
        Color color)
    {
        team = ownerTeam;
        damage = 0f;
        healAmount = healValue;
        speed = projectileSpeed;
        targetUnit = allyTarget;
        targetBase = null;
        projectileColor = color;
        travelDirection = ownerTeam == Team.Player ? Vector3.right : Vector3.left;
        healMode = true;
        initialized = true;

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 26;

        transform.localScale = Vector3.one * 0.24f;

        ChessCombatFx.CreateAttachedGlow(
            transform,
            ChessCombatFx.GetDefaultSprite(),
            "HealProjectileGlow",
            new Color(color.r, color.g, color.b, 0.28f),
            1.8f,
            25,
            0.05f,
            8f);

        ChessSpriteFx pulse = GetComponent<ChessSpriteFx>();
        if (pulse == null)
        {
            pulse = gameObject.AddComponent<ChessSpriteFx>();
        }

        pulse.ConfigurePersistent(color, 0.06f, 9f);
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = GetTargetPosition();

        if (targetUnit != null || targetBase != null)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPosition) <= hitDistance)
            {
                Impact();
            }

            return;
        }

        transform.position += travelDirection * (speed * Time.deltaTime);
    }

    private Vector3 GetTargetPosition()
    {
        if (targetUnit != null)
        {
            return targetUnit.transform.position;
        }

        if (targetBase != null)
        {
            return targetBase.transform.position;
        }

        return transform.position + travelDirection;
    }

    private void Impact()
    {
        Vector3 impactPosition = transform.position;

        if (healMode)
        {
            if (targetUnit != null)
            {
                targetUnit.Heal(healAmount);
                impactPosition = targetUnit.transform.position;
            }
        }
        else if (targetUnit != null)
        {
            targetUnit.TakeDamage(damage);
            impactPosition = targetUnit.transform.position;
        }
        else if (targetBase != null)
        {
            targetBase.TakeDamage(damage);
            impactPosition = targetBase.transform.position;
        }

        ChessCombatFx.SpawnPulse(impactPosition, projectileColor, 0.12f, 0.62f, 0.25f, 27);
        Destroy(gameObject);
    }
}
