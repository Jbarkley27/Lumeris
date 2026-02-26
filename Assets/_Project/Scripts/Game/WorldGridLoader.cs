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



    [Header("Block Prefabs")]
    [Tooltip("Tier 1 block prefab. Must contain WorldBlock component.")]
    [SerializeField] private GameObject tier1BlockPrefab;
    [Tooltip("Tier 2 block prefab. Must contain WorldBlock component.")]
    [SerializeField] private GameObject tier2BlockPrefab;
    [Tooltip("Tier 3 block prefab. Must contain WorldBlock component.")]
    [SerializeField] private GameObject tier3BlockPrefab;



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




    [System.Serializable]
    private class SpecialBlockPrefabEntry
    {
        [Tooltip("Type key that must match BlockCellData.specialBlockTypeId.")]
        public string typeId;
        [Tooltip("Prefab to spawn for this special type.")]
        public GameObject prefab;
    }

    [Tooltip("Special prefab mapping by typeId. Allows multiple special block families in one floor.")]
    [SerializeField] private SpecialBlockPrefabEntry[] specialBlockPrefabs;
    private readonly Dictionary<string, GameObject> specialPrefabByTypeId = new Dictionary<string, GameObject>();


    private void Awake()
    {
        RebuildSpecialPrefabLookup();
    }

    private void RebuildSpecialPrefabLookup()
    {
        specialPrefabByTypeId.Clear();

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

                GameObject prefabToSpawn = ResolvePrefabForCell(cell);
                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"WorldGridLoader: No prefab resolved for cell ({x}, {y}) in floor '{layout.floorId}'.");
                    continue;
                }

                Vector3 spawnPosition = layout.GetWorldPosition(x, y);
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

                block.Initialize(new WorldBlockInitData
                {
                    blockId = blockId,
                    floorId = layout.floorId,
                    gridCoordinate = coordinate,
                    tier = cell.tier,
                    maxHp = cell.maxHp,
                    glassReward = cell.glassReward,
                    isSpecialConditionBlock = cell.isSpecialConditionBlock,

                    // Propagate level-authored destruction rule into runtime block.
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
    /// Chooses which prefab to spawn for a given cell.
    /// </summary>
    private GameObject ResolvePrefabForCell(BlockCellData cell)
    {
        if (cell.isSpecialConditionBlock)
        {
            if (string.IsNullOrWhiteSpace(cell.specialBlockTypeId))
            {
                Debug.LogWarning("Special block cell is missing specialBlockTypeId.");
                return null;
            }

            if (specialPrefabByTypeId.TryGetValue(cell.specialBlockTypeId, out GameObject specialPrefab))
            {
                return specialPrefab;
            }

            Debug.LogWarning($"No special prefab mapped for typeId '{cell.specialBlockTypeId}'.");
            return null;
        }

        switch (cell.tier)
        {
            case BlockTier.Tier1:
                return tier1BlockPrefab;
            case BlockTier.Tier2:
                return tier2BlockPrefab;
            case BlockTier.Tier3:
                return tier3BlockPrefab;
            default:
                return tier1BlockPrefab;
        }
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

    private void OnValidate()
    {
        RebuildSpecialPrefabLookup();
    }

}
