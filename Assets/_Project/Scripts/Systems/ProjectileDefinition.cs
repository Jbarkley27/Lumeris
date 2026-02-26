using UnityEngine;

/// <summary>
/// Reusable projectile config for blasters and skills.
/// One definition can be reused by any shooter/caster.
/// </summary>
[CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "Margin/Combat/Projectile Definition")]
public class ProjectileDefinition : ScriptableObject
{
    [Header("Prefab + Pool")]
    [Tooltip("Prefab that contains ProjectileInstance.")]
    public ProjectileInstance projectilePrefab;
    [Tooltip("How many to pre-create in pool.")]
    [Min(0)] public int prewarmCount = 16;

    [Header("Flight")]
    [Tooltip("Units per second.")]
    [Min(0.01f)] public float speed = 80f;
    [Tooltip("Auto-despawn time in seconds.")]
    [Min(0.01f)] public float lifetimeSeconds = 4f;
    [Tooltip("Physics layers this projectile can hit.")]
    public LayerMask hitMask = ~0;

    [Header("Damage")]
    [Tooltip("Damage applied to WorldBlock on hit.")]
    [Min(1)] public int damage = 1;
    [Tooltip("If true, despawns immediately on first hit.")]
    public bool destroyOnFirstHit = true;
    [Tooltip("Used only when destroyOnFirstHit is false.")]
    [Min(1)] public int maxHitsBeforeDespawn = 1;

    [Header("Polish Placeholders")]
    [Tooltip("Optional muzzle VFX spawned at fire source.")]
    public GameObject muzzleVfxPrefab;
    [Tooltip("Optional hit VFX spawned at collision point.")]
    public GameObject hitVfxPrefab;
    [Tooltip("Applies player recoil via PlayerMovement.KnockBack when fired.")]
    [Min(0f)] public float playerKnockbackOnFire = 0f;
    [Tooltip("Uses ScreenShakeManager.ShootProfile when fired.")]
    public bool applyShootShake = true;
    [Tooltip("Uses ScreenShakeManager.DamagedProfile when hitting.")]
    public bool applyHitShake = false;

    [Header("Audio (FMOD Event Path Overrides)")]
    [Tooltip("Optional override. If empty, AudioLibrary.Projectile_Fire_Basic is used.")]
    public string fireSfxEventPath = "";
    [Tooltip("Optional override. If empty, AudioLibrary.Projectile_Hit_Basic is used.")]
    public string hitSfxEventPath = "";

    [Header("Damage Popup Placeholder")]
    [Tooltip("Optional popup VFX prefab spawned when damage is successfully applied.")]
    public GameObject damagePopupVfxPrefab;

    [Tooltip("Offset applied to popup spawn position from hit point.")]
    public Vector3 damagePopupOffset = new Vector3(0f, 0.25f, 0f);

    [Tooltip("If true, popup is parented to hit target transform.")]
    public bool parentDamagePopupToHitTarget = false;

    [Tooltip("If true, popup can spawn even when hit did not apply damage (ex: indestructible hit).")]
    public bool spawnDamagePopupOnBlockedHit = false;

}
