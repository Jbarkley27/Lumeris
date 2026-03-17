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
    [Tooltip("If true, keeps trying to resolve loader/camera at runtime in case they spawn later.")]
    [SerializeField] private bool autoResolveReferencesAtRuntime = true;
    [Tooltip("If true, polling fallback applies color when floorId changes even if event timing was missed.")]
    [SerializeField] private bool pollForLayoutChanges = true;

    private Coroutine lerpRoutine;
    private string lastAppliedFloorId = string.Empty;
    private WorldGridLoader subscribedWorldGridLoader;

    private void Awake()
    {
        ResolveReferences();
        ApplySolidColorClearFlagsIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureWorldLoadedSubscription();

        if (applyCurrentLayoutOnEnable && worldGridLoader != null && worldGridLoader.CurrentLoadedLayout != null)
            ApplyLayoutColor(worldGridLoader.CurrentLoadedLayout, immediate: true);
    }

    private void OnDisable()
    {
        RemoveWorldLoadedSubscription();

        if (lerpRoutine != null)
        {
            StopCoroutine(lerpRoutine);
            lerpRoutine = null;
        }
    }

    private void Update()
    {
        if (!autoResolveReferencesAtRuntime && !pollForLayoutChanges)
        {
            return;
        }

        if (autoResolveReferencesAtRuntime)
        {
            // Handles cases where camera/loader are instantiated after this component.
            ResolveReferences();
            ApplySolidColorClearFlagsIfNeeded();
            EnsureWorldLoadedSubscription();
        }

        if (!pollForLayoutChanges || worldGridLoader == null || worldGridLoader.CurrentLoadedLayout == null)
        {
            return;
        }

        // Fallback sync in case event order caused us to miss an apply.
        string currentFloorId = worldGridLoader.CurrentLoadedLayout.floorId;
        if (!string.Equals(lastAppliedFloorId, currentFloorId, System.StringComparison.Ordinal))
        {
            ApplyLayoutColor(worldGridLoader.CurrentLoadedLayout, immediate: false);
        }
    }

    private void OnWorldLoaded(WorldLayout2D loadedLayout)
    {
        ApplyLayoutColor(loadedLayout, immediate: false);
    }

    private void ApplyLayoutColor(WorldLayout2D layout, bool immediate)
    {
        if (layout == null)
            return;

        // Retry camera resolve right before applying.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
            return;

        ApplySolidColorClearFlagsIfNeeded();

        // Track applied floor so polling fallback doesn't repeatedly re-apply.
        lastAppliedFloorId = layout.floorId;
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

    private void ResolveReferences()
    {
        if (worldGridLoader == null)
        {
            worldGridLoader = FindFirstObjectByType<WorldGridLoader>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void EnsureWorldLoadedSubscription()
    {
        if (worldGridLoader == null)
        {
            return;
        }

        if (subscribedWorldGridLoader == worldGridLoader)
        {
            return;
        }

        // If loader reference changed, unsubscribe from the old one first.
        RemoveWorldLoadedSubscription();
        worldGridLoader.WorldLoaded += OnWorldLoaded;
        subscribedWorldGridLoader = worldGridLoader;
    }

    private void RemoveWorldLoadedSubscription()
    {
        if (subscribedWorldGridLoader == null)
        {
            return;
        }

        subscribedWorldGridLoader.WorldLoaded -= OnWorldLoaded;
        subscribedWorldGridLoader = null;
    }

    private void ApplySolidColorClearFlagsIfNeeded()
    {
        if (!forceSolidColorClearFlags || targetCamera == null)
        {
            return;
        }

        if (targetCamera.clearFlags != CameraClearFlags.SolidColor)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }
}
