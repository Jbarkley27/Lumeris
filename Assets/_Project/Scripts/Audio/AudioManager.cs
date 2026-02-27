using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using FMOD.Studio;


public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;


    [Header("FMOD Busses")]
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


    /// <summary>
    /// Plays one-shot SFX using enum ID.
    /// Preferred API for gameplay scripts.
    /// </summary>
    public void PlayOneShot(AudioSfxId sfxId)
    {
        string eventPath = AudioLibrary.GetEventPath(sfxId);

        if (string.IsNullOrWhiteSpace(eventPath))
        {
            Debug.LogWarning($"AudioManager.PlayOneShot: No path mapped for SFX id '{sfxId}'.");
            return;
        }

        PlayOneShot(eventPath);
    }

}
