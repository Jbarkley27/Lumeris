using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one slider per color group.
/// Rules:
/// - Past color bars: filled (completed)
/// - Active color bar: fills with current tier progress
/// - Future color bars: empty/default
/// - Tier TMP text visible only on active color bar
/// </summary>
public class ColorProgressBarUI : MonoBehaviour
{
    [Header("Bar Bindings (preplaced in scene order-independent)")]
    [Tooltip("Bind each colorId to one slider container.")]
    [SerializeField] private ColorBarBinding[] bars;

    [Header("Visual Alpha")]
    [Tooltip("Root alpha for future colors.")]
    [Range(0f, 1f)] [SerializeField] private float futureAlpha = 0.5f;

    [Tooltip("Root alpha for active color.")]
    [Range(0f, 1f)] [SerializeField] private float activeAlpha = 1f;

    [Tooltip("Root alpha for completed colors.")]
    [Range(0f, 1f)] [SerializeField] private float completedAlpha = 1f;

    [Header("Slider Animation")]
    [Tooltip("Animate slider value changes.")]
    [SerializeField] private bool animateSliderValue = true;

    [Tooltip("Slider tween duration in seconds.")]
    [Min(0f)] [SerializeField] private float sliderTweenDuration = 0.2f;

    [Tooltip("Tween ease for slider value.")]
    [SerializeField] private Ease sliderTweenEase = Ease.OutQuad;

    [Header("Tier Text")]
    [Tooltip("Format for active tier text. {0}=tier, {1}=max tiers.")]
    [SerializeField] private string activeTierTextFormat = "{0}";

    [Tooltip("Hide tier text on non-active colors.")]
    [SerializeField] private bool hideTierTextWhenNotActive = true;

    [Header("Debug")]
    [SerializeField] private bool logColorUI = false;

    private LevelSequenceDefinition cachedSequence;
    private int cachedTiersPerColor = 5;

    /// <summary>
    /// Must be called before first Refresh.
    /// </summary>
    public void Initialize(LevelSequenceDefinition sequence)
    {
        cachedSequence = sequence;
        cachedTiersPerColor = sequence != null ? sequence.TiersPerColor : 5;

        PrepareBars();
        ApplyVisibilityForSequence();
    }

    /// <summary>
    /// Refreshes all bars from runtime progression state.
    /// </summary>
    public void Refresh(
        LevelSequenceDefinition sequence,
        int currentLevelIndex,
        float activeObjectiveProgress01,
        bool runCompleted)
    {
        if (sequence == null)
        {
            return;
        }

        if (cachedSequence != sequence)
        {
            Initialize(sequence);
        }

        string[] colorOrder = sequence.GetColorOrder();
        int activeColorOrder = -1;
        string activeColorId = string.Empty;
        int activeTierInColor = 0;

        bool validActiveIndex = !runCompleted && currentLevelIndex >= 0 && currentLevelIndex < sequence.LevelCount;
        if (validActiveIndex)
        {
            SequenceLevelEntry activeEntry = sequence.GetEntryAt(currentLevelIndex);
            if (activeEntry != null)
            {
                activeColorId = activeEntry.colorId;
                activeTierInColor = activeEntry.tierInColor;
                activeColorOrder = sequence.GetColorOrderIndex(activeColorId);
            }
        }

        for (int i = 0; i < bars.Length; i++)
        {
            ColorBarBinding bar = bars[i];
            if (bar == null || string.IsNullOrWhiteSpace(bar.colorId))
            {
                continue;
            }

            bool colorExists = ColorExistsInOrder(colorOrder, bar.colorId);
            SetBarVisible(bar, colorExists);

            if (!colorExists)
            {
                continue;
            }

            int thisColorOrder = sequence.GetColorOrderIndex(bar.colorId);
            int completedInThisColor = sequence.CountCompletedTiersForColor(bar.colorId, currentLevelIndex);

            bool isActiveColor = validActiveIndex && thisColorOrder == activeColorOrder;
            bool isPastColor = runCompleted || (thisColorOrder >= 0 && thisColorOrder < activeColorOrder);
            bool isFutureColor = !runCompleted && (thisColorOrder > activeColorOrder);

            float targetSliderValue;
            float targetAlpha;
            bool showTierText = false;
            string tierText = string.Empty;

            if (isPastColor)
            {
                // Finished colors show as full bars.
                targetSliderValue = cachedTiersPerColor;
                targetAlpha = completedAlpha;
            }
            else if (isActiveColor)
            {
                // Active color shows completed tiers + objective progress in current tier.
                float partial = Mathf.Clamp01(activeObjectiveProgress01);
                targetSliderValue = Mathf.Clamp(completedInThisColor + partial, 0f, cachedTiersPerColor);
                // smooth per-tier progress
                // targetSliderValue = Mathf.Clamp(completedInThisColor, 0f, cachedTiersPerColor); // step-only tier progress

                targetAlpha = activeAlpha;

                showTierText = true;
                tierText = activeTierInColor.ToString();
            }
            else if (isFutureColor)
            {
                // Future colors start empty.
                targetSliderValue = 0f;
                targetAlpha = futureAlpha;
            }
            else
            {
                // Safety fallback.
                targetSliderValue = 0f;
                targetAlpha = futureAlpha;
            }

            SetBarAlpha(bar, targetAlpha);
            SetSliderValue(bar, targetSliderValue);
            SetTierText(bar, showTierText, tierText);
        }
    }

