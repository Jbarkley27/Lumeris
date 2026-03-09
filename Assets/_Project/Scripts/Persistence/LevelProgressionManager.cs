using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core level progression runtime manager.
/// Responsibilities:
/// - Loads current level from LevelSequenceDefinition
/// - Tracks objective progress for current level
/// - Advances to next level when objective is completed
/// - Updates ColorProgressBarUI
/// - Persists run progression state (index + completion)
/// </summary>
public class LevelProgressionManager : MonoBehaviour
{
    [Serializable]
    private class TierSpreadEntry
    {
        [Tooltip("Color key from sequence entry (example: Blue).")]
        public string colorId = "Blue";

        [Tooltip("Tier inside color group (1..5).")]
        [Range(1, 5)] public int tierInColor = 1;

        [Tooltip("Relative weight for block tier 1.")]
        [Min(0f)] public float tier1Weight = 0.7f;
        [Tooltip("Relative weight for block tier 2.")]
        [Min(0f)] public float tier2Weight = 0.2f;
        [Tooltip("Relative weight for block tier 3.")]
        [Min(0f)] public float tier3Weight = 0.1f;
        [Tooltip("Relative weight for block tier 4.")]
        [Min(0f)] public float tier4Weight = 0f;
        [Tooltip("Relative weight for block tier 5.")]
        [Min(0f)] public float tier5Weight = 0f;
    }

    [Serializable]
    private struct ProceduralTierCandidate
    {
        public int x;
        public int y;
        public float score;
    }

    private enum ProceduralTierPattern
    {
        MixedFractal = 0,
        BlobClusters = 1,
        RidgeBands = 2,
        EdgeFalloff = 3
    }

    public enum LevelObjectiveRule
    {
        DestroyRegularPercent,
        DestroySpecialBlock
    }

    [Header("References")]
    [SerializeField] private LevelSequenceDefinition levelSequence;
    [SerializeField] private WorldGridLoader worldGridLoader;
    [SerializeField] private ColorProgressBarUI colorProgressBarUI;

    [Tooltip("Auto-find WorldGridLoader if not manually assigned.")]
    [SerializeField] private bool autoFindWorldGridLoader = true;

    [Header("Startup")]
    [Tooltip("If true, loads current sequence level in Start.")]
    [SerializeField] private bool loadCurrentLevelOnStart = true;

    [Header("Procedural Tier Distribution")]
    [Tooltip("If true, each level load clones the layout and procedurally assigns normal block tiers by spread rule.")]
    [SerializeField] private bool useProceduralTierDistribution = true;

    [Tooltip("Rules keyed by sequence colorId + tierInColor. Each rule defines desired tier spread.")]
    [SerializeField] private TierSpreadEntry[] proceduralTierSpreadEntries =
    {
        new TierSpreadEntry
        {
            colorId = "Blue",
            tierInColor = 1,
            tier1Weight = 0.7f,
            tier2Weight = 0.2f,
            tier3Weight = 0.1f,
            tier4Weight = 0f,
            tier5Weight = 0f
        }
    };

    [Tooltip("Base seed for deterministic procedural tier pattern generation.")]
    [SerializeField] private int proceduralTierBaseSeed = 1337;

