using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug-only runtime hotkeys for number format testing.
/// Lets you switch format mode and significant digits during play mode.
/// </summary>
public class SettingsDebugHotkeys : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Runtime settings object that stores number format mode + significant digits.")]
    [SerializeField] private GameSettings gameSettings;

    [Tooltip("If true, auto-find GameSettings when not manually assigned.")]
    [SerializeField] private bool autoFindGameSettings = true;

    [Header("Enable")]
    [Tooltip("If false, hotkeys work only in Editor play mode.")]
    [SerializeField] private bool enableInBuild = false;

    [Tooltip("If true, logs every setting change to Console.")]
    [SerializeField] private bool logChanges = true;

    [Header("Mode Keys")]
    [Tooltip("Set number format mode to Suffix.")]
    [SerializeField] private Key suffixModeKey = Key.Digit7;

    [Tooltip("Set number format mode to Scientific.")]
    [SerializeField] private Key scientificModeKey = Key.Digit8;

    [Tooltip("Set number format mode to Engineering.")]
    [SerializeField] private Key engineeringModeKey = Key.Digit9;

    [Header("Precision Keys")]
    [Tooltip("Decrease significant digits (minimum 2).")]
    [SerializeField] private Key decreaseDigitsKey = Key.Minus;

    [Tooltip("Increase significant digits (maximum 6).")]
    [SerializeField] private Key increaseDigitsKey = Key.Equals;

    [Tooltip("Optional direct-set key for 2 significant digits.")]
    [SerializeField] private Key setDigits2Key = Key.F6;

    [Tooltip("Optional direct-set key for 3 significant digits.")]
    [SerializeField] private Key setDigits3Key = Key.F7;

    [Tooltip("Optional direct-set key for 4 significant digits.")]
    [SerializeField] private Key setDigits4Key = Key.F8;

    [Tooltip("Optional direct-set key for 5 significant digits.")]
    [SerializeField] private Key setDigits5Key = Key.F9;

    [Tooltip("Optional direct-set key for 6 significant digits.")]
    [SerializeField] private Key setDigits6Key = Key.F10;

    private void Awake()
    {
        ResolveSettingsReference();
    }

    private void Update()
    {
        if (!enableInBuild && !Application.isEditor)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (gameSettings == null && autoFindGameSettings)
        {
            ResolveSettingsReference();
        }

        if (gameSettings == null)
        {
            return;
        }

        if (Keyboard.current[suffixModeKey].wasPressedThisFrame)
        {
            SetMode(NumberFormatMode.Suffix);
        }

        if (Keyboard.current[scientificModeKey].wasPressedThisFrame)
        {
            SetMode(NumberFormatMode.Scientific);
        }

        if (Keyboard.current[engineeringModeKey].wasPressedThisFrame)
        {
            SetMode(NumberFormatMode.Engineering);
        }

        if (Keyboard.current[decreaseDigitsKey].wasPressedThisFrame)
        {
            SetDigits(gameSettings.SignificantDigits - 1);
        }

        if (Keyboard.current[increaseDigitsKey].wasPressedThisFrame)
        {
            SetDigits(gameSettings.SignificantDigits + 1);
        }

        if (Keyboard.current[setDigits2Key].wasPressedThisFrame)
        {
            SetDigits(2);
        }

        if (Keyboard.current[setDigits3Key].wasPressedThisFrame)
        {
            SetDigits(3);
        }

        if (Keyboard.current[setDigits4Key].wasPressedThisFrame)
        {
            SetDigits(4);
        }

        if (Keyboard.current[setDigits5Key].wasPressedThisFrame)
        {
            SetDigits(5);
        }

        if (Keyboard.current[setDigits6Key].wasPressedThisFrame)
        {
            SetDigits(6);
        }
    }

    /// <summary>
    /// Applies mode through GameSettings API so save + event refresh both happen.
    /// </summary>
    private void SetMode(NumberFormatMode mode)
    {
        gameSettings.SetNumberFormatMode(mode);

        if (logChanges)
        {
            Debug.Log($"[SettingsDebugHotkeys] Mode -> {mode}");
        }
    }

    /// <summary>
    /// Applies digit precision through GameSettings API (clamped internally).
    /// </summary>
    private void SetDigits(int digits)
    {
        gameSettings.SetSignificantDigits(digits);

        if (logChanges)
        {
            Debug.Log($"[SettingsDebugHotkeys] Significant Digits -> {gameSettings.SignificantDigits}");
        }
    }

    /// <summary>
    /// Finds GameSettings in scene if allowed and not already assigned.
    /// </summary>
    private void ResolveSettingsReference()
    {
        if (gameSettings != null || !autoFindGameSettings)
        {
            return;
        }

        gameSettings = FindFirstObjectByType<GameSettings>();
    }
}
