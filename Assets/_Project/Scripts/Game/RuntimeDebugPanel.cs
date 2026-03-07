using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// Lightweight in-game debug panel for runtime verification.
/// Toggle with a key (default F3).
/// Built to be easy to extend as systems are added (currencies, progression, save state, etc.).
/// </summary>
public class RuntimeDebugPanel : MonoBehaviour
{
    [Header("Toggle")]
    [Tooltip("Keyboard key used to show/hide the debug panel (Input System).")]
    [SerializeField] private Key toggleKey = Key.F3;


    [Tooltip("If true, panel starts visible when play mode begins.")]
    [SerializeField] private bool showOnStart = true;

    [Header("References")]
    [Tooltip("Optional explicit reference. If null, panel tries to find loader automatically.")]
    [SerializeField] private WorldGridLoader worldGridLoader;

    [Header("Layout")]
    [Tooltip("Panel width in pixels.")]
    [SerializeField] private float panelWidth = 420f;

    [Tooltip("Panel height in pixels.")]
    [SerializeField] private float panelHeight = 800f;

    [Tooltip("Panel margin from top-left corner.")]
    [SerializeField] private Vector2 panelMargin = new Vector2(12f, 12f);

    [Tooltip("Overall panel alpha (background + text) for readability tuning.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float panelAlpha = 1f;

    // GUI styles must be created inside OnGUI context.
    private bool stylesInitialized;


    [Tooltip("Optional explicit reference. If null, panel tries to find run currency manager automatically.")]
    [SerializeField] private RunCurrencyManager runCurrencyManager;

    [Tooltip("Optional explicit reference. If null, panel tries to find game settings automatically.")]
    [SerializeField] private GameSettings gameSettings;

    [Tooltip("Optional explicit reference. If null, panel tries to find level progression manager automatically.")]
    [SerializeField] private LevelProgressionManager levelProgressionManager;
    [Tooltip("Optional explicit reference. If null, panel tries to find GameSessionController automatically.")]
    [SerializeField] private GameSessionController gameSessionController;
    [Header("Debug Tools")]
    [Tooltip("Master switch for debug-only save/reset buttons.")]
    [SerializeField] private bool debugToolsEnabled = true;

    /// <summary>
    /// True only in editor/dev contexts when debug tools are enabled.
    /// Prevents shipping debug reset/save controls in release builds.
    /// </summary>
    private bool CanUseDebugActions()
    {
    #if UNITY_EDITOR
        return debugToolsEnabled;
    #else
        return debugToolsEnabled && Debug.isDebugBuild;
    #endif
    }







    private bool isVisible;
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;

    private void Awake()
    {
        // Initial visibility state for quick test sessions.
        isVisible = showOnStart;

        // Auto-wire references for convenience if not manually assigned.
        if (worldGridLoader == null)
        {
            worldGridLoader = FindFirstObjectByType<WorldGridLoader>();
        }

        if (runCurrencyManager == null)
        {
            runCurrencyManager = FindFirstObjectByType<RunCurrencyManager>();
        }

        if (gameSettings == null)
        {
            gameSettings = FindFirstObjectByType<GameSettings>();
        }

        if (levelProgressionManager == null)
        {
            levelProgressionManager = FindFirstObjectByType<LevelProgressionManager>();
        }

        if (gameSessionController == null)
        {
            gameSessionController = FindFirstObjectByType<GameSessionController>();
        }


    }



    private void Update()
    {
        // Input System-safe keyboard toggle.
        if (Keyboard.current == null)
        {
            return;
        }

        // This works for any Key enum value selected in inspector.
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            isVisible = !isVisible;
        }
    }




    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        EnsureGuiStyles();


        Rect panelRect = new Rect(panelMargin.x, panelMargin.y, panelWidth, panelHeight);

        // Apply inspector-controlled transparency for this panel draw pass.
        Color previousGuiColor = GUI.color;
        GUI.color = new Color(previousGuiColor.r, previousGuiColor.g, previousGuiColor.b, panelAlpha);

        GUILayout.BeginArea(panelRect, boxStyle);

        DrawHeader();
        DrawWorldSection();
        DrawProgressionSection();
        DrawCurrencySection();
        DrawSaveSection();




        GUILayout.EndArea();

        // Always restore GUI state so other IMGUI draws are unaffected.
        GUI.color = previousGuiColor;
    }


