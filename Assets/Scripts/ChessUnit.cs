using System.Collections.Generic;
using UnityEngine;

public enum ChessPieceKind
{
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public struct ChessUnitStats
{
    public readonly float hp;
    public readonly float damage;
    public readonly float range;
    public readonly float cooldown;
    public readonly float speed;
    public readonly float damageReduction;

    public ChessUnitStats(float hp, float damage, float range, float cooldown, float speed, float damageReduction = 0f)
    {
        this.hp = hp;
        this.damage = damage;
        this.range = range;
        this.cooldown = cooldown;
        this.speed = speed;
        this.damageReduction = damageReduction;
    }
}

public static class ChessBalance
{
    public const float SharedInitialBaseHp = 800f;
    public const float CapturedBaseHpRatio = 0.35f;
    public const float EndlessBaseHpGrowth = 130f;
    public const float PawnCost = 50f;
    public const float RookCost = 130f;
    public const float KnightCost = 95f;
    public const float BishopCost = 115f;
    public const float QueenCost = 150f;
    public const float KingCost = 190f;

    public static ChessUnitStats GetUnitStats(ChessPieceKind kind)
    {
        switch (kind)
        {
            case ChessPieceKind.Rook:
                return new ChessUnitStats(420f, 15f, 1.25f, 1.35f, 0.78f, 0.35f);
            case ChessPieceKind.Knight:
                return new ChessUnitStats(185f, 22f, 1.25f, 1.0f, 1.95f, 0.05f);
            case ChessPieceKind.Bishop:
                return new ChessUnitStats(125f, 24f, 5.0f, 1.4f, 1.05f);
            case ChessPieceKind.Queen:
                return new ChessUnitStats(150f, 28f, 4.7f, 1.25f, 1.1f);
            case ChessPieceKind.King:
                return new ChessUnitStats(330f, 38f, 1.35f, 0.95f, 1.15f, 0.1f);
            default:
                return new ChessUnitStats(115f, 13f, 1.1f, 0.82f, 1.55f);
        }
    }

    public static int GetUnlockOutpost(ChessPieceKind kind)
    {
        return 0;
    }
}

public class ChessUnit : MonoBehaviour
{
    public static readonly List<ChessUnit> ActiveUnits = new List<ChessUnit>();

    [Header("Team")]
    public Team team = Team.Player;

    [Header("Stats")]
    public ChessPieceKind unitKind = ChessPieceKind.Pawn;
    public string unitName = "Pawn";
    public float maxHp = 100f;
    public float attackDamage = 20f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1f;
    public float moveSpeed = 1.5f;
    [Range(0f, 0.8f)]
    public float damageReduction = 0f;

    [Header("Runtime")]
    public float currentHp;

    private const float KingAuraRadius = 3.2f;
    private const float KingAttackSpeedMultiplier = 1.35f;
    private const float KingMoveSpeedMultiplier = 1.25f;
    private const float BishopProjectileSpeed = 8.5f;
    private const float KnightSplashRadius = 1.6f;
    private const float KnightSplashDamageRatio = 0.65f;
    private const float RearCatchDistance = 1.2f;
    private const float UnitAttackReachPadding = 0.18f;
    private const float BaseAttackReachPadding = 0.18f;
    private const float UnitStopReachOffset = 0.48f;
    private const float BaseStopReachOffset = 0.72f;
    private const float AttackAnimDuration = 0.18f;
    private const float MinStepSpeedVariance = 0.96f;
    private const float MaxStepSpeedVariance = 1.04f;

    private Rigidbody2D rb;
    private CircleCollider2D unitCollider;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraGlowRenderer;
    private DamageFlashFx damageFlashFx;
    private ChessUnit targetUnit;
    private ChessBase targetBase;
    private Vector3 baseLocalScale;
    private Color baseSpriteColor = Color.white;
    private Color attackFlashColor = Color.white;
    private float attackTimer;
    private float moveSpeedMultiplier = 1f;
    private float attackCooldownMultiplier = 1f;
    private float laneY;
    private float walkPhase;
    private float stepSpeedVariance = 1f;
    private float walkAnimSpeedVariance = 1f;
    private float attackAnimTimer;
    private float stepFxTimer;
    private int baseSortingOrder;
    private int lastKnownBattlefieldRevision = -1;

