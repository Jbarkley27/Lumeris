using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Owns between-round intermission UI:
/// - Shows summary / skills / blaster / ship-world tabs
/// - Tracks active tab button state
/// - Shows Retry OR Next based on win/loss
/// - Optional Endless button when campaign completion flag is true
/// - Optionally disables gameplay behaviours while intermission is open
///
/// Note:
/// - This does not own the in-round timer HUD (keep that in RoundTimerRoundEndUI or similar).
/// - Round damage is fed via AddRoundDamage(...), so gameplay systems can report any damage source.
/// </summary>
public class IntermissionUIController : MonoBehaviour
{
    public enum IntermissionTab
    {
        Summary = 0,
        Skills = 1,
        Blaster = 2,
        World = 3,
        Ship = 4
    }


    [Header("References")]
    [SerializeField] private LevelProgressionManager levelProgressionManager;
    [SerializeField] private bool autoFindLevelProgressionManager = true;

    [Tooltip("Optional for round glass delta display.")]
    [SerializeField] private RunCurrencyManager runCurrencyManager;
    [SerializeField] private bool autoFindRunCurrencyManager = true;

    [Header("Root")]
    [SerializeField] private GameObject intermissionRoot;

    [Header("Tab Buttons")]
    [SerializeField] private Button summaryTabButton;
    [SerializeField] private Button skillsTabButton;
    [SerializeField] private Button blasterTabButton;
    [SerializeField] private Button worldTabButton;
    [SerializeField] private Button shipTabButton;

    [Header("Tab Panel Roots")]
    [SerializeField] private GameObject summaryPanelRoot;
    [SerializeField] private GameObject skillsPanelRoot;
    [SerializeField] private GameObject blasterPanelRoot;
    [SerializeField] private GameObject worldPanelRoot;
    [SerializeField] private GameObject shipPanelRoot;

    [Header("Back To Summary Buttons (Optional)")]
    [SerializeField] private Button[] backToSummaryButtons = Array.Empty<Button>();

