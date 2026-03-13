using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pattern families used by procedural visual layout generation.
/// These affect block clustering/spacing and height distribution only.
/// </summary>
public enum LayoutVisualPattern
{
    MixedFractal = 0,
    BlobClusters = 1,
    RidgeBands = 2,
    EdgeFalloff = 3
}

/// <summary>
/// Per-level visual generation profile keyed by sequence color + tier.
/// This is intentionally separate from combat tier weight distribution.
/// </summary>
[Serializable]
public class LayoutVisualGenerationEntry
{
    [Tooltip("Color key from sequence entry (example: Blue).")]
    public string colorId = "Blue";

    [Tooltip("Tier inside color group (1..5).")]
    [Range(1, 5)] public int tierInColor = 1;

    [Header("Occupancy")]
    [Range(0f, 1f)]
    [Tooltip("Percent of candidate cells that should remain occupied after visual generation.")]
    public float occupancyPercent = 0.75f;

    [Tooltip("If false, generation only mutates cells that were already occupied in the source layout.")]
    public bool includeEmptyCellsInGeneration = false;

    [Tooltip("If true, authored special blocks are left untouched by visual generation.")]
    public bool preserveSpecialBlocks = true;

    [Tooltip("If true, authored indestructible blocks are left untouched by visual generation.")]
    public bool preserveIndestructibleBlocks = true;

    [Tooltip("Pattern used to rank which cells become occupied.")]
    public LayoutVisualPattern occupancyPattern = LayoutVisualPattern.BlobClusters;

    [Min(0.001f)]
    [Tooltip("Feature size for occupancy pattern. Lower = larger features.")]
    public float occupancyNoiseScale = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("Blend between occupancy pattern and random hash noise.")]
    public float occupancyRandomBlend = 0.2f;

    [Tooltip("If true, occupancy pattern is inverted (high becomes low).")]
    public bool invertOccupancyPattern = false;

    [Header("Height Offset")]
    [Tooltip("Pattern used to distribute per-cell height offset.")]
    public LayoutVisualPattern heightPattern = LayoutVisualPattern.MixedFractal;

    [Min(0.001f)]
    [Tooltip("Feature size for height pattern. Lower = larger plateaus.")]
    public float heightNoiseScale = 0.09f;

    [Range(0f, 1f)]
    [Tooltip("Blend between height pattern and random hash noise.")]
    public float heightRandomBlend = 0.15f;

    [Min(0f)]
    [Tooltip("Minimum height offset assigned to occupied generated cells.")]
    public float minHeightOffset = 0f;

    [Min(0f)]
    [Tooltip("Maximum height offset assigned to occupied generated cells.")]
    public float maxHeightOffset = 1.1f;
}

/// <summary>
/// Stateless procedural visual generator for WorldLayout2D data.
/// </summary>
public static class LayoutVisualGenerationService
{
    private struct Candidate
    {
        public int x;
        public int y;
        public float score;
    }