    private struct CombatTarget
    {
        public ChessUnit unit;
        public ChessBase enemyBase;
        public float reach;
        public float signedDistance;

        public bool HasUnit
        {
            get { return unit != null; }
        }

        public bool HasBase
        {
            get { return enemyBase != null; }
        }

        public bool HasValue
        {
            get { return HasUnit || HasBase; }
        }
    }

    private float Direction
    {
        get { return team == Team.Player ? 1f : -1f; }
    }

    private void Awake()
    {
        ResolveComponents();
        ApplyUnitDefaultsFromKind(ResolveUnitKind());
        currentHp = maxHp;
        laneY = ChessGameManager.Instance != null ? ChessGameManager.Instance.LaneY : transform.position.y;
        baseLocalScale = transform.localScale;
        baseSpriteColor = spriteRenderer.color;
        baseSortingOrder = spriteRenderer.sortingOrder;
        walkPhase = Random.value * 10f;
        RandomizeStepTempo();

        SetupVisuals();
        SetupCollisionShape();
        AttachRuntimeFx();
    }

    private void OnEnable()
    {
        if (!ActiveUnits.Contains(this))
        {
            ActiveUnits.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveUnits.Remove(this);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyUnitDefaultsFromKind(ResolveUnitKind());
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
    }

    public void Initialize(Team newTeam)
    {
        team = newTeam;
        ApplyUnitDefaultsFromKind(ResolveUnitKind());
        currentHp = maxHp;
        attackTimer = 0f;
        moveSpeedMultiplier = 1f;
        attackCooldownMultiplier = 1f;
        RandomizeStepTempo();
        laneY = ChessGameManager.Instance != null ? ChessGameManager.Instance.LaneY : transform.position.y;
        lastKnownBattlefieldRevision = ChessGameManager.Instance != null ? ChessGameManager.Instance.BattlefieldRevision : -1;
        StickToLaneImmediate();
        RefreshVisualCache();
        SetupVisuals();
        SetupCollisionShape();
        AttachRuntimeFx();
        ChessCombatFx.SpawnUnitSpawnBurst(transform.position, team);
    }

    private void Update()
    {
        if (currentHp <= 0f)
        {
            StopMoving();
            return;
        }

        if (ChessGameManager.Instance != null && !ChessGameManager.Instance.IsPlaying)
        {
            StopMoving();
            return;
        }

        float deltaTime = Time.deltaTime;
        attackTimer -= deltaTime;
        attackAnimTimer = Mathf.Max(0f, attackAnimTimer - deltaTime);
        stepFxTimer -= deltaTime;

        RefreshBattlefieldState();
        StickToLaneImmediate();
        bool auraActive = RefreshKingAuraModifiers();
        UpdateAuraGlow(auraActive);
        AcquireTargets();

        bool shouldMove = ShouldMoveForward();
        if (shouldMove)
        {
            MoveForward(deltaTime);
            TrySpawnStepFx();
        }
        else
        {
            StopMoving();
        }

        TryPerformAction();
        UpdateMotionAnimation(shouldMove);
        UpdateHealthBasedSorting();
    }

    private void ResolveComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        rb.velocity = Vector2.zero;

        unitCollider = GetComponent<CircleCollider2D>();
        if (unitCollider == null)
        {
            unitCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = ChessCombatFx.GetDefaultSprite();
        }
    }

    private void RefreshVisualCache()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        baseLocalScale = transform.localScale;
        baseSpriteColor = spriteRenderer.color;
        baseSortingOrder = spriteRenderer.sortingOrder;
    }

