using UnityEngine;

/// <summary>
/// Strongly-typed IDs for gameplay SFX.
/// Add new SFX IDs here instead of hardcoding string paths in gameplay scripts.
/// </summary>
public enum AudioSfxId
{
    None = 0,

    // World --------------------------------------------------------------------
    World_BlockHit_Indestructible = 1000,

    // Projectile ---------------------------------------------------------------
    Projectile_Fire_Basic = 2000,
    Projectile_Hit_Basic = 2001
}

/// <summary>
/// Central resolver for FMOD event paths.
/// Gameplay code should use AudioSfxId, not raw strings.
/// </summary>
public static class AudioLibrary
{
    /// <summary>
    /// Converts enum ID into FMOD event path.
    /// Returns empty string when no valid mapping exists.
    /// </summary>
    public static string GetEventPath(AudioSfxId sfxId)
    {
        switch (sfxId)
        {
            case AudioSfxId.World_BlockHit_Indestructible:
                return "event:/SFX/World/Block/IndestructibleHit";

            case AudioSfxId.Projectile_Fire_Basic:
                return "event:/SFX/Projectile/Fire_Basic";

            case AudioSfxId.Projectile_Hit_Basic:
                return "event:/SFX/Projectile/Hit_Basic";

            case AudioSfxId.None:
            default:
                return string.Empty;
        }
    }
}
