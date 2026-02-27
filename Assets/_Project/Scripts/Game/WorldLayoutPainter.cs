using UnityEngine;

/// <summary>
/// Editor-time brush tool for painting cells into a WorldLayout2D asset.
/// This avoids hand-editing large serialized cell arrays.
/// </summary>
public class WorldLayoutPainter : MonoBehaviour
{
    [System.Serializable]
    public class BrushSettings
    {
        [Tooltip("Tier assigned to painted non-special cells.")]
        public BlockTier tier = BlockTier.Tier1;

        [Tooltip("If true, painted cells become special blocks.")]
        public bool isSpecialConditionBlock = false;

        [Tooltip("Special type id used when isSpecialConditionBlock is true.")]
        public string specialBlockTypeId = string.Empty;

        [Tooltip("If false, painted blocks are indestructible.")]
        public bool canBeDestroyed = true;

        [Header("Height Offset (Per Painted Cell)")]
        [Tooltip("If true, painting assigns random height offset in [minHeightOffset, maxHeightOffset].")]
        public bool useRandomHeightOffset = false;

        [Tooltip("Minimum height offset used when random height is enabled.")]
        public float minHeightOffset = 0f;

        [Tooltip("Maximum height offset used when random height is enabled.")]
        public float maxHeightOffset = 0.75f;

        [Tooltip("Used when random height is disabled.")]
        public float fixedHeightOffset = 0f;

        [Tooltip("If true and cell already has a block, keep its existing height offset on repaint.")]
        public bool preserveExistingHeightOffsetIfAlreadyOccupied = true;


        /// <summary>
        /// Copies all brush fields from another settings object.
        /// </summary>
        public void CopyFrom(BrushSettings source)
        {
            if (source == null)
            {
                return;
            }

            tier = source.tier;
            isSpecialConditionBlock = source.isSpecialConditionBlock;
            specialBlockTypeId = source.specialBlockTypeId;
            canBeDestroyed = source.canBeDestroyed;

            useRandomHeightOffset = source.useRandomHeightOffset;
            minHeightOffset = source.minHeightOffset;
            maxHeightOffset = source.maxHeightOffset;
            fixedHeightOffset = source.fixedHeightOffset;
            preserveExistingHeightOffsetIfAlreadyOccupied = source.preserveExistingHeightOffsetIfAlreadyOccupied;

        }

    }

    [Header("Target Layout")]
    [Tooltip("Layout asset that receives paint operations.")]
    [SerializeField] private WorldLayout2D layout;

    [Header("Optional Preview Loader")]
    [Tooltip("Optional world loader to refresh scene preview after painting.")]
    [SerializeField] private WorldGridLoader gridLoader;
    [Tooltip("If enabled, loader reloads immediately after each paint operation.")]
    [SerializeField] private bool autoReloadWorldOnPaint = false;

    [Header("Scene Paint")]
    [Tooltip("Enables scene-view paint interaction.")]
    [SerializeField] private bool enableScenePainting = true;

    [Min(0)]
    [Tooltip("Square brush radius in cells. 0=1x1, 1=3x3, 2=5x5.")]
    [SerializeField] private int brushRadius = 0;

    [Tooltip("Cell data written when painting.")]
    [SerializeField] private BrushSettings brush = new BrushSettings();

    public WorldLayout2D Layout => layout;
    public bool EnableScenePainting => enableScenePainting;
    public int BrushRadius => brushRadius;

    [Header("Preset Palette")]
    [Tooltip("Reusable brush presets to switch authoring modes quickly.")]
    [SerializeField] private BrushPreset[] brushPresets = new BrushPreset[0];

    [Tooltip("Currently selected preset index in the palette.")]
    [SerializeField] private int selectedPresetIndex = -1;

    [Header("Layout Height Randomizer")]
    [Tooltip("Minimum offset used by bulk randomize operation.")]
    [SerializeField] private float bulkMinHeightOffset = 0f;

    [Tooltip("Maximum offset used by bulk randomize operation.")]
    [SerializeField] private float bulkMaxHeightOffset = 2f;

    [Tooltip("If true, only cells with hasBlock=true are randomized.")]
    [SerializeField] private bool bulkOnlyOccupiedCells = true;

