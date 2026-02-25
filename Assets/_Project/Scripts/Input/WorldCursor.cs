using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;



public class WorldCursor : MonoBehaviour
{
    public static WorldCursor instance;

    [SerializeField] private LayerMask _cursorLayer;
    [SerializeField] public RectTransform _cursorUI;
    [SerializeField] private InputManager _inputManager;
    [SerializeField] private GameObject _physicalWorldCursor;
    [SerializeField] private float _cursorSpeed = 10;
    [SerializeField] private Image cursorImage;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool IsHoveringOverEnemy;
    [SerializeField] private GameObject CenterDot;



    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found a Cursor Manager object, destroying new one");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }


    void Update()
    {
        Cursor.visible = false;
        CursorUI();
        CenterDot.SetActive(IsHoveringOverEnemy);
    }


    public void CursorUI()
    {
        Vector3 cursorInput = _inputManager.CursorInput;
        Vector3 userCursorInput = Vector3.zero;

        if (_inputManager.CurrentDevice == InputManager.InputDevice.K_M)
        {
            // MOUSE INPUT
            userCursorInput = cursorInput;
        }
        else if (_inputManager.CurrentDevice == InputManager.InputDevice.GAMEPAD)
        {
            // GAMEPAD INPUT

            // make sure the cursor doesn't go off screen
            Vector3 cursorPos = _cursorUI.position;

            cursorPos.x += cursorInput.x * _cursorSpeed;
            cursorPos.y += cursorInput.y * _cursorSpeed;

            userCursorInput = cursorPos;
        }

        // clamp the cursor to the screen bounds
        userCursorInput.x = Mathf.Clamp(userCursorInput.x, 0, Screen.width);
        userCursorInput.y = Mathf.Clamp(userCursorInput.y, 0, Screen.height);

        _cursorUI.position = userCursorInput;

        MovePhysicalCursor();
    }
    

    public void CheckForEnemyHover()
    {
        // 1. Get the center of the UI Image in screen space
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, cursorImage.rectTransform.position);

        // 2. Cast a ray into the world
        Ray ray = Camera.main.ScreenPointToRay(screenPos);

        // 3. Check if it hits something in your target layer
        if (Physics.Raycast(ray, out RaycastHit hit, 100000f, enemyLayer))
        {
            // if its the first time hovering over an enemy, play a sound
            if (!IsHoveringOverEnemy)
            {
                // Play hover sound or any other logic
                // AudioManager.Instance.PlayOneShot(AudioLibrary.Hover_Over_Enemy);
            }

            IsHoveringOverEnemy = true;
        }
        else
        {
            if (IsHoveringOverEnemy)
            {
                // Play hover exit sound or any other logic
                // AudioManager.Instance.PlayOneShot(AudioLibrary.Unhover_Over_Enemy);
            }

            IsHoveringOverEnemy = false;
        }
    }



    public void MovePhysicalCursor()
    {
        if (Camera.main == null || _physicalWorldCursor == null)
        {
            Debug.LogWarning("Camera.main or physicalWorldCursor is null");
            return;
        }
        // raycast to get the position of the line end
        Ray ray = Camera.main.ScreenPointToRay(_cursorUI.GetComponent<RectTransform>().position);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, _cursorLayer))
        {
            Vector3 nextPos = new Vector3(hit.point.x, 0, hit.point.z);
            _physicalWorldCursor.transform.position = nextPos;
        }
    }




    public Vector3 GetDirectionFromWorldCursor(Vector3 source)
    {
        if (_physicalWorldCursor == null)
        {
            Debug.LogError("physicalWorldCursor is null");
            return Vector3.zero;
        }

        return _physicalWorldCursor.transform.position - source;
    }




    public Vector3 GetCursorPosition()
    {
        return cursorImage.transform.position;
    }
}