    private void PrepareBars()
    {
        if (bars == null)
        {
            return;
        }

        for (int i = 0; i < bars.Length; i++)
        {
            ColorBarBinding bar = bars[i];
            if (bar == null)
            {
                continue;
            }

            if (bar.rootObject == null && bar.slider != null)
            {
                bar.rootObject = bar.slider.gameObject.transform.parent != null
                    ? bar.slider.gameObject.transform.parent.gameObject
                    : bar.slider.gameObject;
            }

            if (bar.rootCanvasGroup == null && bar.rootObject != null)
            {
                bar.rootCanvasGroup = bar.rootObject.GetComponent<CanvasGroup>();
            }

            if (bar.slider != null)
            {
                bar.slider.minValue = 0f;
                bar.slider.maxValue = cachedTiersPerColor;
                bar.slider.wholeNumbers = false;
            }

            SetTierText(bar, false, string.Empty);
        }
    }

    private void ApplyVisibilityForSequence()
    {
        if (cachedSequence == null || bars == null)
        {
            return;
        }

        string[] colorOrder = cachedSequence.GetColorOrder();

        for (int i = 0; i < bars.Length; i++)
        {
            ColorBarBinding bar = bars[i];
            if (bar == null)
            {
                continue;
            }

            bool visible = ColorExistsInOrder(colorOrder, bar.colorId);
            SetBarVisible(bar, visible);

            if (logColorUI && !visible)
            {
                Debug.Log($"ColorProgressBarUI: Hiding bar '{bar.colorId}' (not in current sequence).");
            }
        }
    }

    private static bool ColorExistsInOrder(string[] order, string colorId)
    {
        if (order == null || string.IsNullOrWhiteSpace(colorId))
        {
            return false;
        }

        for (int i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i], colorId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetBarVisible(ColorBarBinding bar, bool visible)
    {
        if (bar.rootObject != null)
        {
            bar.rootObject.SetActive(visible);
        }
    }

    private static void SetBarAlpha(ColorBarBinding bar, float alpha)
    {
        if (bar.rootCanvasGroup != null)
        {
            bar.rootCanvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }

    private void SetSliderValue(ColorBarBinding bar, float targetValue)
    {
        if (bar.slider == null)
        {
            return;
        }

        float clamped = Mathf.Clamp(targetValue, bar.slider.minValue, bar.slider.maxValue);

        if (!animateSliderValue || sliderTweenDuration <= 0f || !Application.isPlaying)
        {
            bar.slider.value = clamped;
            return;
        }

        // Kill previous slider tween bound to this slider to prevent stacking.
        DOTween.Kill(bar.slider, complete: false);

        DOTween
            .To(() => bar.slider.value, x => bar.slider.value = x, clamped, sliderTweenDuration)
            .SetEase(sliderTweenEase)
            .SetTarget(bar.slider);
    }

    private void SetTierText(ColorBarBinding bar, bool show, string text)
    {
        if (bar.currentTierText == null)
        {
            return;
        }

        bool shouldShow = show || !hideTierTextWhenNotActive;
        bar.currentTierText.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            bar.currentTierText.text = text;
        }
    }
}

/// <summary>
/// One color bar binding in the inspector.
/// </summary>
[System.Serializable]
public class ColorBarBinding
{
    [Tooltip("Color key this bar represents. Must match SequenceLevelEntry.colorId.")]
    public string colorId;

    [Tooltip("Slider component for this color.")]
    public Slider slider;

    [Tooltip("TMP text showing current tier (visible only on active color).")]
    public TMP_Text currentTierText;

    [Tooltip("Optional root object to show/hide this color bar.")]
    public GameObject rootObject;

    [Tooltip("Optional root canvas group to control alpha states.")]
    public CanvasGroup rootCanvasGroup;
}
