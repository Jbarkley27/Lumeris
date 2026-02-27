using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns WorldBlock prefabs from a WorldLayout2D asset.
/// Also provides block lookup and neighbor queries for gameplay systems.
/// </summary>
public class WorldGridLoader : MonoBehaviour
{
    [Header("Layout Source")]
    [Tooltip("The world layout asset to spawn (handcrafted or procedurally filled).")]
    [SerializeField] private WorldLayout2D layout;


    [Header("Spawn Parent")]
    [Tooltip("Optional parent for spawned blocks. If null, this transform is used.")]
    [SerializeField] private Transform blocksParent;



    [Header("Lifecycle")]
    [Tooltip("When true, existing spawned blocks are cleared before loading.")]
    [SerializeField] private bool clearBeforeLoad = true;
    [Tooltip("When true, calls LoadWorld() in Start().")]
    [SerializeField] private bool loadOnStart = true;


    // Fast coordinate lookup used by neighbor queries and gameplay targeting.
    private readonly Dictionary<Vector2Int, WorldBlock> blocksByCoordinate = new Dictionary<Vector2Int, WorldBlock>();
    // Fast ID lookup used by save/load restore paths.
    private readonly Dictionary<string, WorldBlock> blocksById = new Dictionary<string, WorldBlock>();

