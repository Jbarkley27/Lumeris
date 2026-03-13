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
    [Tooltip("Optional explicit renderers that receive tier material. If empty, all child MeshRenderers are used.")]
    [SerializeField] private MeshRenderer[] tierMaterialRenderers;
    [Tooltip("Optional presenter for status icons shown above this block.")]
    [SerializeField] private StatusIconView statusIconView;

    private Vector3 initialVisualScale;
    private MeshRenderer[] cachedTierMaterialRenderers;
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
    public StatusIconView StatusIconView => statusIconView;



    [Header("Hit Punch")]
    [Tooltip("Base punch percent relative to current scaled block size.")]
    [SerializeField] private float hitPunchPercent = 0.22f;

    [Tooltip("Random variance around base punch percent. 0.1 means +/-10%.")]
    [SerializeField, Range(0f, 0.5f)] private float hitPunchRandomVariance = 0.1f;

    /// <summary>
    /// Raised for every valid block hit attempt coming through ApplyDamage.
    /// - blocked = true  => hit an indestructible block (no HP loss)
    /// - blocked = false => hit a destructible block (HP was reduced)
    /// This is used by global feedback systems (damage number spawner, etc.).
    /// </summary>
    public static event Action<WorldBlock, int, Vector3, bool> DamagePopupRequested;

    public HitFlashModule hitFlashModule;



    /// <summary>
    /// Applies the tier material to this block's renderers.
    /// Intended for non-special blocks during loader spawn.
    /// </summary>
    public void ApplyTierMaterial(Material tierMaterial)
    {
        if (tierMaterial == null)
        {
            Debug.LogWarning($"WorldBlock '{name}' received null tier material.");
            return;
        }

        MeshRenderer[] renderers = GetTierMaterialRenderers();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"WorldBlock '{name}' has no MeshRenderer to apply tier material.");
            return;
        }

        for (int r = 0; r < renderers.Length; r++)
        {
            MeshRenderer renderer = renderers[r];
            if (renderer == null)
            {
                continue;
            }

            Material[] sharedMats = renderer.sharedMaterials;
            if (sharedMats == null || sharedMats.Length == 0)
            {
                renderer.sharedMaterial = tierMaterial;
                continue;
            }

            for (int i = 0; i < sharedMats.Length; i++)
            {
                sharedMats[i] = tierMaterial;
            }

            renderer.sharedMaterials = sharedMats;
        }
    }

    /// <summary>
    /// Returns explicit renderer bindings when assigned, otherwise auto-discovers child MeshRenderers.
    /// </summary>
    private MeshRenderer[] GetTierMaterialRenderers()
    {
        if (tierMaterialRenderers != null && tierMaterialRenderers.Length > 0)
        {
            return tierMaterialRenderers;
        }

        if (cachedTierMaterialRenderers == null || cachedTierMaterialRenderers.Length == 0)
        {
            cachedTierMaterialRenderers = GetComponentsInChildren<MeshRenderer>(true);
        }

        return cachedTierMaterialRenderers;
    }

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

        hitFlashModule = GetComponent<HitFlashModule>();
        if (hitFlashModule == null)
        {
            Debug.LogWarning($"WorldBlock '{name}' is missing a HitFlashModule component.");
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (statusIconView == null)
        {
            statusIconView = GetComponentInChildren<StatusIconView>(true);
        }

        isInitialized = true;


        initialVisualScale = visualRoot.localScale;
        ApplyScaleFromHp();
    }

    /// <summary>
    /// Shows a status icon by logical icon key (example: marked, shield, bomb).
    /// Returns false when icon view is missing or key is not configured.
    /// </summary>
    public bool ShowStatusIcon(string iconId)
    {
        if (statusIconView == null)
        {
            statusIconView = GetComponentInChildren<StatusIconView>(true);
        }

        if (statusIconView == null)
        {
            Debug.LogWarning($"WorldBlock '{name}' cannot show status icon '{iconId}' because StatusIconView is missing.");
            return false;
        }

        return statusIconView.ShowIcon(iconId);
    }

    /// <summary>
    /// Clears active status icon from this block.
    /// </summary>
    public void ClearStatusIcon()
    {
        if (statusIconView == null)
        {
            return;
        }

        statusIconView.ClearIcon();
    }





    /// <summary>
    /// Emits a unified popup request so popup logic stays outside this class.
    /// </summary>
    private void RaiseDamagePopupRequest(int incomingDamage, bool blocked)
    {
        // Use visual root as anchor when available so popup appears near visible mesh.
        Vector3 anchor = (visualRoot != null ? visualRoot.position : transform.position) + Vector3.up * 0.35f;
        DamagePopupRequested?.Invoke(this, Mathf.Max(0, incomingDamage), anchor, blocked);
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

            if (hitFlashModule == null)
            {
                hitFlashModule = GetComponent<HitFlashModule>();
                if (hitFlashModule == null)
                {
                    Debug.LogWarning($"WorldBlock '{name}' is missing a HitFlashModule component.");
                }
            }

            Debug.LogWarning($"WorldBlock '{name}' auto-initialized from serialized scene values. Prefer runtime loader spawn.");
        }


        // Indestructible blocks still receive hit feedback,
        // but they never lose HP and never get destroyed.
        if (!canBeDestroyed)
        {
            RaiseDamagePopupRequest(damageAmount, true);
            PlayIndestructibleHitFeedback();
            HitIndestructible?.Invoke(this);
            return false;
        }


        currentHp = Mathf.Max(0, currentHp - damageAmount);
        hitFlashModule?.FlashAll();
        ApplyScaleFromHp();
        RaiseDamagePopupRequest(damageAmount, false);

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

    [ContextMenu("Debug/Show Status Icon (marked)")]
    private void DebugShowMarkedStatusIcon()
    {
        ShowStatusIcon(BlockStatusIconIds.Marked);
    }

    [ContextMenu("Debug/Clear Status Icon")]
    private void DebugClearStatusIcon()
    {
        ClearStatusIcon();
    }


    private void OnDisable()
    {
        if (visualRoot != null)
        {
            visualRoot.DOKill(false);
        }

        ClearStatusIcon();
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
