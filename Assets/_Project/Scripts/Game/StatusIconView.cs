using System;
using UnityEngine;

/// <summary>
/// Lightweight world-space status icon presenter for blocks.
/// Uses SpriteRenderer for readability and low setup cost.
/// </summary>
public class StatusIconView : MonoBehaviour
{
    [Serializable]
    public class StatusIconEntry
    {
        [Tooltip("Logical icon key (example: marked, shield, bomb).")]
        public string iconId = "marked";

        [Tooltip("Sprite rendered for this icon key.")]
        public Sprite sprite;

        [Tooltip("Tint applied to this icon sprite.")]
        public Color tint = Color.white;

        [Min(0.01f)]
        [Tooltip("Per-icon scale multiplier for readability tuning.")]
        public float scale = 1f;
    }

    [Header("References")]
    [Tooltip("Optional explicit renderer. If null, first child SpriteRenderer is used.")]
    [SerializeField] private SpriteRenderer iconRenderer;

    [Tooltip("Optional root transform for billboard/offset. If null, this transform is used.")]
    [SerializeField] private Transform iconRoot;

    [Header("Placement")]
    [Tooltip("Local-space offset from block root.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.4f, 0f);

    [Min(0.01f)]
    [Tooltip("Base uniform scale applied before per-icon scale.")]
    [SerializeField] private float baseScale = 1f;

    [Header("Billboard")]
    [Tooltip("If true, icon rotates to face camera.")]
    [SerializeField] private bool billboardToCamera = true;

    [Tooltip("If true, only yaw rotates toward camera (keeps icon upright).")]
    [SerializeField] private bool yawOnlyBillboard = true;

    [Tooltip("If true, uses Camera.main when explicit camera is null.")]
    [SerializeField] private bool useMainCameraFallback = true;

    [SerializeField] private Camera explicitCamera;

    [Header("Icons")]
    [Tooltip("Lookup table from iconId to sprite/tint/scale.")]
    [SerializeField] private StatusIconEntry[] iconEntries = Array.Empty<StatusIconEntry>();

    [Tooltip("Hide renderer when no active icon is selected.")]
    [SerializeField] private bool hideWhenNoIcon = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private string activeIconId = string.Empty;

    private bool initialized;
    private Camera cachedCamera;

    /// <summary>
    /// Current active icon key (empty when hidden).
    /// </summary>
    public string ActiveIconId => activeIconId;

    /// <summary>
    /// Returns true while any icon is currently visible.
    /// </summary>
    public bool HasIcon => !string.IsNullOrEmpty(activeIconId);

    private void Awake()
    {
        EnsureInitialized();
        ClearIcon();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        ApplyTransformDefaults();
    }

    private void LateUpdate()
    {
        if (!HasIcon || !billboardToCamera)
        {
            return;
        }

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null || iconRoot == null)
        {
            return;
        }

        Vector3 direction = cameraToUse.transform.position - iconRoot.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (yawOnlyBillboard)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }
        }

        iconRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    /// <summary>
    /// Shows icon by logical key using this view's icon table.
    /// </summary>
    public bool ShowIcon(string iconId)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(iconId))
        {
            ClearIcon();
            return false;
        }

        if (!TryResolveEntry(iconId, out StatusIconEntry entry))
        {
            Debug.LogWarning($"StatusIconView '{name}' has no icon entry for id '{iconId}'.");
            ClearIcon();
            return false;
        }

        if (entry.sprite == null)
        {
            Debug.LogWarning($"StatusIconView '{name}' icon entry '{iconId}' is missing a sprite.");
            ClearIcon();
            return false;
        }

        activeIconId = iconId;
        iconRenderer.sprite = entry.sprite;
        iconRenderer.color = entry.tint;
        iconRenderer.enabled = true;

        if (iconRoot != null)
        {
            iconRoot.localPosition = localOffset;
            iconRoot.localScale = Vector3.one * (baseScale * Mathf.Max(0.01f, entry.scale));
        }

        return true;
    }

    /// <summary>
    /// Hides current icon and clears active key.
    /// </summary>
    public void ClearIcon()
    {
        EnsureInitialized();

        activeIconId = string.Empty;

        if (iconRenderer == null)
        {
            return;
        }

        if (hideWhenNoIcon)
        {
            iconRenderer.enabled = false;
        }
        else
        {
            iconRenderer.enabled = true;
            iconRenderer.sprite = null;
        }
    }

    /// <summary>
    /// Returns true if icon key exists in local icon table.
    /// </summary>
    public bool HasIconEntry(string iconId)
    {
        return TryResolveEntry(iconId, out _);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (iconRoot == null)
        {
            iconRoot = transform;
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        initialized = true;
        cachedCamera = null;
    }

    private void ApplyTransformDefaults()
    {
        if (iconRoot == null)
        {
            return;
        }

        iconRoot.localPosition = localOffset;
        iconRoot.localScale = Vector3.one * Mathf.Max(0.01f, baseScale);
    }

    private bool TryResolveEntry(string iconId, out StatusIconEntry entry)
    {
        entry = null;

        if (iconEntries == null)
        {
            return false;
        }

        for (int i = 0; i < iconEntries.Length; i++)
        {
            StatusIconEntry candidate = iconEntries[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.iconId))
            {
                continue;
            }

            if (string.Equals(candidate.iconId, iconId, StringComparison.OrdinalIgnoreCase))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    private Camera ResolveCamera()
    {
        if (explicitCamera != null)
        {
            return explicitCamera;
        }

        if (!useMainCameraFallback)
        {
            return null;
        }

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main;
        }

        return cachedCamera;
    }
}