    /// <summary>
    /// Shows level progression state for objective validation and bar-sync debugging.
    /// </summary>
    private void DrawProgressionSection()
    {
        GUILayout.Label("<b>Progression</b>", labelStyle);

        if (levelProgressionManager == null)
        {
            GUILayout.Label("LevelProgressionManager: <color=yellow>Not Found</color>", labelStyle);
            GUILayout.Space(6f);
            return;
        }

        GUILayout.Label($"Level Index: {levelProgressionManager.CurrentLevelIndex}", labelStyle);
        GUILayout.Label($"Run Completed: {levelProgressionManager.IsRunCompleted}", labelStyle);
        GUILayout.Label($"FloorId: {levelProgressionManager.ActiveFloorId}", labelStyle);
        GUILayout.Label($"Color: {levelProgressionManager.ActiveColorId}", labelStyle);
        GUILayout.Label($"Tier In Color: {levelProgressionManager.ActiveTierInColor}", labelStyle);
        GUILayout.Label($"Objective Rule: {levelProgressionManager.ActiveObjectiveRule}", labelStyle);
        GUILayout.Label($"Objective Progress: {(levelProgressionManager.ObjectiveProgress01 * 100f):0.0}%", labelStyle);
        GUILayout.Label($"Objective Completed: {levelProgressionManager.IsObjectiveCompleted}", labelStyle);

        if (levelProgressionManager.ActiveObjectiveRule == LevelProgressionManager.LevelObjectiveRule.DestroyRegularPercent)
        {
            GUILayout.Label($"Required Regular %: {(levelProgressionManager.ActiveRequiredPercent * 100f):0.0}%", labelStyle);
            GUILayout.Label(
                $"Regular Destroyed: {levelProgressionManager.DestroyedBreakableRegularBlocks}/{levelProgressionManager.TotalBreakableRegularBlocks}",
                labelStyle);
        }
        else
        {
            GUILayout.Label(
                $"Special Destroyed: {levelProgressionManager.DestroyedBreakableSpecialBlocks}/{levelProgressionManager.TotalBreakableSpecialBlocks}",
                labelStyle);
        }

        GUILayout.Space(6f);
    }



    /// <summary>
    /// Shows current run currency values for quick gameplay-loop validation.
    /// Includes Glass + Fragments + Lumen.
    /// </summary>
    private void DrawCurrencySection()
    {
        GUILayout.Label("<b>Currency</b>", labelStyle);

        if (runCurrencyManager == null)
        {
            GUILayout.Label("RunCurrencyManager: <color=yellow>Not Found</color>", labelStyle);
            GUILayout.Space(6f);
            return;
        }

        // If formatting settings are missing, still show raw values so debugging continues.
        if (gameSettings == null)
        {
            GUILayout.Label($"Glass: {runCurrencyManager.CurrentGlass}", labelStyle);
            GUILayout.Label($"Fragments: {runCurrencyManager.CurrentFragments}", labelStyle);
            GUILayout.Label($"Lumen: {runCurrencyManager.CurrentLumen}", labelStyle);
            GUILayout.Space(6f);
            return;
        }

        string formattedGlass = NumberFormatter.Format(runCurrencyManager.CurrentGlass, gameSettings);
        string formattedFragments = NumberFormatter.Format(runCurrencyManager.CurrentFragments, gameSettings);
        string formattedLumen = NumberFormatter.Format(runCurrencyManager.CurrentLumen, gameSettings);

        GUILayout.Label($"Glass: {formattedGlass}", labelStyle);
        GUILayout.Label($"Fragments: {formattedFragments}", labelStyle);
        GUILayout.Label($"Lumen: {formattedLumen}", labelStyle);
        GUILayout.Label($"Pending Fragments: {runCurrencyManager.PendingFragments:0.######}", labelStyle);
        GUILayout.Label($"Pending Lumen: {runCurrencyManager.PendingLumen:0.######}", labelStyle);


        // Keep formatter state visible so hotkey/settings changes are obvious.
        GUILayout.Label($"Format: {gameSettings.NumberFormatMode} ({gameSettings.SignificantDigits} sig)", labelStyle);
        GUILayout.Space(6f);
    }





