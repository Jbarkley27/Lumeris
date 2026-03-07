using UnityEngine;

/// <summary>
/// GAME SESSION FLOW + SAVE KEY MAP
/// -----------------------------------------------------------------------------------------
/// This class is the "menu command hub" for run-level session actions:
/// - ContinueGame()     : reload saved run state and continue where player left off.
/// - ResetGame()        : wipe current run data completely (fresh start).
/// - ResetForPrestige() : wipe current run progression/currency, but keep lifetime-ready data.
///
/// WHY THIS EXISTS:
/// We keep this logic in one place so UI buttons do not directly manipulate many systems.
/// Buttons call one method here, and this class coordinates managers consistently.
///
/// KEY DOMAINS (PlayerPrefs):
/// 1) Run Currency Domain (owned by RunCurrencyManager)
///    Prefix: "run.currency"
///    Primary:
///    - run.currency.glass
///    - run.currency.fragments
///    - run.currency.lumen
///    Supporting:
///    - run.currency.lifetime_glass
///    - run.currency.lifetime_fragments
///    - run.currency.lifetime_lumen
///    - run.currency.pending_fragments
///    - run.currency.pending_lumen
///    - run.currency.ui_seen_fragments
///    - run.currency.ui_seen_lumen
///
/// 2) Run Progression Domain (owned by LevelProgressionManager)
///    Prefix: "run.progression"
///    Sequence-scoped when enabled:
///    - run.progression.<sequenceToken>.level_index
///    - run.progression.<sequenceToken>.run_completed
///
/// 3) Settings Domain (owned by GameSettings)
///    - settings.number_format_mode
///    - settings.significant_digits

///
/// RESET BEHAVIOR:
/// - ResetGame():
///   Clears run currency + run progression.
///   Optionally resets display settings.
///   Intended to behave like "New Game".
///
/// - ResetForPrestige():
///   Clears run progression.
///   Clears current run glass.
///   Keeps lifetime glass (meta-ready seed for future prestige economy).
///   Intended to be expandable later for augment/meta rewards.
///
/// SAVE FEEDBACK:
/// SaveFeedbackEvents.RunWithFeedback(...) wraps reset operations so save icon/UI can
/// blink during operation and hide when finished, even if an exception occurs.
/// -----------------------------------------------------------------------------------------
/// </summary>

public class GameSessionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelProgressionManager levelProgressionManager;
    [SerializeField] private RunCurrencyManager runCurrencyManager;

    [Tooltip("If true, references are auto-found if missing.")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Reset Options")]
    [Tooltip("If true, full reset also clears number-format settings.")]
    [SerializeField] private bool clearDisplaySettingsOnFullReset = false;

    [SerializeField] private GameSettings gameSettings;


    private void Awake()
    {
        ResolveReferences();
    }

    #if UNITY_EDITOR
    [ContextMenu("Debug/Continue Game")]
    private void DebugContextContinueGame()
    {
        ContinueGame();
    }

    [ContextMenu("Debug/Reset Game (Full Run Reset)")]
    private void DebugContextResetGame()
    {
        ResetGame();
    }
    #endif


    /// <summary>
    /// UI button target: Continue existing run.
    /// Loads saved progression and run currency state.
    /// </summary>
    public void ContinueGame()
    {
        ResolveReferences();

        if (runCurrencyManager != null)
        {
            runCurrencyManager.LoadRunCurrencyState();
        }

        if (levelProgressionManager != null)
        {
            levelProgressionManager.ContinueFromPersistedStateAndLoad();
        }
    }

    /// <summary>
    /// Canonical full run reset.
    /// Definition of "Delete/Reset" across the project:
    /// - Clears runtime run state (currencies + progression)
    /// - Clears persisted run save keys
    /// - Starts from default first level state
    /// This method is the single source of truth for reset behavior.
    /// </summary>
    public void ResetGame()
    {
        ResolveReferences();

        // Show one save-feedback cycle for this explicit user command.
        SaveFeedbackEvents.RunWithFeedback(() =>
        {
            if (runCurrencyManager != null)
            {
                runCurrencyManager.ResetRunCurrency(clearLifetime: true, saveAfterReset: false);
                runCurrencyManager.ClearRunCurrencyState(withFeedback: false);
            }

            if (levelProgressionManager != null)
            {
                levelProgressionManager.ResetRunProgressToStart(
                    clearPersistedState: true,
                    loadFirstLevel: true);
            }

            if (clearDisplaySettingsOnFullReset && gameSettings != null)
            {
                // Restore display settings defaults (suffix + 3 sig digits).
                gameSettings.ApplyNumberFormatSettings(NumberFormatMode.Suffix, 3);
            }
        });
    }

    /// <summary>
    /// Future prestige flow:
    /// - clear run progression
    /// - clear current run currency
    /// - keep lifetime/meta-capable values (example: lifetimeGlassEarned) intact
    /// This is where future augment/meta systems should hook in.
    /// </summary>
    public void ResetForPrestige()
    {
        ResolveReferences();

        SaveFeedbackEvents.RunWithFeedback(() =>
        {
            if (runCurrencyManager != null)
            {
                runCurrencyManager.ResetRunCurrency(clearLifetime: false, saveAfterReset: false);
                runCurrencyManager.SaveRunCurrencyState(withFeedback: false);
            }

            if (levelProgressionManager != null)
            {
                levelProgressionManager.ResetRunProgressToStart(
                    clearPersistedState: true,
                    loadFirstLevel: true);
            }

            // Meta systems (future) should NOT be cleared here.
            // Add future calls here for prestige rewards, augment grants, etc.
        });
    }

    private void ResolveReferences()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (levelProgressionManager == null)
        {
            levelProgressionManager = FindFirstObjectByType<LevelProgressionManager>();
        }

        if (runCurrencyManager == null)
        {
            runCurrencyManager = FindFirstObjectByType<RunCurrencyManager>();
        }

        if (gameSettings == null)
        {
            gameSettings = FindFirstObjectByType<GameSettings>();
        }
    }
}
