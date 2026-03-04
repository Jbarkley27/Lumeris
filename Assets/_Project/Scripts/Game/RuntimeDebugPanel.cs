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

    [Tooltip("Optional explicit reference. If null, panel tries to find player automatically.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Layout")]
    [Tooltip("Panel width in pixels.")]
    [SerializeField] private float panelWidth = 420f;

    [Tooltip("Panel height in pixels.")]
    [SerializeField] private float panelHeight = 300f;

    [Tooltip("Panel margin from top-left corner.")]
    [SerializeField] private Vector2 panelMargin = new Vector2(12f, 12f);

    // GUI styles must be created inside OnGUI context.
    private bool stylesInitialized;


    [Tooltip("Optional explicit reference. If null, panel tries to find run currency manager automatically.")]
    [SerializeField] private RunCurrencyManager runCurrencyManager;

    [Tooltip("Optional explicit reference. If null, panel tries to find game settings automatically.")]
    [SerializeField] private GameSettings gameSettings;



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

        if (playerMovement == null)
        {
            playerMovement = FindFirstObjectByType<PlayerMovement>();
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

        GUILayout.BeginArea(panelRect, boxStyle);

        DrawHeader();
        DrawWorldSection();
        DrawPlayerSection();
        DrawCurrencySection();


        GUILayout.EndArea();
    }


    /// <summary>
    /// Shows current run currency values needed for the immediate gameplay loop tests.
    /// </summary>
    private void DrawCurrencySection()
    {
        GUILayout.Label("<b>Currencies (MVP1)</b>", labelStyle);

        if (runCurrencyManager == null)
        {
            GUILayout.Label("RunCurrencyManager: <color=yellow>Not Found</color>", labelStyle);
            return;
        }

        GUILayout.Label($"Glass: {runCurrencyManager.CurrentGlass}", labelStyle);
        GUILayout.Label($"Lifetime Glass Earned: {runCurrencyManager.LifetimeGlassEarned}", labelStyle);

        if (gameSettings == null)
        {
            GUILayout.Label("GameSettings: <color=yellow>Not Found</color>", labelStyle);
            GUILayout.Label($"Glass (Raw): {runCurrencyManager.CurrentGlass}", labelStyle);
            GUILayout.Label($"Lifetime (Raw): {runCurrencyManager.LifetimeGlassEarned}", labelStyle);
            return;
        }

        GUILayout.Label($"Mode: {gameSettings.NumberFormatMode}", labelStyle);
        GUILayout.Label($"Sig Digits: {gameSettings.SignificantDigits}", labelStyle);

        string formattedGlass = NumberFormatter.Format(runCurrencyManager.CurrentGlass, gameSettings);
        string formattedLifetime = NumberFormatter.Format(runCurrencyManager.LifetimeGlassEarned, gameSettings);

        GUILayout.Label($"Glass (Raw): {runCurrencyManager.CurrentGlass}", labelStyle);
        GUILayout.Label($"Glass (Fmt): {formattedGlass}", labelStyle);
        GUILayout.Label($"Lifetime (Raw): {runCurrencyManager.LifetimeGlassEarned}", labelStyle);
        GUILayout.Label($"Lifetime (Fmt): {formattedLifetime}", labelStyle);

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
    /// Creates simple readable runtime GUI styles.
    /// </summary>
    private void BuildGuiStyles()
    {
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
        GUILayout.Label($"Source: {(worldGridLoader.DebugUsedDebugOverride ? "DEBUG OVERRIDE" : "DEFAULT")}", labelStyle);
        GUILayout.Label($"Spawned Blocks: {worldGridLoader.DebugSpawnedBlockCount}", labelStyle);
        GUILayout.Label($"Safe Area Enabled: {worldGridLoader.DebugEnforceSpawnSafeArea}", labelStyle);
        GUILayout.Label($"Safe Radius Cells: {worldGridLoader.DebugSpawnSafeRadiusCells}", labelStyle);
        GUILayout.Label($"Spawn World Pos: {worldGridLoader.DebugSpawnWorldPosition}", labelStyle);

        GUILayout.Space(6f);
    }

    /// <summary>
    /// Draws player transform/velocity state for spawn and movement checks.
    /// </summary>
    private void DrawPlayerSection()
    {
        GUILayout.Label("<b>Player</b>", labelStyle);

        if (playerMovement == null)
        {
            GUILayout.Label("PlayerMovement: <color=yellow>Not Found</color>", labelStyle);
            GUILayout.Space(6f);
            return;
        }

        Vector3 pos = playerMovement.transform.position;
        Vector3 vel = playerMovement.GetPlayerVelocity();

        GUILayout.Label($"Position: {pos}", labelStyle);
        GUILayout.Label($"Velocity: {vel}", labelStyle);

        GUILayout.Space(6f);
    }

    /// <summary>
    /// Placeholder section for upcoming systems.
    /// Keep this panel extensible without rewriting structure later.
    /// </summary>
    private void DrawPlaceholderSection()
    {
        GUILayout.Label("<b>Upcoming Hooks</b>", labelStyle);
        GUILayout.Label("Currencies: Glass / Refined / Lumen / Fragments", labelStyle);
        GUILayout.Label("Progression: Color/Tier/Objective state", labelStyle);
        GUILayout.Label("Save: Current level + run state flags", labelStyle);
    }
}
