using DG.Tweening;
using UnityEngine;

/// <summary>
/// Shows a save icon while save is running:
/// - On save start: icon appears and blinks
/// - On save finish: blink stops and icon fades out
/// </summary>
public class SaveFeedbackUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Root object of save icon. If null, this GameObject is used.")]
    [SerializeField] private GameObject iconRoot;

    [Tooltip("CanvasGroup controlling icon alpha. Required for blink/fade.")]
    [SerializeField] private CanvasGroup iconCanvasGroup;

    [Header("Blink")]
    [Range(0f, 1f)] [SerializeField] private float blinkMinAlpha = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float blinkMaxAlpha = 1f;
    [Min(0.05f)] [SerializeField] private float blinkHalfDuration = 0.2f;

    [Header("Hide")]
    [Min(0f)] [SerializeField] private float hideFadeDuration = 0.12f;

    private Tween blinkTween;
    private Tween hideTween;

    private void Awake()
    {
        if (iconRoot == null)
        {
            iconRoot = gameObject;
        }

        if (iconCanvasGroup == null && iconRoot != null)
        {
            iconCanvasGroup = iconRoot.GetComponent<CanvasGroup>();
        }

        // Start hidden by default.
        SetVisibleImmediate(false);
    }

    private void OnEnable()
    {
        SaveFeedbackEvents.SaveStarted += OnSaveStarted;
        SaveFeedbackEvents.SaveFinished += OnSaveFinished;
    }

    private void OnDisable()
    {
        SaveFeedbackEvents.SaveStarted -= OnSaveStarted;
        SaveFeedbackEvents.SaveFinished -= OnSaveFinished;

        KillTweens();
    }

    private void OnSaveStarted()
    {
        if (iconRoot == null || iconCanvasGroup == null)
        {
            return;
        }

        KillTweens();

        iconRoot.SetActive(true);
        iconCanvasGroup.alpha = blinkMinAlpha;

        blinkTween = iconCanvasGroup
            .DOFade(blinkMaxAlpha, blinkHalfDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void OnSaveFinished()
    {
        if (iconRoot == null || iconCanvasGroup == null)
        {
            return;
        }

        if (blinkTween != null && blinkTween.IsActive())
        {
            blinkTween.Kill(false);
        }

        if (hideFadeDuration <= 0f)
        {
            SetVisibleImmediate(false);
            return;
        }

        hideTween = iconCanvasGroup
            .DOFade(0f, hideFadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnComplete(() => SetVisibleImmediate(false));
    }

    private void SetVisibleImmediate(bool visible)
    {
        if (iconRoot == null || iconCanvasGroup == null)
        {
            return;
        }

        iconRoot.SetActive(visible);
        iconCanvasGroup.alpha = visible ? blinkMaxAlpha : 0f;
    }

    private void KillTweens()
    {
        if (blinkTween != null && blinkTween.IsActive())
        {
            blinkTween.Kill(false);
        }

        if (hideTween != null && hideTween.IsActive())
        {
            hideTween.Kill(false);
        }

        blinkTween = null;
        hideTween = null;
    }
}
