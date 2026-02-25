using UnityEngine;
using DG.Tweening;

public class AnimationLibrary : MonoBehaviour
{
    public static AnimationLibrary Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PunchScale(Transform target, float scaleAmount, float duration, Ease easeType = Ease.OutElastic,
    Vector3 originalScale = default, TweenCallback onComplete = null)
    {
        target.DOKill(); // Stop any ongoing tweens on the target to prevent stacking
        target.transform.localScale = originalScale == default ? target.transform.localScale : originalScale; // Reset to original scale before punching

        target.DOPunchScale(
            Vector3.one * scaleAmount,   // 3% punch-ish
            duration,
            vibrato: 6,
            elasticity: 0.2f
        ).SetEase(easeType)
        .OnComplete(() => target.transform.localScale = originalScale == default ? target.transform.localScale : originalScale) // Ensure it resets to original scale after the punch
        .OnComplete(onComplete);
    }

    public void FadeCanvasGroup(CanvasGroup canvasGroup, float targetAlpha, float duration, Ease easeType = Ease.Linear, TweenCallback onComplete = null)
    {
        canvasGroup.DOFade(targetAlpha, duration).SetEase(easeType).OnComplete(onComplete);
    }
}