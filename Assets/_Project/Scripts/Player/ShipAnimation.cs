using UnityEngine;
using UnityEngine.VFX;

public class ShipAnimation : MonoBehaviour
{
    [Header("General")]
    public PlayerMovement playerMovement;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private float _dampTime;


    [Header("VFX")]
    public VisualEffect engineFlamesVFX;



    public void Update()
    {
        HandleAnimations();
        HandleSpeedVFX();
    }



    public void HandleAnimations()
    {
        if (_animator == null)
            return;

        float finalRollAmount = playerMovement._rotateDifference;

        if (playerMovement._rotateDirection < 0)
        {
            finalRollAmount *= -1;
        }

        float scaledRoll = ScaleValue(finalRollAmount);

        _animator.SetFloat("RotateDifference", scaledRoll, _dampTime, Time.deltaTime);
    }





    public float ScaleValue(float value)
    {
        float min = -45f;
        float max = 45f;

        // Ensure the value is clamped within the original range
        value = Mathf.Clamp(value, min, max);

        // Scale the value to the range -1 to 1
        return value / max; // Equivalent to (value - min) / (max - min) * 2 - 1
    }




    // VFX -----------------------------------------------------------------------
    public void HandleSpeedVFX()
    {
        if (!GlobalDataStore.Instance.InputManager.IsBoosting)
        {
            engineFlamesVFX.gameObject.SetActive(false);
            return;
        }

        engineFlamesVFX.gameObject.SetActive(true);
    }
}
