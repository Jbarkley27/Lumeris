using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime HUD for run currencies.
/// Rules:
/// - Glass is always visible.
/// - Fragments/Lumen roots are hidden until each has been earned at least once this run.
/// - On Continue, if loaded value > 0 (or seen flags are true), roots show immediately.
/// </summary>
public class RunCurrencyHUD : MonoBehaviour
{
    [Header("Currency Roots")]
    [Tooltip("Glass UI root. Always shown.")]
    [SerializeField] private GameObject glassRoot;

    [Tooltip("Fragments UI root. Hidden until first fragment this run.")]
    [SerializeField] private GameObject fragmentsRoot;

    [Tooltip("Lumen UI root. Hidden until first lumen this run.")]
    [SerializeField] private GameObject lumenRoot;

    [Header("Text References")]
    [SerializeField] private TMP_Text glassText;
    [SerializeField] private TMP_Text fragmentsText;
    [SerializeField] private TMP_Text lumenText;

    [Header("Optional Glass Icon")]
    [SerializeField] private Image glassIconImage;
    [SerializeField] private Sprite glassIconSprite;

    [Header("Glass Gain Animation")]
    [Tooltip("Transform to punch when Glass increases. Defaults to glassText transform.")]
    [SerializeField] private Transform glassPunchTarget;

    [SerializeField] private float gainPunchStrength = 0.18f;
    [SerializeField] private float gainPunchDuration = 0.16f;
    [SerializeField] private int gainPunchVibrato = 8;
    [SerializeField] private float gainPunchElasticity = 0.9f;

    [Header("Display")]
    [SerializeField] private string missingValueText = "--";

    [Header("Runtime Wiring")]
    [SerializeField] private RunCurrencyManager runCurrencyManager;
    [SerializeField] private bool autoFindRunCurrencyManager = true;

    [SerializeField] private GameSettings gameSettings;
    [SerializeField] private bool autoFindGameSettings = true;

    // Used to trigger punch only on positive Glass change.
    private bool hasSeenFirstGlassValue;
    private double lastKnownGlass;

    [Header("Passive Generation Sliders")]
    [Tooltip("Optional slider showing progress to next Fragments passive tick.")]
    [SerializeField] private Slider fragmentsGenerationSlider;

    [Tooltip("Optional slider showing progress to next Lumen passive tick.")]
    [SerializeField] private Slider lumenGenerationSlider;


    private void Awake()
    {
        ResolveManagerReference();
        ResolveSettingsReference();
        ApplyIconSetup();
        ConfigureSliderDefaults();
    }

    private void OnEnable()
    {
        // Unified currency update event (new system).
        RunCurrencyManager.CurrencyStateChanged += OnCurrencyStateChanged;

        // Keep legacy compatibility in case some flows still emit this only.
        RunCurrencyManager.GlassChanged += OnGlassChangedLegacy;

        if (gameSettings != null)
        {
            gameSettings.NumberFormatSettingsChanged += OnNumberFormatSettingsChanged;
        }

        RefreshFromManager();
    }

    private void OnDisable()
    {
        RunCurrencyManager.CurrencyStateChanged -= OnCurrencyStateChanged;
        RunCurrencyManager.GlassChanged -= OnGlassChangedLegacy;

        if (gameSettings != null)
        {
            gameSettings.NumberFormatSettingsChanged -= OnNumberFormatSettingsChanged;
        }
    }

    private void Update()
    {
        // Late auto-wire if manager is spawned after HUD.
        if (runCurrencyManager == null && autoFindRunCurrencyManager)
        {
            ResolveManagerReference();

            if (runCurrencyManager != null)
            {
                RefreshFromManager();
            }
        }

        // Late auto-wire if settings is spawned after HUD.
        if (gameSettings == null && autoFindGameSettings)
        {
            ResolveSettingsReference();

            if (gameSettings != null)
            {
                gameSettings.NumberFormatSettingsChanged += OnNumberFormatSettingsChanged;
                RefreshFromManager();
            }
        }

        UpdatePassiveSliders();

    }

    /// <summary>
    /// Public helper for external forced refresh (optional debug usage).
    /// </summary>
    public void ForceRefresh()
    {
        RefreshFromManager();
    }

    private void OnCurrencyStateChanged()
    {
        RefreshFromManager();
    }

    /// <summary>
    /// Legacy callback. We still refresh full HUD from manager state.
    /// </summary>
    private void OnGlassChangedLegacy(double currentGlass, double lifetimeGlass)
    {
        RefreshFromManager();
    }

    private void OnNumberFormatSettingsChanged()
    {
        RefreshFromManager();
    }

