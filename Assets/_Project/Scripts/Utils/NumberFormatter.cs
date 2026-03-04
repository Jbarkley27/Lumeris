using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Global number formatter for currency-style displays.
/// Uses runtime settings every call; no formatted-value caching.
/// </summary>
public static class NumberFormatter
{
    // Suffix index maps to groups of 10^3.
    // 1=K, 2=M, ... 21=Vg
    private static readonly string[] Suffixes =
    {
        "",
        "K",
        "M",
        "B",
        "T",
        "Qa",
        "Qi",
        "Sx",
        "Sp",
        "Oc",
        "No",
        "Dc",
        "Ud",
        "Dd",
        "Td",
        "Qad",
        "Qid",
        "Sxd",
        "Spd",
        "Ocd",
        "Nod",
        "Vg"
    };

    // For values under 1,000,000 we still apply significant-digit rounding,
    // but clamp visible decimals to keep HUD readable.
    private const int MaxBelowMillionDecimals = 2;

    /// <summary>
    /// Formats a value using explicit mode + significant digits.
    /// </summary>
    public static string Format(double value, NumberFormatMode mode, int significantDigits)
    {
        int sigDigits = Mathf.Clamp(
            significantDigits,
            GameSettings.MinSignificantDigits,
            GameSettings.MaxSignificantDigits);

        // Edge cases.
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "∞";
        }

        if (value == 0d)
        {
            return "0";
        }

        bool isNegative = value < 0d;
        double absValue = Math.Abs(value);

        string body;

        if (absValue < 1_000_000d)
        {
            // Applies sig-digit logic under 1M too, with decimal clamp.
            body = FormatBelowOneMillion(absValue, sigDigits, out double roundedUnderMillion);

            // If rounding pushes us into >= 1M, switch to large-number mode.
            if (roundedUnderMillion >= 1_000_000d)
            {
                body = FormatLargeValue(roundedUnderMillion, mode, sigDigits);
            }
        }
        else
        {
            body = FormatLargeValue(absValue, mode, sigDigits);
        }

