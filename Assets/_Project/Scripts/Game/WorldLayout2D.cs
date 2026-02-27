using System;
using UnityEngine;

/// <summary>
/// Authoring asset for one world/floor grid.
/// This is the single source of truth for handcrafted layouts,
/// and the same structure can be filled by procedural generation.
/// </summary>
[CreateAssetMenu(fileName = "WorldLayout2D", menuName = "Margin/World/World Layout 2D")]
public class WorldLayout2D : ScriptableObject
{
    [Header("World Identity")]
    [Tooltip("Stable floor identifier used in deterministic block IDs and save keys.")]
    public string floorId = "blue_01";

    [Header("Grid")]
    [Min(1)]
    public int width = 60;

    [Min(1)]
    public int height = 60;

    [Min(0.1f)]
    [Tooltip("Distance in world units between neighboring cells.")]
    public float cellSize = 1.0f;

    [Tooltip("World-space origin for cell (0, 0).")]
    public Vector3 worldOrigin = Vector3.zero;

    [Header("Player Spawn")]
    [Tooltip("Spawn cell for player when entering this floor.")]
    public Vector2Int playerSpawnCell = new Vector2Int(0, 0);

    [Header("Cells")]
    [SerializeField]
    private BlockCellData[] cells = Array.Empty<BlockCellData>();

    /// <summary>
    /// Total expected cell count for this layout.
    /// </summary>
    public int CellCount => width * height;

    [Header("Per-Floor Prefab Mapping")]
    [Tooltip("Tier 1 prefab used by this specific floor layout.")]
    [SerializeField] private GameObject tier1BlockPrefab;
    [Tooltip("Tier 2 prefab used by this specific floor layout.")]
    [SerializeField] private GameObject tier2BlockPrefab;
    [Tooltip("Tier 3 prefab used by this specific floor layout.")]
    [SerializeField] private GameObject tier3BlockPrefab;
    [Tooltip("Tier 4 prefab used by this specific floor layout.")]
    [SerializeField] private GameObject tier4BlockPrefab;

    [Tooltip("Tier 5 prefab used by this specific floor layout.")]
    [SerializeField] private GameObject tier5BlockPrefab;


    [Tooltip("Special prefab mapping for this floor (typeId -> prefab).")]
    [SerializeField] private SpecialBlockPrefabEntry[] specialBlockPrefabs = Array.Empty<SpecialBlockPrefabEntry>();

    // Runtime lookup cache for special block type IDs.
    // Built lazily and rebuilt on OnValidate.
    private readonly System.Collections.Generic.Dictionary<string, GameObject> specialPrefabByTypeId =
        new System.Collections.Generic.Dictionary<string, GameObject>();

    // Tracks whether the special prefab lookup has been built at least once.
    private bool specialLookupBuilt;

    [Header("Grid Origin Mode")]
    [Tooltip("If true, worldOrigin is treated as grid center. If false, worldOrigin is cell (0,0).")]
    [SerializeField] private bool useCenteredOrigin = true;
    public bool UseCenteredOrigin => useCenteredOrigin;


    [Header("Progression Multipliers")]
    [Range(1, 5)]
    [Tooltip("Progression tier of this specific layout/floor variant (1..5).")]
    [SerializeField] private int layoutTier = 1;

    [Min(0f)]
    [Tooltip("Global HP multiplier for this floor/color/layout.")]
    [SerializeField] private float floorHpMultiplier = 1.0f;

    [Min(0f)]
    [Tooltip("Global Glass reward multiplier for this floor/color/layout.")]
    [SerializeField] private float floorRewardMultiplier = 1.0f;

    /// <summary>
    /// Layout progression tier used to fetch tier-scaling multipliers from balance config.
    /// </summary>
    public int LayoutTier => layoutTier;

    /// <summary>
    /// Floor-wide HP scalar applied to all normal block tiers in this layout.
    /// </summary>
    public float FloorHpMultiplier => floorHpMultiplier;