    /// <summary>
    /// Finds profile entry by color + tier.
    /// Returns false when no explicit profile exists.
    /// </summary>
    public static bool TryFindEntry(
        LayoutVisualGenerationEntry[] entries,
        string colorId,
        int tierInColor,
        out LayoutVisualGenerationEntry resolved)
    {
        resolved = null;

        if (entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            LayoutVisualGenerationEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            if (!string.Equals(entry.colorId, colorId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.tierInColor != tierInColor)
            {
                continue;
            }

            resolved = entry;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Applies occupancy visual generation and optional height-offset generation.
    /// This mutates hasBlock and heightOffset only for editable candidates.
    /// </summary>
    public static void ApplyToLayout(
        WorldLayout2D layout,
        LayoutVisualGenerationEntry entry,
        int seed,
        bool applyHeightOffsets)
    {
        if (layout == null || entry == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        List<Candidate> candidates = new List<Candidate>(layout.width * layout.height);

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockCellData cell = layout.GetCell(x, y) ?? new BlockCellData();

                if (entry.preserveSpecialBlocks && cell.isSpecialConditionBlock)
                {
                    continue;
                }

                if (entry.preserveIndestructibleBlocks && cell.hasBlock && !cell.canBeDestroyed)
                {
                    continue;
                }

                if (!entry.includeEmptyCellsInGeneration && !cell.hasBlock)
                {
                    continue;
                }

                float pattern01 = EvaluatePattern01(
                    x,
                    y,
                    layout.width,
                    layout.height,
                    entry.occupancyPattern,
                    entry.occupancyNoiseScale,
                    seed);

                if (entry.invertOccupancyPattern)
                {
                    pattern01 = 1f - pattern01;
                }

                float random01 = Hash01(x, y, seed ^ unchecked((int)0x9E3779B9u));
                float finalScore = Mathf.Lerp(pattern01, random01, Mathf.Clamp01(entry.occupancyRandomBlend));

                candidates.Add(new Candidate
                {
                    x = x,
                    y = y,
                    score = finalScore
                });
            }
        }

        if (candidates.Count <= 0)
        {
            return;
        }

        candidates.Sort((a, b) => a.score.CompareTo(b.score));

        int targetOccupiedCount = Mathf.Clamp(
            Mathf.RoundToInt(candidates.Count * Mathf.Clamp01(entry.occupancyPercent)),
            0,
            candidates.Count);

        float minHeight = Mathf.Min(entry.minHeightOffset, entry.maxHeightOffset);
        float maxHeight = Mathf.Max(entry.minHeightOffset, entry.maxHeightOffset);

        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            bool shouldBeOccupied = i < targetOccupiedCount;

            BlockCellData cell = layout.GetCell(candidate.x, candidate.y) ?? new BlockCellData();

            if (shouldBeOccupied)
            {
                cell.hasBlock = true;
                cell.isSpecialConditionBlock = false;
                cell.specialBlockTypeId = string.Empty;

                if (applyHeightOffsets)
                {
                    float patternHeight01 = EvaluatePattern01(
                        candidate.x,
                        candidate.y,
                        layout.width,
                        layout.height,
                        entry.heightPattern,
                        entry.heightNoiseScale,
                        seed ^ unchecked((int)0x85EBCA6Bu));

                    float randomHeight01 = Hash01(candidate.x, candidate.y, seed ^ unchecked((int)0xC2B2AE35u));
                    float finalHeight01 = Mathf.Lerp(
                        patternHeight01,
                        randomHeight01,
                        Mathf.Clamp01(entry.heightRandomBlend));

                    cell.heightOffset = Mathf.Lerp(minHeight, maxHeight, finalHeight01);
                }
                else
                {
                    cell.heightOffset = 0f;
                }
            }
            else
            {
                cell.hasBlock = false;
                cell.isSpecialConditionBlock = false;
                cell.specialBlockTypeId = string.Empty;
                cell.heightOffset = 0f;
            }

            layout.SetCell(candidate.x, candidate.y, cell);
        }
    }

    private static float EvaluatePattern01(
        int x,
        int y,
        int width,
        int height,
        LayoutVisualPattern pattern,
        float noiseScale,
        int seed)
    {
        float safeScale = Mathf.Max(0.0001f, noiseScale);
        float nx = x * safeScale;
        float ny = y * safeScale;

        switch (pattern)
        {
            case LayoutVisualPattern.BlobClusters:
            {
                float coarse = PerlinSeeded(nx * 0.45f, ny * 0.45f, seed);
                float detail = PerlinSeeded(nx * 1.6f, ny * 1.6f, seed + 17);
                return Mathf.Clamp01(coarse * 0.8f + detail * 0.2f);
            }

            case LayoutVisualPattern.RidgeBands:
            {
                float n = PerlinSeeded(nx * 1.2f, ny * 1.2f, seed);
                return 1f - Mathf.Abs(2f * n - 1f);
            }

            case LayoutVisualPattern.EdgeFalloff:
            {
                float cx = (width - 1) * 0.5f;
                float cy = (height - 1) * 0.5f;
                float dx = x - cx;
                float dy = y - cy;
                float maxRadius = Mathf.Max(0.0001f, Mathf.Sqrt(cx * cx + cy * cy));
                return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxRadius);
            }

            case LayoutVisualPattern.MixedFractal:
            default:
            {
                float o1 = PerlinSeeded(nx * 0.50f, ny * 0.50f, seed);
                float o2 = PerlinSeeded(nx * 1.20f, ny * 1.20f, seed + 31);
                float o3 = PerlinSeeded(nx * 2.40f, ny * 2.40f, seed + 67);
                return Mathf.Clamp01(o1 * 0.55f + o2 * 0.30f + o3 * 0.15f);
            }
        }
    }

    private static float PerlinSeeded(float x, float y, int seed)
    {
        float ox = (seed & 1023) * 0.073f;
        float oy = ((seed >> 10) & 1023) * 0.117f;
        return Mathf.PerlinNoise(x + ox, y + oy);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed;
            h ^= x * 374761393;
            h = (h << 13) ^ h;
            h ^= y * 668265263;
            h = h * 1274126177;

            uint u = (uint)h;
            return (u & 0x00FFFFFF) / 16777215f;
        }
    }
}
