using System;
using UnityEngine;

/// <summary>
/// Global runtime balance for normal block tiers.
/// Layout decides "what block goes where", this config decides "how strong/rewarding that tier is".
/// </summary>
[CreateAssetMenu(fileName = "BlockTierBalanceConfig", menuName = "Margin/World/Block Tier Balance Config")]
public class BlockTierBalanceConfig : ScriptableObject
{
    [Header("Per-Tier Runtime Stats")]
    [Tooltip("One entry per tier used at runtime for HP and Glass reward.")]
    [SerializeField] private TierBalanceEntry[] entries = new TierBalanceEntry[]
    {
        new TierBalanceEntry { tier = BlockTier.Tier1, maxHp = 10, glassReward = 1 },
        new TierBalanceEntry { tier = BlockTier.Tier2, maxHp = 20, glassReward = 2 },
        new TierBalanceEntry { tier = BlockTier.Tier3, maxHp = 35, glassReward = 3 },
        new TierBalanceEntry { tier = BlockTier.Tier4, maxHp = 50, glassReward = 5 },
        new TierBalanceEntry { tier = BlockTier.Tier5, maxHp = 70, glassReward = 8 }
    };



    [Header("Layout Tier Multipliers (1..5)")]
    [Tooltip("Additional multipliers based on WorldLayout2D.layoutTier.")]
    [SerializeField] private LayoutTierMultiplierEntry[] layoutTierMultipliers = new LayoutTierMultiplierEntry[]
    {
        new LayoutTierMultiplierEntry { layoutTier = 1, hpMultiplier = 1.00f, rewardMultiplier = 1.00f },
        new LayoutTierMultiplierEntry { layoutTier = 2, hpMultiplier = 1.15f, rewardMultiplier = 1.10f },
        new LayoutTierMultiplierEntry { layoutTier = 3, hpMultiplier = 1.30f, rewardMultiplier = 1.20f },
        new LayoutTierMultiplierEntry { layoutTier = 4, hpMultiplier = 1.45f, rewardMultiplier = 1.30f },
        new LayoutTierMultiplierEntry { layoutTier = 5, hpMultiplier = 1.60f, rewardMultiplier = 1.40f }
    };

    /// <summary>
    /// Returns multipliers for a layout tier (1..5). Falls back to identity multipliers if missing.
    /// </summary>
    public LayoutTierRuntimeMultipliers GetLayoutTierMultipliers(int layoutTier)
    {
        if (layoutTierMultipliers != null)
        {
            for (int i = 0; i < layoutTierMultipliers.Length; i++)
            {
                if (layoutTierMultipliers[i].layoutTier == layoutTier)
                {
                    return layoutTierMultipliers[i].ToRuntime();
                }
            }
        }

        Debug.LogWarning($"BlockTierBalanceConfig '{name}' missing layout tier entry for {layoutTier}. Using x1 multipliers.");
        return LayoutTierRuntimeMultipliers.Identity;
    }


    /// <summary>
    /// Resolves runtime stats for a tier.
    /// If missing, returns safe fallback so gameplay still runs.
    /// </summary>
    public TierRuntimeStats GetRuntimeStats(BlockTier tier)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].tier == tier)
                {
                    return entries[i].ToRuntimeStats();
                }
            }
        }

        Debug.LogWarning($"BlockTierBalanceConfig '{name}' missing entry for {tier}. Using fallback values.");
        return TierRuntimeStats.Fallback;
    }
}

/// <summary>
/// Authoring entry for one tier.
/// </summary>
[Serializable]
public struct TierBalanceEntry
{
    [Tooltip("Tier this balance entry applies to.")]
    public BlockTier tier;

    [Min(1)]
    [Tooltip("Max HP used when a block of this tier is spawned.")]
    public int maxHp;

    [Min(0)]
    [Tooltip("Glass reward granted when this tier is destroyed.")]
    public int glassReward;

    /// <summary>
    /// Converts authoring data into runtime-safe clamped values.
    /// </summary>
    public TierRuntimeStats ToRuntimeStats()
    {
        return new TierRuntimeStats
        {
            maxHp = Mathf.Max(1, maxHp),
            glassReward = Mathf.Max(0, glassReward)
        };
    }
}

/// <summary>
/// Runtime payload consumed by loader/block initialization.
/// </summary>
public struct TierRuntimeStats
{
    public int maxHp;
    public int glassReward;

    /// <summary>
    /// Safe fallback values if config is missing/incomplete.
    /// </summary>
    public static TierRuntimeStats Fallback => new TierRuntimeStats
    {
        maxHp = 1,
        glassReward = 0
    };
}





/// <summary>
/// Authoring entry for per-layout-tier multipliers.
/// </summary>
[Serializable]
public struct LayoutTierMultiplierEntry
{
    [Range(1, 5)]
    [Tooltip("Layout tier this multiplier entry applies to.")]
    public int layoutTier;

    [Min(0f)]
    [Tooltip("HP multiplier for this layout tier.")]
    public float hpMultiplier;

    [Min(0f)]
    [Tooltip("Glass reward multiplier for this layout tier.")]
    public float rewardMultiplier;

    /// <summary>
    /// Converts authoring values to clamped runtime values.
/// </summary>
    public LayoutTierRuntimeMultipliers ToRuntime()
    {
        return new LayoutTierRuntimeMultipliers
        {
            hpMultiplier = Mathf.Max(0f, hpMultiplier),
            rewardMultiplier = Mathf.Max(0f, rewardMultiplier)
        };
    }
}

/// <summary>
/// Runtime multiplier payload used during spawn stat calculation.
/// </summary>
public struct LayoutTierRuntimeMultipliers
{
    public float hpMultiplier;
    public float rewardMultiplier;

    /// <summary>
/// Neutral multipliers used as safe fallback.
/// </summary>
    public static LayoutTierRuntimeMultipliers Identity => new LayoutTierRuntimeMultipliers
    {
        hpMultiplier = 1f,
        rewardMultiplier = 1f
    };
}

