using UnityEngine;

/// <summary>
/// Collectible component that gives time and/or points when player touches it.
/// Scales down and disappears after collection.
/// Add this to any object with a Rigidbody and Collider.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    [Header("Time Effect")]
    [SerializeField]
    [Tooltip("Enable time bonus/penalty")]
    private bool affectTime = true;
    
    [SerializeField]
    [Tooltip("Seconds to add (positive) or subtract (negative) from timer")]
    private float timeAmount = 5f;
    
    [Header("Points Effect")]
    [SerializeField]
    [Tooltip("Enable physics points bonus/penalty")]
    private bool affectPoints = false;
    
    [SerializeField]
    [Tooltip("Points to add (positive) or subtract (negative)")]
    private float pointsAmount = 100f;
    
    [Header("Scale Animation")]
    [SerializeField]
    [Tooltip("Speed at which the object scales down (units per second)")]
    private float scaleDownSpeed = 3f;
    
    [SerializeField]
    [Tooltip("Delay before starting to scale down (seconds)")]
    private float scaleDownDelay = 0f;
    
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when collected (optional)")]
    private AudioClip collectSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the collect sound")]
    private float collectSoundVolume = 0.8f;
    
    [Header("Debug")]
    [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    // Internal state
    private bool hasBeenCollected = false;
    private bool isScalingDown = false;
    private float scaleDownTimer = 0f;
    private Vector3 originalScale;
    private TimerDisplayText timerReference;
    private RagdollPointsSystem pointsSystem;
    
    // Public property to check if collected
    public bool HasBeenCollected => hasBeenCollected;
    
    // Event fired when this collectible is collected
    public event System.Action OnCollected;
    
    private void Awake()
    {
        // Store original scale
        originalScale = transform.localScale;
        
        // Ensure rigidbody is kinematic (so it doesn't fall)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true;
            // if (false) // showDebugMessages

        }
    }
    
    private void Start()
    {
        // Find timer in scene
        if (affectTime)
        {
            timerReference = FindFirstObjectByType<TimerDisplayText>();
            if (timerReference == null && false) // showDebugMessages
            {
// Debug.LogWarning($"Collectible on {gameObject.name}: No TimerDisplayText found in scene. Time effect will not work.");
            }
        }
        
        // Find points system
        if (affectPoints)
        {
            pointsSystem = RagdollPointsSystem.Instance;
            if (pointsSystem == null && false) // showDebugMessages
            {
// Debug.LogWarning($"Collectible on {gameObject.name}: No RagdollPointsSystem found in scene. Points effect will not work.");
            }
        }
    }
    
    private void Update()
    {
        // Handle scale down animation
        if (isScalingDown)
        {
            scaleDownTimer += Time.deltaTime;
            
            // Wait for delay
            if (scaleDownTimer < scaleDownDelay)
                return;
            
            // Scale down
            float scaleFactor = 1f - ((scaleDownTimer - scaleDownDelay) * scaleDownSpeed);
            scaleFactor = Mathf.Max(0f, scaleFactor);
            
            transform.localScale = originalScale * scaleFactor;
            
            // Destroy when fully scaled down
            if (scaleFactor <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Only trigger once
        if (hasBeenCollected)
            return;
        
        // Try to find the player
        Rigidbody collidedRigidbody = collision.rigidbody;
        if (collidedRigidbody == null)
            return;
        
        // Check if this is part of the player's ragdoll
        DefaultBehaviour playerBehaviour = collidedRigidbody.GetComponentInParent<DefaultBehaviour>();
        if (playerBehaviour == null)
        {
            // Not the player
            return;
        }
        
        // COLLECT IT!
        Collect();
    }
    
    /// <summary>
    /// Creates a temporary GameObject to play a sound in 2D space, then destroys it when done
    /// </summary>
    private void PlaySoundAndDestroy(AudioClip clip, float volume)
    {
        if (clip == null) return;
        
        // Create empty GameObject for the sound
        GameObject soundObject = new GameObject($"TempSound_{clip.name}");
        soundObject.transform.position = transform.position; // Position doesn't matter for 2D sound but set it anyway
        
        // Add and configure AudioSource
        AudioSource tempAudioSource = soundObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 0f; // 2D sound
        tempAudioSource.playOnAwake = false;
        tempAudioSource.Play();
        
        // Destroy the GameObject after the clip finishes playing
        // Add a small buffer (0.1s) to ensure the sound fully completes
        Destroy(soundObject, clip.length + 0.1f);
    }
    
    private void Collect()
    {
        hasBeenCollected = true;
        
        // Fire collected event
        Debug.Log($"[Collectible] {gameObject.name} collected! Firing OnCollected event. Subscribers: {(OnCollected != null ? OnCollected.GetInvocationList().Length : 0)}");
        OnCollected?.Invoke();
        
        // Apply time effect
        if (affectTime && timerReference != null && timeAmount != 0)
        {
            timerReference.AddTime(timeAmount);
            if (false) // showDebugMessages
            {
                string effect = timeAmount > 0 ? "bonus" : "penalty";
            }
        }
        
        // Apply points effect
        if (affectPoints && pointsSystem != null && pointsAmount != 0)
        {
            pointsSystem.AddPoints(pointsAmount);
            if (false) // showDebugMessages
            {
                string effect = pointsAmount > 0 ? "bonus" : "penalty";
            }
        }
        
        // Play audio using a temporary game object so sound isn't cut off
        if (collectSound != null)
        {
            PlaySoundAndDestroy(collectSound, collectSoundVolume);
        }
        
        // Start scale down animation
        isScalingDown = true;
        scaleDownTimer = 0f;
        
        // Disable collider so it can't be collected again
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }
}

