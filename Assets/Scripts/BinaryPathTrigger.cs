using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple trigger component that activates BinaryPathMovement components when player touches it.
/// This component is automatically added to trigger objects referenced by BinaryPathMovement.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BinaryPathTrigger : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Only trigger once")]
    [SerializeField] private bool oneShot = true;
    
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when triggered (optional)")]
    private AudioClip triggerSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the trigger sound")]
    private float triggerSoundVolume = 0.8f;
    
    [Header("Debug")]
    [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    // Internal state
    private List<BinaryPathMovement> registeredMovements = new List<BinaryPathMovement>();
    private bool hasBeenTriggered = false;
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
    /// Register a BinaryPathMovement component to be activated when this trigger is touched
    /// </summary>
    public void RegisterMovement(BinaryPathMovement movement)
    {
        if (!registeredMovements.Contains(movement))
        {
            registeredMovements.Add(movement);
            // if (false) // showDebugMessages

        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Only trigger once if one-shot enabled
        if (oneShot && hasBeenTriggered)
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
        
        // TRIGGER IT!
        ActivateMovements();
        hasBeenTriggered = true;
    }
    
    private void ActivateMovements()
    {
        // Play audio
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        // Enable all registered movement components
        foreach (BinaryPathMovement movement in registeredMovements)
        {
            if (movement != null)
            {
                movement.enabled = true;
                // if (false) // showDebugMessages

            }
        }
        
        if (false) // showDebugMessages
        {
        }
    }
    
    /// <summary>
    /// Manually reset the trigger to allow it to be triggered again
    /// </summary>
    public void ResetTrigger()
    {
        hasBeenTriggered = false;
    }
}

