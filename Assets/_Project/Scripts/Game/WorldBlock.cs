using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Runtime block instance that exists in the world.
/// This stores combat/destruction state and emits destruction events for systems like rewards/save.
/// </summary>
public class WorldBlock : MonoBehaviour
{
    [Header("Identity (Read Only At Runtime)")]
    [SerializeField] private string blockId;
    [SerializeField] private string floorId;
    [SerializeField] private Vector2Int gridCoordinate;

    [Header("Stats (Read Only At Runtime)")]
    [SerializeField] private BlockTier tier;
    [SerializeField] private int maxHp;
    [SerializeField] private int currentHp;
    [SerializeField] private int glassReward;
    [SerializeField] private bool isSpecialConditionBlock;
    // Runtime destruction rule copied from layout cell.
    [SerializeField] private bool canBeDestroyed = true;


    [Header("Visual")]
    [Tooltip("Optional root to scale for HP feedback. If null, this transform is used.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Minimum scale factor when HP reaches zero-ish visual state.")]
    [SerializeField] private float minVisualScale = 0.35f;

    private Vector3 initialVisualScale;
    public bool IsDestroyed { get; private set; }

    // Prevent using damage logic before the block receives runtime setup data.
    private bool isInitialized;


    /// <summary>
    /// Raised when this block is destroyed.
    /// Hook reward/save systems here in later steps.
    /// </summary>
    public static event Action<WorldBlock> Destroyed;

    /// <summary>
    /// Raised when this block takes valid non-lethal damage.
    /// Useful for damage numbers, generic hit VFX, etc.
    /// </summary>
    public static event Action<WorldBlock> Damaged;

    /// <summary>
    /// Raised when this block is hit but ignores damage due to indestructible setting.
    /// Useful for UI/FX hooks.
    /// </summary>
    public static event Action<WorldBlock> HitIndestructible;


    public string BlockId => blockId;
    public string FloorId => floorId;
    public Vector2Int GridCoordinate => gridCoordinate;
    public BlockTier Tier => tier;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public int GlassReward => glassReward;
    public bool IsSpecialConditionBlock => isSpecialConditionBlock;



    [Header("Hit Punch")]
    [Tooltip("Base punch percent relative to current scaled block size.")]
    [SerializeField] private float hitPunchPercent = 0.22f;

    [Tooltip("Random variance around base punch percent. 0.1 means +/-10%.")]
    [SerializeField, Range(0f, 0.5f)] private float hitPunchRandomVariance = 0.1f;


    /// <summary>
    /// Initializes this block from layout/loader data.
    /// Call exactly once right after prefab instantiation.
    /// </summary>
    public void Initialize(WorldBlockInitData initData)
    {
        blockId = initData.blockId;
        floorId = initData.floorId;
        gridCoordinate = initData.gridCoordinate;

        tier = initData.tier;
        maxHp = Mathf.Max(1, initData.maxHp);
        currentHp = maxHp;
        glassReward = Mathf.Max(0, initData.glassReward);
        isSpecialConditionBlock = initData.isSpecialConditionBlock;

        // Apply per-cell destruction rule.
        canBeDestroyed = initData.canBeDestroyed;


        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        isInitialized = true;


        initialVisualScale = visualRoot.localScale;
        ApplyScaleFromHp();
    }

    /// <summary>
    /// Applies damage to this block.
    /// Returns true if damage was accepted.
    /// </summary>
    public bool ApplyDamage(int damageAmount)
    {
        if (IsDestroyed || damageAmount <= 0)
        {
            return false;
        }

        if (!isInitialized)
        {
            // Fallback for scene-saved/editor-preview blocks that were not spawned through loader.
            // This keeps gameplay functional while still warning you that this block came from a non-runtime path.
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (maxHp <= 0)
            {
                Debug.LogWarning($"WorldBlock '{name}' cannot auto-init because maxHp <= 0. It must be loader-initialized.");
                return false;
            }

            if (currentHp <= 0 || currentHp > maxHp)
            {
                currentHp = maxHp;
            }

            initialVisualScale = visualRoot.localScale;
            isInitialized = true;

            Debug.LogWarning($"WorldBlock '{name}' auto-initialized from serialized scene values. Prefer runtime loader spawn.");
        }


        // Indestructible blocks still receive hit feedback,
        // but they never lose HP and never get destroyed.
        if (!canBeDestroyed)
        {
            PlayIndestructibleHitFeedback();
            HitIndestructible?.Invoke(this);
            return false;
        }


        currentHp = Mathf.Max(0, currentHp - damageAmount);
        ApplyScaleFromHp();

        if (currentHp == 0)
        {
            DestroyBlock();
            return true;
        }

        // Non-lethal successful hit.
        Damaged?.Invoke(this);
        return true;

    }

    /// <summary>
    /// Updates visual scale based on HP percentage.
    /// </summary>
    private void ApplyScaleFromHp()
    {
        float hpPercent = Mathf.Clamp01((float)currentHp / maxHp);
        float scaleFactor = Mathf.Lerp(minVisualScale, 1f, hpPercent);

        Vector3 baseScale = initialVisualScale * scaleFactor;


        float randomizedMultiplier = 1f + UnityEngine.Random.Range(-hitPunchRandomVariance, hitPunchRandomVariance);
        float randomizedPunchPercent = hitPunchPercent * randomizedMultiplier;
        Vector3 punch = baseScale * randomizedPunchPercent;


        // Prevent stacked tweens from fighting each other.

        // Apply HP shrink first, then punch around that base.
        visualRoot.DOKill(false);
        visualRoot.localScale = baseScale;
        visualRoot
            .DOPunchScale(punch, 0.14f, 8, 0.9f)
            .SetEase(Ease.OutQuad)
            .SetLink(visualRoot.gameObject, LinkBehaviour.KillOnDestroy);

    }




    /// <summary>
    /// Finalizes block destruction and emits event.
    /// </summary>
    private void DestroyBlock()
    {
        if (IsDestroyed)
        {
            return;
        }

        IsDestroyed = true;
        Destroyed?.Invoke(this);
        if (visualRoot != null)
        {
            visualRoot.DOKill(false);
        }

        Destroy(gameObject, .1f);
    }



    /// <summary>
    /// Routes indestructible-hit audio through the shared AudioManager/AudioLibrary system.
    /// </summary>
    private void PlayIndestructibleHitFeedback()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlayOneShot(AudioSfxId.World_BlockHit_Indestructible);
    }


    /// <summary>
    /// True when this block can take damage and be destroyed.
    /// </summary>
    public bool CanBeDestroyed => canBeDestroyed;


    private void OnDisable()
    {
        if (visualRoot != null)
        {
            visualRoot.DOKill(false);
        }
    }


}






/// <summary>
/// Initialization payload used by world loader when spawning blocks.
/// </summary>
[Serializable]
public struct WorldBlockInitData
{
    public string blockId;
    public string floorId;
    public Vector2Int gridCoordinate;
    public BlockTier tier;
    public int maxHp;
    public int glassReward;
    public bool isSpecialConditionBlock;

    // Loader passes per-cell destructibility into runtime block state.
    public bool canBeDestroyed;


}