    private void RefreshFromManager()
    {
        if (runCurrencyManager == null)
        {
            SetMissingState();
            return;
        }

        double glass = runCurrencyManager.CurrentGlass;
        double fragments = runCurrencyManager.CurrentFragments;
        double lumen = runCurrencyManager.CurrentLumen;

        // Detect positive glass delta for punch feedback.
        bool shouldPunch = hasSeenFirstGlassValue && glass > lastKnownGlass;

        // Format strings from current runtime settings every refresh.
        string glassFormatted = FormatValue(glass);
        string fragmentsFormatted = FormatValue(fragments);
        string lumenFormatted = FormatValue(lumen);

        if (glassText != null) glassText.text = glassFormatted;
        if (fragmentsText != null) fragmentsText.text = fragmentsFormatted;
        if (lumenText != null) lumenText.text = lumenFormatted;

        // Visibility rules:
        // - Glass always visible.
        // - Other currencies visible only after first earn this run
        //   (or loaded > 0 on Continue path).
        SetRootActive(glassRoot, true);
        SetRootActive(fragmentsRoot, runCurrencyManager.HasSeenFragmentsThisRun || fragments > 0d);
        SetRootActive(lumenRoot, runCurrencyManager.HasSeenLumenThisRun || lumen > 0d);

        lastKnownGlass = glass;
        hasSeenFirstGlassValue = true;

        if (shouldPunch)
        {
            PlayGlassGainPunch();
        }
    }

    private string FormatValue(double value)
    {
        if (gameSettings == null)
        {
            // Fallback when settings manager is missing.
            return value.ToString("0.###");
        }

        return NumberFormatter.Format(value, gameSettings);
    }

    private void SetMissingState()
    {
        if (glassText != null) glassText.text = missingValueText;
        if (fragmentsText != null) fragmentsText.text = missingValueText;
        if (lumenText != null) lumenText.text = missingValueText;

        // Keep glass root visible per design rule.
        SetRootActive(glassRoot, true);
        SetRootActive(fragmentsRoot, false);
        SetRootActive(lumenRoot, false);

        hasSeenFirstGlassValue = false;
        lastKnownGlass = 0d;

        SetSliderValue(fragmentsGenerationSlider, 0f);
        SetSliderValue(lumenGenerationSlider, 0f);
    }

    private void SetRootActive(GameObject root, bool active)
    {
        if (root == null)
        {
            return;
        }

        if (root.activeSelf != active)
        {
            root.SetActive(active);
        }
    }

    private void PlayGlassGainPunch()
    {
        Transform target = glassPunchTarget != null ? glassPunchTarget : (glassText != null ? glassText.transform : null);
        if (target == null)
        {
            return;
        }

        // Kill previous tween before creating a new one to prevent stack buildup.
        target.DOKill(false);
        target.localScale = Vector3.one;

        target.DOPunchScale(
            Vector3.one * gainPunchStrength,
            gainPunchDuration,
            gainPunchVibrato,
            gainPunchElasticity);
    }

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


    /// <summary>
    /// Ensures optional sliders use normalized 0..1 range.
    /// </summary>
    private void ConfigureSliderDefaults()
    {
        ConfigureSlider01(fragmentsGenerationSlider);
        ConfigureSlider01(lumenGenerationSlider);
    }

    private void ConfigureSlider01(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    /// <summary>
    /// Updates passive-generation sliders each frame for smooth fill motion.
    /// Uses one shared tick progress source from RunCurrencyManager.
    /// Hidden currencies keep slider at 0 to avoid visual noise.
    /// </summary>
    private void UpdatePassiveSliders()
    {
        if (runCurrencyManager == null)
        {
            SetSliderValue(fragmentsGenerationSlider, 0f);
            SetSliderValue(lumenGenerationSlider, 0f);
            return;
        }

        bool showFragments = runCurrencyManager.HasSeenFragmentsThisRun || runCurrencyManager.CurrentFragments > 0d;
        bool showLumen = runCurrencyManager.HasSeenLumenThisRun || runCurrencyManager.CurrentLumen > 0d;

        float fragmentsProgress = runCurrencyManager.FragmentsProgressToNextWhole01;
        float lumenProgress = runCurrencyManager.LumenProgressToNextWhole01;


        SetSliderValue(fragmentsGenerationSlider, showFragments ? fragmentsProgress : 0f);
        SetSliderValue(lumenGenerationSlider, showLumen ? lumenProgress : 0f);
    }



    private void SetSliderValue(Slider slider, float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.value = Mathf.Clamp01(value);
    }

}
