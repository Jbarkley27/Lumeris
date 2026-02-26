using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple non-generic projectile pool keyed to one ProjectileDefinition.
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    [SerializeField] private ProjectileDefinition definition;

    // Queue of inactive instances ready to launch.
    private readonly Queue<ProjectileInstance> available = new Queue<ProjectileInstance>();
    private bool built;

    /// <summary>
    /// Runtime setup path when pool is created from code.
    /// </summary>
    public void InitializeRuntime(ProjectileDefinition runtimeDefinition)
    {
        definition = runtimeDefinition;
        BuildIfNeeded();
    }

    private void Awake()
    {
        BuildIfNeeded();
    }

    /// <summary>
    /// Gets an instance from pool (or creates one if empty).
    /// </summary>
    public ProjectileInstance Get()
    {
        BuildIfNeeded();

        ProjectileInstance instance = available.Count > 0 ? available.Dequeue() : CreateOne();
        instance.gameObject.SetActive(true);
        return instance;
    }

    /// <summary>
    /// Returns an instance back to pool.
    /// </summary>
    public void Return(ProjectileInstance instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform, worldPositionStays: false);
        available.Enqueue(instance);
    }

    private void BuildIfNeeded()
    {
        if (built) return;

        if (definition == null || definition.projectilePrefab == null)
        {
            Debug.LogError("ProjectilePool: Missing definition or projectile prefab.");
            return;
        }

        built = true;

        for (int i = 0; i < definition.prewarmCount; i++)
        {
            ProjectileInstance instance = CreateOne();
            instance.gameObject.SetActive(false);
            available.Enqueue(instance);
        }
    }

    private ProjectileInstance CreateOne()
    {
        ProjectileInstance instance = Instantiate(definition.projectilePrefab, transform);
        return instance;
    }
}
