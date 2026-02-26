using UnityEngine;

/// <summary>
/// Which side an object belongs to for projectile collision filtering.
/// </summary>
public enum ProjectileOwner
{
    Player = 0,
    Enemy = 1
}

/// <summary>
/// Attach to root objects that should be team-filtered (player, enemies).
/// </summary>
public class CombatTeam : MonoBehaviour
{
    [SerializeField] private ProjectileOwner team = ProjectileOwner.Player;
    public ProjectileOwner Team => team;
}
