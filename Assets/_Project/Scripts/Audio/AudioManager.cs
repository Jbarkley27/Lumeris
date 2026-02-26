using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using FMOD.Studio;


public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;


    [Header("FMOD Busses")]
    private Bus musicBus;
    private Bus masterBus;
    public string musicBusPath = "bus:/MusicBus"; 
    public string masterBusPath = "bus:/MasterBus";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {

    }


    void Update()
    {

    }

    // --- Public API ---

    public void PlayOneShot(string eventPath)
    {
        RuntimeManager.PlayOneShot(eventPath);
    }

}
