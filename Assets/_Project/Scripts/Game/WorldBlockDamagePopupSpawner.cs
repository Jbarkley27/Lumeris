using UnityEngine;
using DamageNumbersPro;

/// <summary>
/// Global listener that spawns damage popup prefabs from WorldBlock hit events.
/// Put this once in scene (Managers object).
/// </summary>
public class WorldBlockDamagePopupSpawner : MonoBehaviour
{
    [Header("Popup Prefabs")]
    [Tooltip("Spawned when a destructible block actually takes damage.")]
    [SerializeField] private DamageNumber breakableHitPopupPrefab;

    [Tooltip("Spawned when an indestructible block is hit.")]
    [SerializeField] private DamageNumber indestructibleHitPopupPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Extra world offset applied on top of block-provided anchor.")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.1f, 0f);

    [Tooltip("Fallback cleanup in case popup prefab does not self-destroy.")]
    [SerializeField] private float fallbackAutoDestroySeconds = 2f;

    [Tooltip("If true, popup follows block transform. Usually false for world-space popups.")]
    [SerializeField] private bool parentToBlock = false;

    private void OnEnable()
    {
        WorldBlock.DamagePopupRequested += OnDamagePopupRequested;
    }

    private void OnDisable()
    {
        WorldBlock.DamagePopupRequested -= OnDamagePopupRequested;
    }

    /// <summary>
    /// Spawns correct prefab based on blocked/non-blocked hit.
    /// </summary>
    private void OnDamagePopupRequested(WorldBlock block, int damageAmount, Vector3 worldAnchor, bool blocked)
    {
        DamageNumber prefabToSpawn = blocked ? indestructibleHitPopupPrefab : breakableHitPopupPrefab;
        if (prefabToSpawn == null)
        {
            return;
        }

        Transform parent = (parentToBlock && block != null) ? block.transform : null;
        // DamageNumber instance = Instantiate(prefabToSpawn, worldAnchor + spawnOffset, Quaternion.identity, parent);
        // DamageNumber damageNumber = instance.GetComponent<DamageNumber>();
        if (prefabToSpawn != null)
        {
            prefabToSpawn.Spawn(worldAnchor + spawnOffset, damageAmount);
        }

        // Let popup prefab handle its own animation/text logic.
        // This is only a safety cleanup if prefab does not auto-destroy.
        // if (fallbackAutoDestroySeconds > 0f)
        // {
        //     Destroy(prefabToSpawn, fallbackAutoDestroySeconds);
        // }
    }
}
