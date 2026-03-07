using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered story sequence for level progression.
/// Each entry maps a WorldLayout2D to:
/// - color group (used by progress bars)
/// - tier inside that color (1..tiersPerColor, usually 5)
/// </summary>
[CreateAssetMenu(fileName = "LevelSequenceDefinition", menuName = "Margin/World/Level Sequence Definition")]
public class LevelSequenceDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID for save keys and debugging. If empty, asset name is used.")]
    [SerializeField] private string sequenceId = "story_main";

    [Header("Color/Tier Rules")]
    [Tooltip("Max tier per color for UI sliders (usually 5).")]
    [Min(1)] [SerializeField] private int tiersPerColor = 5;

    [Header("Ordered Levels")]
    [Tooltip("Ordered progression list. Index 0 is first level.")]
    [SerializeField] private SequenceLevelEntry[] levels = Array.Empty<SequenceLevelEntry>();

    [Header("Validation")]
    [Tooltip("Logs validation issues in editor when true.")]
    [SerializeField] private bool logValidationMessages = true;

    public string SequenceId => string.IsNullOrWhiteSpace(sequenceId) ? name : sequenceId.Trim();
    public int LevelCount => levels != null ? levels.Length : 0;
    public int TiersPerColor => Mathf.Max(1, tiersPerColor);
    public SequenceLevelEntry[] Levels => levels;

    private void OnValidate()
    {
        if (levels == null)
        {
            levels = Array.Empty<SequenceLevelEntry>();
        }

        tiersPerColor = Mathf.Max(1, tiersPerColor);
        Validate(logValidationMessages);
    }

    /// <summary>
    /// Returns sequence entry at index or null if out of bounds.
    /// </summary>
    public SequenceLevelEntry GetEntryAt(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length)
        {
            return null;
        }

        return levels[index];
    }

    /// <summary>
    /// Finds sequence index by floorId.
    /// Requires floorId uniqueness across the sequence.
    /// </summary>
    public int GetIndexByFloorId(string floorId)
    {
        if (string.IsNullOrWhiteSpace(floorId) || levels == null)
        {
            return -1;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            SequenceLevelEntry entry = levels[i];
            if (entry == null || entry.layout == null)
            {
                continue;
            }

            if (string.Equals(entry.layout.floorId, floorId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Returns first-seen color order from sequence.
    /// Used to map "past/active/future" color bars.
    /// </summary>
    public string[] GetColorOrder()
    {
        List<string> order = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (levels == null)
        {
            return order.ToArray();
        }

        for (int i = 0; i < levels.Length; i++)
        {
            SequenceLevelEntry entry = levels[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.colorId))
            {
                continue;
            }

            if (seen.Add(entry.colorId))
            {
                order.Add(entry.colorId);
            }
        }

        return order.ToArray();
    }

    /// <summary>
    /// Returns color order index or -1 if not in sequence.
    /// </summary>
    public int GetColorOrderIndex(string colorId)
    {
        if (string.IsNullOrWhiteSpace(colorId))
        {
            return -1;
        }

        string[] order = GetColorOrder();
        for (int i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], colorId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts completed levels for a color given current level index.
    /// Completed = indices < currentLevelIndex.
    /// </summary>
    public int CountCompletedTiersForColor(string colorId, int currentLevelIndex)
    {
        if (levels == null || string.IsNullOrWhiteSpace(colorId))
        {
            return 0;
        }

        int cappedCurrent = Mathf.Clamp(currentLevelIndex, 0, LevelCount);
        int count = 0;

        for (int i = 0; i < cappedCurrent; i++)
        {
            SequenceLevelEntry entry = levels[i];
            if (entry == null)
            {
                continue;
            }

            if (string.Equals(entry.colorId, colorId, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Validates:
    /// - non-null layouts
    /// - unique floorId
    /// - valid colorId
    /// - valid tier range
    /// - unique (colorId + tierInColor) pair
    /// </summary>
    public bool Validate(bool logMessages)
    {
        bool isValid = true;

        HashSet<string> floorIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> colorTierPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (levels == null || levels.Length == 0)
        {
            if (logMessages)
            {
                Debug.LogWarning($"LevelSequenceDefinition '{name}' has no levels assigned.");
            }

            return false;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            SequenceLevelEntry entry = levels[i];

            if (entry == null)
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' has null entry at index {i}.");
                continue;
            }

            if (entry.layout == null)
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' entry {i} has null layout.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.layout.floorId))
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' entry {i} layout '{entry.layout.name}' has empty floorId.");
            }
            else if (!floorIds.Add(entry.layout.floorId))
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' duplicate floorId '{entry.layout.floorId}'. floorId must be unique.");
            }

            if (string.IsNullOrWhiteSpace(entry.colorId))
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' entry {i} has empty colorId.");
            }

            entry.tierInColor = Mathf.Clamp(entry.tierInColor, 1, TiersPerColor);

            string pairKey = $"{entry.colorId}:{entry.tierInColor}";
            if (!string.IsNullOrWhiteSpace(entry.colorId) && !colorTierPairs.Add(pairKey))
            {
                isValid = false;
                if (logMessages) Debug.LogError($"Sequence '{name}' duplicate color/tier pair '{pairKey}'.");
            }
        }

        return isValid;
    }
}

/// <summary>
/// One progression step in sequence.
/// </summary>
[Serializable]
public class SequenceLevelEntry
{
    [Tooltip("Color group key used by UI bars. Example: Blue, Green, Red.")]
    public string colorId = "Blue";

    [Tooltip("Tier index inside this color group (1..tiersPerColor).")]
    [Range(1, 5)] public int tierInColor = 1;

    [Tooltip("Layout loaded when this sequence entry becomes active.")]
    public WorldLayout2D layout;
}
