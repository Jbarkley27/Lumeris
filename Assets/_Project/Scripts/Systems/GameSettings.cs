using System;
using UnityEngine;

/// <summary>
/// Runtime user settings container.
/// Stores number display settings and persists them via PlayerPrefs.
/// </summary>
public class GameSettings : MonoBehaviour
{
    // Stable PlayerPrefs keys.
    private const string NumberFormatModeKey = "settings.number_format_mode";
    private const string SignificantDigitsKey = "settings.significant_digits";

    // Centralized bounds so future changes are one-line edits.
    public const int MinSignificantDigits = 2;
    public const int MaxSignificantDigits = 6;

    [Header("Number Formatting")]
    [Tooltip("How large values are displayed in UI.")]
    public NumberFormatMode numberFormatMode = NumberFormatMode.Suffix;

    [Tooltip("Significant digits used by Suffix/Scientific/Engineering formatting.")]
    [Range(MinSignificantDigits, MaxSignificantDigits)]
    public int significantDigits = 3;

    /// <summary>
    /// Fired whenever number formatting settings change.
    /// UI can subscribe and redraw.
    /// </summary>
    public event Action NumberFormatSettingsChanged;

    public NumberFormatMode NumberFormatMode => numberFormatMode;
    public int SignificantDigits => Mathf.Clamp(significantDigits, MinSignificantDigits, MaxSignificantDigits);



    private void Awake()
    {
        LoadNumberFormatSettings();
    }

    /// <summary>
    /// Sets format mode and persists immediately.
    /// </summary>
    public void SetNumberFormatMode(NumberFormatMode mode)
    {
        if (numberFormatMode == mode)
        {
            return;
        }

        numberFormatMode = mode;
        SaveNumberFormatSettings();
        NumberFormatSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Sets significant digits (clamped) and persists immediately.
    /// </summary>
    public void SetSignificantDigits(int digits)
    {
        int clamped = Mathf.Clamp(digits, MinSignificantDigits, MaxSignificantDigits);
        if (significantDigits == clamped)
        {
            return;
        }

        significantDigits = clamped;
        SaveNumberFormatSettings();
        NumberFormatSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Applies both number format settings in one call.
    /// </summary>
    public void ApplyNumberFormatSettings(NumberFormatMode mode, int digits)
    {
        numberFormatMode = mode;
        significantDigits = Mathf.Clamp(digits, MinSignificantDigits, MaxSignificantDigits);
        SaveNumberFormatSettings();
        NumberFormatSettingsChanged?.Invoke();
    }

    /// <summary>
    /// Loads persisted settings from PlayerPrefs.
    /// </summary>
    public void LoadNumberFormatSettings()
    {
        int defaultMode = (int)NumberFormatMode.Suffix;
        int loadedMode = PlayerPrefs.GetInt(NumberFormatModeKey, defaultMode);

        // Clamp enum value to known range.
        if (loadedMode < (int)NumberFormatMode.Suffix || loadedMode > (int)NumberFormatMode.Engineering)
        {
            loadedMode = defaultMode;
        }

        numberFormatMode = (NumberFormatMode)loadedMode;
        significantDigits = Mathf.Clamp(
            PlayerPrefs.GetInt(SignificantDigitsKey, 3),
            MinSignificantDigits,
            MaxSignificantDigits);
    }

    /// <summary>
    /// Persists current settings to PlayerPrefs.
    /// </summary>
    public void SaveNumberFormatSettings()
    {
        PlayerPrefs.SetInt(NumberFormatModeKey, (int)numberFormatMode);
        PlayerPrefs.SetInt(SignificantDigitsKey, Mathf.Clamp(significantDigits, MinSignificantDigits, MaxSignificantDigits));
        PlayerPrefs.Save();
    }
}