    [Tooltip("If true, bulk randomization uses deterministic seed.")]
    [SerializeField] private bool bulkUseSeed = true;

    [Tooltip("Seed for deterministic bulk height pattern generation.")]
    [SerializeField] private int bulkSeed = 1337;


    /// <summary>
    /// Authoring-friendly height pattern modes for bulk layout randomization.
    /// </summary>
    public enum HeightPatternPreset
    {
        FlatJitter = 0,   // Pure random micro variation.
        Terraces = 1,     // Quantized "step" layers.
        BlobClusters = 2, // Large grouped islands of height.
        RidgeBands = 3,   // Linear/ridge-like bands.
        EdgeFalloff = 4,  // Center-high or edge-high gradient.
        MixedFractal = 5  // Organic mix of octaves + jitter.
    }

    [Tooltip(
        "Pattern algorithm used by bulk randomize.\n" +
        "FlatJitter: uniform random height variation.\n" +
        "Terraces: quantized step layers (stair-like plateaus).\n" +
        "BlobClusters: grouped raised islands.\n" +
        "RidgeBands: banded/ridge-like lines.\n" +
        "EdgeFalloff: center-to-edge gradient (or inverted).\n" +
        "MixedFractal: layered octave noise with extra jitter for organic variation.")]
    [SerializeField] private HeightPatternPreset bulkHeightPreset = HeightPatternPreset.FlatJitter;


    [Min(0.001f)]
    [Tooltip("Pattern frequency. Lower = larger features, higher = tighter features.")]
    [SerializeField] private float bulkNoiseScale = 0.08f;

    [Range(2, 12)]
    [Tooltip("Number of step layers used by Terraces preset.")]
    [SerializeField] private int bulkTerraceSteps = 4;

    [Tooltip("If true, EdgeFalloff is edge-high instead of center-high.")]
    [SerializeField] private bool bulkInvertEdgeFalloff = false;

    [Range(0f, 1f)]
    [Tooltip("0 = pure preset pattern, 1 = pure random. Useful to break repetition.")]
    [SerializeField] private float bulkRandomBlend = 0.2f;





    /// <summary>
    /// Total preset count available in this painter.
    /// </summary>
    public int PresetCount => brushPresets != null ? brushPresets.Length : 0;

    /// <summary>
    /// Selected preset index (-1 means none selected).
    /// </summary>
    public int SelectedPresetIndex => selectedPresetIndex;

    /// <summary>
    /// Returns display names for preset popup UI.
    /// </summary>
    public string[] GetPresetNames()
    {
        if (PresetCount <= 0)
        {
            return new[] { "(No Presets)" };
        }

        string[] names = new string[PresetCount];
        for (int i = 0; i < PresetCount; i++)
        {
            BrushPreset preset = brushPresets[i];
            names[i] = preset != null && !string.IsNullOrWhiteSpace(preset.presetName)
                ? preset.presetName
                : $"Preset {i}";
        }

        return names;
    }

    /// <summary>
    /// Selects a preset index without mutating current brush fields.
    /// </summary>
    public bool SelectPreset(int index)
    {
        if (!TryGetPreset(index, out _))
        {
            return false;
        }

        selectedPresetIndex = index;
        return true;
    }

    /// <summary>
    /// Applies the selected preset into active brush + brush radius.
    /// </summary>
    public bool ApplyPresetToBrush(int index)
    {
        if (!TryGetPreset(index, out BrushPreset preset))
        {
            return false;
        }

        selectedPresetIndex = index;
        brushRadius = Mathf.Max(0, preset.brushRadius);

        if (brush == null)
        {
            brush = new BrushSettings();
        }

        if (preset.brush == null)
        {
            preset.brush = new BrushSettings();
        }

        brush.CopyFrom(preset.brush);
        return true;
    }

    /// <summary>
    /// Saves current active brush + radius back into selected preset.
    /// </summary>
    public bool SaveBrushToPreset(int index)
    {
        if (!TryGetPreset(index, out BrushPreset preset))
        {
            return false;
        }

        selectedPresetIndex = index;

        if (brush == null)
        {
            brush = new BrushSettings();
        }

        if (preset.brush == null)
        {
            preset.brush = new BrushSettings();
        }

        preset.brushRadius = Mathf.Max(0, brushRadius);
        preset.brush.CopyFrom(brush);
        return true;
    }

