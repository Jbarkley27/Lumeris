using UnityEngine;

/// <summary>
/// Central registry of FMOD event paths used by gameplay code.
/// Keep all event path strings here to avoid hardcoding paths in feature scripts.
/// </summary>
public static class AudioLibrary
{
    // WORLD ----------------------------------------------------------------------

    /// <summary>
    /// Played when a block is hit but is marked indestructible (ex: statue/anchor block).
    /// </summary>
    public const string World_BlockHit_Indestructible = "event:/SFX/World/Block/IndestructibleHit";

    // PROJECTILES ----------------------------------------------------------------

    /// <summary>
    /// Default projectile fire event (used when definition has no override).
    /// </summary>
    public const string Projectile_Fire_Basic = "event:/SFX/Projectile/Fire_Basic";

    /// <summary>
    /// Default projectile hit event (used when definition has no override).
    /// </summary>
    public const string Projectile_Hit_Basic = "event:/SFX/Projectile/Hit_Basic";
}