    // Fixed 8-direction offsets (N, NE, E, SE, S, SW, W, NW).
    private static readonly Vector2Int[] NeighborOffsets8 = new Vector2Int[]
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 1),
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1)
    };

    /// <summary>
    /// Public read-only access for other systems that need spawned blocks.
    /// </summary>
    public IReadOnlyDictionary<Vector2Int, WorldBlock> BlocksByCoordinate => blocksByCoordinate;

    [Header("Balance")]
    [Tooltip("Global tier balance used for runtime HP/reward. Layout cells no longer provide these values.")]
    [SerializeField] private BlockTierBalanceConfig tierBalanceConfig;


    /// <summary>
    /// Returns player spawn position in world space based on layout spawn cell.
    /// </summary>
    public Vector3 PlayerSpawnWorldPosition
    {
        get
        {
            if (layout == null)
            {
                return Vector3.zero;
            }

            return layout.GetWorldPosition(layout.playerSpawnCell.x, layout.playerSpawnCell.y);
        }
    }




    private void Start()
    {
        if (loadOnStart)
        {
            LoadWorld();
        }
    }

    /// <summary>
    /// Spawns all blocks described by the assigned layout.
    /// </summary>
    [ContextMenu("Load World")]
    public void LoadWorld()
    {
        if (layout == null)
        {
            Debug.LogError("WorldGridLoader.LoadWorld failed: layout is null.");
            return;
        }

        if (tierBalanceConfig == null)
        {
            Debug.LogError("WorldGridLoader.LoadWorld failed: tierBalanceConfig is null.");
            return;
        }


        if (blocksParent == null)
        {
            blocksParent = transform;
        }

        // Ensure array size is valid before reading cells.
        layout.EnsureCellArraySize();

        if (clearBeforeLoad)
        {
            ClearSpawnedBlocks();
        }

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockCellData cell = layout.GetCell(x, y);

                // Skip empty cells.
                if (cell == null || !cell.hasBlock)
                {
                    continue;
                }

                // Prefab selection is now owned by the layout asset (per-floor mapping).
                GameObject prefabToSpawn = layout.ResolvePrefabForCell(cell);

                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"WorldGridLoader: No prefab resolved for cell ({x}, {y}) in floor '{layout.floorId}'.");
                    continue;
                }

                // Base grid position from layout.
                Vector3 spawnPosition = layout.GetWorldPosition(x, y);

                // Apply saved per-cell height offset so handcrafted patterns persist in play mode.
                spawnPosition.y += cell.heightOffset;

                GameObject spawnedObject = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, blocksParent);


                WorldBlock block = spawnedObject.GetComponent<WorldBlock>();
                if (block == null)
                {
                    Debug.LogError($"WorldGridLoader: Prefab '{prefabToSpawn.name}' is missing WorldBlock. Destroying spawned instance.");
                    SafeDestroy(spawnedObject);
                    continue;
                }

                Vector2Int coordinate = new Vector2Int(x, y);
                string blockId = layout.BuildBlockId(x, y);

                // Base stats from global block tier definition (Tier1..Tier5 baseline).
                TierRuntimeStats baseStats = tierBalanceConfig.GetRuntimeStats(cell.tier);

                // Extra scaling by layout progression tier (1..5).
                LayoutTierRuntimeMultipliers layoutTierMultipliers = tierBalanceConfig.GetLayoutTierMultipliers(layout.LayoutTier);

                // Floor/color/layout-wide scaling.
                float floorHpMultiplier = Mathf.Max(0f, layout.FloorHpMultiplier);
                float floorRewardMultiplier = Mathf.Max(0f, layout.FloorRewardMultiplier);

                // Final scalar = floor scalar * layout-tier scalar.
                float finalHpScalar = floorHpMultiplier * layoutTierMultipliers.hpMultiplier;
                float finalRewardScalar = floorRewardMultiplier * layoutTierMultipliers.rewardMultiplier;

                // Final runtime stats passed to block init.
                int finalMaxHp = Mathf.Max(1, Mathf.RoundToInt(baseStats.maxHp * finalHpScalar));
                int finalGlassReward = Mathf.Max(0, Mathf.RoundToInt(baseStats.glassReward * finalRewardScalar));


                block.Initialize(new WorldBlockInitData
                {
                    blockId = blockId,
                    floorId = layout.floorId,
                    gridCoordinate = coordinate,

                    // Tier still comes from the painted layout cell.
                    tier = cell.tier,

                    // HP and reward now come from balance config (not per-cell values).
                    maxHp = finalMaxHp,
                    glassReward = finalGlassReward,


                    isSpecialConditionBlock = cell.isSpecialConditionBlock,

                    // Destructibility remains a level-authored property.
                    canBeDestroyed = cell.canBeDestroyed,
                });


                blocksByCoordinate[coordinate] = block;
                blocksById[blockId] = block;
            }
        }
    }

    /// <summary>
    /// Removes all currently spawned blocks and clears lookup dictionaries.
    /// </summary>
    [ContextMenu("Clear Spawned Blocks")]
    public void ClearSpawnedBlocks()
    {
        if (blocksParent != null)
        {
            for (int i = blocksParent.childCount - 1; i >= 0; i--)
            {
                Transform child = blocksParent.GetChild(i);
                SafeDestroy(child.gameObject);
            }
        }

        blocksByCoordinate.Clear();
        blocksById.Clear();
    }

    /// <summary>
    /// Tries to get a block by grid coordinate.
    /// </summary>
    public bool TryGetBlockAt(Vector2Int coordinate, out WorldBlock block)
    {
        return blocksByCoordinate.TryGetValue(coordinate, out block);
    }

    /// <summary>
    /// Tries to get a block by deterministic ID.
    /// </summary>
    public bool TryGetBlockById(string blockId, out WorldBlock block)
    {
        return blocksById.TryGetValue(blockId, out block);
    }

    /// <summary>
    /// Returns existing neighboring blocks in all 8 directions.
    /// </summary>
    public List<WorldBlock> GetNeighbors8(Vector2Int centerCoordinate)
    {
        List<WorldBlock> neighbors = new List<WorldBlock>(8);

        for (int i = 0; i < NeighborOffsets8.Length; i++)
        {
            Vector2Int neighborCoordinate = centerCoordinate + NeighborOffsets8[i];

            WorldBlock neighbor;
            if (blocksByCoordinate.TryGetValue(neighborCoordinate, out neighbor) && neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    /// <summary>
    /// Destroys objects correctly in play mode and edit mode.
    /// </summary>
    private static void SafeDestroy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

}