    /// <summary>
    /// Creates a starter preset palette for fast authoring.
    /// Presets now define structure only (tier/special/destructibility).
    /// Runtime HP/reward come from balance config.
    /// </summary>
    [ContextMenu("Create Default Brush Presets")]
    public void CreateDefaultBrushPresets()
    {
        brushPresets = new[]
        {
            CreatePreset("T1 Breakable", 0, BlockTier.Tier1, false, string.Empty, true),
            CreatePreset("T2 Breakable", 0, BlockTier.Tier2, false, string.Empty, true),
            CreatePreset("T3 Breakable", 0, BlockTier.Tier3, false, string.Empty, true),
            CreatePreset("T4 Breakable", 0, BlockTier.Tier4, false, string.Empty, true),
            CreatePreset("T5 Breakable", 0, BlockTier.Tier5, false, string.Empty, true),
            CreatePreset("Statue (Indestructible)", 0, BlockTier.Tier1, false, string.Empty, false),
            CreatePreset("Special Key Block", 0, BlockTier.Tier1, true, "key_blue", true),
        };

        selectedPresetIndex = 0;
        ApplyPresetToBrush(selectedPresetIndex);
    }


    /// <summary>
    /// Internal helper to validate preset index and null-safe fetch.
    /// </summary>
    private bool TryGetPreset(int index, out BrushPreset preset)
    {
        preset = null;

        if (brushPresets == null || index < 0 || index >= brushPresets.Length)
        {
            return false;
        }

        preset = brushPresets[index];
        if (preset == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Factory for creating default presets with brush payload.
    /// Only structural fields are authored here; combat values are runtime-balanced elsewhere.
    /// </summary>
    private static BrushPreset CreatePreset(
        string name,
        int radius,
        BlockTier tier,
        bool isSpecial,
        string specialTypeId,
        bool canBeDestroyed)
    {
        return new BrushPreset
        {
            presetName = name,
            brushRadius = Mathf.Max(0, radius),
            brush = new BrushSettings
            {
                tier = tier,
                isSpecialConditionBlock = isSpecial,
                specialBlockTypeId = isSpecial ? specialTypeId : string.Empty,
                canBeDestroyed = canBeDestroyed
            }
        };
    }






    /// <summary>
    /// Converts world position to nearest layout cell coordinate.
    /// </summary>
    public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
    {
        cell = default;

        if (layout == null)
        {
            return false;
        }

        float localX;
        float localY;

        if (!layout.UseCenteredOrigin) // expose getter if needed
        {
            localX = (worldPosition.x - layout.worldOrigin.x) / layout.cellSize;
            localY = (worldPosition.z - layout.worldOrigin.z) / layout.cellSize;
        }
        else
        {
            float halfWidth = (layout.width - 1) * 0.5f;
            float halfHeight = (layout.height - 1) * 0.5f;

            localX = (worldPosition.x - layout.worldOrigin.x) / layout.cellSize + halfWidth;
            localY = (worldPosition.z - layout.worldOrigin.z) / layout.cellSize + halfHeight;
        }

        int x = Mathf.RoundToInt(localX);
        int y = Mathf.RoundToInt(localY);

        if (!layout.IsInBounds(x, y))
        {
            return false;
        }

        cell = new Vector2Int(x, y);
        return true;
    }


    /// <summary>
    /// Converts cell coordinate to world center position.
    /// </summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        if (layout == null)
        {
            return Vector3.zero;
        }

        return layout.GetWorldPosition(cell.x, cell.y);
    }

    /// <summary>
    /// Applies brush (paint or erase) at a specific center cell.
    /// </summary>
    public void ApplyBrushAtCell(Vector2Int centerCell, bool erase)
    {
        if (layout == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        for (int dy = -brushRadius; dy <= brushRadius; dy++)
        {
            for (int dx = -brushRadius; dx <= brushRadius; dx++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + dx, centerCell.y + dy);

                if (!layout.IsInBounds(cell.x, cell.y))
                {
                    continue;
                }

                if (erase)
                {
                    EraseCellInternal(cell);
                }
                else
                {
                    PaintCellInternal(cell);
                }
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(layout);
#endif

        if (autoReloadWorldOnPaint && gridLoader != null)
        {
            gridLoader.LoadWorld();
        }
    }

    /// <summary>
    /// Marks every layout cell as empty.
    /// </summary>
    public void ClearAllCells()
    {
        if (layout == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                EraseCellInternal(new Vector2Int(x, y));
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(layout);
#endif

        if (gridLoader != null)
        {
            gridLoader.LoadWorld();
        }
    }

    /// <summary>
    /// Manually reloads world preview if loader reference is assigned.
    /// </summary>
    public void ReloadWorldPreview()
    {
        if (gridLoader != null)
        {
            gridLoader.LoadWorld();
        }
    }

    /// <summary>
    /// Writes brush settings into one cell and marks it occupied.
    /// </summary>
    private void PaintCellInternal(Vector2Int cell)
    {
        // Load existing cell or create default if missing.
        BlockCellData data = layout.GetCell(cell.x, cell.y) ?? new BlockCellData();

        // Capture occupancy BEFORE we overwrite hasBlock.
        bool cellAlreadyOccupied = data.hasBlock;

        // Mark as occupied and apply brush gameplay fields.
        data.hasBlock = true;
        data.tier = brush.tier;
        data.isSpecialConditionBlock = brush.isSpecialConditionBlock;
        data.specialBlockTypeId = brush.isSpecialConditionBlock ? brush.specialBlockTypeId : string.Empty;
        data.canBeDestroyed = brush.canBeDestroyed;

        // Decide whether to assign a new offset or preserve existing authored value.
        bool shouldAssignNewHeight =
            !brush.preserveExistingHeightOffsetIfAlreadyOccupied || !cellAlreadyOccupied;

        if (shouldAssignNewHeight)
        {
            if (brush.useRandomHeightOffset)
            {
                float min = Mathf.Min(brush.minHeightOffset, brush.maxHeightOffset);
                float max = Mathf.Max(brush.minHeightOffset, brush.maxHeightOffset);
                data.heightOffset = Random.Range(min, max);
            }
            else
            {
                data.heightOffset = brush.fixedHeightOffset;
            }
        }

        layout.SetCell(cell.x, cell.y, data);
    }

    /// <summary>
    /// Clears one cell so no block spawns there.
    /// </summary>
    private void EraseCellInternal(Vector2Int cell)
    {
        BlockCellData data = layout.GetCell(cell.x, cell.y) ?? new BlockCellData();

        data.hasBlock = false;
        data.isSpecialConditionBlock = false;
        data.specialBlockTypeId = string.Empty;

        layout.SetCell(cell.x, cell.y, data);
    }


    [System.Serializable]
    public class BrushPreset
    {
        [Tooltip("Display name shown in preset dropdown/buttons.")]
        public string presetName = "New Preset";

        [Min(0)]
        [Tooltip("Brush radius used when this preset is applied.")]
        public int brushRadius = 0;

        [Tooltip("Brush payload used when this preset is applied.")]
        public BrushSettings brush = new BrushSettings();
    }





   /// <summary>
    /// Randomizes stored height offsets across layout cells using a selected preset pattern.
    /// Result is saved into the layout asset and preview can be reloaded.
    /// </summary>
    [ContextMenu("Randomize Height Offsets (Layout)")]
    public void RandomizeHeightOffsetsInLayout()
    {
        if (layout == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        float min = Mathf.Min(bulkMinHeightOffset, bulkMaxHeightOffset);
        float max = Mathf.Max(bulkMinHeightOffset, bulkMaxHeightOffset);

        // Deterministic with seed when enabled; otherwise changes each invocation.
        int seed = bulkUseSeed ? bulkSeed : System.Environment.TickCount;

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockCellData data = layout.GetCell(x, y) ?? new BlockCellData();

                if (bulkOnlyOccupiedCells && !data.hasBlock)
                {
                    continue;
                }

                // Compute preset-driven normalized value [0..1].
                float pattern01 = EvaluateHeightPattern01(x, y, layout.width, layout.height, seed);

                

                // Blend in additional randomness to avoid overly uniform patterns.
                // Hash salt constant (golden-ratio bit pattern).
                // Cast with 'unchecked' so we intentionally keep the same raw 32-bit pattern as a signed int.
                const int randomHashSalt = unchecked((int)0x9E3779B9u);

                // Blend in additional randomness to avoid overly uniform patterns.
                float random01 = Hash01(x, y, seed ^ randomHashSalt);

                float final01 = Mathf.Lerp(pattern01, random01, bulkRandomBlend);

                // Convert normalized value into actual authored offset range.
                data.heightOffset = Mathf.Lerp(min, max, final01);
                layout.SetCell(x, y, data);
            }
        }

    #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(layout);
    #endif

        ReloadWorldPreview();
    }







    /// <summary>
    /// Resets all stored cell height offsets to zero.
    /// </summary>
    [ContextMenu("Flatten Height Offsets (Layout)")]
    public void FlattenHeightOffsetsInLayout()
    {
        if (layout == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockCellData data = layout.GetCell(x, y) ?? new BlockCellData();
                data.heightOffset = 0f;
                layout.SetCell(x, y, data);
            }
        }

    #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(layout);
    #endif

        ReloadWorldPreview();
    }





    /// <summary>
    /// Returns normalized [0..1] height value based on selected preset.
    /// </summary>
    private float EvaluateHeightPattern01(int x, int y, int width, int height, int seed)
    {
        float nx = x * bulkNoiseScale;
        float ny = y * bulkNoiseScale;

        switch (bulkHeightPreset)
        {
            case HeightPatternPreset.FlatJitter:
                return Hash01(x, y, seed);

            case HeightPatternPreset.Terraces:
            {
                float n = PerlinSeeded(nx, ny, seed);
                int steps = Mathf.Max(2, bulkTerraceSteps);
                return Mathf.Round(n * (steps - 1)) / (steps - 1);
            }

            case HeightPatternPreset.BlobClusters:
            {
                float coarse = PerlinSeeded(nx * 0.45f, ny * 0.45f, seed);
                float detail = PerlinSeeded(nx * 1.6f, ny * 1.6f, seed + 17);
                return Mathf.Clamp01(coarse * 0.8f + detail * 0.2f);
            }

            case HeightPatternPreset.RidgeBands:
            {
                float n = PerlinSeeded(nx * 1.2f, ny * 1.2f, seed);
                return 1f - Mathf.Abs(2f * n - 1f);
            }

            case HeightPatternPreset.EdgeFalloff:
            {
                float cx = (width - 1) * 0.5f;
                float cy = (height - 1) * 0.5f;
                float dx = x - cx;
                float dy = y - cy;
                float maxRadius = Mathf.Max(0.0001f, Mathf.Sqrt(cx * cx + cy * cy));
                float dist01 = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxRadius);
                return bulkInvertEdgeFalloff ? dist01 : (1f - dist01);
            }

            case HeightPatternPreset.MixedFractal:
            default:
            {
                float o1 = PerlinSeeded(nx * 0.50f, ny * 0.50f, seed);
                float o2 = PerlinSeeded(nx * 1.20f, ny * 1.20f, seed + 31);
                float o3 = PerlinSeeded(nx * 2.40f, ny * 2.40f, seed + 67);
                float mix = o1 * 0.55f + o2 * 0.30f + o3 * 0.15f;
                return Mathf.Clamp01(mix);
            }
        }
    }

    /// <summary>
    /// Seeded Perlin helper so the same seed reproduces the same pattern.
    /// </summary>
    private static float PerlinSeeded(float x, float y, int seed)
    {
        float ox = (seed & 1023) * 0.073f;
        float oy = ((seed >> 10) & 1023) * 0.117f;
        return Mathf.PerlinNoise(x + ox, y + oy);
    }

    /// <summary>
    /// Fast deterministic hash noise in [0..1] from x/y/seed.
    /// </summary>
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
