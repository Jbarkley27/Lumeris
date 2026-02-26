using UnityEngine;

/// <summary>
/// Runtime projectile behavior.
/// Handles movement, hit detection, hit VFX/audio placeholders, and pool return.
/// </summary>
public class ProjectileInstance : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool rotateToVelocity = true;

    private ProjectileDefinition definition;
    private ProjectilePool ownerPool;

    private Vector3 direction;
    private float ageSeconds;
    private int hitCount;
    private bool isActiveProjectile;

    // Team of the shooter that fired this projectile.
    private ProjectileOwner ownerTeam;


    /// <summary>
    /// Called by shooter when projectile is fired.
    /// </summary>
    public void Launch(
        ProjectileDefinition runtimeDefinition, 
        ProjectilePool pool, 
        Vector3 spawnPosition, 
        Vector3 fireDirection, 
        ProjectileOwner shooterTeam)
    {
        definition = runtimeDefinition;
        ownerPool = pool;
        ownerTeam = shooterTeam;

        transform.position = spawnPosition;
        direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : transform.forward;

        if (rotateToVelocity)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        ageSeconds = 0f;
        hitCount = 0;
        isActiveProjectile = true;
    }

    private void Update()
    {
        if (!isActiveProjectile || definition == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        ageSeconds += dt;

        if (ageSeconds >= definition.lifetimeSeconds)
        {
            Despawn();
            return;
        }

        SimulateStep(dt);
    }



    private void SimulateStep(float dt)
    {
        // Total travel distance for this frame.
        float remainingDistance = definition.speed * dt;
        // Current raycast start along this frame's segment.
        Vector3 rayStart = transform.position;

        // Small forward nudge used to skip ignored hits without getting stuck.
        const float bypassEpsilon = 0.02f;
        // Safety cap to avoid infinite loops if colliders are misconfigured.
        const int maxBypassCount = 8;
        int bypassCount = 0;

        while (remainingDistance > 0f)
        {
            // No hit in the remaining segment: move full remaining distance and finish.
            if (!Physics.Raycast(rayStart, direction, out RaycastHit hit, remainingDistance, definition.hitMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = rayStart + direction * remainingDistance;
                return;
            }

            // Ignore same-team hits (owner side) and keep scanning the rest of this frame.
            if (IsSameTeamHit(hit))
            {
                float consumedDistance = hit.distance + bypassEpsilon;
                remainingDistance -= consumedDistance;
                rayStart = hit.point + direction * bypassEpsilon;

                bypassCount++;
                if (bypassCount >= maxBypassCount)
                {
                    // Bail out safely if we're repeatedly skipping very dense/overlapping colliders.
                    transform.position = rayStart;
                    return;
                }

                continue;
            }

            // Valid hit target reached.
            transform.position = hit.point;
            HandleHit(hit);
            return;
        }
    }


    
    /// <summary>
    /// Handles impact flow in one place:
    /// VFX/audio, damage application, popup placeholder, shake, and despawn policy.
    /// </summary>
    private void HandleHit(RaycastHit hit)
    {
        SpawnHitVfx(hit);
        PlayHitAudio();

        // Try to apply gameplay damage first so popup logic knows whether hit was effective.
        bool damageApplied = ApplyDamageIfBlock(hit);

        // Spawn popup placeholder object (configured by definition flags).
        SpawnDamagePopupVfx(hit, damageApplied);

        if (definition.applyHitShake && ScreenShakeManager.Instance != null)
        {
            ScreenShakeManager.Instance.DoShake(ScreenShakeManager.Instance.DamagedProfile);
        }

        hitCount++;

        bool shouldDespawn =
            definition.destroyOnFirstHit ||
            hitCount >= Mathf.Max(1, definition.maxHitsBeforeDespawn);

        if (shouldDespawn)
        {
            Despawn();
        }
    }


    private void SpawnHitVfx(RaycastHit hit)
    {
        if (definition.hitVfxPrefab == null)
        {
            return;
        }

        Quaternion rotation = hit.normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(hit.normal)
            : Quaternion.identity;

        Instantiate(definition.hitVfxPrefab, hit.point, rotation);
    }

    private void PlayHitAudio()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        string eventPath = string.IsNullOrWhiteSpace(definition.hitSfxEventPath)
            ? AudioLibrary.Projectile_Hit_Basic
            : definition.hitSfxEventPath;

        if (!string.IsNullOrWhiteSpace(eventPath))
        {
            AudioManager.Instance.PlayOneShot(eventPath);
        }
    }

    /// <summary>
    /// Applies damage when hit target contains a WorldBlock.
    /// Returns true only when damage was accepted by the block.
    /// </summary>
    private bool ApplyDamageIfBlock(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        WorldBlock block = hit.collider.GetComponentInParent<WorldBlock>();
        if (block == null)
        {
            return false;
        }

        return block.ApplyDamage(definition.damage);
    }



    /// <summary>
    /// Spawns the damage popup VFX placeholder object.
    /// This does not enforce any popup behavior script; your prefab can animate itself.
    /// </summary>
    private void SpawnDamagePopupVfx(RaycastHit hit, bool damageApplied)
    {
        if (definition.damagePopupVfxPrefab == null)
        {
            return;
        }

        // Skip blocked hits unless explicitly enabled.
        if (!damageApplied && !definition.spawnDamagePopupOnBlockedHit)
        {
            return;
        }

        Vector3 spawnPosition = hit.point + definition.damagePopupOffset;
        Transform parent = null;

        if (definition.parentDamagePopupToHitTarget && hit.collider != null)
        {
            parent = hit.collider.transform;
        }

        Instantiate(definition.damagePopupVfxPrefab, spawnPosition, Quaternion.identity, parent);
    }



    /// <summary>
    /// True when hit collider belongs to the same team as projectile owner.
    /// </summary>
    private bool IsSameTeamHit(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        CombatTeam team = hit.collider.GetComponentInParent<CombatTeam>();
        if (team == null)
        {
            // No team marker means not filtered by owner-team rule.
            return false;
        }

        return team.Team == ownerTeam;
    }




    private void Despawn()
    {
        isActiveProjectile = false;

        if (ownerPool != null)
        {
            ownerPool.Return(this);
            return;
        }

        Destroy(gameObject);
    }
}
