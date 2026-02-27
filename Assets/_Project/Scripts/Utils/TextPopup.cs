using UnityEngine;
using DG.Tweening;
using System.Collections;
using TMPro;

public class TextPopup : MonoBehaviour
{
    public TextMeshPro textMesh;
    public CanvasGroup canvasGroup;
    public float moveSpeed = 1f;
    public float fadeDuration = 1f;
    public float lifetime = 1f;
    public float axisOffset = 0.5f;

    // Call to initialize the popup with text and start the animation.
    public void Initialize(string text, Vector3 position)
    {
        textMesh.text = text;
        lifetime = 0f;
        this.canvasGroup = GetComponent<CanvasGroup>();
        this.canvasGroup.alpha = 0f;
        transform.position = position + new Vector3(0, axisOffset, 0);
        gameObject.transform.localScale = Vector3.zero;

        Decay();
    }

    public void Decay()
    {
        textMesh.DOFade(1f, .1f)
        .OnComplete(() =>
        {
            textMesh.DOFade(0f, fadeDuration).SetEase(Ease.InCubic)
            .OnComplete(() => Destroy(gameObject));
        });

        gameObject.transform.DOScale(Vector3.one, .15f).SetEase(Ease.OutBack);

        gameObject.transform.DOMove(transform.position + Vector3.up, moveSpeed).SetEase(Ease.OutCubic);
    }
}