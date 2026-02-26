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

    private void OnValidate()
    {
        // Keep serialized array size aligned with width*height in editor changes.
        EnsureCellArraySize();

        // Keep spawn cell valid if grid dimensions are edited.
        ClampSpawnCellToBounds();
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
    /// Converts grid cell to world position on XZ plane (Y fixed to origin.y).
    /// </summary>
    public Vector3 GetWorldPosition(int x, int y)
    {
        return worldOrigin + new Vector3(x * cellSize, 0f, y * cellSize);
    }

    private void ClampSpawnCellToBounds()
    {
        playerSpawnCell.x = Mathf.Clamp(playerSpawnCell.x, 0, Mathf.Max(0, width - 1));
        playerSpawnCell.y = Mathf.Clamp(playerSpawnCell.y, 0, Mathf.Max(0, height - 1));
    }
}

/// <summary>
/// Authoring data for one grid cell.
/// Runtime block instances will be created from this.
/// </summary>
[Serializable]
public class BlockCellData
{
    [Tooltip("If false, loader skips spawning a block in this cell.")]
    public bool hasBlock = false;

    [Tooltip("Core strength tier. Tier controls baseline difficulty and rewards.")]
    public BlockTier tier = BlockTier.Tier1;

    [Min(1)]
    [Tooltip("Maximum HP for spawned block.")]
    public int maxHp = 10;

    [Min(0)]
    [Tooltip("Glass granted when block is destroyed.")]
    public int glassReward = 1;

    [Tooltip("Marks this as a special condition block (specials do not use tier prefab mapping).")]
    public bool isSpecialConditionBlock = false;

    [Tooltip("Special block type key (example: key_blue, augment_drop, portal_lock). Used only when isSpecialConditionBlock=true.")]
    public string specialBlockTypeId = "";

    // True = normal breakable block.
    // False = permanent block for this floor session (ex: statues, anchors, scenery blockers).
    [Tooltip("If false, this block ignores all damage and cannot be destroyed until floor unload.")]
    public bool canBeDestroyed = true;


}

/// <summary>
/// Core block progression tiers.
/// </summary>
public enum BlockTier
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3
}
