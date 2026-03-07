using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;



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

    [Header("Debug Layout Override")]
    [Tooltip("When enabled, loader uses Debug Layout instead of the normal source layout.")]
    [SerializeField] private bool useDebugLayoutOverride = false;

    [Tooltip("Debug/test layout asset used when override is enabled.")]
    [SerializeField] private WorldLayout2D debugLayoutOverride;

    [Tooltip("If true, debug override is only allowed while running in Unity Editor.")]
    [SerializeField] private bool debugOverrideEditorOnly = true;

    [Header("Spawn Animation (Play Mode)")]
    [Tooltip("If true, blocks animate in with a slight stagger when spawned during play mode.")]
    [SerializeField] private bool animateSpawnOnLoad = true;

    [Min(0f)]
    [Tooltip("Delay between each spawned block animation in seconds.")]
    [SerializeField] private float spawnStaggerSeconds = 0.003f;

    [Min(0.01f)]
    [Tooltip("Duration of each block spawn scale animation.")]
    [SerializeField] private float spawnTweenDuration = 0.12f;

    [Range(0.01f, 1f)]
    [Tooltip("Starting scale factor for spawn animation. 1 = no scale-in effect.")]
    [SerializeField] private float spawnStartScaleFactor = 0.2f;

    [Tooltip("Tween ease used for block spawn animation.")]
    [SerializeField] private Ease spawnTweenEase = Ease.OutBack;

    [Header("Debug Logging")]
    [Tooltip("Logs which layout source was used (default vs debug override) when loading.")]
    [SerializeField] private bool logActiveLayoutOnLoad = true;

    // Cached debug state from the last load operation.
    private WorldLayout2D lastLoadedLayout;
    private bool lastLoadUsedDebugOverride;

    /// <summary>
    /// Last resolved layout name used by LoadWorld, useful for runtime debug panels.
    /// </summary>
    public string DebugActiveLayoutName => lastLoadedLayout != null ? lastLoadedLayout.name : "(none)";

    /// <summary>
    /// Last resolved floor id used by LoadWorld.
    /// </summary>
    public string DebugActiveFloorId => lastLoadedLayout != null ? lastLoadedLayout.floorId : "(none)";

    /// <summary>
    /// True when last load used debug override layout source.
    /// </summary>
    public bool DebugUsedDebugOverride => lastLoadUsedDebugOverride;

    /// <summary>
    /// Number of currently spawned blocks tracked by loader.
    /// </summary>
    public int DebugSpawnedBlockCount => blocksByCoordinate.Count;


    [Header("Spawn Safety")]
    [Tooltip("If true, blocks inside a square around the computed center spawn cell are skipped.")]
    [SerializeField] private bool enforceSpawnSafeArea = true;

    [Min(0)]
    [Tooltip("Safe zone radius in cells around center spawn. 0=single cell, 1=3x3, 2=5x5.")]
    [SerializeField] private int spawnSafeRadiusCells = 1;

    [Header("Player Reposition On Load")]
    [Tooltip("If true, player is moved to computed center spawn each time a level loads.")]
    [SerializeField] private bool repositionPlayerOnLoad = true;

    [Tooltip("Optional explicit player reference. If null and auto-find is enabled, loader will find PlayerMovement.")]
    [SerializeField] private PlayerMovement playerToReposition;

    [Tooltip("If true and player reference is missing, loader will find PlayerMovement in scene.")]
    [SerializeField] private bool autoFindPlayerIfMissing = true;

    [Tooltip("Optional world-space Y offset applied on top of center spawn height.")]
    [SerializeField] private float playerSpawnYOffset = 0f;



    /// <summary>
    /// Exposes whether spawn safe-zone clearing is enabled.
    /// Useful for runtime debug UI.
    /// </summary>
    public bool DebugEnforceSpawnSafeArea => enforceSpawnSafeArea;

    /// <summary>
    /// Exposes configured safe-zone radius in cells.
    /// Useful for runtime debug UI.
    /// </summary>
    public int DebugSpawnSafeRadiusCells => spawnSafeRadiusCells;

    /// <summary>
    /// Exposes computed spawn world position currently used by loader.
    /// </summary>
    public Vector3 DebugSpawnWorldPosition => PlayerSpawnWorldPosition;


    [Header("Spawn Punch")]
    [SerializeField] private bool useSpawnPunch = true;
    [Range(0.05f, 1.5f)] [SerializeField] private float spawnPunchStrength = 0.55f;
    [Min(0.05f)] [SerializeField] private float spawnPunchDuration = 0.22f;
    [Range(1, 20)] [SerializeField] private int spawnPunchVibrato = 5;
    [Range(0f, 1f)] [SerializeField] private float spawnPunchElasticity = 0.9f;


    /// <summary>
    /// Fired after a world layout finishes loading and spawning.
    /// </summary>
    public event Action<WorldLayout2D> WorldLoaded;

    /// <summary>
    /// Last loaded layout reference for external systems.
    /// </summary>
    public WorldLayout2D CurrentLoadedLayout => lastLoadedLayout;





    /// <summary>
    /// Forces loader to use a specific layout and load it immediately.
    /// Used by sequence progression manager.
    /// </summary>
    public void LoadSpecificLayout(WorldLayout2D specificLayout)
    {
        if (specificLayout == null)
        {
            Debug.LogError("WorldGridLoader.LoadSpecificLayout failed: specificLayout is null.");
            return;
        }

        // Use supplied layout as current source.
        layout = specificLayout;

        // Disable debug override for explicit sequence loads.
        useDebugLayoutOverride = false;

        LoadWorld();
    }





    /// <summary>
    /// Resolves which layout should be used for this load call.
    /// Priority:
    /// 1) Debug override layout (if enabled, assigned, and allowed in current runtime)
    /// 2) Default layout reference
    /// </summary>
    private WorldLayout2D ResolveActiveLayout()
    {
        // Explicit user toggle to force debug layout during iteration/testing.
        bool wantsDebugOverride = useDebugLayoutOverride;

        // Override only works when a debug asset is actually assigned.
        bool hasDebugLayout = debugLayoutOverride != null;

        // Prevent shipping debug override in builds when desired.
        bool debugAllowedHere = !debugOverrideEditorOnly || Application.isEditor;

        if (wantsDebugOverride && hasDebugLayout && debugAllowedHere)
        {
            return debugLayoutOverride;
        }

        // Fallback to standard layout source.
        return layout;
    }




    /// <summary>
    /// Returns player spawn position in world space based on center cell of active layout.
    /// For even dimensions, this uses the lower-center deterministic cell.
    /// </summary>
    public Vector3 PlayerSpawnWorldPosition
    {
        get
        {
            WorldLayout2D activeLayout = ResolveActiveLayout();
            if (activeLayout == null)
            {
                return Vector3.zero;
            }

            Vector2Int centerSpawnCell = GetCenterSpawnCell(activeLayout);
            Vector3 spawn = activeLayout.GetWorldPosition(centerSpawnCell.x, centerSpawnCell.y);
            spawn.y += playerSpawnYOffset;

            return spawn;
        }
    }







    /// <summary>
    /// Computes deterministic center spawn cell from layout dimensions.
    /// Odd sizes map to exact center cell.
    /// Even sizes map to lower-center cell (stable and predictable).
    /// Example: width=6 -> center x = 2 (between 2 and 3, we pick 2).
    /// </summary>
    private static Vector2Int GetCenterSpawnCell(WorldLayout2D activeLayout)
    {
        int centerX = (activeLayout.width - 1) / 2;
        int centerY = (activeLayout.height - 1) / 2;
        return new Vector2Int(centerX, centerY);
    }





    /// <summary>
    /// Moves player to the center spawn point on level load and clears velocity.
    /// This avoids stale momentum carrying into a newly loaded level.
    /// </summary>
    private void MovePlayerToSpawnPoint(WorldLayout2D activeLayout)
    {
        if (!repositionPlayerOnLoad || activeLayout == null)
        {
            return;
        }

        if (playerToReposition == null && autoFindPlayerIfMissing)
        {
            playerToReposition = FindFirstObjectByType<PlayerMovement>();
        }

        if (playerToReposition == null)
        {
            Debug.LogWarning("WorldGridLoader: Could not move player to spawn because PlayerMovement was not found.");
            return;
        }

        Vector3 spawnWorld = PlayerSpawnWorldPosition;

        // Prefer rigidbody-safe teleport path when available.
        if (playerToReposition._rb != null)
        {
            playerToReposition._rb.linearVelocity = Vector3.zero;
            playerToReposition._rb.angularVelocity = Vector3.zero;
            playerToReposition._rb.position = spawnWorld;
        }
        else
        {
            playerToReposition.transform.position = spawnWorld;
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
    /// Spawns all blocks described by the resolved active layout.
    /// Active layout can be normal source or debug override source.
    /// </summary>
    [ContextMenu("Load World")]
    public void LoadWorld()
    {
        // Resolve runtime layout source first so all downstream logic uses one consistent asset.
        WorldLayout2D activeLayout = ResolveActiveLayout();

        if (activeLayout == null)
        {
            Debug.LogError("WorldGridLoader.LoadWorld failed: no active layout resolved (default/debug both missing).");
            return;
        }


        lastLoadedLayout = activeLayout;
        lastLoadUsedDebugOverride =
            useDebugLayoutOverride &&
            debugLayoutOverride != null &&
            activeLayout == debugLayoutOverride;

        if (logActiveLayoutOnLoad)
        {
            Debug.Log(
                $"WorldGridLoader.LoadWorld -> Source={(lastLoadUsedDebugOverride ? "DEBUG" : "DEFAULT")} " +
                $"Layout='{activeLayout.name}' FloorId='{activeLayout.floorId}'");
        }






        if (tierBalanceConfig == null)
        {
            Debug.LogError("WorldGridLoader.LoadWorld failed: tierBalanceConfig is null.");
            return;
        }

        // Optional hint when debug toggle is on but no debug asset exists.
        if (useDebugLayoutOverride && debugLayoutOverride == null)
        {
            Debug.LogWarning("WorldGridLoader: Debug override enabled, but debugLayoutOverride is not assigned. Using default layout.");
        }

        if (blocksParent == null)
        {
            blocksParent = transform;
        }

        // Ensure layout cell array matches current width/height before we read from it.
        activeLayout.EnsureCellArraySize();

        if (clearBeforeLoad)
        {
            ClearSpawnedBlocks();
        }


        // Move player first so camera/follow systems see correct position immediately.
        MovePlayerToSpawnPoint(activeLayout);



        int spawnOrder = 0;


        for (int y = 0; y < activeLayout.height; y++)
        {
            for (int x = 0; x < activeLayout.width; x++)
            {
                BlockCellData cell = activeLayout.GetCell(x, y);

                // Skip empty cells.
                if (cell == null || !cell.hasBlock)
                {
                    continue;
                }

                // Skip cells inside the player spawn safe area if that option is enabled.
                if (IsInsideSpawnSafeArea(activeLayout, x, y))
                {
                    continue;
                }


                // Prefab selection remains layout-owned (tier mapping + special mapping).
                GameObject prefabToSpawn = activeLayout.ResolvePrefabForCell(cell);

                if (prefabToSpawn == null)
                {
                    Debug.LogWarning($"WorldGridLoader: No prefab resolved for cell ({x}, {y}) in floor '{activeLayout.floorId}'.");
                    continue;
                }

                // Base grid position from active layout.
                Vector3 spawnPosition = activeLayout.GetWorldPosition(x, y);

                // Apply saved per-cell height offset for authored/procedural vertical variation.
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
                string blockId = activeLayout.BuildBlockId(x, y);

                // Base stats from global block tier definition.
                TierRuntimeStats baseStats = tierBalanceConfig.GetRuntimeStats(cell.tier);

                // Tier-scaling multiplier comes from the active layout tier progression value.
                LayoutTierRuntimeMultipliers layoutTierMultipliers =
                    tierBalanceConfig.GetLayoutTierMultipliers(activeLayout.LayoutTier);

                // Floor/color/layout multipliers from active layout.
                float floorHpMultiplier = Mathf.Max(0f, activeLayout.FloorHpMultiplier);
                float floorRewardMultiplier = Mathf.Max(0f, activeLayout.FloorRewardMultiplier);

                // Final scalar = floor scalar * layout-tier scalar.
                float finalHpScalar = floorHpMultiplier * layoutTierMultipliers.hpMultiplier;
                float finalRewardScalar = floorRewardMultiplier * layoutTierMultipliers.rewardMultiplier;

                // Final runtime stats passed to block init.
                int finalMaxHp = Mathf.Max(1, Mathf.RoundToInt(baseStats.maxHp * finalHpScalar));
                int finalGlassReward = Mathf.Max(0, Mathf.RoundToInt(baseStats.glassReward * finalRewardScalar));

                block.Initialize(new WorldBlockInitData
                {
                    blockId = blockId,
                    floorId = activeLayout.floorId,
                    gridCoordinate = coordinate,
                    tier = cell.tier,
                    maxHp = finalMaxHp,
                    glassReward = finalGlassReward,
                    isSpecialConditionBlock = cell.isSpecialConditionBlock,

                    // Destructibility remains level-authored.
                    canBeDestroyed = cell.canBeDestroyed,
                });

                PlaySpawnTween(spawnedObject.transform, spawnOrder);
                spawnOrder++;


                blocksByCoordinate[coordinate] = block;
                blocksById[blockId] = block;
            }
        }


        WorldLoaded?.Invoke(activeLayout);
    }







    /// <summary>
    /// Returns true when a cell is inside center-spawn safe area.
    /// This guarantees clear space around the player's start position.
    /// </summary>
    private bool IsInsideSpawnSafeArea(WorldLayout2D activeLayout, int x, int y)
    {
        if (!enforceSpawnSafeArea || activeLayout == null)
        {
            return false;
        }

        Vector2Int spawn = GetCenterSpawnCell(activeLayout);
        int dx = Mathf.Abs(x - spawn.x);
        int dy = Mathf.Abs(y - spawn.y);

        return dx <= spawnSafeRadiusCells && dy <= spawnSafeRadiusCells;
    }







    /// <summary>
    /// Plays spawn animation:
    /// 1) scale in from small -> full
    /// 2) optional punch at full size
    /// This keeps final scale correct while preserving punch style.
    /// </summary>
    private void PlaySpawnTween(Transform target, int spawnOrder)
    {
        if (!animateSpawnOnLoad || !Application.isPlaying || target == null)
        {
            return;
        }

        Vector3 fullScale = target.localScale;
        Vector3 startScale = fullScale * spawnStartScaleFactor;

        // Kill prior tweens on this transform to prevent stacking.
        target.DOKill(false);
        target.localScale = startScale;

        float delay = spawnOrder * spawnStaggerSeconds;

        Sequence seq = DOTween.Sequence();
        seq.SetDelay(delay);

        float baseScale = Mathf.Max(fullScale.x, Mathf.Max(fullScale.y, fullScale.z));
        float punchMagnitude = Mathf.Max(0.05f, baseScale * spawnPunchStrength);
        Vector3 punch = Vector3.one * punchMagnitude;

        // Step 1: scale in.
        seq.Append(target.DOScale(fullScale, spawnTweenDuration).SetEase(spawnTweenEase));

        // Step 2: stronger punch (optional).
        if (useSpawnPunch)
        {
            seq.AppendInterval(0.01f);
            seq.Append(target.DOPunchScale(punch, spawnPunchDuration, spawnPunchVibrato, spawnPunchElasticity));
        }

        seq = DOTween.Sequence()
             .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);


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
                child.DOKill(false);
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