    /// <summary>
    /// Floor-wide reward scalar applied to all normal block tiers in this layout.
    /// </summary>
    public float FloorRewardMultiplier => floorRewardMultiplier;

    



    private void OnValidate()
    {
        // Keep serialized array size aligned with width*height in editor changes.
        EnsureCellArraySize();

        // Keep spawn cell valid if grid dimensions are edited.
        ClampSpawnCellToBounds();

        // Keep special prefab lookup in sync with inspector edits.
        RebuildSpecialPrefabLookup();

        ClampProgressionFields();


    }

    /// <summary>
    /// Keeps progression fields in safe ranges while authoring.
    /// </summary>
    private void ClampProgressionFields()
    {
        layoutTier = Mathf.Clamp(layoutTier, 1, 5);
        floorHpMultiplier = Mathf.Max(0f, floorHpMultiplier);
        floorRewardMultiplier = Mathf.Max(0f, floorRewardMultiplier);
    }


    /// <summary>
    /// Ensures the backing array size matches width*height.
    /// </summary>
    public void EnsureCellArraySize()
    {
        int targetCount = Mathf.Max(1, width * height);

        if (cells == null || cells.Length != targetCount)
        {
            Array.Resize(ref cells, targetCount);
        }

        // Ensure each entry is non-null so editor tools can safely edit fields.
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null)
            {
                cells[i] = new BlockCellData();
            }
        }
    }

    /// <summary>
    /// True if coordinate is inside [0..width-1] and [0..height-1].
    /// </summary>
    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    /// <summary>
    /// Converts grid coordinate to flattened array index.
    /// </summary>
    public int ToIndex(int x, int y)
    {
        return y * width + x;
    }

    /// <summary>
    /// Gets cell data by grid coordinate. Returns null when out of bounds.
    /// </summary>
    public BlockCellData GetCell(int x, int y)
    {
        if (!IsInBounds(x, y))
        {
            return null;
        }

        return cells[ToIndex(x, y)];
    }

    /// <summary>
    /// Sets cell data by grid coordinate. Ignores out-of-bounds writes.
    /// </summary>
    public void SetCell(int x, int y, BlockCellData data)
    {
        if (!IsInBounds(x, y))
        {
            return;
        }

        cells[ToIndex(x, y)] = data ?? new BlockCellData();
    }

    /// <summary>
    /// Deterministic unique block ID format: floorId_x_y
    /// </summary>
    public string BuildBlockId(int x, int y)
    {
        return $"{floorId}_{x}_{y}";
    }


    

    /// <summary>
    /// Converts grid cell to world position on XZ plane.
    /// Supports either corner-origin or centered-origin mode.
    /// </summary>
    public Vector3 GetWorldPosition(int x, int y)
    {
        if (!useCenteredOrigin)
        {
            // Legacy behavior: origin is cell (0,0).
            return worldOrigin + new Vector3(x * cellSize, 0f, y * cellSize);
        }

        // Centered behavior:
        // width=5 => center cell is x=2
        // width=6 => center lies between x=2 and x=3
        float halfWidth = (width - 1) * 0.5f;
        float halfHeight = (height - 1) * 0.5f;

        float worldX = worldOrigin.x + (x - halfWidth) * cellSize;
        float worldZ = worldOrigin.z + (y - halfHeight) * cellSize;

        return new Vector3(worldX, worldOrigin.y, worldZ);
    }


    private void ClampSpawnCellToBounds()
    {
        playerSpawnCell.x = Mathf.Clamp(playerSpawnCell.x, 0, Mathf.Max(0, width - 1));
        playerSpawnCell.y = Mathf.Clamp(playerSpawnCell.y, 0, Mathf.Max(0, height - 1));
    }


    /// <summary>
    /// Resolves which prefab should spawn for a given cell using this floor's mapping.
    /// </summary>
    public GameObject ResolvePrefabForCell(BlockCellData cell)
    {
        if (cell == null || !cell.hasBlock)
        {
            return null;
        }

        EnsureSpecialLookupBuilt();

        // Special blocks use special type mapping, not tier mapping.
        if (cell.isSpecialConditionBlock)
        {
            if (string.IsNullOrWhiteSpace(cell.specialBlockTypeId))
            {
                Debug.LogWarning($"WorldLayout2D '{name}': special cell missing specialBlockTypeId.");
                return null;
            }

            if (specialPrefabByTypeId.TryGetValue(cell.specialBlockTypeId, out GameObject specialPrefab))
            {
                return specialPrefab;
            }

            Debug.LogWarning($"WorldLayout2D '{name}': no special prefab for typeId '{cell.specialBlockTypeId}'.");
            return null;
        }

        // Normal blocks resolve by tier.
        switch (cell.tier)
        {
            case BlockTier.Tier1: return tier1BlockPrefab;
            case BlockTier.Tier2: return tier2BlockPrefab;
            case BlockTier.Tier3: return tier3BlockPrefab;
            case BlockTier.Tier4: return tier4BlockPrefab;
            case BlockTier.Tier5: return tier5BlockPrefab;

            // Fallback keeps old behavior safe if a tier is ever unset/unknown.
            default: return tier1BlockPrefab;
        }

    }

    /// <summary>
    /// Ensures runtime lookup exists (OnValidate is editor-only, so runtime may still need build).
    /// </summary>
    private void EnsureSpecialLookupBuilt()
    {
        if (!specialLookupBuilt)
        {
            RebuildSpecialPrefabLookup();
        }
    }

    /// <summary>
    /// Rebuilds special typeId -> prefab lookup from serialized mapping entries.
    /// </summary>
    private void RebuildSpecialPrefabLookup()
    {
        specialPrefabByTypeId.Clear();
        specialLookupBuilt = true;

        if (specialBlockPrefabs == null)
        {
            return;
        }

        for (int i = 0; i < specialBlockPrefabs.Length; i++)
        {
            SpecialBlockPrefabEntry entry = specialBlockPrefabs[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.typeId) || entry.prefab == null)
            {
                continue;
            }

            specialPrefabByTypeId[entry.typeId] = entry.prefab;
        }
    }

}





