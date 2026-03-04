using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime HUD for run currencies.
/// Shows Glass/lifetime values, supports large-number formatting,
/// and plays gain feedback animation on Glass increases.
/// </summary>
public class RunCurrencyHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text field for current Glass amount.")]
    [SerializeField] private TMP_Text glassText;

    [Tooltip("Text field for lifetime Glass earned amount.")]
    [SerializeField] private TMP_Text lifetimeGlassText;

    [Tooltip("Optional icon image shown next to Glass text.")]
    [SerializeField] private Image glassIconImage;

    [Tooltip("Optional sprite assigned to the Glass icon image at startup.")]
    [SerializeField] private Sprite glassIconSprite;

    [Tooltip("Transform to animate when Glass is gained. If null, glassText transform is used.")]
    [SerializeField] private Transform glassPunchTarget;

    [Header("Gain Animation")]
    [Tooltip("Scale delta used by DOPunchScale on Glass gain.")]
    [SerializeField] private float gainPunchStrength = 0.18f;

    [Tooltip("Punch duration in seconds.")]
    [SerializeField] private float gainPunchDuration = 0.16f;

    [Tooltip("Punch vibrato count.")]
    [SerializeField] private int gainPunchVibrato = 8;

    [Tooltip("Punch elasticity value.")]
    [SerializeField] private float gainPunchElasticity = 0.9f;

    [Header("Text Labels")]
    [SerializeField] private string missingValueText = "--";

    [Header("Runtime Wiring")]
    [SerializeField] private RunCurrencyManager runCurrencyManager;
    [SerializeField] private bool autoFindRunCurrencyManager = true;

    [SerializeField] private GameSettings gameSettings;
    [SerializeField] private bool autoFindGameSettings = true;

    // Tracks last value to detect positive gain and trigger punch.
    private double lastKnownGlass;
    private bool hasSeenFirstGlassValue;

    private void Awake()
    {
        ResolveManagerReference();
        ResolveSettingsReference();
        ApplyIconSetup();
    }

    private void OnEnable()
    {
        RunCurrencyManager.GlassChanged += OnGlassChanged;

        if (gameSettings != null)
        {
            gameSettings.NumberFormatSettingsChanged += OnNumberFormatSettingsChanged;
        }

        RefreshFromManager();
    }

    private void OnDisable()
    {
        RunCurrencyManager.GlassChanged -= OnGlassChanged;

        if (gameSettings != null)
        {
            gameSettings.NumberFormatSettingsChanged -= OnNumberFormatSettingsChanged;
        }
    }

    private void Update()
    {
        if (runCurrencyManager == null && autoFindRunCurrencyManager)
        {
            ResolveManagerReference();

            if (runCurrencyManager != null)
            {
                RefreshFromManager();
            }
        }

        if (gameSettings == null && autoFindGameSettings)
        {
            ResolveSettingsReference();

            if (gameSettings != null)
            {
                gameSettings.NumberFormatSettingsChanged += OnNumberFormatSettingsChanged;
                RefreshFromManager();
            }
        }
    }

    /// <summary>
    /// Re-render values when number format settings change at runtime.
    /// </summary>
    private void OnNumberFormatSettingsChanged()
    {
        RefreshFromManager();
    }

    /// <summary>
    /// Event callback when Glass changes.
    /// </summary>
    private void OnGlassChanged(double currentGlass, double lifetimeGlassEarned)
    {
        bool shouldPunch = hasSeenFirstGlassValue && currentGlass > lastKnownGlass;

        SetHudValues(currentGlass, lifetimeGlassEarned);

        lastKnownGlass = currentGlass;
        hasSeenFirstGlassValue = true;

        if (shouldPunch)
        {
            PlayGlassGainPunch();
        }
    }

    private void RefreshFromManager()
    {
        if (runCurrencyManager == null)
        {
            SetMissingState();
            return;
        }

        double current = runCurrencyManager.CurrentGlass;
        double lifetime = runCurrencyManager.LifetimeGlassEarned;

        SetHudValues(current, lifetime);

        lastKnownGlass = current;
        hasSeenFirstGlassValue = true;
    }

    /// <summary>
    /// Updates text with current formatter settings.
    /// </summary>
    private void SetHudValues(double currentGlass, double lifetimeGlassEarned)
    {
        string currentFormatted = NumberFormatter.Format(currentGlass, gameSettings);
        string lifetimeFormatted = NumberFormatter.Format(lifetimeGlassEarned, gameSettings);

        if (glassText != null)
        {
            glassText.text = currentFormatted;
        }

        if (lifetimeGlassText != null)
        {
            lifetimeGlassText.text = lifetimeFormatted;
        }
    }


    private void SetMissingState()
    {
        if (glassText != null)
        {
            glassText.text = missingValueText;
        }

        if (lifetimeGlassText != null)
        {
            lifetimeGlassText.text = missingValueText;
        }
    }


    /// <summary>
    /// Punch feedback for Glass gain.
    /// Kills previous tweens first to avoid stacking during rapid gains.
    /// </summary>
    private void PlayGlassGainPunch()
    {
        Transform target = glassPunchTarget != null ? glassPunchTarget : (glassText != null ? glassText.transform : null);
        if (target == null)
        {
            return;
        }

        // Prevent tween buildup when gains happen rapidly.
        target.DOKill(false);
        target.localScale = Vector3.one;

        target.DOPunchScale(
            Vector3.one * gainPunchStrength,
            gainPunchDuration,
            gainPunchVibrato,
            gainPunchElasticity);
    }

    /// <summary>
    /// Applies optional icon sprite setup.
    /// </summary>
    private void ApplyIconSetup()
    {
        if (glassIconImage == null)
        {
            return;
        }

        if (glassIconSprite != null)
        {
            glassIconImage.sprite = glassIconSprite;
            glassIconImage.enabled = true;
        }
    }

    private void ResolveManagerReference()
    {
        if (runCurrencyManager != null || !autoFindRunCurrencyManager)
        {
            return;
        }

        runCurrencyManager = FindFirstObjectByType<RunCurrencyManager>();
    }

    private void ResolveSettingsReference()
    {
        if (gameSettings != null || !autoFindGameSettings)
        {
            return;
        }

        gameSettings = FindFirstObjectByType<GameSettings>();
    }
}
