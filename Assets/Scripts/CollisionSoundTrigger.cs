using UnityEngine;

/// <summary>
/// Self-contained collision sound system.
/// Just attach to an object, drag in a sound clip, and you're done!
/// Works with both triggers and collisions.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CollisionSoundTrigger : MonoBehaviour
{
    [Header("Drag & Drop Your Sound Here")]
    [Tooltip("The sound clip(s) to play on collision. Multiple clips = random selection.")]
    [SerializeField] private AudioClip[] soundClips;
    
    [Header("Settings")]
    [Tooltip("Play sound only once? Toggle on for one-time sounds.")]
    [SerializeField] private bool playOnce = false;
    
    [Tooltip("Only play sound when player touches it? (requires 'Player' tag on player)")]
    [SerializeField] private bool playerOnly = true;
    
    [Tooltip("Volume of the sound (0 to 1)")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1.0f;
    
    [Tooltip("Use trigger detection? Check if your collider has 'Is Trigger' enabled.")]
    [SerializeField] private bool useTrigger = false;
    
    [Tooltip("Minimum impact force to trigger sound (only for non-trigger collisions)")]
    [Range(0f, 5f)]
    [SerializeField] private float minImpactForce = 0.1f;
    
    // Internal references - automatically managed
    private AudioSource audioSource;
    private bool hasPlayed = false;
    
    private void Awake()
    {
        // Automatically create and configure AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f; // 2D sound - consistent volume everywhere
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (useTrigger) return; // Skip if using trigger mode
        
        // Player check
        if (playerOnly && !collision.gameObject.CompareTag("Player"))
        {
            return;
        }
        
        // One-time check
        if (playOnce && hasPlayed)
            return;
        
        // Impact force check (relaxed for kinematic rigidbodies)
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < minImpactForce)
        {
            return;
        }
        
        PlaySound();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger) return; // Skip if using collision mode
        
        // Player check
        if (playerOnly && !other.CompareTag("Player"))
        {
            return;
        }
        
        // One-time check
        if (playOnce && hasPlayed)
            return;
        
        PlaySound();
    }
    
    private void PlaySound()
    {
        // Validation
        if (soundClips == null || soundClips.Length == 0)
        {
            // Debug.LogWarning($"CollisionSoundTrigger on '{gameObject.name}': No sound clips assigned!");
            return;
        }
        
        // Pick a random clip if multiple provided
        AudioClip clip = soundClips[Random.Range(0, soundClips.Length)];
        
        if (clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
            hasPlayed = true;
        }
        // else
        // {
        //     Debug.LogWarning($"Selected clip is null!");
        // }
    }
}



