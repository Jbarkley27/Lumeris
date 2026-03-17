using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI bridge for the new timer-based round loop.
/// Responsibilities:
/// - Render remaining round time
/// - Render current level block-clear progress
/// - Show round-end panel on win/loss
/// - Show only Retry on loss, only Next on win
/// </summary>
public class RoundTimerRoundEndUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Progression manager that owns round state + timer values.")]
    [SerializeField] private LevelProgressionManager levelProgressionManager;

    [Tooltip("Auto-find LevelProgressionManager if not assigned.")]
    [SerializeField] private bool autoFindManager = true;

    [Header("Timer UI")]
    [Tooltip("Optional root object for timer UI.")]
    [SerializeField] private GameObject timerRoot;

    [Tooltip("Text that displays remaining time.")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Color while timer is in normal range.")]
    [SerializeField] private Color timerNormalColor = Color.white;

    [Tooltip("Color while timer is in warning range.")]
    [SerializeField] private Color timerWarningColor = new Color(1f, 0.35f, 0.35f);

    [Min(0f)]
    [Tooltip("Switch to warning color when remaining time <= this value.")]
    [SerializeField] private float warningThresholdSeconds = 10f;

    [Header("Block Progress UI")]
    [Tooltip("Optional root object for per-level blocks-destroyed progress.")]
    [SerializeField] private GameObject blockProgressRoot;

    [Tooltip("Slider that represents current level clear progress (destroyed / total).")]
    [SerializeField] private Slider blockProgressSlider;

    [Tooltip("Optional text showing destroyed/total counts.")]
    [SerializeField] private TMP_Text blockProgressText;

    [Tooltip("Format for progress text. {0}=destroyed, {1}=total, {2}=percent.")]
    [SerializeField] private string blockProgressFormat = "{0}/{1}";

    [Tooltip("Hide block progress root when current level has no breakable blocks.")]
    [SerializeField] private bool hideBlockProgressWhenNoTargets = false;

    [Header("Round End UI")]
    [Tooltip("Root object for the end-of-round panel.")]
    [SerializeField] private GameObject roundEndRoot;

    [Tooltip("Text label for Win/Loss status.")]
    [SerializeField] private TMP_Text roundResultText;

    [Tooltip("Retry button (shown on loss only).")]
    [SerializeField] private Button retryButton;

    [Tooltip("Next button (shown on win only).")]
    [SerializeField] private Button nextButton;

    [Tooltip("Result message shown when player wins.")]
    [SerializeField] private string winMessage = "Round Complete";

    [Tooltip("Result message shown when player loses.")]
    [SerializeField] private string lossMessage = "Round Failed";

    // Cache last state so we only rebuild panel visuals when state changes.
    private LevelProgressionManager.RoundState lastAppliedState = LevelProgressionManager.RoundState.RoundOver;

    private void Awake()
    {
        ResolveManager();
        ConfigureBlockProgressSliderDefaults();
        WireButtons();
    }

    private void OnEnable()
    {
        ResolveManager();

        // We subscribe for immediate response on win/loss.
        if (levelProgressionManager != null)
        {
            levelProgressionManager.RoundEnded += OnRoundEnded;
        }

        // Force initial visual state.
        RefreshAllVisuals(force: true);
    }

    private void OnDisable()
    {
        if (levelProgressionManager != null)
        {
            levelProgressionManager.RoundEnded -= OnRoundEnded;
        }
    }

    private void Update()
    {
        // Late resolve if manager spawns after this UI.
        if (levelProgressionManager == null && autoFindManager)
        {
            ResolveManager();
            RefreshAllVisuals(force: true);
        }

        // Keep timer text smooth and hide/show panel if state changes.
        RefreshAllVisuals(force: false);
    }

    private void OnRoundEnded(LevelProgressionManager.RoundState _)
    {
        // Event gives immediate transition for win/loss.
        RefreshAllVisuals(force: true);
    }

    /// <summary>
    /// Refreshes timer + panel visuals.
    /// - Timer updates every frame.
    /// - Panel layout only updates when round state changes (unless forced).
    /// </summary>
    private void RefreshAllVisuals(bool force)
    {
        if (levelProgressionManager == null)
        {
            SetActiveIfChanged(timerRoot, false);
            SetActiveIfChanged(blockProgressRoot, false);
            SetActiveIfChanged(roundEndRoot, false);
            if (timerText != null) timerText.text = "--:--";
            if (blockProgressText != null) blockProgressText.text = "0/0";
            return;
        }

        // Timer is shown whenever a level is loaded and running/end result is visible.
        // If you only want it during active play, set this to (state == Playing).
        LevelProgressionManager.RoundState state = levelProgressionManager.CurrentRoundState;
        bool showTimer = state == LevelProgressionManager.RoundState.Playing
                         || state == LevelProgressionManager.RoundState.Won
                         || state == LevelProgressionManager.RoundState.Lost;

        SetActiveIfChanged(timerRoot, showTimer);
        UpdateTimerText();
        UpdateBlockProgressUI(showTimer);

        // Only rebuild end panel when needed.
        if (force || state != lastAppliedState)
        {
            ApplyRoundStateVisuals(state);
            lastAppliedState = state;
        }
    }

    /// <summary>
    /// Applies end panel visibility and Retry/Next button rules:
    /// - Won  => show panel, show Next, hide Retry
    /// - Lost => show panel, show Retry, hide Next
    /// - Other states => hide panel
    /// </summary>
    private void ApplyRoundStateVisuals(LevelProgressionManager.RoundState state)
    {
        bool isWon = state == LevelProgressionManager.RoundState.Won;
        bool isLost = state == LevelProgressionManager.RoundState.Lost;
        bool showPanel = isWon || isLost;

        SetActiveIfChanged(roundEndRoot, showPanel);

        if (!showPanel)
        {
            return;
        }

        if (roundResultText != null)
        {
            roundResultText.text = isWon ? winMessage : lossMessage;
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(isLost);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(isWon);
        }
    }

    /// <summary>
    /// Formats remaining time as MM:SS.
    /// </summary>
    private void UpdateTimerText()
    {
        if (timerText == null || levelProgressionManager == null)
        {
            return;
        }

        float remaining = Mathf.Max(0f, levelProgressionManager.RemainingRoundSeconds);
        int totalSeconds = Mathf.CeilToInt(remaining);

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = $"{minutes:00}:{seconds:00}";
        timerText.color = remaining <= warningThresholdSeconds ? timerWarningColor : timerNormalColor;
    }

    /// <summary>
    /// Updates destroyed-block slider + text from LevelProgressionManager counters.
    /// </summary>
    private void UpdateBlockProgressUI(bool showByRoundState)
    {
        if (levelProgressionManager == null)
        {
            return;
        }

        int total = Mathf.Max(
            0,
            levelProgressionManager.TotalBreakableRegularBlocks +
            levelProgressionManager.TotalBreakableSpecialBlocks);

        int destroyed = Mathf.Clamp(
            levelProgressionManager.DestroyedBreakableRegularBlocks +
            levelProgressionManager.DestroyedBreakableSpecialBlocks,
            0,
            total);

        bool showProgress = showByRoundState && (!hideBlockProgressWhenNoTargets || total > 0);
        SetActiveIfChanged(blockProgressRoot, showProgress);

        if (blockProgressSlider != null)
        {
            // Keep max in sync in case total changes after load logic updates.
            float sliderMax = Mathf.Max(1f, total);
            if (!Mathf.Approximately(blockProgressSlider.maxValue, sliderMax))
            {
                blockProgressSlider.maxValue = sliderMax;
            }

            blockProgressSlider.value = Mathf.Clamp(destroyed, 0, Mathf.RoundToInt(sliderMax));
        }

        if (blockProgressText != null)
        {
            float percent = total > 0 ? (destroyed / (float)total) * 100f : 0f;
            blockProgressText.text = string.Format(
                blockProgressFormat,
                destroyed,
                total,
                percent.ToString("0"));
        }
    }

    private void ConfigureBlockProgressSliderDefaults()
    {
        if (blockProgressSlider == null)
        {
            return;
        }

        blockProgressSlider.minValue = 0f;
        blockProgressSlider.maxValue = 1f;
        blockProgressSlider.wholeNumbers = false;
        blockProgressSlider.value = 0f;
    }

    private void OnRetryPressed()
    {
        if (levelProgressionManager == null)
        {
            return;
        }

        levelProgressionManager.RetryCurrentLevelRound();
    }

    // private void OnNextPressed()
    // {
    //     if (levelProgressionManager == null)
    //     {
    //         return;
    //     }

    //     levelProgressionManager.ContinueToNextLevelAfterRoundWin();
    // }

    private void WireButtons()
    {
        // Clear old listeners so this script is the single source of behavior.
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryPressed);
            retryButton.onClick.AddListener(OnRetryPressed);
        }

        // if (nextButton != null)
        // {
        //     nextButton.onClick.RemoveListener(OnNextPressed);
        //     nextButton.onClick.AddListener(OnNextPressed);
        // }
    }

    private void ResolveManager()
    {
        if (levelProgressionManager != null || !autoFindManager)
        {
            return;
        }

        levelProgressionManager = FindFirstObjectByType<LevelProgressionManager>();
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
}