    private void AttachRuntimeFx()
    {
        damageFlashFx = ChessCombatFx.AttachDamageFlash(transform);
        ChessCombatFx.AttachHealthBar(
            transform,
            () => currentHp,
            () => maxHp,
            new Vector3(0f, 1.58f, 0f),
            1.43f,
            0.18f);
    }

    private void SetupCollisionShape()
    {
        if (unitCollider == null)
        {
            return;
        }

        float radius = 0.38f;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            radius = Mathf.Max(0.28f, Mathf.Min(spriteRenderer.sprite.bounds.extents.x, spriteRenderer.sprite.bounds.extents.y) * 0.85f);
        }

        unitCollider.isTrigger = true;
        unitCollider.radius = radius;
        unitCollider.offset = Vector2.zero;

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].isTrigger = true;
            }
        }
    }

    private void SetupVisuals()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (auraGlowRenderer != null)
        {
            Destroy(auraGlowRenderer.gameObject);
            auraGlowRenderer = null;
        }

        Sprite sourceSprite = spriteRenderer.sprite != null ? spriteRenderer.sprite : ChessCombatFx.GetDefaultSprite();
        if (IsKing())
        {
            auraGlowRenderer = ChessCombatFx.CreateAttachedGlow(
                transform,
                sourceSprite,
                "KingAuraGlow",
                new Color(1f, 0.84f, 0.28f, 0.58f),
                1.08f,
                spriteRenderer.sortingOrder - 1,
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
                1.68f,
                spriteRenderer.sortingOrder - 1,
                0.05f,
                4.5f);
            auraGlowRenderer.gameObject.SetActive(false);
        }
    }

    private void StickToLaneImmediate()
    {
        Vector3 position = transform.position;
        position.y = laneY;
        transform.position = position;
    }

    private void AcquireTargets()
    {
        CombatTarget resolved = ResolveCombatTarget();
        targetUnit = resolved.unit;
        targetBase = resolved.enemyBase;
    }

    private void RefreshBattlefieldState()
    {
        ChessGameManager manager = ChessGameManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (lastKnownBattlefieldRevision == manager.BattlefieldRevision)
        {
            return;
        }

        lastKnownBattlefieldRevision = manager.BattlefieldRevision;
        targetUnit = null;
        targetBase = null;
        attackTimer = Mathf.Min(attackTimer, 0.12f);
    }

    private void ClearInvalidTargets()
    {
        if (!IsValidEnemyUnit(targetUnit))
        {
            targetUnit = null;
        }

        if (!IsValidEnemyBase(targetBase))
        {
            targetBase = null;
        }
    }

    private bool ShouldMoveForward()
    {
        ClearInvalidTargets();
        if (targetUnit == null && targetBase == null)
        {
            return true;
        }

        return GetCurrentTargetTravelGap() > 0.001f;
    }

    private void MoveForward(float deltaTime)
    {
        StopMoving();

        float step = moveSpeed * moveSpeedMultiplier * stepSpeedVariance * deltaTime;
        float moveAmount = Mathf.Min(step, GetCurrentTargetTravelGap());

        if (moveAmount <= 0f)
        {
            return;
        }

        float targetX = transform.position.x + Direction * moveAmount;
        transform.position = new Vector3(targetX, laneY, transform.position.z);
    }

    private void StopMoving()
    {
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }
    }

    private void TryPerformAction()
    {
        ClearInvalidTargets();

        if (IsQueen())
        {
            TryHealAlly();
            return;
        }

        if (targetUnit != null && IsUnitInAttackRange(targetUnit))
        {
            TryAttackUnit(targetUnit);
            return;
        }

        if (targetUnit == null && targetBase != null && IsBaseInAttackRange(targetBase))
        {
            TryAttackBase(targetBase);
        }
    }

    private void TryAttackUnit(ChessUnit unitTarget)
    {
        if (attackTimer > 0f || !IsValidEnemyUnit(unitTarget) || !IsUnitInAttackRange(unitTarget))
        {
            return;
        }

        StartCooldownAndAttackFx(ChessCombatFx.TeamAccent(team));

        if (IsBishop())
        {
            FireBishopProjectile(unitTarget, null);
            return;
        }

        if (IsKnight())
        {
            DealKnightSplashDamage(unitTarget);
            return;
        }

        unitTarget.TakeDamage(attackDamage);
    }

    private void TryAttackBase(ChessBase baseTarget)
    {
        if (attackTimer > 0f || !IsValidEnemyBase(baseTarget) || !IsBaseInAttackRange(baseTarget))
        {
            return;
        }

        StartCooldownAndAttackFx(ChessCombatFx.TeamAccent(team));

        if (IsBishop())
        {
            FireBishopProjectile(null, baseTarget);
            return;
        }

        baseTarget.TakeDamage(attackDamage);
    }

    private void TryHealAlly()
    {
        if (attackTimer > 0f)
        {
            return;
        }

        ChessUnit ally = FindBestHealTarget();
        if (ally == null)
        {
            return;
        }

        StartCooldownAndAttackFx(ChessCombatFx.HealColor());
        ally.Heal(attackDamage);
        ChessCombatFx.SpawnHealingPlusBurst(ally.transform.position + new Vector3(0f, 0.28f, 0f));
    }

    private ChessUnit FindBestHealTarget()
    {
        ChessUnit best = null;
        float bestRatio = 1f;
        List<ChessUnit> units = ActiveUnits;

        for (int i = 0; i < units.Count; i++)
        {
            ChessUnit candidate = units[i];
            if (candidate == null || candidate.team != team || candidate.currentHp <= 0f || candidate.currentHp >= candidate.maxHp)
            {
                continue;
            }

            if (!IsInRange(candidate.transform.position.x, attackRange))
            {
                continue;
            }

            float ratio = candidate.currentHp / Mathf.Max(1f, candidate.maxHp);
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = candidate;
            }
        }

        return best;
    }

    private void DealKnightSplashDamage(ChessUnit primaryTarget)
    {
        if (primaryTarget == null)
        {
            return;
        }

        primaryTarget.TakeDamage(attackDamage);
        ChessCombatFx.SpawnPulse(primaryTarget.transform.position, ChessCombatFx.TeamAccent(team), 0.27f, 1.5f, 0.22f, 28);

        List<ChessUnit> units = ActiveUnits;
        float splashDamage = attackDamage * KnightSplashDamageRatio;

        for (int i = 0; i < units.Count; i++)
        {
            ChessUnit candidate = units[i];
            if (candidate == null || candidate == this || candidate == primaryTarget || candidate.team == team || candidate.currentHp <= 0f)
            {
                continue;
            }

            if (Mathf.Abs(candidate.transform.position.x - primaryTarget.transform.position.x) <= KnightSplashRadius)
            {
                candidate.TakeDamage(splashDamage);
            }
        }
    }

    private void StartCooldownAndAttackFx(Color flashColor)
    {
        attackTimer = Mathf.Max(0.05f, attackCooldown * attackCooldownMultiplier);
        attackAnimTimer = AttackAnimDuration;
        attackFlashColor = flashColor;
        ChessCombatFx.SpawnPulse(transform.position + new Vector3(Direction * 0.42f, 0.08f, 0f), flashColor, 0.15f, 0.75f, 0.18f, 24);
    }

    private void FireBishopProjectile(ChessUnit unitTarget, ChessBase baseTarget)
    {
        Vector3 startPosition = transform.position + new Vector3(Direction * 0.55f, 0.08f, 0f);
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

    private bool RefreshKingAuraModifiers()
    {
        bool auraActive = HasFriendlyKingAura();
        moveSpeedMultiplier = auraActive ? KingMoveSpeedMultiplier : 1f;
        attackCooldownMultiplier = auraActive ? 1f / KingAttackSpeedMultiplier : 1f;
        return IsKing() || auraActive;
    }

    private bool HasFriendlyKingAura()
    {
        List<ChessUnit> units = ActiveUnits;

        for (int i = 0; i < units.Count; i++)
        {
            ChessUnit candidate = units[i];
            if (candidate == null || candidate == this || candidate.team != team || !candidate.IsKing() || candidate.currentHp <= 0f)
            {
                continue;
            }

            if (Mathf.Abs(candidate.transform.position.x - transform.position.x) <= KingAuraRadius)
            {
                return true;
            }
        }

        return false;
    }

    public void TakeDamage(float damage)
    {
        if (currentHp <= 0f)
        {
            return;
        }

        ApplyDamageNow(Mathf.Max(0f, damage * (1f - Mathf.Clamp01(damageReduction))));
    }

    public void Heal(float amount)
    {
        if (currentHp <= 0f)
        {
            return;
        }

        currentHp = Mathf.Min(maxHp, currentHp + Mathf.Max(0f, amount));
        UpdateHealthBasedSorting();
    }

    private void RandomizeStepTempo()
    {
        stepSpeedVariance = Random.Range(MinStepSpeedVariance, MaxStepSpeedVariance);
        walkAnimSpeedVariance = Random.Range(MinStepSpeedVariance, MaxStepSpeedVariance);
    }

    private void TrySpawnStepFx()
    {
        if (stepFxTimer > 0f)
        {
            return;
        }

        stepFxTimer = Mathf.Lerp(0.42f, 0.24f, Mathf.Clamp01(moveSpeed * stepSpeedVariance * 0.35f));
        ChessCombatFx.SpawnPulse(
            transform.position + new Vector3(-Direction * 0.22f, -0.18f, 0f),
            new Color(0.78f, 0.82f, 0.88f, 0.32f),
            0.12f,
            0.48f,
            0.22f,
            5);
    }

    private void UpdateMotionAnimation(bool moving)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float attackT = attackAnimTimer <= 0f ? 0f : 1f - attackAnimTimer / AttackAnimDuration;
        float attackWave = attackAnimTimer <= 0f ? 0f : Mathf.Sin(attackT * Mathf.PI);
        float walkWave = moving ? Mathf.Sin(Time.time * 10f * walkAnimSpeedVariance + walkPhase) * 0.025f : 0f;

        Vector3 scale = baseLocalScale;
        scale.x *= 1f + attackWave * 0.12f;
        scale.y *= 1f - attackWave * 0.06f + walkWave;
        transform.localScale = scale;

        spriteRenderer.color = attackAnimTimer > 0f
            ? Color.Lerp(baseSpriteColor, attackFlashColor, 0.45f + attackWave * 0.35f)
            : baseSpriteColor;
    }

    private void UpdateHealthBasedSorting()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float hpRatio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 0f;
        int lowHealthPriority = Mathf.RoundToInt((1f - hpRatio) * 80f);
        spriteRenderer.sortingOrder = baseSortingOrder + lowHealthPriority;

        if (auraGlowRenderer != null)
        {
            auraGlowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
        }
    }

    private void UpdateAuraGlow(bool active)
    {
        if (auraGlowRenderer == null)
        {
            return;
        }

        bool shouldShow = IsKing() || active;
        if (auraGlowRenderer.gameObject.activeSelf != shouldShow)
        {
            auraGlowRenderer.gameObject.SetActive(shouldShow);
        }
    }

    private bool IsInRange(float targetX, float range)
    {
        return Mathf.Abs(targetX - transform.position.x) <= range;
    }

    private bool IsCurrentTargetInRange()
    {
        if (targetUnit != null)
        {
            return IsUnitInAttackRange(targetUnit);
        }

        if (targetBase != null)
        {
            return IsBaseInAttackRange(targetBase);
        }

        return false;
    }

    private float GetCurrentTargetTravelGap()
    {
        if (targetUnit != null)
        {
            return Mathf.Max(0f, SignedXDistance(targetUnit.transform.position.x) - GetUnitStopReach(targetUnit));
        }

        if (targetBase != null)
        {
            return Mathf.Max(0f, SignedXDistance(targetBase.transform.position.x) - GetBaseStopReach(targetBase));
        }

        return 0f;
    }

    private CombatTarget ResolveCombatTarget()
    {
        CombatTarget best = default;
        float bestGap = Mathf.Infinity;
        float bestSignedDistance = Mathf.Infinity;
        List<ChessUnit> units = ActiveUnits;

        for (int i = 0; i < units.Count; i++)
        {
            ChessUnit candidate = units[i];
            if (!IsValidEnemyUnit(candidate))
            {
                continue;
            }

            float reach = GetUnitAttackReach(candidate);
            float signedDistance = SignedXDistance(candidate.transform.position.x);
            if (signedDistance < -reach)
            {
                continue;
            }

            float gap = Mathf.Max(0f, signedDistance - reach);
            if (gap < bestGap || (Mathf.Approximately(gap, bestGap) && signedDistance < bestSignedDistance))
            {
                best.unit = candidate;
                best.enemyBase = null;
                best.reach = reach;
                best.signedDistance = signedDistance;
                bestGap = gap;
                bestSignedDistance = signedDistance;
            }
        }

        ChessBase candidateBase = GetPrimaryEnemyBase();
        if (IsValidEnemyBase(candidateBase))
        {
            float reach = GetBaseAttackReach(candidateBase);
            float signedDistance = SignedXDistance(candidateBase.transform.position.x);
            if (signedDistance >= -reach)
            {
                float gap = Mathf.Max(0f, signedDistance - reach);
                if (gap < bestGap || (Mathf.Approximately(gap, bestGap) && signedDistance < bestSignedDistance))
                {
                    best.unit = null;
                    best.enemyBase = candidateBase;
                    best.reach = reach;
                    best.signedDistance = signedDistance;
                }
            }
        }

        return best;
    }

    private bool IsUnitInAttackRange(ChessUnit unitTarget)
    {
        if (!IsValidEnemyUnit(unitTarget))
        {
            return false;
        }

        float distance = Mathf.Abs(unitTarget.transform.position.x - transform.position.x);
        float signedDistance = SignedXDistance(unitTarget.transform.position.x);
        float reach = GetUnitAttackReach(unitTarget);
        return distance <= reach && signedDistance >= -reach;
    }

    private bool IsBaseInAttackRange(ChessBase baseTarget)
    {
        if (!IsValidEnemyBase(baseTarget))
        {
            return false;
        }

        float reach = GetBaseAttackReach(baseTarget);
        float signedDistance = SignedXDistance(baseTarget.transform.position.x);
        float distance = Mathf.Abs(baseTarget.transform.position.x - transform.position.x);
        return distance <= reach && signedDistance >= -reach;
    }

    private float GetUnitAttackReach(ChessUnit unitTarget)
    {
        // Units have no physical blocking, so add only a small grace area.
        // Do not use sprite bounds here; chess sprites can have large transparent bounds.
        return attackRange + UnitAttackReachPadding;
    }

    private float GetUnitStopReach(ChessUnit unitTarget)
    {
        return Mathf.Max(0.2f, GetUnitAttackReach(unitTarget) - UnitStopReachOffset);
    }

    private float GetBaseAttackReach(ChessBase baseTarget)
    {
        float baseHalfWidth = baseTarget != null ? baseTarget.GetCombatHalfWidth() : 0f;
        return attackRange + baseHalfWidth + BaseAttackReachPadding;
    }

    private float GetBaseStopReach(ChessBase baseTarget)
    {
        return Mathf.Max(0.25f, GetBaseAttackReach(baseTarget) - BaseStopReachOffset);
    }

    private bool IsValidEnemyUnit(ChessUnit unitTarget)
    {
        return unitTarget != null
            && unitTarget != this
            && unitTarget.team != team
            && unitTarget.currentHp > 0f;
    }

    private bool IsValidEnemyBase(ChessBase baseTarget)
    {
        return baseTarget != null
            && baseTarget.team != team
            && baseTarget.IsAlive;
    }

    private ChessBase GetPrimaryEnemyBase()
    {
        ChessGameManager manager = ChessGameManager.Instance;
        if (manager == null)
        {
            return null;
        }

        return manager.GetFrontlineBaseForTeam(team);
    }

    private void ApplyDamageNow(float appliedDamage)
    {
        if (appliedDamage <= 0f || currentHp <= 0f)
        {
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - appliedDamage);

        if (damageFlashFx == null)
        {
            damageFlashFx = ChessCombatFx.AttachDamageFlash(transform);
        }

        if (damageFlashFx != null)
        {
            damageFlashFx.Flash();
        }

        ChessCombatFx.SpawnHitSpark(transform.position, team);
        ChessCombatFx.SpawnFloatingText(
            "-" + Mathf.CeilToInt(appliedDamage),
            transform.position + new Vector3(0f, 0.72f, 0f),
            new Color(1f, 0.28f, 0.22f, 1f),
            1.11f,
            1.38f,
            0.55f,
            0.45f,
            130);

        if (currentHp <= 0f)
        {
            StopMoving();
            ChessCombatFx.SpawnDeathBurst(transform.position, team);
            Destroy(gameObject);
        }

        UpdateHealthBasedSorting();
    }

    private float SignedXDistance(float targetX)
    {
        return (targetX - transform.position.x) * Direction;
    }

    private bool IsQueen()
    {
        return unitKind == ChessPieceKind.Queen;
    }

    private bool IsKing()
    {
        return unitKind == ChessPieceKind.King;
    }

    private bool IsBishop()
    {
        return unitKind == ChessPieceKind.Bishop;
    }

    private bool IsKnight()
    {
        return unitKind == ChessPieceKind.Knight;
    }

    private void ApplyUnitDefaultsFromKind(ChessPieceKind kind)
    {
        unitKind = kind;
        unitName = unitKind.ToString();

        ChessUnitStats stats = ChessBalance.GetUnitStats(unitKind);
        maxHp = Mathf.Max(1f, stats.hp);
        attackDamage = Mathf.Max(0f, stats.damage);
        attackRange = Mathf.Max(0.1f, stats.range);
        attackCooldown = Mathf.Max(0.05f, stats.cooldown);
        moveSpeed = Mathf.Max(0f, stats.speed);
        damageReduction = Mathf.Clamp01(stats.damageReduction);
    }

    private ChessPieceKind ResolveUnitKind()
    {
        string objectName = NormalizeKindSource(gameObject.name);
        if (TryResolveUnitKind(objectName, out ChessPieceKind kindFromObjectName))
        {
            return kindFromObjectName;
        }

        string serializedName = NormalizeKindSource(unitName);
        if (TryResolveUnitKind(serializedName, out ChessPieceKind kindFromSerializedName))
        {
            return kindFromSerializedName;
        }

        return unitKind;
    }

    private string NormalizeKindSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        return source.Replace("(Clone)", string.Empty).Replace("Player", string.Empty).Replace("Enemy", string.Empty).Trim();
    }

    private bool TryResolveUnitKind(string source, out ChessPieceKind kind)
    {
        if (source.Contains("Pawn"))
        {
            kind = ChessPieceKind.Pawn;
            return true;
        }

        if (source.Contains("Rook"))
        {
            kind = ChessPieceKind.Rook;
            return true;
        }

        if (source.Contains("Knight"))
        {
            kind = ChessPieceKind.Knight;
            return true;
        }

        if (source.Contains("Bishop"))
        {
            kind = ChessPieceKind.Bishop;
            return true;
        }

        if (source.Contains("Queen"))
        {
            kind = ChessPieceKind.Queen;
            return true;
        }

        if (source.Contains("King"))
        {
            kind = ChessPieceKind.King;
            return true;
        }

        kind = unitKind;
        return false;
    }
}
