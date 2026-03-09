using System.Collections;
using UnityEngine;

/// <summary>
/// Listens for WorldGridLoader world-load events and lerps camera background color
/// to the active layout's configured color.
/// </summary>
public class LayoutCameraBackgroundLerper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WorldGridLoader worldGridLoader;
    [SerializeField] private Camera targetCamera;

    [Header("Transition")]
    [Min(0f)]
    [SerializeField] private float transitionDuration = 0.6f;
    [SerializeField] private bool forceSolidColorClearFlags = true;
    [SerializeField] private bool applyCurrentLayoutOnEnable = true;

    private Coroutine lerpRoutine;

    private void Awake()
    {
        if (worldGridLoader == null)
            worldGridLoader = FindFirstObjectByType<WorldGridLoader>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (forceSolidColorClearFlags && targetCamera != null)
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void OnEnable()
    {
        if (worldGridLoader != null)
            worldGridLoader.WorldLoaded += OnWorldLoaded;

        if (applyCurrentLayoutOnEnable && worldGridLoader != null && worldGridLoader.CurrentLoadedLayout != null)
            ApplyLayoutColor(worldGridLoader.CurrentLoadedLayout, immediate: true);
    }

    private void OnDisable()
    {
        if (worldGridLoader != null)
            worldGridLoader.WorldLoaded -= OnWorldLoaded;

        if (lerpRoutine != null)
        {
            StopCoroutine(lerpRoutine);
            lerpRoutine = null;
        }
    }

    private void OnWorldLoaded(WorldLayout2D loadedLayout)
    {
        ApplyLayoutColor(loadedLayout, immediate: false);
    }

    private void ApplyLayoutColor(WorldLayout2D layout, bool immediate)
    {
        if (layout == null || targetCamera == null)
            return;

        Color target = layout.CameraBackgroundColor;

        if (immediate || transitionDuration <= 0f)
        {
            targetCamera.backgroundColor = target;
            return;
        }

        if (lerpRoutine != null)
            StopCoroutine(lerpRoutine);

        lerpRoutine = StartCoroutine(LerpBackground(target));
    }

    private IEnumerator LerpBackground(Color target)
    {
        Color start = targetCamera.backgroundColor;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / transitionDuration;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            targetCamera.backgroundColor = Color.Lerp(start, target, eased);
            yield return null;
        }

        targetCamera.backgroundColor = target;
        lerpRoutine = null;
    }
}
