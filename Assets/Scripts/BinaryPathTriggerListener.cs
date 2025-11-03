using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Trigger listener component that activates BinaryPathMovement components when player touches it.
/// This component is automatically added to trigger objects referenced by BinaryPathMovement.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BinaryPathTriggerListener : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when triggered (optional)")]
    private AudioClip triggerSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the trigger sound")]
    private float triggerSoundVolume = 0.8f;
    
    // [Header("Debug")]
    // [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    [Header("Info")]
    [SerializeField]
    [Tooltip("List of movements registered to this trigger (read-only)")]
    private int registeredMovementCount = 0;
    
    // Internal state
    private List<BinaryPathMovement> registeredMovements = new List<BinaryPathMovement>();
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Ensure rigidbody is kinematic (so the trigger doesn't fall)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true;
            // if (false) // showDebugMessages

        }
        
        // Setup audio source if trigger sound is assigned
        if (triggerSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.clip = triggerSound;
            audioSource.volume = triggerSoundVolume;
        }
    }
    
    /// <summary>
    /// Register a BinaryPathMovement component to be triggered when player touches this
    /// </summary>
    public void RegisterMovement(BinaryPathMovement movement)
    {
        if (!registeredMovements.Contains(movement))
        {
            registeredMovements.Add(movement);
            registeredMovementCount = registeredMovements.Count;
            // if (false) // showDebugMessages

        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
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
        
        // TRIGGER ALL REGISTERED MOVEMENTS!
        TriggerMovements();
    }
    
    private void OnCollisionExit(Collision collision)
    {
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
        
        // RELEASE ALL REGISTERED MOVEMENTS!
        ReleaseMovements();
    }
    
    private void TriggerMovements()
    {
        // Play audio
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        // Trigger all registered movement components
        int triggeredCount = 0;
        foreach (BinaryPathMovement movement in registeredMovements)
        {
            if (movement != null)
            {
                movement.OnTriggered();
                triggeredCount++;
                // if (false) // showDebugMessages

            }
        }
        
        if (false) // showDebugMessages
        {
        }
    }
    
    private void ReleaseMovements()
    {
        // Release all registered movement components (for hold modes)
        int releasedCount = 0;
        foreach (BinaryPathMovement movement in registeredMovements)
        {
            if (movement != null)
            {
                movement.OnTriggerReleased();
                releasedCount++;
                // if (false) // showDebugMessages

            }
        }
        
        if (false) // showDebugMessages
        {
        }
    }
}

