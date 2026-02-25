using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class GlobalDataStore : MonoBehaviour
{
    public static GlobalDataStore Instance { get; private set; }

    [Header("Player State")]
    // public CameraPanZoomController CameraController;
    public CinemachineCamera CineCam;
    public InputManager InputManager;
    public float nearClip = -50f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // keep cinemachine near clip plane value at -50
        if (CineCam != null)
        {
            SetNearClip();
        }
    }


    public void SetNearClip()
    {
        var lens = CineCam.Lens; 
        lens.NearClipPlane = nearClip;
        CineCam.Lens = lens; 
    }
}