    [Header("Summary Texts")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text nextOrBlocksLeftText;
    [SerializeField] private TMP_Text blocksDestroyedText;
    [SerializeField] private TMP_Text damageDoneText;
    [SerializeField] private TMP_Text glassEarnedText;
    [SerializeField] private TMP_Text currentLevelText;

    [Header("Summary Labels")]
    [SerializeField] private string winLabel = "WIN";
    [SerializeField] private string lossLabel = "LOSS";
    [SerializeField] private string nextLevelPrefix = "Next Level:";
    [SerializeField] private string blocksLeftPrefix = "Blocks Left:";
    [SerializeField] private string blocksDestroyedPrefix = "Blocks Destroyed:";
    [SerializeField] private string damageDonePrefix = "Damage Done:";
    [SerializeField] private string glassEarnedPrefix = "Glass Earned:";
    [SerializeField] private string currentLevelPrefix = "Current Level:";

    [Header("Footer Actions")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button endlessButton;

    [Tooltip("If true, endless button visibility can be driven by LevelProgressionManager.IsRunCompleted.")]
    [SerializeField] private bool useRunCompletedForEndlessAvailability = true;

    [Tooltip("Manual fallback for endless availability when not using runCompleted flag.")]
    [SerializeField] private bool endlessAvailableManual = false;

    [Tooltip("Called when Endless button is pressed.")]
    [SerializeField] private UnityEvent onEndlessClicked;

    [Header("Gameplay Lock (Optional)")]
    [Tooltip("These behaviours will be disabled while intermission is open, then restored.")]
    [SerializeField] private Behaviour[] gameplayBehavioursToDisable = Array.Empty<Behaviour>();
    [SerializeField] private GameObject[] uiRootsToHideDuringIntermission = Array.Empty<GameObject>();

    [Header("Runtime (Read Only)")]
    [SerializeField] private IntermissionTab activeTab = IntermissionTab.Summary;
    [SerializeField] private bool isIntermissionOpen = false;
    [SerializeField] private float roundDamageDone = 0f;
    [SerializeField] private double roundGlassEarned = 0d;

    private LevelProgressionManager.RoundState lastRoundState = LevelProgressionManager.RoundState.RoundOver;
    private double roundGlassStartValue = 0d;
    // UI-side guard so Next cannot be spammed during level transition.
    private bool isAdvancingLevel = false;
    // Tracks whether gameplay lock is currently active so we never double-cache disabled states.
    private bool gameplayLockApplied = false;

    // Stores original enabled state for behaviours so we restore correctly.
    private readonly Dictionary<Behaviour, bool> cachedBehaviourEnabledStates = new Dictionary<Behaviour, bool>();

    public bool IsIntermissionOpen => isIntermissionOpen;
    public IntermissionTab ActiveTab => activeTab;




    private void Awake()
    {
        ResolveReferences();
        WireButtons();

        // Intermission should start hidden.
        SetActiveIfChanged(intermissionRoot, false);
        isIntermissionOpen = false;

        // Baseline state so first transition logic is clean.
        if (levelProgressionManager != null)
        {
            lastRoundState = levelProgressionManager.CurrentRoundState;
        }
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (levelProgressionManager != null)
        {
            levelProgressionManager.RoundEnded += OnRoundEnded;
        }

        // Force initial refresh.
        RefreshStateFromManager(force: true);
    }

    private void OnDisable()
    {
        if (levelProgressionManager != null)
        {
            levelProgressionManager.RoundEnded -= OnRoundEnded;
        }

        // Safety restore if object disables while intermission is open.
        ApplyGameplayLock(false);
    }

    private void Update()
    {
        if (levelProgressionManager == null || autoFindLevelProgressionManager)
        {
            ResolveReferences();
        }

        RefreshStateFromManager(force: false);

        // While panel is open, keep summary stats fresh.
        if (isIntermissionOpen)
        {
            UpdateSummaryStats();
        }
    }

    /// <summary>
    /// Called by any damage source to contribute to round "Damage Done".
    /// This should include all player-caused damage channels.
    /// </summary>
    public void AddRoundDamage(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        roundDamageDone += amount;
        if (isIntermissionOpen)
        {
            UpdateSummaryStats();
        }
    }

    /// <summary>
    /// External/manual way to unlock endless button if needed.
    /// </summary>
    public void SetEndlessButtonAvailable(bool available)
    {
        endlessAvailableManual = available;
        if (isIntermissionOpen)
        {
            ApplyFooterButtonRules(levelProgressionManager != null ? levelProgressionManager.CurrentRoundState : LevelProgressionManager.RoundState.RoundOver);
        }
    }

    private void OnRoundEnded(LevelProgressionManager.RoundState state)
    {
        // Intermission opens only on terminal result states.
        if (state == LevelProgressionManager.RoundState.Won || state == LevelProgressionManager.RoundState.Lost)
        {
            OpenIntermission(state);
        }
    }

    private void RefreshStateFromManager(bool force)
    {
        if (levelProgressionManager == null)
        {
            return;
        }

        LevelProgressionManager.RoundState current = levelProgressionManager.CurrentRoundState;

        // Detect transition into active play to reset per-round accumulators and close panel.
        if (current == LevelProgressionManager.RoundState.Playing && (force || lastRoundState != LevelProgressionManager.RoundState.Playing))
        {
            // New round started successfully; unlock Next interaction for future wins.
            isAdvancingLevel = false;
            if (nextButton != null)
            {
                nextButton.interactable = true;
            }

            BeginRoundTracking();
            CloseIntermission();
        }

        // Detect transitions to result states in case event was missed.
        if ((current == LevelProgressionManager.RoundState.Won || current == LevelProgressionManager.RoundState.Lost) &&
            (force || current != lastRoundState))
        {
            OpenIntermission(current);
        }

        lastRoundState = current;
    }

    private void BeginRoundTracking()
    {
        roundDamageDone = 0f;
        roundGlassEarned = 0d;

        if (runCurrencyManager != null)
        {
            roundGlassStartValue = runCurrencyManager.CurrentGlass;
        }
        else
        {
            roundGlassStartValue = 0d;
        }
    }

    private void OpenIntermission(LevelProgressionManager.RoundState endState)
    {
        // If already open, refresh visible content only.
        // Important: do NOT re-apply gameplay lock, or we may cache "already disabled" states
        // and fail to restore original enabled values later.
        if (isIntermissionOpen)
        {
            ApplyResultHeader(endState);
            UpdateSummaryStats();
            ApplyFooterButtonRules(endState);
            return;
        }

        isIntermissionOpen = true;
        SetActiveIfChanged(intermissionRoot, true);

        // Default to summary tab whenever intermission opens.
        SwitchTab(IntermissionTab.Summary);

        ApplyResultHeader(endState);
        UpdateSummaryStats();
        ApplyFooterButtonRules(endState);

        // Disable gameplay behaviours while intermission is active.
        ApplyGameplayLock(true);
    }

    private void CloseIntermission()
    {
        if (!isIntermissionOpen)
        {
            return;
        }

        isIntermissionOpen = false;
        SetActiveIfChanged(intermissionRoot, false);

        // Restore gameplay scripts.
        ApplyGameplayLock(false);
    }

    private void ApplyResultHeader(LevelProgressionManager.RoundState endState)
    {
        bool won = endState == LevelProgressionManager.RoundState.Won;

        if (resultText != null)
        {
            resultText.text = won ? winLabel : lossLabel;
        }

        if (nextOrBlocksLeftText != null && levelProgressionManager != null)
        {
            if (won)
            {
                // Keep this simple and deterministic from active color + tier.
                // After next press, progression manager advances.
                string levelLabel = BuildNextLevelLabel();
                nextOrBlocksLeftText.text = $"{nextLevelPrefix} {levelLabel}";
            }
            else
            {
                int total = levelProgressionManager.TotalBreakableRegularBlocks + levelProgressionManager.TotalBreakableSpecialBlocks;
                int destroyed = levelProgressionManager.DestroyedBreakableRegularBlocks + levelProgressionManager.DestroyedBreakableSpecialBlocks;
                int left = Mathf.Max(0, total - destroyed);

                nextOrBlocksLeftText.text = $"{blocksLeftPrefix} {left}";
            }
        }
    }

    private void UpdateSummaryStats()
    {
        if (levelProgressionManager == null)
        {
            return;
        }

        int total = levelProgressionManager.TotalBreakableRegularBlocks + levelProgressionManager.TotalBreakableSpecialBlocks;
        int destroyed = levelProgressionManager.DestroyedBreakableRegularBlocks + levelProgressionManager.DestroyedBreakableSpecialBlocks;

        if (blocksDestroyedText != null)
        {
            blocksDestroyedText.text = $"{blocksDestroyedPrefix} {destroyed}/{Mathf.Max(0, total)}";
        }

        if (damageDoneText != null)
        {
            damageDoneText.text = $"{damageDonePrefix} {Mathf.RoundToInt(Mathf.Max(0f, roundDamageDone))}";
        }

        if (runCurrencyManager != null)
        {
            roundGlassEarned = Math.Max(0d, runCurrencyManager.CurrentGlass - roundGlassStartValue);
        }

        if (glassEarnedText != null)
        {
            glassEarnedText.text = $"{glassEarnedPrefix} {Math.Floor(Math.Max(0d, roundGlassEarned)):0}";
        }

        if (currentLevelText != null)
        {
            currentLevelText.text = $"{currentLevelPrefix} {BuildCurrentLevelLabel()}";
        }
    }

    private string BuildCurrentLevelLabel()
    {
        if (levelProgressionManager == null)
        {
            return "Unknown";
        }

        string color = string.IsNullOrWhiteSpace(levelProgressionManager.ActiveColorId)
            ? "Unknown"
            : levelProgressionManager.ActiveColorId;

        int tier = Mathf.Max(1, levelProgressionManager.ActiveTierInColor);
        return $"{color} - Tier {tier}";
    }

    private void ApplyFooterButtonRules(LevelProgressionManager.RoundState endState)
    {
        bool won = endState == LevelProgressionManager.RoundState.Won;
        bool lost = endState == LevelProgressionManager.RoundState.Lost;

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(lost);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(won);
            // Ensure button is clickable when panel first opens after a win.
            nextButton.interactable = won;
        }

        bool endlessAvailable = false;
        if (useRunCompletedForEndlessAvailability && levelProgressionManager != null)
        {
            endlessAvailable = levelProgressionManager.IsRunCompleted;
        }
        else
        {
            endlessAvailable = endlessAvailableManual;
        }

        // Endless is a separate button and only makes sense after a win context.
        if (endlessButton != null)
        {
            endlessButton.gameObject.SetActive(won && endlessAvailable);
        }
    }

    private void WireButtons()
    {
        // Tab buttons
        if (summaryTabButton != null)
        {
            summaryTabButton.onClick.RemoveListener(OnSummaryTabClicked);
            summaryTabButton.onClick.AddListener(OnSummaryTabClicked);
        }

        if (skillsTabButton != null)
        {
            skillsTabButton.onClick.RemoveListener(OnSkillsTabClicked);
            skillsTabButton.onClick.AddListener(OnSkillsTabClicked);
        }

        if (blasterTabButton != null)
        {
            blasterTabButton.onClick.RemoveListener(OnBlasterTabClicked);
            blasterTabButton.onClick.AddListener(OnBlasterTabClicked);
        }

        if (worldTabButton != null)
        {
            worldTabButton.onClick.RemoveListener(OnWorldTabClicked);
            worldTabButton.onClick.AddListener(OnWorldTabClicked);
        }

        if (shipTabButton != null)
        {
            shipTabButton.onClick.RemoveListener(OnShipTabClicked);
            shipTabButton.onClick.AddListener(OnShipTabClicked);
        }


        // Back-to-summary buttons from other tabs
        if (backToSummaryButtons != null)
        {
            for (int i = 0; i < backToSummaryButtons.Length; i++)
            {
                Button b = backToSummaryButtons[i];
                if (b == null) continue;

                b.onClick.RemoveListener(OnSummaryTabClicked);
                b.onClick.AddListener(OnSummaryTabClicked);
            }
        }

        // Footer actions
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnNextClicked);
            nextButton.onClick.AddListener(OnNextClicked);
        }