    /// <summary>
    /// GUI styles must be created from inside OnGUI because GUI.skin is not valid elsewhere.
    /// </summary>
    private void EnsureGuiStyles()
    {
        if (stylesInitialized && boxStyle != null && labelStyle != null)
        {
            return;
        }

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 12,
            padding = new RectOffset(10, 10, 10, 10)
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            richText = true
        };

        stylesInitialized = true;
    }

    /// <summary>
    /// Draws title + quick hint.
    /// </summary>
    private void DrawHeader()
    {
        GUILayout.Label("<b>Runtime Debug Panel</b>", labelStyle);
        GUILayout.Label($"Toggle: {toggleKey}", labelStyle);
        GUILayout.Space(6f);
    }

    /// <summary>
    /// Draws world loader state useful during generation/spawn validation.
    /// </summary>
    private void DrawWorldSection()
    {
        GUILayout.Label("<b>World</b>", labelStyle);

        if (worldGridLoader == null)
        {
            GUILayout.Label("Loader: <color=yellow>Not Found</color>", labelStyle);
            GUILayout.Space(6f);
            return;
        }

        GUILayout.Label($"Layout: {worldGridLoader.DebugActiveLayoutName}", labelStyle);
        GUILayout.Label($"FloorId: {worldGridLoader.DebugActiveFloorId}", labelStyle);
        GUILayout.Label($"Spawned Blocks: {worldGridLoader.DebugSpawnedBlockCount}", labelStyle);

        GUILayout.Space(6f);
    }


    /// <summary>
    /// Shows save-system state + resolved keys + currently saved values.
    /// This is intentionally explicit so save issues can be diagnosed quickly in play mode.
    /// </summary>
    private void DrawSaveSection()
    {
        if (CanUseDebugActions())
        {
             GUILayout.Label("<b>Save</b>", labelStyle);

            // Manual save trigger for quick validation of key writes + save icon blink.
            if (GUILayout.Button("Manual Save Now"))
            {
                // One unified feedback cycle so icon blinks once.
                SaveFeedbackEvents.RunWithFeedback(() =>
                {
                    // Use withFeedback:false because outer wrapper already emits events.
                    runCurrencyManager?.SaveRunCurrencyState(withFeedback: false);
                    levelProgressionManager?.SaveProgressionStateNow(withFeedback: false);
                });
            }


            // Manual load trigger to verify continue-flow behavior from persisted data.
            if (GUILayout.Button("Manual Load Now"))
            {
                runCurrencyManager?.LoadRunCurrencyState();
                levelProgressionManager?.ContinueFromPersistedStateAndLoad();
            }

            // Reset run via the same canonical path used by gameplay/menu UI.
            // This guarantees "delete/reset" means one behavior everywhere.
            if (GUILayout.Button("Reset Run (Delete Save)"))
            {
                if (gameSessionController != null)
                {
                    gameSessionController.ResetGame();
                }
                else
                {
                    Debug.LogWarning("RuntimeDebugPanel: GameSessionController not found, cannot reset run.");
                }
            }





            // Global save feedback status (drives save icon behavior too).
            GUILayout.Label($"Save In Progress: {SaveFeedbackEvents.IsSaveInProgress}", labelStyle);
            GUILayout.Label($"Total Save Ops: {SaveFeedbackEvents.TotalSaveOperations}", labelStyle);

            // Run currency save info.
            if (runCurrencyManager == null)
            {
                GUILayout.Label("RunCurrencyManager: <color=yellow>Not Found</color>", labelStyle);
            }
            else
            {
                GUILayout.Label($"RunCurrency Persist: {runCurrencyManager.DebugPersistRunCurrencyState}", labelStyle);

                // Show resolved save keys for each primary run currency.
                GUILayout.Label($"Key (Glass): {runCurrencyManager.DebugGlassSaveKey}", labelStyle);
                GUILayout.Label($"Key (Fragments): {runCurrencyManager.DebugFragmentsSaveKey}", labelStyle);
                GUILayout.Label($"Key (Lumen): {runCurrencyManager.DebugLumenSaveKey}", labelStyle);

                // Show resolved lifetime/visibility keys too, since these influence passive conversion + UI reveal behavior.
                GUILayout.Label($"Key (Lifetime Glass): {runCurrencyManager.DebugLifetimeGlassSaveKey}", labelStyle);
                GUILayout.Label($"Key (Lifetime Fragments): {runCurrencyManager.DebugLifetimeFragmentsSaveKey}", labelStyle);
                GUILayout.Label($"Key (Lifetime Lumen): {runCurrencyManager.DebugLifetimeLumenSaveKey}", labelStyle);
                GUILayout.Label($"Key (Seen Fragments): {runCurrencyManager.DebugSeenFragmentsSaveKey}", labelStyle);
                GUILayout.Label($"Key (Seen Lumen): {runCurrencyManager.DebugSeenLumenSaveKey}", labelStyle);
                GUILayout.Label($"Key (Pending Fragments): {runCurrencyManager.DebugPendingFragmentsSaveKey}", labelStyle);
                GUILayout.Label($"Key (Pending Lumen): {runCurrencyManager.DebugPendingLumenSaveKey}", labelStyle);


                // Read currently saved values without mutating runtime state.
                if (runCurrencyManager.TryReadSavedCurrentGlass(out double savedGlass))
                {
                    GUILayout.Label($"Saved Glass: {savedGlass}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Glass: <none>", labelStyle);
                }

                if (runCurrencyManager.TryReadSavedCurrentFragments(out double savedFragments))
                {
                    GUILayout.Label($"Saved Fragments: {savedFragments}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Fragments: <none>", labelStyle);
                }

                if (runCurrencyManager.TryReadSavedCurrentLumen(out double savedLumen))
                {
                    GUILayout.Label($"Saved Lumen: {savedLumen}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Lumen: <none>", labelStyle);
                }


                if (runCurrencyManager.TryReadSavedPendingFragments(out double savedPendingFragments))
                {
                    GUILayout.Label($"Saved Pending Fragments: {savedPendingFragments}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Pending Fragments: <none>", labelStyle);
                }

                if (runCurrencyManager.TryReadSavedPendingLumen(out double savedPendingLumen))
                {
                    GUILayout.Label($"Saved Pending Lumen: {savedPendingLumen}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Pending Lumen: <none>", labelStyle);
                }


            }

            // Progression save info.
            if (levelProgressionManager == null)
            {
                GUILayout.Label("LevelProgressionManager: <color=yellow>Not Found</color>", labelStyle);
            }
            else
            {
                GUILayout.Label($"Progress Persist: {levelProgressionManager.DebugPersistProgressionState}", labelStyle);
                GUILayout.Label($"Key (Level Index): {levelProgressionManager.DebugLevelIndexSaveKey}", labelStyle);
                GUILayout.Label($"Key (Run Completed): {levelProgressionManager.DebugRunCompletedSaveKey}", labelStyle);

                if (levelProgressionManager.TryReadSavedProgressState(out int savedIndex, out bool savedCompleted))
                {
                    GUILayout.Label($"Saved Level Index: {savedIndex}", labelStyle);
                    GUILayout.Label($"Saved Run Completed: {savedCompleted}", labelStyle);
                }
                else
                {
                    GUILayout.Label("Saved Progress State: <none>", labelStyle);
                }
            }

            GUILayout.Space(6f);
        }
        else
        {
            GUILayout.Label("Debug save actions disabled in this build.", labelStyle);
        }

    }
}
