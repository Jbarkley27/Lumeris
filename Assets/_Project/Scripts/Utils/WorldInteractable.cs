using UnityEngine;
using System;

public abstract class WorldInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private bool interactable = true;

    public bool IsInteractable => interactable;
    public bool IsHovered { get; private set; }
    public static event Action<WorldInteractable> Clicked;

    /// <summary>
    /// Set this from derived classes to gate click behavior.
    /// </summary>
    public virtual bool CanClick() => interactable;

    internal void NotifyHoverEnter()
    {
        if (!interactable || IsHovered) return;
        IsHovered = true;
        OnHoverEnter();
    }

    internal void NotifyHoverExit()
    {
        if (!IsHovered) return;
        IsHovered = false;
        OnHoverExit();
    }

    internal void NotifyHoverStay()
    {
        if (!interactable || !IsHovered) return;
        OnHoverStay();
    }

    internal void NotifyClick()
    {
        if (!CanClick()) return;
        Clicked?.Invoke(this);
        OnClicked();
    }

    protected virtual void OnHoverEnter() { }
    protected virtual void OnHoverExit() { }
    protected virtual void OnHoverStay() { }
    protected virtual void OnClicked() { }
}
