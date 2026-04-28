using UnityEngine;

public class ChessUnit : MonoBehaviour
{
    [Header("Team")]
    public Team team = Team.Player;

    [Header("Stats")]
    public string unitName = "Pawn";
    public float maxHp = 100f;
    public float attackDamage = 20f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1f;
    public float moveSpeed = 1.5f;

    [Header("Runtime")]
    public float currentHp;

    private const float KingAuraRadius = 3.2f;
    private const float KingAttackSpeedMultiplier = 1.35f;
    private const float KingMoveSpeedMultiplier = 1.25f;
    private const float BishopProjectileSpeed = 8.5f;

    private float attackTimer;
    private float healTimer;
    private float moveSpeedMultiplier = 1f;
    private float attackCooldownMultiplier = 1f;
    private ChessUnit targetUnit;
    private ChessBase targetBase;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraGlowRenderer;
    private DamageFlashFx damageFlashFx;
    private Vector3 baseLocalScale;
    private Color baseSpriteColor = Color.white;
    private float walkPhase;
    private float attackAnimTimer;
    private const float AttackAnimDuration = 0.18f;
    private Color attackFlashColor = Color.white;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        ApplyUnitDefaults();
        currentHp = maxHp;
        SetupVisualEffects();
        damageFlashFx = ChessCombatFx.AttachDamageFlash(transform);
        ChessCombatFx.AttachHealthBar(
            transform,
            () => currentHp,
            () => maxHp,
            new Vector3(0f, 1.05f, 0f),
            0.95f,
            0.12f);
        baseLocalScale = transform.localScale;
        baseSpriteColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        walkPhase = Random.value * 10f;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyUnitDefaults();
        maxHp = Mathf.Max(1f, maxHp);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
    }

    public void Initialize(Team newTeam, float hpMultiplier = 1f, float damageMultiplier = 1f, float speedMultiplier = 1f)
    {
        team = newTeam;
        maxHp = Mathf.Max(1f, maxHp * hpMultiplier);
        attackDamage *= damageMultiplier;
        moveSpeed *= speedMultiplier;
        currentHp = maxHp;
        attackTimer = 0f;
        healTimer = 0f;
    }

    private void Update()
    {
        if (ChessGameManager.Instance != null && !ChessGameManager.Instance.IsPlaying)
        {
            StopMoving();
            return;
        }

        attackTimer -= Time.deltaTime;
        healTimer -= Time.deltaTime;
        attackAnimTimer = Mathf.Max(0f, attackAnimTimer - Time.deltaTime);

        bool auraActive = RefreshKingAuraModifiers();
        UpdateAuraGlow(auraActive);

        bool isMoving = true;

        FindTargetInFront();

        if (targetUnit != null)
        {
            float distance = Mathf.Abs(targetUnit.transform.position.x - transform.position.x);

            if (distance <= attackRange)
            {
                StopMoving();
                isMoving = false;

                if (!IsQueen())
                {
                    TryAttackUnit();
                }
            }
            else
            {
                MoveForward();
            }
        }
        else if (targetBase != null)
        {
            float distance = Mathf.Abs(targetBase.transform.position.x - transform.position.x);

            if (distance <= attackRange)
            {
                StopMoving();
                isMoving = false;

                if (!IsQueen())
                {
                    TryAttackBase();
                }
            }
            else
            {
                MoveForward();
            }
        }
        else
        {
            MoveForward();
        }

        if (IsQueen())
        {
            TryHealAllies();
        }

        UpdateMotionAnimation(isMoving);
    }

    private void MoveForward()
    {
        float direction = team == Team.Player ? 1f : -1f;
        rb.velocity = new Vector2(direction * moveSpeed * moveSpeedMultiplier, 0f);
    }

    private void StopMoving()
    {
        rb.velocity = Vector2.zero;
    }

    private void FindTargetInFront()
    {
        targetUnit = null;
        targetBase = null;

        float direction = team == Team.Player ? 1f : -1f;
        float closestDistance = Mathf.Infinity;

        ChessUnit[] allUnits = FindObjectsByType<ChessUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < allUnits.Length; i++)
        {
            ChessUnit unit = allUnits[i];
            if (unit == null || unit == this || unit.team == team)
            {
                continue;
            }

            float xDifference = unit.transform.position.x - transform.position.x;
            if (direction > 0f && xDifference <= 0f)
            {
                continue;
            }

            if (direction < 0f && xDifference >= 0f)
            {
                continue;
            }

            float distance = Mathf.Abs(xDifference);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                targetUnit = unit;
            }
        }

        if (targetUnit != null)
        {
            return;
        }

        ChessBase[] allBases = FindObjectsByType<ChessBase>(FindObjectsSortMode.None);

        for (int i = 0; i < allBases.Length; i++)
        {
            ChessBase chessBase = allBases[i];
            if (chessBase == null || chessBase.team == team)
            {
                continue;
            }

            float xDifference = chessBase.transform.position.x - transform.position.x;
            if (direction > 0f && xDifference <= 0f)
            {
                continue;
            }

            if (direction < 0f && xDifference >= 0f)
            {
                continue;
            }

            targetBase = chessBase;
            return;
        }
    }

    private void TryAttackUnit()
    {
        if (attackTimer > 0f)
        {
            return;
        }

        if (targetUnit == null)
        {
            return;
        }

        attackTimer = attackCooldown * attackCooldownMultiplier;
        TriggerAttackAnimation(ChessCombatFx.TeamAccent(team));

        if (IsBishop())
        {
            FireBishopProjectile(targetUnit, null);
            return;
        }

        targetUnit.TakeDamage(attackDamage);
    }

    private void TryAttackBase()
    {
        if (attackTimer > 0f)
        {
            return;
        }

        if (targetBase == null)
        {
            return;
        }

        attackTimer = attackCooldown * attackCooldownMultiplier;
        TriggerAttackAnimation(ChessCombatFx.TeamAccent(team));

        if (IsBishop())
        {
            FireBishopProjectile(null, targetBase);
            return;
        }

        targetBase.TakeDamage(attackDamage);
    }

    private void TryHealAllies()
    {
        if (healTimer > 0f)
        {
            return;
        }

        ChessUnit bestTarget = null;
        float bestHealthRatio = 1f;

        ChessUnit[] allUnits = FindObjectsByType<ChessUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < allUnits.Length; i++)
        {
            ChessUnit unit = allUnits[i];
            if (unit == null || unit.team != team)
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, unit.transform.position);
            if (distance > attackRange)
            {
                continue;
            }

            if (unit.currentHp >= unit.maxHp)
            {
                continue;
            }

            float healthRatio = unit.currentHp / Mathf.Max(1f, unit.maxHp);
            if (healthRatio >= bestHealthRatio)
            {
                continue;
            }

            bestHealthRatio = healthRatio;
            bestTarget = unit;
        }

        if (bestTarget == null && currentHp < maxHp)
        {
            bestTarget = this;
        }

        if (bestTarget != null)
        {
            healTimer = attackCooldown * attackCooldownMultiplier;
            TriggerAttackAnimation(ChessCombatFx.HealColor());
            ChessCombatFx.SpawnHealingPlusBurst(bestTarget.transform.position + new Vector3(0f, 0.28f, 0f));
        }
    }

    private void TriggerAttackAnimation(Color flashColor)
    {
        attackAnimTimer = AttackAnimDuration;
        attackFlashColor = flashColor;
    }

    private void UpdateMotionAnimation(bool isMoving)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float attackT = attackAnimTimer <= 0f ? 0f : 1f - (attackAnimTimer / AttackAnimDuration);
        float attackWave = attackAnimTimer <= 0f ? 0f : Mathf.Sin(attackT * Mathf.PI);
        float walkWave = isMoving ? Mathf.Sin((Time.time * 10f) + walkPhase) * 0.025f : 0f;

        Vector3 scale = baseLocalScale;
        float squash = 1f + attackWave * 0.12f;
        scale.x *= squash;
        scale.y *= (1f - attackWave * 0.06f) + walkWave;
        transform.localScale = scale;

        if (attackAnimTimer > 0f)
        {
            float flash = 0.45f + attackWave * 0.35f;
            spriteRenderer.color = Color.Lerp(baseSpriteColor, attackFlashColor, flash);
        }
        else
        {
            spriteRenderer.color = baseSpriteColor;
        }
    }

    private bool RefreshKingAuraModifiers()
    {
        bool auraActive = HasFriendlyKingAura();
        moveSpeedMultiplier = auraActive ? KingMoveSpeedMultiplier : 1f;
        attackCooldownMultiplier = auraActive ? 1f / KingAttackSpeedMultiplier : 1f;

        if (IsKing())
        {
            moveSpeedMultiplier = KingMoveSpeedMultiplier;
            attackCooldownMultiplier = 1f / KingAttackSpeedMultiplier;
            return true;
        }

        return auraActive;
    }

    private bool HasFriendlyKingAura()
    {
        ChessUnit[] allUnits = FindObjectsByType<ChessUnit>(FindObjectsSortMode.None);

        for (int i = 0; i < allUnits.Length; i++)
        {
            ChessUnit unit = allUnits[i];
            if (unit == null || unit.team != team || !unit.IsKing())
            {
                continue;
            }

            float distance = Vector2.Distance(transform.position, unit.transform.position);
            if (distance <= KingAuraRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateAuraGlow(bool auraActive)
    {
        if (auraGlowRenderer == null)
        {
            return;
        }

        bool shouldShow = IsKing() || auraActive;
        if (auraGlowRenderer.gameObject.activeSelf != shouldShow)
        {
            auraGlowRenderer.gameObject.SetActive(shouldShow);
        }
    }

    private bool IsQueen()
    {
        return unitName == "Queen";
    }

    private bool IsKing()
    {
        return unitName == "King";
    }

    private bool IsBishop()
    {
        return unitName == "Bishop";
    }

    private void ApplyUnitDefaults()
    {
        switch (unitName)
        {
            case "Pawn":
                maxHp = 90f;
                attackDamage = 14f;
                attackRange = 1.15f;
                attackCooldown = 0.9f;
                moveSpeed = 1.65f;
                break;
            case "Rook":
                maxHp = 260f;
                attackDamage = 18f;
                attackRange = 1.35f;
                attackCooldown = 1.15f;
                moveSpeed = 0.9f;
                break;
            case "Knight":
                maxHp = 150f;
                attackDamage = 21f;
                attackRange = 1.2f;
                attackCooldown = 0.95f;
                moveSpeed = 2.05f;
                break;
            case "Bishop":
                maxHp = 115f;
                attackDamage = 15f;
                attackRange = 4.5f;
                attackCooldown = 1.25f;
                moveSpeed = 1.25f;
                break;
            case "Queen":
                maxHp = 135f;
                attackDamage = 18f;
                attackRange = 4.8f;
                attackCooldown = 1.0f;
                moveSpeed = 1.15f;
                break;
            case "King":
                maxHp = 280f;
                attackDamage = 34f;
                attackRange = 1.35f;
                attackCooldown = 0.85f;
                moveSpeed = 1.3f;
                break;
        }
    }

    private void SetupVisualEffects()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        }

        Sprite sourceSprite = spriteRenderer.sprite != null ? spriteRenderer.sprite : ChessCombatFx.GetDefaultSprite();
        int glowOrder = spriteRenderer.sortingOrder - 1;

        if (IsKing())
        {
            auraGlowRenderer = ChessCombatFx.CreateAttachedGlow(
                transform,
                sourceSprite,
                "KingAuraGlow",
                new Color(1f, 0.84f, 0.28f, 0.58f),
                1.28f,
                glowOrder,
                0.08f,
                3.5f);
        }
        else
        {
            auraGlowRenderer = ChessCombatFx.CreateAttachedGlow(
                transform,
                sourceSprite,
                "BuffAuraGlow",
                new Color(1f, 0.94f, 0.45f, 0.34f),
                1.12f,
                glowOrder,
                0.05f,
                4.5f);

            if (auraGlowRenderer != null)
            {
                auraGlowRenderer.gameObject.SetActive(false);
            }
        }
    }

    private void FireBishopProjectile(ChessUnit unitTarget, ChessBase baseTarget)
    {
        if (!IsBishop())
        {
            return;
        }

        float direction = team == Team.Player ? 1f : -1f;
        Vector3 startPosition = transform.position + new Vector3(direction * 0.55f, 0.08f, 0f);
        Color projectileColor = ChessCombatFx.TeamAccent(team);

        GameObject projectileObject = new GameObject("BishopProjectile");
        projectileObject.transform.position = startPosition;

        SpriteRenderer projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
        projectileRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        projectileRenderer.color = projectileColor;
        projectileRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 2;

        ChessProjectile projectile = projectileObject.AddComponent<ChessProjectile>();
        projectile.Initialize(team, attackDamage, BishopProjectileSpeed, unitTarget, baseTarget, projectileColor);
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

        if (currentHp <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public void Heal(float amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
    }
}
