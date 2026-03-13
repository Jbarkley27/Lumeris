using UnityEngine;

/// <summary>
/// Centralized run-seed utilities:
/// - generation
/// - load/save/clear persistence
/// Keeping this logic outside gameplay managers reduces duplication.
/// </summary>
public static class RunSeedService
{
    /// <summary>
    /// Generates a new full-range signed int seed.
    /// </summary>
    public static int GenerateSeed()
    {
        return Random.Range(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Tries to load a seed from PlayerPrefs.
    /// </summary>
    public static bool TryLoadSeed(string key, out int seed)
    {
        seed = 0;

        if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
        {
            return false;
        }

        seed = PlayerPrefs.GetInt(key, 0);
        return true;
    }

    /// <summary>
    /// Saves the seed value immediately.
    /// </summary>
    public static void SaveSeed(string key, int seed, bool saveNow = true)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        PlayerPrefs.SetInt(key, seed);
        if (saveNow)
        {
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Clears persisted seed key.
    /// </summary>
    public static void ClearSeed(string key, bool saveNow = true)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        PlayerPrefs.DeleteKey(key);
        if (saveNow)
        {
            PlayerPrefs.Save();
        }
    }
}