    [Min(0.001f)]
    [Tooltip("Feature size of procedural tier pattern. Lower = larger blobs, higher = noisier distribution.")]
    [SerializeField] private float proceduralTierNoiseScale = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("Blend amount between preset pattern and hash noise.")]
    [SerializeField] private float proceduralTierRandomBlend = 0.2f;

    [SerializeField] private ProceduralTierPattern proceduralTierPattern = ProceduralTierPattern.MixedFractal;

    [Header("Objective Rules (tiers 1..4)")]
    [Range(0.01f, 1f)] [SerializeField] private float tier1RequiredRegularDestroyPercent = 0.40f;
    [Range(0.01f, 1f)] [SerializeField] private float tier2RequiredRegularDestroyPercent = 0.50f;
    [Range(0.01f, 1f)] [SerializeField] private float tier3RequiredRegularDestroyPercent = 0.60f;
    [Range(0.01f, 1f)] [SerializeField] private float tier4RequiredRegularDestroyPercent = 0.70f;

    [Header("Tier 5 Fallback")]
    [Tooltip("If tier5 has no breakable special blocks, fallback to regular-percent objective.")]
    [SerializeField] private bool fallbackToRegularPercentWhenNoSpecialTargets = true;

    [Range(0.01f, 1f)] [SerializeField] private float fallbackRegularPercentForTier5 = 0.80f;

    [Header("Advance Behavior")]
    [Tooltip("Automatically advance after objective completion.")]
    [SerializeField] private bool autoAdvanceOnComplete = true;

    [Min(0f)] [SerializeField] private float autoAdvanceDelaySeconds = 0.5f;

    [Header("Save (Run Domain)")]
    [Tooltip("If true, run progression state is persisted in PlayerPrefs.")]
    [SerializeField] private bool persistProgressionState = true;

    [Tooltip("Base key prefix for progression save data.")]
    [SerializeField] private string progressionSaveKeyPrefix = "run.progression";

    [Tooltip("If true, sequence ID is appended so different sequences don't share same save slot.")]
    [SerializeField] private bool useSequenceScopedSaveKeys = true;

    [Tooltip("If true, progression save emits save-start/save-finish UI events.")]
    [SerializeField] private bool emitSaveFeedbackEvents = true;

    [Header("Debug")]
    [SerializeField] private bool logProgression = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private int currentLevelIndex = 0;
    [SerializeField] private bool runCompleted = false;

    [SerializeField] private string activeFloorId = string.Empty;
    [SerializeField] private string activeColorId = string.Empty;
    [SerializeField] private int activeTierInColor = 1;
    [SerializeField] private LevelObjectiveRule activeObjectiveRule = LevelObjectiveRule.DestroyRegularPercent;
    [SerializeField] private float activeRequiredPercent = 0.5f;

    [SerializeField] private int totalBreakableRegularBlocks = 0;
    [SerializeField] private int destroyedBreakableRegularBlocks = 0;
    [SerializeField] private int totalBreakableSpecialBlocks = 0;
    [SerializeField] private int destroyedBreakableSpecialBlocks = 0;

    [SerializeField] private float objectiveProgress01 = 0f;
    [SerializeField] private bool objectiveCompleted = false;

    private Coroutine pendingAutoAdvanceRoutine;
    private WorldLayout2D runtimeProceduralLayoutInstance;

    public int CurrentLevelIndex => currentLevelIndex;
    public bool IsRunCompleted => runCompleted;
    public float ObjectiveProgress01 => objectiveProgress01;
    public bool IsObjectiveCompleted => objectiveCompleted;

    public string ActiveFloorId => activeFloorId;
    public string ActiveColorId => activeColorId;
    public int ActiveTierInColor => activeTierInColor;
    public LevelObjectiveRule ActiveObjectiveRule => activeObjectiveRule;
    public float ActiveRequiredPercent => activeRequiredPercent;

    public int TotalBreakableRegularBlocks => totalBreakableRegularBlocks;
    public int DestroyedBreakableRegularBlocks => destroyedBreakableRegularBlocks;
    public int TotalBreakableSpecialBlocks => totalBreakableSpecialBlocks;
    public int DestroyedBreakableSpecialBlocks => destroyedBreakableSpecialBlocks;


    /// <summary>
    /// Exposes whether progression persistence is enabled (for runtime debug UI).
    /// </summary>
    public bool DebugPersistProgressionState => persistProgressionState;

    /// <summary>
    /// Exposes fully-resolved saved key for level index (includes sequence scoping if enabled).
    /// </summary>
    public string DebugLevelIndexSaveKey => BuildRunSaveKey("level_index");

    /// <summary>
    /// Exposes fully-resolved saved key for completion flag (includes sequence scoping if enabled).
    /// </summary>
    public string DebugRunCompletedSaveKey => BuildRunSaveKey("run_completed");

    /// <summary>
    /// Debug helper to read currently saved progression values without mutating runtime state.
    /// Returns false if persistence is off or no keys exist yet.
    /// </summary>
    public bool TryReadSavedProgressState(out int savedLevelIndex, out bool savedRunCompleted)
    {
        savedLevelIndex = 0;
        savedRunCompleted = false;

        if (!persistProgressionState)
        {
            return false;
        }

        string indexKey = BuildRunSaveKey("level_index");
        string completedKey = BuildRunSaveKey("run_completed");

        bool hasAnyKey = PlayerPrefs.HasKey(indexKey) || PlayerPrefs.HasKey(completedKey);
        if (!hasAnyKey)
        {
            return false;
        }

        savedLevelIndex = Mathf.Max(0, PlayerPrefs.GetInt(indexKey, 0));
        savedRunCompleted = PlayerPrefs.GetInt(completedKey, 0) == 1;
        return true;
    }


    private void Awake()
    {
        ResolveReferences();
        LoadPersistentProgressState();
    }

    private void OnEnable()
    {
        WorldBlock.Destroyed += OnWorldBlockDestroyed;

        if (worldGridLoader != null)
        {
            worldGridLoader.WorldLoaded += OnWorldLoaded;
        }
    }

    private void Start()
    {
        if (colorProgressBarUI != null)
        {
            colorProgressBarUI.Initialize(levelSequence);
        }

        if (loadCurrentLevelOnStart)
        {
            LoadCurrentLevelFromSequence();
        }
        else
        {
            RefreshColorProgressUI();
        }
    }

    private void OnDisable()
    {
        WorldBlock.Destroyed -= OnWorldBlockDestroyed;

        if (worldGridLoader != null)
        {
            worldGridLoader.WorldLoaded -= OnWorldLoaded;
        }

        if (pendingAutoAdvanceRoutine != null)
        {
            StopCoroutine(pendingAutoAdvanceRoutine);
            pendingAutoAdvanceRoutine = null;
        }

        ReleaseRuntimeProceduralLayout();
    }

    [ContextMenu("Go To Next Level")]
    public void GoToNextLevel()
    {
        if (levelSequence == null)
        {
            return;
        }

        currentLevelIndex++;

        // Completion sentinel: currentLevelIndex == LevelCount means run complete.
        if (currentLevelIndex >= levelSequence.LevelCount)
        {
            runCompleted = true;
            currentLevelIndex = levelSequence.LevelCount;

            objectiveCompleted = true;
            objectiveProgress01 = 1f;

            if (logProgression)
            {
                Debug.Log("LevelProgressionManager: Run completed.");
            }

            SavePersistentProgressState(emitSaveFeedbackEvents);
            RefreshColorProgressUI();
            return;
        }

        runCompleted = false;
        SavePersistentProgressState(emitSaveFeedbackEvents);

        LoadCurrentLevelFromSequence();
    }

    [ContextMenu("Reload Current Level")]
    public void ReloadCurrentLevel()
    {
        LoadCurrentLevelFromSequence();
    }

    /// <summary>
    /// Public API for "Continue" button.
    /// Reloads persisted state and loads appropriate level.
    /// </summary>
    public void ContinueFromPersistedStateAndLoad()
    {
        LoadPersistentProgressState();
        LoadCurrentLevelFromSequence();
    }

    /// <summary>
    /// Public API for run reset flows.
    /// - clearPersistedState=true removes saved progression keys.
    /// - loadFirstLevel=true immediately starts at level 0.
    /// </summary>
    public void ResetRunProgressToStart(bool clearPersistedState, bool loadFirstLevel)
    {
        runCompleted = false;
        currentLevelIndex = 0;

        activeFloorId = string.Empty;
        activeColorId = string.Empty;
        activeTierInColor = 1;
        activeObjectiveRule = LevelObjectiveRule.DestroyRegularPercent;
        activeRequiredPercent = 0.5f;

        totalBreakableRegularBlocks = 0;
        destroyedBreakableRegularBlocks = 0;
        totalBreakableSpecialBlocks = 0;
        destroyedBreakableSpecialBlocks = 0;

        objectiveProgress01 = 0f;
        objectiveCompleted = false;

        if (clearPersistedState)
        {
            ClearPersistentProgressState(emitSaveFeedbackEvents);
        }
        else
        {
            SavePersistentProgressState(emitSaveFeedbackEvents);
        }

        if (loadFirstLevel)
        {
            LoadCurrentLevelFromSequence();
        }
        else
        {
            RefreshColorProgressUI();
        }
    }

    private void ResolveReferences()
    {
        if (worldGridLoader == null && autoFindWorldGridLoader)
        {
            worldGridLoader = FindFirstObjectByType<WorldGridLoader>();
        }
    }

    /// <summary>
    /// Clones the authored layout and applies procedural tier distribution.
    /// This keeps source assets immutable while enabling per-load generation.
    /// </summary>
    private WorldLayout2D BuildRuntimeProceduralLayout(SequenceLevelEntry entry, int levelIndex)
    {
        if (entry == null || entry.layout == null)
        {
            return null;
        }

        ReleaseRuntimeProceduralLayout();

        runtimeProceduralLayoutInstance = Instantiate(entry.layout);
        runtimeProceduralLayoutInstance.name = $"{entry.layout.name}_Runtime_{levelIndex}";

        int seed = BuildProceduralSeed(entry.layout.floorId, levelIndex);
        ApplyTierSpreadToLayout(runtimeProceduralLayoutInstance, entry.colorId, entry.tierInColor, seed);

        return runtimeProceduralLayoutInstance;
    }

    private void ReleaseRuntimeProceduralLayout()
    {
        if (runtimeProceduralLayoutInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeProceduralLayoutInstance);
        }
        else
        {
            DestroyImmediate(runtimeProceduralLayoutInstance);
        }

        runtimeProceduralLayoutInstance = null;
    }

    /// <summary>
    /// Applies tier spread to breakable non-special cells:
    /// 1) score cells from procedural pattern
    /// 2) compute exact tier quotas from spread
    /// 3) assign lowest score->Tier1 ... highest score->Tier5
    /// </summary>
    private void ApplyTierSpreadToLayout(WorldLayout2D layout, string colorId, int tierInColor, int seed)
    {
        if (layout == null)
        {
            return;
        }

        layout.EnsureCellArraySize();

        List<ProceduralTierCandidate> candidates = new List<ProceduralTierCandidate>(layout.width * layout.height);

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockCellData cell = layout.GetCell(x, y);
                if (cell == null || !cell.hasBlock)
                {
                    continue;
                }

                if (cell.isSpecialConditionBlock || !cell.canBeDestroyed)
                {
                    continue;
                }

                float pattern01 = EvaluateTierPattern01(x, y, layout.width, layout.height, seed);
                float random01 = Hash01(x, y, seed ^ unchecked((int)0x9E3779B9u));
                float finalScore = Mathf.Lerp(pattern01, random01, proceduralTierRandomBlend);

                candidates.Add(new ProceduralTierCandidate
                {
                    x = x,
                    y = y,
                    score = finalScore
                });
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        candidates.Sort((a, b) => a.score.CompareTo(b.score));

        float[] weights = ResolveSpreadWeights(colorId, tierInColor);
        int[] tierCounts = ComputeExactTierCounts(candidates.Count, weights);

        int cursor = 0;
        for (int tier = 1; tier <= 5; tier++)
        {
            int take = tierCounts[tier - 1];
            for (int i = 0; i < take && cursor < candidates.Count; i++)
            {
                ProceduralTierCandidate candidate = candidates[cursor++];
                BlockCellData cell = layout.GetCell(candidate.x, candidate.y);
                if (cell == null)
                {
                    continue;
                }

                cell.tier = (BlockTier)tier;
                layout.SetCell(candidate.x, candidate.y, cell);
            }
        }
    }

    private float[] ResolveSpreadWeights(string colorId, int tierInColor)
    {
        if (proceduralTierSpreadEntries != null)
        {
            for (int i = 0; i < proceduralTierSpreadEntries.Length; i++)
            {
                TierSpreadEntry entry = proceduralTierSpreadEntries[i];
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

                return new[]
                {
                    Mathf.Max(0f, entry.tier1Weight),
                    Mathf.Max(0f, entry.tier2Weight),
                    Mathf.Max(0f, entry.tier3Weight),
                    Mathf.Max(0f, entry.tier4Weight),
                    Mathf.Max(0f, entry.tier5Weight),
                };
            }
        }

        int clampedTier = Mathf.Clamp(tierInColor, 1, 5);
        float[] fallback = new float[5];
        fallback[clampedTier - 1] = 1f;
        return fallback;
    }

    private static int[] ComputeExactTierCounts(int totalCount, float[] weights)
    {
        int[] counts = new int[5];
        if (totalCount <= 0)
        {
            return counts;
        }

        float totalWeight = 0f;
        for (int i = 0; i < 5; i++)
        {
            totalWeight += Mathf.Max(0f, weights != null && i < weights.Length ? weights[i] : 0f);
        }

        if (totalWeight <= 0f)
        {
            counts[0] = totalCount;
            return counts;
        }

        float[] fractions = new float[5];
        int assigned = 0;

        for (int i = 0; i < 5; i++)
        {
            float safeWeight = Mathf.Max(0f, weights[i]);
            float exact = safeWeight / totalWeight * totalCount;
            counts[i] = Mathf.FloorToInt(exact);
            fractions[i] = exact - counts[i];
            assigned += counts[i];
        }

        int remaining = totalCount - assigned;
        for (int r = 0; r < remaining; r++)
        {
            int bestIndex = 0;
            for (int i = 1; i < 5; i++)
            {
                if (fractions[i] > fractions[bestIndex])
                {
                    bestIndex = i;
                }
            }

            counts[bestIndex]++;
            fractions[bestIndex] = -1f;
        }

        return counts;
    }

    private float EvaluateTierPattern01(int x, int y, int width, int height, int seed)
    {
        float nx = x * proceduralTierNoiseScale;
        float ny = y * proceduralTierNoiseScale;

        switch (proceduralTierPattern)
        {
            case ProceduralTierPattern.BlobClusters:
            {
                float coarse = PerlinSeeded(nx * 0.45f, ny * 0.45f, seed);
                float detail = PerlinSeeded(nx * 1.6f, ny * 1.6f, seed + 17);
                return Mathf.Clamp01(coarse * 0.8f + detail * 0.2f);
            }

            case ProceduralTierPattern.RidgeBands:
            {
                float n = PerlinSeeded(nx * 1.2f, ny * 1.2f, seed);
                return 1f - Mathf.Abs(2f * n - 1f);
            }

            case ProceduralTierPattern.EdgeFalloff:
            {
                float cx = (width - 1) * 0.5f;
                float cy = (height - 1) * 0.5f;
                float dx = x - cx;
                float dy = y - cy;
                float maxRadius = Mathf.Max(0.0001f, Mathf.Sqrt(cx * cx + cy * cy));
                return Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxRadius);
            }

            case ProceduralTierPattern.MixedFractal:
            default:
            {
                float o1 = PerlinSeeded(nx * 0.50f, ny * 0.50f, seed);
                float o2 = PerlinSeeded(nx * 1.20f, ny * 1.20f, seed + 31);
                float o3 = PerlinSeeded(nx * 2.40f, ny * 2.40f, seed + 67);
                return Mathf.Clamp01(o1 * 0.55f + o2 * 0.30f + o3 * 0.15f);
            }
        }
    }

    private int BuildProceduralSeed(string floorId, int levelIndex)
    {
        unchecked
        {
            int hash = proceduralTierBaseSeed;
            hash = hash * 397 ^ levelIndex;
            hash = hash * 397 ^ StableStringHash(floorId);
            return hash;
        }
    }

    private static int StableStringHash(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
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

    /// <summary>
    /// Loads the level at currentLevelIndex from sequence.
    /// </summary>
    private void LoadCurrentLevelFromSequence()
    {
        ResolveReferences();

        if (levelSequence == null || worldGridLoader == null)
        {
            Debug.LogError("LevelProgressionManager: Missing levelSequence or worldGridLoader.");
            return;
        }

        if (!levelSequence.Validate(logMessages: false))
        {
            Debug.LogError("LevelProgressionManager: LevelSequenceDefinition validation failed.");
            return;
        }

        // Clamp to [0..LevelCount] where LevelCount means "completed sentinel index".
        currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, levelSequence.LevelCount);

        if (runCompleted || currentLevelIndex >= levelSequence.LevelCount)
        {
            runCompleted = true;
            currentLevelIndex = levelSequence.LevelCount;
            objectiveCompleted = true;
            objectiveProgress01 = 1f;

            SavePersistentProgressState(emitSaveFeedbackEvents);
            RefreshColorProgressUI();
            return;
        }

        SequenceLevelEntry entry = levelSequence.GetEntryAt(currentLevelIndex);
        if (entry == null || entry.layout == null)
        {
            Debug.LogError($"LevelProgressionManager: Invalid sequence entry at index {currentLevelIndex}.");
            return;
        }

        runCompleted = false;

        WorldLayout2D layoutToLoad = entry.layout;
        if (useProceduralTierDistribution)
        {
            layoutToLoad = BuildRuntimeProceduralLayout(entry, currentLevelIndex);
        }
        else
        {
            ReleaseRuntimeProceduralLayout();
        }

        if (layoutToLoad == null)
        {
            Debug.LogError("LevelProgressionManager: Failed to resolve runtime layout for loading.");
            return;
        }

        worldGridLoader.LoadSpecificLayout(layoutToLoad);
    }

    /// <summary>
    /// Called after WorldGridLoader has finished spawning blocks.
    /// </summary>
    private void OnWorldLoaded(WorldLayout2D loadedLayout)
    {
        if (loadedLayout == null || levelSequence == null)
        {
            return;
        }

        int resolvedIndex = levelSequence.GetIndexByFloorId(loadedLayout.floorId);
        if (resolvedIndex < 0)
        {
            // Ignore non-sequence loads.
            return;
        }

        currentLevelIndex = resolvedIndex;
        runCompleted = false;
        SavePersistentProgressState(emitSaveFeedbackEvents);

        SequenceLevelEntry activeEntry = levelSequence.GetEntryAt(currentLevelIndex);
        activeFloorId = loadedLayout.floorId;
        activeColorId = activeEntry != null ? activeEntry.colorId : string.Empty;
        activeTierInColor = activeEntry != null ? activeEntry.tierInColor : 1;

        RebuildObjectiveTargetsFromSpawnedBlocks();
        ConfigureObjectiveRule();
        EvaluateObjectiveProgress();
        RefreshColorProgressUI();

        if (logProgression)
        {
            Debug.Log(
                $"LevelProgressionManager: Loaded level index={currentLevelIndex}, floorId='{activeFloorId}', " +
                $"color='{activeColorId}', tier={activeTierInColor}, objective={activeObjectiveRule}");
        }
    }

    /// <summary>
    /// Recounts breakable regular/special blocks for objective tracking.
    /// </summary>
    private void RebuildObjectiveTargetsFromSpawnedBlocks()
    {
        totalBreakableRegularBlocks = 0;
        destroyedBreakableRegularBlocks = 0;

        totalBreakableSpecialBlocks = 0;
        destroyedBreakableSpecialBlocks = 0;

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

            if (!block.CanBeDestroyed)
            {
                continue;
            }

            if (block.IsSpecialConditionBlock)
            {
                totalBreakableSpecialBlocks++;
            }
            else
            {
                totalBreakableRegularBlocks++;
            }
        }
    }

    /// <summary>
    /// Picks objective rule for active tier.
    /// Tier 1..4 => regular block destroy percent.
    /// Tier 5 => destroy special block (with fallback if needed).
    /// </summary>
    private void ConfigureObjectiveRule()
    {
        objectiveCompleted = false;
        objectiveProgress01 = 0f;

        if (activeTierInColor >= 5)
        {
            activeObjectiveRule = LevelObjectiveRule.DestroySpecialBlock;
            activeRequiredPercent = 1f;

            if (totalBreakableSpecialBlocks <= 0 && fallbackToRegularPercentWhenNoSpecialTargets)
            {
                activeObjectiveRule = LevelObjectiveRule.DestroyRegularPercent;
                activeRequiredPercent = Mathf.Clamp01(fallbackRegularPercentForTier5);
            }

            return;
        }

        activeObjectiveRule = LevelObjectiveRule.DestroyRegularPercent;
        activeRequiredPercent = GetRequiredPercentForTier(activeTierInColor);
    }

    private float GetRequiredPercentForTier(int tierInColor)
    {
        switch (tierInColor)
        {
            case 1: return Mathf.Clamp01(tier1RequiredRegularDestroyPercent);
            case 2: return Mathf.Clamp01(tier2RequiredRegularDestroyPercent);
            case 3: return Mathf.Clamp01(tier3RequiredRegularDestroyPercent);
            case 4: return Mathf.Clamp01(tier4RequiredRegularDestroyPercent);
            default: return Mathf.Clamp01(fallbackRegularPercentForTier5);
        }
    }

    private void OnWorldBlockDestroyed(WorldBlock block)
    {
        if (block == null || runCompleted)
        {
            return;
        }

        if (!string.Equals(block.FloorId, activeFloorId, System.StringComparison.Ordinal))
        {
            return;
        }

        if (!block.CanBeDestroyed)
        {
            return;
        }

        if (block.IsSpecialConditionBlock)
        {
            destroyedBreakableSpecialBlocks++;
        }
        else
        {
            destroyedBreakableRegularBlocks++;
        }

        bool wasCompleted = objectiveCompleted;

        EvaluateObjectiveProgress();
        RefreshColorProgressUI();

        if (!wasCompleted && objectiveCompleted)
        {
            OnCurrentLevelObjectiveCompleted();
        }
    }

    /// <summary>
    /// Computes objectiveProgress01 and completion flag from current counters.
    /// </summary>
    private void EvaluateObjectiveProgress()
    {
        if (activeObjectiveRule == LevelObjectiveRule.DestroyRegularPercent)
        {
            if (totalBreakableRegularBlocks <= 0 || activeRequiredPercent <= 0f)
            {
                objectiveProgress01 = 1f;
                objectiveCompleted = true;
                return;
            }

            float regularDestroyedPercent = (float)destroyedBreakableRegularBlocks / Mathf.Max(1, totalBreakableRegularBlocks);
            objectiveProgress01 = Mathf.Clamp01(regularDestroyedPercent / activeRequiredPercent);
            objectiveCompleted = regularDestroyedPercent >= activeRequiredPercent;
            return;
        }

        if (totalBreakableSpecialBlocks <= 0)
        {
            objectiveProgress01 = 0f;
            objectiveCompleted = false;
            return;
        }

        objectiveProgress01 = destroyedBreakableSpecialBlocks > 0 ? 1f : 0f;
        objectiveCompleted = destroyedBreakableSpecialBlocks > 0;
    }

    private void OnCurrentLevelObjectiveCompleted()
    {
        if (logProgression)
        {
            Debug.Log(
                $"LevelProgressionManager: Objective completed at level index={currentLevelIndex}, floorId='{activeFloorId}'.");
        }

        if (!autoAdvanceOnComplete)
        {
            return;
        }

        if (pendingAutoAdvanceRoutine != null)
        {
            StopCoroutine(pendingAutoAdvanceRoutine);
        }

        pendingAutoAdvanceRoutine = StartCoroutine(AutoAdvanceAfterDelay());
    }

    private IEnumerator AutoAdvanceAfterDelay()
    {
        float delay = Mathf.Max(0f, autoAdvanceDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        pendingAutoAdvanceRoutine = null;

        if (!objectiveCompleted || runCompleted)
        {
            yield break;
        }

        GoToNextLevel();
    }

    /// <summary>
    /// Pushes current progression state into color bar UI.
    /// </summary>
    private void RefreshColorProgressUI()
    {
        if (colorProgressBarUI == null || levelSequence == null)
        {
            return;
        }

        colorProgressBarUI.Refresh(
            levelSequence,
            currentLevelIndex,
            objectiveProgress01,
            runCompleted);
    }

    /// <summary>
    /// Loads progression state from PlayerPrefs into runtime fields.
    /// What this does, step-by-step:
    /// 1) Early-out if persistence is disabled (designer can run fully in-memory).
    /// 2) Resolve final keys using BuildRunSaveKey(...) so sequence scoping is respected.
    /// 3) Read raw values from PlayerPrefs with safe defaults.
    /// 4) Clamp/sanitize loaded values before assigning runtime fields.
    /// 5) Do NOT load/respawn world here; this method only restores raw state.
    ///    World loading is handled later by ContinueFromPersistedStateAndLoad / Start flow.
    /// </summary>
    private void LoadPersistentProgressState()
    {
        // If persistence is off, leave current in-memory values unchanged.
        if (!persistProgressionState)
        {
            return;
        }

        // Key names are centralized so if prefix/sequence scoping changes,
        // all load/save/clear paths still stay consistent.
        string indexKey = BuildRunSaveKey("level_index");
        string completedKey = BuildRunSaveKey("run_completed");

        // Read persisted values with safe fallbacks:
        // - index defaults to 0 (first level)
        // - completion defaults to false
        int loadedIndex = PlayerPrefs.GetInt(indexKey, 0);
        bool loadedCompleted = PlayerPrefs.GetInt(completedKey, 0) == 1;

        // Sanitize before assigning:
        // - index should never be negative
        currentLevelIndex = Mathf.Max(0, loadedIndex);
        runCompleted = loadedCompleted;
    }


    /// <summary>
    /// Public explicit save entry point for debug/UI.
    /// This only writes progression state keys; it does not reload/spawn anything.
    /// </summary>
    public void SaveProgressionStateNow(bool withFeedback)
    {
        SavePersistentProgressState(withFeedback);
    }



    private void SavePersistentProgressState(bool withFeedback)
    {
        if (!persistProgressionState)
        {
            return;
        }

        void Write()
        {
            string indexKey = BuildRunSaveKey("level_index");
            string completedKey = BuildRunSaveKey("run_completed");

            PlayerPrefs.SetInt(indexKey, Mathf.Max(0, currentLevelIndex));
            PlayerPrefs.SetInt(completedKey, runCompleted ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (withFeedback)
        {
            SaveFeedbackEvents.RunWithFeedback(Write);
        }
        else
        {
            Write();
        }
    }

    private void ClearPersistentProgressState(bool withFeedback)
    {
        void Clear()
        {
            PlayerPrefs.DeleteKey(BuildRunSaveKey("level_index"));
            PlayerPrefs.DeleteKey(BuildRunSaveKey("run_completed"));
            PlayerPrefs.Save();
        }

        if (withFeedback)
        {
            SaveFeedbackEvents.RunWithFeedback(Clear);
        }
        else
        {
            Clear();
        }
    }

    private string BuildRunSaveKey(string token)
    {
        string sequenceToken = "default";

        if (useSequenceScopedSaveKeys && levelSequence != null && !string.IsNullOrWhiteSpace(levelSequence.SequenceId))
        {
            sequenceToken = SanitizeSaveToken(levelSequence.SequenceId);
        }

        return $"{progressionSaveKeyPrefix}.{sequenceToken}.{token}";
    }

    private static string SanitizeSaveToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "empty";
        }

        char[] chars = raw.ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];

            bool valid =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '_' || c == '-';

            if (!valid)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    [ContextMenu("Clear Saved Progression (Debug)")]
    public void ClearSavedProgressionDebug()
    {
        ResetRunProgressToStart(clearPersistedState: true, loadFirstLevel: false);
    }

    /// <summary>
    /// Debug utility: instantly satisfies current objective and triggers normal completion flow.
    /// </summary>
    [ContextMenu("Complete Current Objective (Debug)")]
    public void CompleteCurrentObjectiveDebug()
    {
        if (runCompleted)
        {
            return;
        }

        if (activeObjectiveRule == LevelObjectiveRule.DestroyRegularPercent)
        {
            totalBreakableRegularBlocks = Mathf.Max(totalBreakableRegularBlocks, 1);
            destroyedBreakableRegularBlocks = totalBreakableRegularBlocks;
        }
        else
        {
            totalBreakableSpecialBlocks = Mathf.Max(totalBreakableSpecialBlocks, 1);
            destroyedBreakableSpecialBlocks = totalBreakableSpecialBlocks;
        }

        bool wasCompleted = objectiveCompleted;

        EvaluateObjectiveProgress();
        RefreshColorProgressUI();

        if (!wasCompleted && objectiveCompleted)
        {
            OnCurrentLevelObjectiveCompleted();
        }
    }



    /// <summary>
    /// Public explicit clear entry point for debug/UI.
    /// Clears persisted progression keys only.
    /// </summary>
    public void ClearProgressionStateNow(bool withFeedback)
    {
        ClearPersistentProgressState(withFeedback);
    }


    /// <summary>
    /// Autosave progression when app goes to background.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            return;
        }

        SaveProgressionStateNow(withFeedback: false);
    }

    /// <summary>
    /// Autosave progression on app quit.
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveProgressionStateNow(withFeedback: false);
    }


}