        if (endlessButton != null)
        {
            endlessButton.onClick.RemoveListener(OnEndlessClicked);
            endlessButton.onClick.AddListener(OnEndlessClicked);
        }
    }

    private void OnSummaryTabClicked() => SwitchTab(IntermissionTab.Summary);
    private void OnSkillsTabClicked() => SwitchTab(IntermissionTab.Skills);
    private void OnBlasterTabClicked() => SwitchTab(IntermissionTab.Blaster);
    private void OnWorldTabClicked() => SwitchTab(IntermissionTab.World);
    private void OnShipTabClicked() => SwitchTab(IntermissionTab.Ship);

    private void SwitchTab(IntermissionTab tab)
    {
        activeTab = tab;

        SetActiveIfChanged(summaryPanelRoot, tab == IntermissionTab.Summary);
        SetActiveIfChanged(skillsPanelRoot, tab == IntermissionTab.Skills);
        SetActiveIfChanged(blasterPanelRoot, tab == IntermissionTab.Blaster);
        SetActiveIfChanged(worldPanelRoot, tab == IntermissionTab.World);
        SetActiveIfChanged(shipPanelRoot, tab == IntermissionTab.Ship);

        ApplyTabButtonState(summaryTabButton, tab == IntermissionTab.Summary);
        ApplyTabButtonState(skillsTabButton, tab == IntermissionTab.Skills);
        ApplyTabButtonState(blasterTabButton, tab == IntermissionTab.Blaster);
        ApplyTabButtonState(worldTabButton, tab == IntermissionTab.World);
        ApplyTabButtonState(shipTabButton, tab == IntermissionTab.Ship);
    }


    private static void ApplyTabButtonState(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = !isActive;
    }

    private void OnRetryClicked()
    {
        if (levelProgressionManager == null)
        {
            return;
        }

        CloseIntermission();
        levelProgressionManager.RetryCurrentLevelRound();
    }

    private void OnNextClicked()
    {
        if (levelProgressionManager == null || isAdvancingLevel)
        {
            return;
        }

        // One-way gate until progression transitions back into Playing state.
        isAdvancingLevel = true;
        if (nextButton != null)
        {
            nextButton.interactable = false;
        }

        CloseIntermission();
        levelProgressionManager.ContinueToNextLevelAfterRoundWin();
    }

    private void OnEndlessClicked()
    {
        // Intermission closes here as well; endless flow owner can decide final destination.
        CloseIntermission();
        onEndlessClicked?.Invoke();
    }

    private void ResolveReferences()
    {
        if (levelProgressionManager == null && autoFindLevelProgressionManager)
        {
            levelProgressionManager = FindFirstObjectByType<LevelProgressionManager>();
        }

        if (runCurrencyManager == null && autoFindRunCurrencyManager)
        {
            runCurrencyManager = FindFirstObjectByType<RunCurrencyManager>();
        }
    }

    private void ApplyGameplayLock(bool lockGameplay)
    {
        if (lockGameplay)
        {
            if (gameplayLockApplied)
            {
                return;
            }

            // Cache original states and disable.
            cachedBehaviourEnabledStates.Clear();

            for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
            {
                Behaviour b = gameplayBehavioursToDisable[i];
                if (b == null)
                {
                    continue;
                }

                cachedBehaviourEnabledStates[b] = b.enabled;
                b.enabled = false;
            }


            // Additionally, hide any specified UI roots to prevent interaction.
            for (int i = 0; i < uiRootsToHideDuringIntermission.Length; i++)
            {
                GameObject root = uiRootsToHideDuringIntermission[i];
                if (root != null) root.SetActive(false);
            }

            gameplayLockApplied = true;

            return;
        }

        if (!gameplayLockApplied)
        {
            return;
        }

        // Restore original states.
        foreach (var pair in cachedBehaviourEnabledStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        // Restore UI roots as well.
        for (int i = 0; i < uiRootsToHideDuringIntermission.Length; i++)
        {
            GameObject root = uiRootsToHideDuringIntermission[i];
            if (root != null) root.SetActive(true);
        }


        cachedBehaviourEnabledStates.Clear();
        gameplayLockApplied = false;
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        if (target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }



    private string BuildNextLevelLabel()
    {
        if (levelProgressionManager == null)
        {
            return "Unknown";
        }

        int nextIndex = levelProgressionManager.CurrentLevelIndex + 1;
        if (levelProgressionManager.TryGetLevelLabelAtIndex(nextIndex, out string nextLabel))
        {
            return nextLabel;
        }

        return "Complete";
    }

}
