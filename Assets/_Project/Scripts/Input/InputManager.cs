using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("General")]
    public PlayerInput _playerInput;
    public PlayerMovement _playerMovement;


    [Header("Thrust")]
    public Vector2 ThrustInput;


    [Header("Cursor")]
    public Vector2 CursorInput;
    public Vector3 CursorPosition;

    [Header("Boosting")]
    public bool IsBoosting = false;
    public bool BoostTriggerForFirstTime = false;

    [Header("Shooting")]
    public bool IsShootingBlaster = false;
    public bool HasReleasedShooting = true;


    [Header("Current Device Settings")]
    public InputDevice CurrentDevice;
    public enum InputDevice { K_M, GAMEPAD };



    private void Start()
    {
        CursorInput = new Vector2(0, 0);
        _playerInput = GetComponent<PlayerInput>();
        _playerMovement = GetComponent<PlayerMovement>();
    }



    void Update()
    {
        GetCurrentDevice();
        ShootBlaster();
    }



    public void Cursor(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            CursorInput = context.ReadValue<Vector2>();
        }
    }




    public void Boost()
    {
        if (_playerInput.actions["Boost"].IsPressed())
        {
            IsBoosting = true;
        }
        else if (_playerInput.actions["Boost"].WasReleasedThisFrame())
        {
            IsBoosting = false;
            BoostTriggerForFirstTime = true;
        }
    }


    public void Thrust(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ThrustInput = context.ReadValue<Vector2>();
        }
    }


    public void ShootBlaster()
    {

        if (_playerInput.actions["ShootBlaster"].IsPressed())
        {
            IsShootingBlaster = true;
            return;
        } else
        {
            HasReleasedShooting = true;
            IsShootingBlaster = false;
        }
    }
    




    public void GetCurrentDevice()
    {
        if (_playerInput.currentControlScheme == "M&K")
        {
            CurrentDevice = InputDevice.K_M;
        }
        else if (_playerInput.currentControlScheme == "Gamepad")
        {
            CurrentDevice = InputDevice.GAMEPAD;
        }
    }
}
