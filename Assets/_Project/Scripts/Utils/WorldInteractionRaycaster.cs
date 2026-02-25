using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class WorldInteractionRaycaster : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;

    [Header("Raycast")]
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private float rayDistance = 500f;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Input")]
    [SerializeField] private bool blockWhenPointerOverUI = true;
    [SerializeField] private bool requireMousePresent = true;

    private WorldInteractable _hovered;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Update()
    {
        if (worldCamera == null) return;
        if (requireMousePresent && Mouse.current == null) return;
        if (blockWhenPointerOverUI && IsPointerOverUI()) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        WorldInteractable hitInteractable = RaycastInteractable(screenPos);
        HandleHover(hitInteractable);

        bool clickPressed = Mouse.current != null
            ? Mouse.current.leftButton.wasPressedThisFrame
            : Input.GetMouseButtonDown(0);

        if (clickPressed && _hovered != null)
        {
            _hovered.NotifyClick();
        }
    }

    private void OnDisable()
    {
        ClearHover();
    }

    private WorldInteractable RaycastInteractable(Vector2 screenPos)
    {
        Ray ray = worldCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableMask, queryTriggerInteraction))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<WorldInteractable>();
    }

    private void HandleHover(WorldInteractable next)
    {
        if (_hovered == next)
        {
            if (_hovered != null) _hovered.NotifyHoverStay();
            return;
        }

        if (_hovered != null)
        {
            _hovered.NotifyHoverExit();
        }

        _hovered = next;

        if (_hovered != null)
        {
            _hovered.NotifyHoverEnter();
        }
    }

    private void ClearHover()
    {
        if (_hovered == null) return;
        _hovered.NotifyHoverExit();
        _hovered = null;
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }
}
