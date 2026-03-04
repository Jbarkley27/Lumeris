using System;
using UnityEngine;

/// <summary>
/// Runtime run-currency manager (MVP1 scope: Glass only).
/// Uses double to support very large incremental-game values.
/// </summary>
public class RunCurrencyManager : MonoBehaviour
{
    public static RunCurrencyManager Instance { get; private set; }

    [Header("Debug")]
    [Tooltip("Optional starting Glass for quick test sessions.")]
    [SerializeField] private double startingGlass = 0d;

    [Tooltip("If true, logs Glass gains from destroyed blocks.")]
    [SerializeField] private bool logGlassGains = false;

    [Header("Read Only (Runtime)")]
    [SerializeField] private double currentGlass;
    [SerializeField] private double lifetimeGlassEarned;

    /// <summary>
    /// Fired whenever Glass value changes.
    /// Args: currentGlass, lifetimeGlassEarned
    /// </summary>
    public static event Action<double, double> GlassChanged;

    public double CurrentGlass => currentGlass;
    public double LifetimeGlassEarned => lifetimeGlassEarned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentGlass = Math.Max(0d, startingGlass);
    }

    private void OnEnable()
    {
        WorldBlock.Destroyed += OnWorldBlockDestroyed;
    }

    private void OnDisable()
    {
        WorldBlock.Destroyed -= OnWorldBlockDestroyed;
    }

    private void OnWorldBlockDestroyed(WorldBlock block)
    {
        if (block == null)
        {
            return;
        }

        AddGlass(block.GlassReward);
    }

    /// <summary>
    /// Adds run Glass and increments lifetime earned counter.
    /// </summary>
    public void AddGlass(double amount)
    {
        if (amount <= 0d)
        {
            return;
        }

        currentGlass += amount;
        lifetimeGlassEarned += amount;

        if (logGlassGains)
        {
            Debug.Log($"RunCurrencyManager: +{amount} Glass -> Current={currentGlass}, LifetimeEarned={lifetimeGlassEarned}");
        }

        GlassChanged?.Invoke(currentGlass, lifetimeGlassEarned);
    }

    public void ResetRunGlass()
    {
        currentGlass = 0d;
        GlassChanged?.Invoke(currentGlass, lifetimeGlassEarned);
    }
}
