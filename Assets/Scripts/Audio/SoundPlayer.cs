using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Simple component for playing audio clips.
/// Attach this to any GameObject to make it play sounds.
/// Each AudioSource will automatically route through the Audio Mixer groups.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SoundPlayer : MonoBehaviour
{
    [Header("Audio Source Setup")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("Audio Mixer Group")]
    [Tooltip("Leave empty to use the AudioSource's current output group")]
    [SerializeField] private AudioMixerGroup mixerGroup;

    private void Awake()
    {
        // Get or create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Configure for 2D sound
        audioSource.spatialBlend = 0f; // 2D sound - location independent

        // Assign mixer group if specified
        if (mixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = mixerGroup;
        }
    }

    /// <summary>
    /// Play a sound clip once
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
// Debug.LogWarning("SoundPlayer: No audio clip provided!");
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Play a sound clip with custom volume
    /// </summary>
    public void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
        {
// Debug.LogWarning("SoundPlayer: No audio clip provided!");
            return;
        }

        audioSource.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Play and loop a sound clip
    /// </summary>
    public void PlayLooping(AudioClip clip)
    {
        if (clip == null)
        {
// Debug.LogWarning("SoundPlayer: No audio clip provided!");
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();
    }

    /// <summary>
    /// Stop the currently playing sound
    /// </summary>
    public void Stop()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// Pause the currently playing sound
    /// </summary>
    public void Pause()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// Resume a paused sound
    /// </summary>
    public void Resume()
    {
        audioSource.UnPause();
    }

    /// <summary>
    /// Play a sound at a specific position in 3D space
    /// </summary>
    public static void PlayAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
        {
// Debug.LogWarning("SoundPlayer: No audio clip provided!");
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    /// <summary>
    /// Set the volume of this player's AudioSource
    /// </summary>
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Set the pitch of this player's AudioSource
    /// </summary>
    public void SetPitch(float pitch)
    {
        audioSource.pitch = pitch;
    }
}

