using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraPanZoomController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera overviewCamera;
    [SerializeField] private CinemachineCamera focusCamera;
    [SerializeField] private Camera outputCamera;

    [Header("Pan (Overview)")]
    [SerializeField] private float moveSpeed = 100f;

    [Header("Zoom")]
    [SerializeField] private float baseOrthographicSize = 70f;
    // [SerializeField] private bool useFixedY = true;
    // [SerializeField] private bool forceOverviewNoTarget = true;

    [Header("Drag Settings")]
    [SerializeField] private float dragPanMultiplier = 1f;
    [SerializeField] private float dragPlaneY = 0f; // same plane your overview camera should pan on
    [SerializeField] private bool useMiddleMouseForDrag = true;
    [SerializeField] private float dragDeadzone = 0.0001f;

    private bool _isDragging;
    private Vector2 _lastDragScreenPoint;

    [SerializeField] private bool autoFocusClickedInteractables = true;

    [Header("Lens Sync")]
    [SerializeField] private bool keepLensSynced = true;

    private bool _hasWarnedMissingRefs;

    public enum CameraMode
    {
        Overview,
        Focus
    }

    public CameraMode CurrentCameraMode = CameraMode.Focus;

    private void Awake()
    {
        if (overviewCamera == null || focusCamera == null || outputCamera == null)
        {
            Debug.LogError("CameraPanZoomController: Missing camera references. Please assign in inspector.");
            _hasWarnedMissingRefs = true;
        }

        // SwitchToCameraMode(CurrentCameraMode);
        // overviewCamera.gameObject.transform.position = focusCamera.gameObject.transform.position;

    }


    private void Update()
    {
        if (_hasWarnedMissingRefs) return;

        // ReadPanInput();
        // SyncCameras();
    }


    private void LateUpdate() 
    {
        if (_hasWarnedMissingRefs) return;

        // HandleDragPan();  
    }


    private void HandleDragPan()
    {
        if (_hasWarnedMissingRefs|| Mouse.current == null) return;

        var dragButton = useMiddleMouseForDrag ? Mouse.current.middleButton : Mouse.current.rightButton;

        if (dragButton.wasPressedThisFrame)
        {
            SwitchToCameraMode(CameraMode.Overview);
            _isDragging = true;
            _lastDragScreenPoint = Mouse.current.position.ReadValue();
            return;
        }

        if (dragButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            return;
        }

        if (!_isDragging || !dragButton.isPressed) return;

        SwitchToCameraMode(CameraMode.Overview);

        Vector2 currentScreenPoint = Mouse.current.position.ReadValue();
        if (!TryGetScreenDeltaOnPanPlane(_lastDragScreenPoint, currentScreenPoint, out Vector3 delta))
        {
            _lastDragScreenPoint = currentScreenPoint;
            return;
        }

        if (delta.sqrMagnitude > dragDeadzone * dragDeadzone)
        {
            Vector3 next = overviewCamera.transform.position + delta;
            overviewCamera.transform.position = next;
        }

        _lastDragScreenPoint = currentScreenPoint;
    }

    private bool TryGetScreenDeltaOnPanPlane(Vector2 fromScreenPoint, Vector2 toScreenPoint, out Vector3 worldDelta)
    {
        worldDelta = default;
        if (outputCamera == null) return false;

        Ray fromRay = outputCamera.ScreenPointToRay(fromScreenPoint);
        Ray toRay = outputCamera.ScreenPointToRay(toScreenPoint);

        Plane panPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneY, 0f));
        if (!panPlane.Raycast(fromRay, out float fromEnter)) return false;
        if (!panPlane.Raycast(toRay, out float toEnter)) return false;

        Vector3 fromWorld = fromRay.GetPoint(fromEnter);
        Vector3 toWorld = toRay.GetPoint(toEnter);

        // Invert so drag feels like grabbing and moving the world.
        worldDelta = (fromWorld - toWorld) * dragPanMultiplier;
        worldDelta.y = 0f;
        return true;
    }



    private Vector2 ReadPanInput()
    {
        if (Keyboard.current == null) return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (Keyboard.current.aKey.isPressed) x -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.sKey.isPressed) y -= 1f;
        if (Keyboard.current.wKey.isPressed) y += 1f;

        Vector2 input = new Vector2(x, y);
        if (input.sqrMagnitude > 1f)
            input = input.normalized;

        if (input.sqrMagnitude <= 0f)
            return Vector2.zero;

        SwitchToCameraMode(CameraMode.Overview);
        PanOverview(input);
        
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public void SwitchToCameraMode(CameraMode mode)
    {
        if (_hasWarnedMissingRefs) return;

        bool alreadyInRequestedMode =
            CurrentCameraMode == mode &&
            overviewCamera.gameObject.activeSelf == (mode == CameraMode.Overview) &&
            focusCamera.gameObject.activeSelf == (mode == CameraMode.Focus);

        if (alreadyInRequestedMode) return;

        CinemachineCamera fromCamera = CurrentCameraMode == CameraMode.Overview ? overviewCamera : focusCamera;
        CinemachineCamera toCamera = mode == CameraMode.Overview ? overviewCamera : focusCamera;

        // Prevent jump: start the target camera at the current live camera pose/lens.
        toCamera.transform.SetPositionAndRotation(fromCamera.transform.position, fromCamera.transform.rotation);
        toCamera.Lens = fromCamera.Lens;

        CurrentCameraMode = mode;
        if (mode == CameraMode.Overview)
        {
            overviewCamera.gameObject.SetActive(true);
            focusCamera.gameObject.SetActive(false);
        }
        else
        {
            overviewCamera.gameObject.SetActive(false);
            focusCamera.gameObject.SetActive(true);
        }
    }



    private void PanOverview(Vector2 input)
    {
        if (_hasWarnedMissingRefs) return;

        Vector3 right = outputCamera.transform.right;
        Vector3 forward = outputCamera.transform.forward;

        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        Vector3 move = (right * input.x + forward * input.y) * (moveSpeed * Time.deltaTime);

        Vector3 next = overviewCamera.transform.position + move;

        overviewCamera.transform.position = next;
    }


    public void SyncCameras()
    {
        if (_hasWarnedMissingRefs) return;

        // sync lens settings
        if (keepLensSynced)
        {
            LensSettings lens = overviewCamera.Lens;
            lens.OrthographicSize = baseOrthographicSize;
            overviewCamera.Lens = lens;

            focusCamera.Lens = lens;
        }
            
    }


    public void FocusOnTarget(Transform target)
    {
        if (_hasWarnedMissingRefs || target == null) return;

        focusCamera.Follow = target;
        focusCamera.LookAt = target;

        // overviewCamera.gameObject.transform.position = focusCamera.gameObject.transform.position;

        SwitchToCameraMode(CameraMode.Focus);
    }
}
