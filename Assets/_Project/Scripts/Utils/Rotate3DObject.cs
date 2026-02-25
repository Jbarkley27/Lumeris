using UnityEngine;
using DG.Tweening;

public class Rotate3DObject : MonoBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 30, 0); // Degrees per second
    public bool CanRotate = true;

    private void Update()
    {
        if (CanRotate)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }

    public void StartRotation()
    {
        CanRotate = true;
    }

    public void StopRotation()
    {
        CanRotate = false;
    }
}