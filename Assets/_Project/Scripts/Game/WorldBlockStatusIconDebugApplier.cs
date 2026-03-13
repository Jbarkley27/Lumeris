using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional debug utility for icon readability testing.
/// Applies one icon key to a subset of loaded blocks.
/// </summary>
public class WorldBlockStatusIconDebugApplier : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldGridLoader worldGridLoader;

    [Header("Apply Trigger")]
    [SerializeField] private bool applyOnWorldLoaded = true;

    [Header("Icon Test")]
    [SerializeField] private string iconId = BlockStatusIconIds.Marked;
    [Range(0f, 1f)] [SerializeField] private float applyChance = 0.2f;
    [SerializeField] private bool deterministic = true;
    [SerializeField] private int seed = 1337;
    [SerializeField] private bool clearIconsBeforeApply = true;
    [SerializeField] private bool includeSpecialBlocks = true;
    [SerializeField] private bool includeIndestructibleBlocks = true;

    [Header("Hit Event Test")]
    [Tooltip("If true, applies icon to blocks as they are hit.")]
    [SerializeField] private bool applyOnBlockHit = true;

    [Tooltip("If true, indestructible-hit events also apply icon.")]
    [SerializeField] private bool applyOnIndestructibleHit = true;

    [Min(0f)]
    [Tooltip("If > 0, auto-clears icon after this many seconds from latest hit.")]
    [SerializeField] private float clearAfterSeconds = 0.75f;

    // Per-block clear generation to avoid earlier clear coroutine removing a newer hit icon.
    private readonly Dictionary<WorldBlock, int> clearGenerationByBlock = new Dictionary<WorldBlock, int>();

    private void Awake()
    {
        if (worldGridLoader == null)
        {
            worldGridLoader = FindFirstObjectByType<WorldGridLoader>();
        }
    }

    private void OnEnable()
    {
        if (worldGridLoader != null)
        {
            worldGridLoader.WorldLoaded += OnWorldLoaded;
        }

        WorldBlock.Damaged += OnBlockDamaged;
        WorldBlock.HitIndestructible += OnBlockHitIndestructible;
        WorldBlock.Destroyed += OnBlockDestroyed;
    }

    private void OnDisable()
    {
        if (worldGridLoader != null)
        {
            worldGridLoader.WorldLoaded -= OnWorldLoaded;
        }

        WorldBlock.Damaged -= OnBlockDamaged;
        WorldBlock.HitIndestructible -= OnBlockHitIndestructible;
        WorldBlock.Destroyed -= OnBlockDestroyed;

        clearGenerationByBlock.Clear();
    }

    private void OnWorldLoaded(WorldLayout2D _)
    {
        if (!applyOnWorldLoaded)
        {
            return;
        }

        ApplyNow();
    }

    [ContextMenu("Apply Test Icons Now")]
    public void ApplyNow()
    {
        if (worldGridLoader == null)
        {
            return;
        }

        foreach (var pair in worldGridLoader.BlocksByCoordinate)
        {
            WorldBlock block = pair.Value;
            if (block == null)
            {
                continue;
            }

            if (!includeSpecialBlocks && block.IsSpecialConditionBlock)
            {
                continue;
            }

            if (!includeIndestructibleBlocks && !block.CanBeDestroyed)
            {
                continue;
            }

            if (clearIconsBeforeApply)
            {
                block.ClearStatusIcon();
            }

            float chance = Mathf.Clamp01(applyChance);
            if (chance <= 0f)
            {
                continue;
            }

            float roll = deterministic
                ? Hash01(block.GridCoordinate.x, block.GridCoordinate.y, seed)
                : Random.value;

            if (roll <= chance)
            {
                block.ShowStatusIcon(iconId);
            }
        }
    }

    [ContextMenu("Clear All Test Icons")]
    public void ClearAll()
    {
        if (worldGridLoader == null)
        {
            return;
        }

        foreach (var pair in worldGridLoader.BlocksByCoordinate)
        {
            WorldBlock block = pair.Value;
            if (block == null)
            {
                continue;
            }

            block.ClearStatusIcon();
        }
    }

    private void OnBlockDamaged(WorldBlock block)
    {
        if (!applyOnBlockHit)
        {
            return;
        }

        TryApplyIconToBlock(block);
    }

    private void OnBlockHitIndestructible(WorldBlock block)
    {
        if (!applyOnBlockHit || !applyOnIndestructibleHit)
        {
            return;
        }

        TryApplyIconToBlock(block);
    }

    private void OnBlockDestroyed(WorldBlock block)
    {
        if (block == null)
        {
            return;
        }

        clearGenerationByBlock.Remove(block);
    }

    private void TryApplyIconToBlock(WorldBlock block)
    {
        if (block == null)
        {
            return;
        }

        if (!includeSpecialBlocks && block.IsSpecialConditionBlock)
        {
            return;
        }

        if (!includeIndestructibleBlocks && !block.CanBeDestroyed)
        {
            return;
        }

        block.ShowStatusIcon(iconId);

        if (clearAfterSeconds > 0f)
        {
            int generation = NextClearGeneration(block);
            StartCoroutine(ClearIconAfterDelay(block, generation, clearAfterSeconds));
        }
    }

    private int NextClearGeneration(WorldBlock block)
    {
        int next = 1;
        if (clearGenerationByBlock.TryGetValue(block, out int current))
        {
            next = current + 1;
        }

        clearGenerationByBlock[block] = next;
        return next;
    }

    private IEnumerator ClearIconAfterDelay(WorldBlock block, int expectedGeneration, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (block == null)
        {
            yield break;
        }

        if (!clearGenerationByBlock.TryGetValue(block, out int currentGeneration))
        {
            yield break;
        }

        if (currentGeneration != expectedGeneration)
        {
            yield break;
        }

        clearGenerationByBlock.Remove(block);
        block.ClearStatusIcon();
    }

    private static float Hash01(int x, int y, int seedValue)
    {
        unchecked
        {
            int h = seedValue;
            h ^= x * 374761393;
            h = (h << 13) ^ h;
            h ^= y * 668265263;
            h = h * 1274126177;

            uint u = (uint)h;
            return (u & 0x00FFFFFF) / 16777215f;
        }
    }
}
