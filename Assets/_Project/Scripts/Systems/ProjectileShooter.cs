using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable projectile launcher.
/// Can be driven by InputManager (blaster) and also called directly by skills.
/// </summary>
public class ProjectileShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform fireSource;
    [SerializeField] private Transform poolsRoot;

    [Header("Blaster Input Mode")]
    [SerializeField] private bool driveFromInputManager = true;
    [SerializeField] private ProjectileDefinition blasterDefinition;
    [SerializeField] private float blasterShotsPerSecond = 8f;

    // One pool per projectile definition so blaster + skills can coexist.
    private readonly Dictionary<ProjectileDefinition, ProjectilePool> poolsByDefinition = new Dictionary<ProjectileDefinition, ProjectilePool>();
    private float nextBlasterShotTime;

    [Header("Ownership")]
    [SerializeField] private ProjectileOwner shooterTeam = ProjectileOwner.Player;


    private void Awake()
    {
        if (fireSource == null)
        {
            fireSource = transform;
        }

        if (poolsRoot == null)
        {
            poolsRoot = transform;
        }
    }

    private void Update()
    {
        if (!driveFromInputManager || inputManager == null || blasterDefinition == null)
        {
            return;
        }

        if (!inputManager.IsShootingBlaster)
        {
            return;
        }

        float shotInterval = 1f / Mathf.Max(0.01f, blasterShotsPerSecond);
        if (Time.time < nextBlasterShotTime)
        {
            return;
        }

        nextBlasterShotTime = Time.time + shotInterval;
        FireProjectileTowardsDirection(blasterDefinition, GetAimDirection());
    }

    /// <summary>
    /// Public API for skills/abilities that fire by direction.
    /// </summary>
    public bool FireProjectileTowardsDirection(ProjectileDefinition definition, Vector3 worldDirection)
    {
        Vector3 origin = fireSource != null ? fireSource.position : transform.position;
        return FireProjectileInternal(definition, origin, worldDirection);
    }

    /// <summary>
    /// Public API for skills/abilities that fire at world point.
    /// </summary>
    public bool FireProjectileTowardsPoint(ProjectileDefinition definition, Vector3 worldPoint)
    {
        Vector3 origin = fireSource != null ? fireSource.position : transform.position;
        return FireProjectileInternal(definition, origin, worldPoint - origin);
    }

    private bool FireProjectileInternal(ProjectileDefinition definition, Vector3 origin, Vector3 direction)
    {
        if (definition == null || definition.projectilePrefab == null)
        {
            Debug.LogWarning("ProjectileShooter: Missing ProjectileDefinition or projectile prefab.");
            return false;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector3 fireDir = direction.normalized;

        ProjectilePool pool = GetOrCreatePool(definition);
        if (pool == null)
        {
            return false;
        }

        ProjectileInstance projectile = pool.Get();
        projectile.Launch(definition, pool, origin, fireDir, shooterTeam);

        SpawnMuzzleVfx(definition, origin, fireDir);
        ApplyFireFeedback(definition);

        return true;
    }

    private Vector3 GetAimDirection()
    {
        if (WorldCursor.instance != null)
        {
            Vector3 origin = fireSource != null ? fireSource.position : transform.position;
            Vector3 cursorDirection = WorldCursor.instance.GetDirectionFromWorldCursor(origin);

            if (cursorDirection.sqrMagnitude > 0.0001f)
            {
                return cursorDirection.normalized;
            }
        }

        return transform.forward;
    }

    private ProjectilePool GetOrCreatePool(ProjectileDefinition definition)
    {
        if (poolsByDefinition.TryGetValue(definition, out ProjectilePool existingPool) && existingPool != null)
        {
            return existingPool;
        }

        GameObject poolObject = new GameObject($"ProjectilePool_{definition.name}");
        poolObject.transform.SetParent(poolsRoot, false);

        ProjectilePool newPool = poolObject.AddComponent<ProjectilePool>();
        newPool.InitializeRuntime(definition);

        poolsByDefinition[definition] = newPool;
        return newPool;
    }

    private void SpawnMuzzleVfx(ProjectileDefinition definition, Vector3 origin, Vector3 fireDir)
    {
        if (definition.muzzleVfxPrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(fireDir, Vector3.up);
        GameObject vfx =Instantiate(definition.muzzleVfxPrefab, origin, rotation);
        Destroy(vfx, 1.2f); // Cleanup after 2 seconds (tweak if needed)
    }

    private void ApplyFireFeedback(ProjectileDefinition definition)
    {
        // Placeholder recoil hook. Uses your existing PlayerMovement knockback logic.
        if (playerMovement != null && definition.playerKnockbackOnFire > 0f)
        {
            playerMovement.KnockBack(definition.playerKnockbackOnFire);
        }

        // Placeholder camera shake hook.
        if (definition.applyShootShake && ScreenShakeManager.Instance != null)
        {
            ScreenShakeManager.Instance.DoShake(ScreenShakeManager.Instance.ShootProfile);
        }

        // Placeholder audio hook using strongly-typed SFX ID.
        if (AudioManager.Instance != null && definition.fireSfxId != AudioSfxId.None)
        {
            AudioManager.Instance.PlayOneShot(definition.fireSfxId);
        }

    }
}
