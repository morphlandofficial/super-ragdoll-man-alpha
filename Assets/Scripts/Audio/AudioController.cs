using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Simple audio controller for Unity's Audio Mixer.
/// Manages volume levels and saves/loads player preferences.
/// </summary>
public class AudioController : MonoBehaviour
{
    public static AudioController Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Exposed Parameter Names")]
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SFXVolume";
    [SerializeField] private string uiVolumeParam = "UIVolume";

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        LoadVolumeSettings();
    }

    #region Volume Control

    /// <summary>
    /// Sets the master volume level (0 to 1)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        SetVolume(masterVolumeParam, volume);
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    /// <summary>
    /// Sets the music volume level (0 to 1)
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        SetVolume(musicVolumeParam, volume);
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }

    /// <summary>
    /// Sets the SFX volume level (0 to 1)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        SetVolume(sfxVolumeParam, volume);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    /// <summary>
    /// Sets the UI sound volume level (0 to 1)
    /// </summary>
    public void SetUIVolume(float volume)
    {
        SetVolume(uiVolumeParam, volume);
        PlayerPrefs.SetFloat("UIVolume", volume);
    }

    /// <summary>
    /// Internal method to set volume on the Audio Mixer
    /// Converts linear 0-1 range to decibels (-80 to 0 dB)
    /// </summary>
    private void SetVolume(string parameterName, float volume)
    {
        if (audioMixer == null)
        {
// Debug.LogWarning("AudioController: Audio Mixer is not assigned!");
            return;
        }

        // Clamp volume between 0.0001 and 1 (to avoid log(0))
        volume = Mathf.Clamp(volume, 0.0001f, 1f);
        
        // Convert linear volume (0-1) to decibels (-80 to 0 dB)
        // Formula: dB = 20 * log10(volume)
        float volumeDB = 20f * Mathf.Log10(volume);
        
        // Clamp to -80 dB minimum (effectively silent)
        volumeDB = Mathf.Clamp(volumeDB, -80f, 0f);
        
        audioMixer.SetFloat(parameterName, volumeDB);
    }

    #endregion

    #region Get Volume Values

    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat("MasterVolume", 1f);
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat("MusicVolume", 0.8f);
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat("SFXVolume", 1f);
    }

    public float GetUIVolume()
    {
        return PlayerPrefs.GetFloat("UIVolume", 1f);
    }

    #endregion

    #region Settings Persistence

    private void LoadVolumeSettings()
    {
        SetMasterVolume(GetMasterVolume());
        SetMusicVolume(GetMusicVolume());
        SetSFXVolume(GetSFXVolume());
        SetUIVolume(GetUIVolume());
    }

    public void ResetToDefaults()
    {
        SetMasterVolume(1f);
        SetMusicVolume(0.8f);
        SetSFXVolume(1f);
        SetUIVolume(1f);
        PlayerPrefs.Save();
    }

    #endregion
}