        return isNegative ? "-" + body : body;
    }

    /// <summary>
    /// Formats using settings object.
    /// </summary>
    public static string Format(double value, GameSettings settings)
    {
        if (settings == null)
        {
            return Format(value, NumberFormatMode.Suffix, 3);
        }

        return Format(value, settings.NumberFormatMode, settings.SignificantDigits);
    }

    /// <summary>
    /// Handles values >= 1,000,000 according to selected mode.
    /// </summary>
    private static string FormatLargeValue(double value, NumberFormatMode mode, int significantDigits)
    {
        switch (mode)
        {
            case NumberFormatMode.Scientific:
                return FormatScientific(value, significantDigits, includeSpaceBeforeExponent: false);

            case NumberFormatMode.Engineering:
                return FormatEngineering(value, significantDigits);

            case NumberFormatMode.Suffix:
            default:
                return FormatSuffix(value, significantDigits);
        }
    }

    /// <summary>
    /// Formats [0, 1,000,000) using sig-digit rounding + clamped decimals.
    /// Also keeps grouping commas for values >= 1000.
    /// </summary>
    private static string FormatBelowOneMillion(double value, int significantDigits, out double roundedValue)
    {
        if (value <= 0d)
        {
            roundedValue = 0d;
            return "0";
        }

        // Significant-digit-based decimal suggestion.
        int suggestedDecimals = GetSuggestedSignificantDecimals(value, significantDigits);

        // Clamp for readability in lower range.
        int decimals = Mathf.Clamp(suggestedDecimals, 0, MaxBelowMillionDecimals);

        roundedValue = Math.Round(value, decimals, MidpointRounding.AwayFromZero);

        // Use grouped format once value is at least 1,000.
        bool useGrouping = roundedValue >= 1000d;
        string format = useGrouping ? "N" + decimals.ToString(CultureInfo.InvariantCulture)
                                    : "F" + decimals.ToString(CultureInfo.InvariantCulture);

        string raw = roundedValue.ToString(format, CultureInfo.InvariantCulture);
        return TrimTrailingZeros(raw);
    }

    /// <summary>
    /// Suffix mode for large values.
    /// </summary>
    private static string FormatSuffix(double value, int significantDigits)
    {
        int exponentGroup = (int)Math.Floor(Math.Log10(value) / 3d);
        double scaled = value / Math.Pow(10d, exponentGroup * 3);
        int suffixIndex = exponentGroup;

        // Round and check overflow into next suffix group (e.g., 999.95M -> 1.00B).
        int decimals;
        double roundedScaled = RoundToSignificantDisplayValue(scaled, significantDigits, out decimals);

        if (roundedScaled >= 1000d)
        {
            scaled /= 1000d;
            suffixIndex += 1;
            roundedScaled = RoundToSignificantDisplayValue(scaled, significantDigits, out decimals);
        }

        // Fallback if suffix table is exceeded.
        if (suffixIndex >= Suffixes.Length)
        {
            return FormatScientific(value, significantDigits, includeSpaceBeforeExponent: true);
        }

        string scaledText = roundedScaled.ToString(
            "F" + decimals.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        return scaledText + Suffixes[suffixIndex];
    }

    /// <summary>
    /// Scientific notation: mantissa e exponent.
    /// </summary>
    private static string FormatScientific(double value, int significantDigits, bool includeSpaceBeforeExponent)
    {
        int exponent = (int)Math.Floor(Math.Log10(value));
        double mantissa = value / Math.Pow(10d, exponent);

        int decimals;
        double roundedMantissa = RoundToSignificantDisplayValue(mantissa, significantDigits, out decimals);

        // Normalize overflow after rounding.
        if (roundedMantissa >= 10d)
        {
            roundedMantissa /= 10d;
            exponent += 1;
            roundedMantissa = RoundToSignificantDisplayValue(roundedMantissa, significantDigits, out decimals);
        }

        string mantissaText = roundedMantissa.ToString(
            "F" + decimals.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        string expToken = includeSpaceBeforeExponent ? " e" : "e";
        return mantissaText + expToken + exponent.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Engineering notation: exponent forced to multiple of 3.
    /// </summary>
    private static string FormatEngineering(double value, int significantDigits)
    {
        int exponent = (int)Math.Floor(Math.Log10(value));
        int engineeringExponent = exponent - (exponent % 3);
        double mantissa = value / Math.Pow(10d, engineeringExponent);

        int decimals;
        double roundedMantissa = RoundToSignificantDisplayValue(mantissa, significantDigits, out decimals);

        // Normalize overflow after rounding.
        if (roundedMantissa >= 1000d)
        {
            roundedMantissa /= 1000d;
            engineeringExponent += 3;
            roundedMantissa = RoundToSignificantDisplayValue(roundedMantissa, significantDigits, out decimals);
        }

        string mantissaText = roundedMantissa.ToString(
            "F" + decimals.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        return mantissaText + "e" + engineeringExponent.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Rounds a positive value to significant digits and returns needed decimals.
    /// </summary>
    private static double RoundToSignificantDisplayValue(double value, int significantDigits, out int decimals)
    {
        if (value <= 0d)
        {
            decimals = 0;
            return 0d;
        }

        int order = (int)Math.Floor(Math.Log10(value));
        decimals = significantDigits - 1 - order;
        decimals = Mathf.Clamp(decimals, 0, significantDigits - 1);

        return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Returns decimal count suggested by significant-digit rules.
    /// </summary>
    private static int GetSuggestedSignificantDecimals(double value, int significantDigits)
    {
        if (value <= 0d)
        {
            return 0;
        }

        int order = (int)Math.Floor(Math.Log10(value));
        return significantDigits - 1 - order;
    }

    /// <summary>
    /// Removes trailing decimal zeros while preserving grouping separators.
    /// </summary>
    private static string TrimTrailingZeros(string text)
    {
        int dotIndex = text.IndexOf('.');
        if (dotIndex < 0)
        {
            return text;
        }

        int end = text.Length - 1;
        while (end > dotIndex && text[end] == '0')
        {
            end--;
        }

        // Remove decimal separator if no fractional part remains.
        if (end == dotIndex)
        {
            end--;
        }

        return text.Substring(0, end + 1);
    }
}

/*
TEST CASES
- 999 -> "999"
- 12_345 -> "12,345"
- 1_234_567 (Suffix,3) -> "1.23M"
- 12_345_678 (Suffix,3) -> "12.3M"
- 123_456_789 (Suffix,3) -> "123M"
- 999_950 -> "999,950"
- 999_950_000 (Suffix,3) -> "1.00B"   // not "1000M"
- Very large suffix overflow -> scientific fallback, e.g. "1.23 e345"
*/