/// <summary>
/// Authoring data for one grid cell.
/// This stores structure/placement only.
/// Combat/reward values come from BlockTierBalanceConfig at runtime.
/// </summary>
[Serializable]
public class BlockCellData
{
    [Tooltip("If false, loader skips spawning a block in this cell.")]
    public bool hasBlock = false;

    [Tooltip("Core strength tier. Used only when isSpecialConditionBlock=false.")]
    public BlockTier tier = BlockTier.Tier1;

    [Tooltip("Marks this as a special condition block (specials do not use tier prefab mapping).")]
    public bool isSpecialConditionBlock = false;

    [Tooltip("Special block type key (example: key_blue, augment_drop, portal_lock). Used only when isSpecialConditionBlock=true.")]
    public string specialBlockTypeId = "";

    // True = normal breakable block.
    // False = permanent block for this floor session (ex: statues, anchors, scenery blockers).
    [Tooltip("If false, this block ignores all damage and cannot be destroyed until floor unload.")]
    public bool canBeDestroyed = true;

    [Tooltip("Per-cell vertical offset applied at spawn time. Saved in layout asset.")]
    public float heightOffset = 0f;
}





/// <summary>
/// Core block progression tiers.
/// Appending new values preserves existing serialized enum indices.
/// </summary>
public enum BlockTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
    Tier4 = 4,
    Tier5 = 5
}


/// <summary>
/// Mapping entry for special block prefab selection.
/// </summary>
[Serializable]
public class SpecialBlockPrefabEntry
{
    [Tooltip("Type key (must match BlockCellData.specialBlockTypeId).")]
    public string typeId;

    [Tooltip("Prefab used when this special type is requested.")]
    public GameObject prefab;
}

