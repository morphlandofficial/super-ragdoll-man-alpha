using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Records and rewinds character physics state
/// </summary>
public class TimeRewindController : MonoBehaviour
{
    [Header("Rewind Settings")]
    [SerializeField]
    [Tooltip("How many seconds of gameplay to record")]
    private float rewindBufferSeconds = 5f;
    
    [SerializeField]
    [Tooltip("Main rigidbody to rewind (usually the torso)")]
    private Rigidbody targetRigidbody;
    
    [SerializeField]
    [Tooltip("Colliders to disable during rewind")]
    private Collider[] collidersToDisable;
    
    [Header("Audio Settings")]
    [SerializeField]
    [Tooltip("Sound effect to play during rewind (non-looping)")]
    private AudioClip rewindSound;
    
    [SerializeField]
    [Tooltip("Audio Mixer Group for rewind sound (should be SFX)")]
    private AudioMixerGroup rewindMixerGroup;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the rewind sound")]
    private float rewindSoundVolume = 0.8f;
    
    private AudioSource rewindAudioSource;
    
    // Recording data structure
    private struct PhysicsSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float timestamp;
    }
    
    // State - Using circular buffer for performance
    private PhysicsSnapshot[] recordedFrames;
    private int currentFrameIndex = 0;
    private int recordedFrameCount = 0;
    private bool isRewinding = false;
    private int rewindIndex = 0;
    private int maxFrames;
    private bool wasKinematic;
    
    private void Start()
    {
        // Calculate max frames based on buffer time
        // Recording at ~50 fps (FixedUpdate rate)
        maxFrames = Mathf.CeilToInt(rewindBufferSeconds * 50f);
        
        // Initialize circular buffer array
        recordedFrames = new PhysicsSnapshot[maxFrames];
        currentFrameIndex = 0;
        recordedFrameCount = 0;
        
        // Auto-find rigidbody if not assigned
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
        
        // Auto-find colliders if not assigned
        if (collidersToDisable == null || collidersToDisable.Length == 0)
        {
            collidersToDisable = GetComponentsInChildren<Collider>();
        }
        
        if (targetRigidbody == null)
        {
// Debug.LogError("TimeRewindController requires a Rigidbody!");
            enabled = false;
        }
        
        // Set up audio source for rewind sound
        SetupRewindAudio();
    }
    
    private void SetupRewindAudio()
    {
        // Create AudioSource for rewind sound
        GameObject audioObj = new GameObject("Rewind Audio Source");
        audioObj.transform.SetParent(transform);
        audioObj.transform.localPosition = Vector3.zero;
        
        rewindAudioSource = audioObj.AddComponent<AudioSource>();
        rewindAudioSource.playOnAwake = false;
        rewindAudioSource.loop = false; // Important: Don't loop
        rewindAudioSource.volume = rewindSoundVolume;
        rewindAudioSource.spatialBlend = 0f; // 2D sound (not spatial)
        
        // Assign mixer group if specified
        if (rewindMixerGroup != null)
        {
            rewindAudioSource.outputAudioMixerGroup = rewindMixerGroup;
        }
    }
    
    private void FixedUpdate()
    {
        if (isRewinding)
        {
            // Rewind mode - play back recorded frames in reverse
            if (rewindIndex >= 0 && recordedFrameCount > 0)
            {
                PhysicsSnapshot snapshot = recordedFrames[rewindIndex];
                
                // Apply recorded state
                targetRigidbody.position = snapshot.position;
                targetRigidbody.rotation = snapshot.rotation;
                
                // Only set velocity if rigidbody is NOT kinematic
                if (!targetRigidbody.isKinematic)
                {
                    targetRigidbody.linearVelocity = -snapshot.velocity; // Reverse velocity for visual effect
                    targetRigidbody.angularVelocity = -snapshot.angularVelocity;
                }
                
                // Move backwards through recording
                rewindIndex--;
                
                // If we've reached the beginning, stop rewinding
                if (rewindIndex < 0)
                {
                    StopRewind();
                }
            }
            else
            {
                StopRewind();
            }
        }
        else
        {
            // Normal mode - record current state
            RecordFrame();
        }
    }
    
    private void RecordFrame()
    {
        PhysicsSnapshot snapshot = new PhysicsSnapshot
        {
            position = targetRigidbody.position,
            rotation = targetRigidbody.rotation,
            velocity = targetRigidbody.linearVelocity,
            angularVelocity = targetRigidbody.angularVelocity,
            timestamp = Time.time
        };
        
        // Circular buffer - overwrite oldest frame when full
        recordedFrames[currentFrameIndex] = snapshot;
        currentFrameIndex = (currentFrameIndex + 1) % maxFrames;
        
        // Track how many frames we've recorded (caps at maxFrames)
        if (recordedFrameCount < maxFrames)
        {
            recordedFrameCount++;
        }
    }
    
    public void StartRewind()
    {
        if (isRewinding || recordedFrameCount == 0)
            return;
        
        isRewinding = true;
        
        // Start from most recent frame (one before currentFrameIndex)
        rewindIndex = (currentFrameIndex - 1 + maxFrames) % maxFrames;
        
        // Store kinematic state and make kinematic during rewind
        wasKinematic = targetRigidbody.isKinematic;
        targetRigidbody.isKinematic = true;
        
        // Disable all collisions
        foreach (Collider col in collidersToDisable)
        {
            if (col != null)
                col.enabled = false;
        }
        
        // Play rewind sound
        PlayRewindSound();
    }
    
    private void PlayRewindSound()
    {
        if (rewindSound == null || rewindAudioSource == null)
            return;
        
        // Stop any currently playing sound first
        if (rewindAudioSource.isPlaying)
        {
            rewindAudioSource.Stop();
        }
        
        // Play the rewind sound (non-looping)
        rewindAudioSource.clip = rewindSound;
        rewindAudioSource.Play();
    }
    
    public void StopRewind()
    {
        if (!isRewinding)
            return;
        
        isRewinding = false;
        
        // Restore kinematic state
        targetRigidbody.isKinematic = wasKinematic;
        
        // Re-enable all collisions
        foreach (Collider col in collidersToDisable)
        {
            if (col != null)
                col.enabled = true;
        }
        
        // Reset recording from current position
        // Clear the buffer and start fresh to prevent "future" data
        if (rewindIndex >= 0)
        {
            currentFrameIndex = (rewindIndex + 1) % maxFrames;
            recordedFrameCount = 0; // Start fresh recording
        }
        
        rewindIndex = 0;
        
        // Stop rewind sound when button is released
        StopRewindSound();
    }
    
    private void StopRewindSound()
    {
        if (rewindAudioSource != null && rewindAudioSource.isPlaying)
        {
            rewindAudioSource.Stop();
        }
    }
    
    public bool IsRewinding => isRewinding;
    
    /// <summary>
    /// Refresh rigidbody and collider references after costume swap
    /// </summary>
    public void RefreshCostumeReferences()
    {
        // Find the active physical torso rigidbody
        var activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (activeRagdoll != null && activeRagdoll.PhysicalTorso != null)
        {
            targetRigidbody = activeRagdoll.PhysicalTorso;
        }
        else
        {
            // Fallback: find rigidbody on this GameObject
            targetRigidbody = GetComponent<Rigidbody>();
        }
        
        // Re-find all active colliders (only from active costume)
        collidersToDisable = GetComponentsInChildren<Collider>(false);
        
        // Reset recording buffer after costume change
        currentFrameIndex = 0;
        recordedFrameCount = 0;
        
        // Stop any active rewind
        if (isRewinding)
        {
            StopRewind();
        }
    }
    
    // Visual debug info
    private void OnGUI()
    {
        if (isRewinding)
        {
            GUI.color = Color.cyan;
            GUI.Label(new Rect(10, 10, 300, 30), $"<< REWINDING >> ({rewindIndex}/{recordedFrameCount} frames)");
        }
        else
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 300, 30), $"Recording: {recordedFrameCount}/{maxFrames} frames");
        }
    }
}

