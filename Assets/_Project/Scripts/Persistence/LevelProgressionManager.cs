using System.Collections;
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
        worldGridLoader.LoadSpecificLayout(entry.layout);
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
