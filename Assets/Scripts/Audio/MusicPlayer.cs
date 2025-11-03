using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Persistent music player that automatically plays different tracks for different scenes.
/// Attach to a persistent GameObject (like AudioController) and configure music per scene.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [System.Serializable]
    public class SceneMusic
    {
#if UNITY_EDITOR
        [Tooltip("Drag and drop the scene asset here")]
        public SceneAsset sceneAsset;
#endif
        
        [HideInInspector]
        [Tooltip("The name of the scene (automatically set from scene asset)")]
        public string sceneName;
        
        [Tooltip("The music track to play in this scene")]
        public AudioClip musicTrack;
        
        [Tooltip("Should this music loop?")]
        public bool loop = true;
    }

    public static MusicPlayer Instance { get; private set; }

    [Header("Audio Setup")]
    [SerializeField] private AudioSource audioSource;
    
    [Tooltip("Audio Mixer Group for music (should be the Music group from your mixer)")]
    [SerializeField] private AudioMixerGroup musicMixerGroup;

    [Header("Scene-Specific Music")]
    [Tooltip("Configure music tracks for each scene")]
    [SerializeField] private SceneMusic[] sceneMusicList;
    
    [Header("Special Music (Title Screen)")]
    [Tooltip("Special music track to play on title screen when ALL levels are unlocked")]
    [SerializeField] private AudioClip allLevelsUnlockedMusic;
    
    [Tooltip("Should the 'all unlocked' music loop?")]
    [SerializeField] private bool allLevelsUnlockedMusicLoop = true;
    
    [Header("Settings")]
    // [Tooltip("Fade between tracks when changing scenes?")]
    // [SerializeField] private bool fadeTransitions = false; // Unused
    
    [Tooltip("Default fade duration when transitioning between tracks (if fade enabled)")]
    [SerializeField] private float defaultFadeDuration = 1.5f;

    private Coroutine fadeCoroutine;
    private string currentSceneName = "";

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatically update scene names from scene assets
        if (sceneMusicList != null)
        {
            foreach (var sceneMusic in sceneMusicList)
            {
                if (sceneMusic.sceneAsset != null)
                {
                    sceneMusic.sceneName = sceneMusic.sceneAsset.name;
                }
            }
        }
    }
#endif

    private void Awake()
    {
        // Singleton pattern - this persists across scenes
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
            return;
        }
        
        // Get or create AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ConfigureAudioSource();
        
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Play music for the initial scene
        PlayMusicForCurrentScene();
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loaded event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Automatically play the correct music when a new scene loads
        PlayMusicForCurrentScene();
    }

    /// <summary>
    /// Force a refresh of the current music (useful when game state changes mid-scene)
    /// </summary>
    public void RefreshMusic()
    {
        currentSceneName = ""; // Reset to force a re-check
        PlayMusicForCurrentScene();
    }
    
    private void PlayMusicForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        
        // Don't restart if we're already playing the right track
        if (sceneName == currentSceneName)
            return;
        
        currentSceneName = sceneName;
        
        // SPECIAL CASE: Title screen with all levels unlocked
        if (allLevelsUnlockedMusic != null && IsTitleScreen(sceneName) && AreAllLevelsUnlocked())
        {
            Debug.Log("[MusicPlayer] 🎵 All levels unlocked! Playing special victory music...");
            audioSource.loop = allLevelsUnlockedMusicLoop;
            PlayMusic(allLevelsUnlockedMusic, false);
            return;
        }
        
        // Find the music for this scene
        SceneMusic sceneMusic = System.Array.Find(sceneMusicList, sm => sm.sceneName == sceneName);
        
        if (sceneMusic != null && sceneMusic.musicTrack != null)
        {
            audioSource.loop = sceneMusic.loop;
            // Always hard cut between scenes - no fade
            PlayMusic(sceneMusic.musicTrack, false);
        }
        else
        {
            // No music configured for this scene - stop current music immediately
            Stop(false);
        }
    }
    
    /// <summary>
    /// Check if the current scene is the title screen
    /// </summary>
    private bool IsTitleScreen(string sceneName)
    {
        // Check for common title screen scene names
        string lowerSceneName = sceneName.ToLower();
        return lowerSceneName.Contains("title") || lowerSceneName.Contains("menu") || lowerSceneName.Contains("main");
    }
    
    /// <summary>
    /// Check if ALL levels are currently unlocked
    /// </summary>
    private bool AreAllLevelsUnlocked()
    {
        if (LevelManager.Instance == null)
        {
            return false; // No Level Manager = can't check
        }
        
        return LevelManager.Instance.AreAllLevelsUnlocked();
    }

    private void ConfigureAudioSource()
    {
        if (audioSource == null) return;

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D sound - location independent
        
        // Assign to mixer group
        if (musicMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = musicMixerGroup;
        }
    }

    /// <summary>
    /// Play a music track for a specific scene by name
    /// </summary>
    public void PlayMusicForScene(string sceneName, bool fade = true)
    {
        SceneMusic sceneMusic = System.Array.Find(sceneMusicList, sm => sm.sceneName == sceneName);
        
        if (sceneMusic != null && sceneMusic.musicTrack != null)
        {
            audioSource.loop = sceneMusic.loop;
            PlayMusic(sceneMusic.musicTrack, fade);
        }
        else
        {
// Debug.LogWarning($"MusicPlayer: No music configured for scene '{sceneName}'");
        }
    }

    /// <summary>
    /// Play a specific music clip
    /// </summary>
    public void PlayMusic(AudioClip clip, bool fade = true)
    {
        if (clip == null)
        {
// Debug.LogWarning("MusicPlayer: Music clip is null!");
            return;
        }

        // If already playing this clip, do nothing
        if (audioSource.clip == clip && audioSource.isPlaying)
            return;

        if (fade)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            
            fadeCoroutine = StartCoroutine(FadeToNewTrack(clip, defaultFadeDuration));
        }
        else
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    /// <summary>
    /// Stop the music
    /// </summary>
    public void Stop(bool fade = true)
    {
        if (fade)
        {
            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);
            
            fadeCoroutine = StartCoroutine(FadeOut(defaultFadeDuration));
        }
        else
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Pause the music
    /// </summary>
    public void Pause()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// Resume the music
    /// </summary>
    public void Resume()
    {
        audioSource.UnPause();
    }

    /// <summary>
    /// Set the volume (0 to 1)
    /// Note: Audio Mixer controls take precedence
    /// </summary>
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    #region Fade Coroutines

    private IEnumerator FadeToNewTrack(AudioClip newClip, float duration)
    {
        float halfDuration = duration / 2f;

        // Fade out current track
        if (audioSource.isPlaying)
        {
            yield return StartCoroutine(FadeVolume(audioSource.volume, 0f, halfDuration));
        }

        // Switch to new track
        audioSource.clip = newClip;
        audioSource.Play();

        // Fade in new track
        yield return StartCoroutine(FadeVolume(0f, 1f, halfDuration));

        fadeCoroutine = null;
    }

    private IEnumerator FadeOut(float duration)
    {
        float startVolume = audioSource.volume;
        yield return StartCoroutine(FadeVolume(startVolume, 0f, duration));
        
        audioSource.Stop();
        audioSource.volume = startVolume;
        
        fadeCoroutine = null;
    }

    private IEnumerator FadeVolume(float startVolume, float targetVolume, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    #endregion

    #region Public Getters

    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }

    public string GetCurrentTrackName()
    {
        return audioSource.clip != null ? audioSource.clip.name : "None";
    }

    public string GetCurrentSceneName()
    {
        return currentSceneName;
    }

    #endregion
}

