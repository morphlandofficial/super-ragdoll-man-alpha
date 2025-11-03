using UnityEngine;

/// <summary>
/// Attach to any body part GameObject to detect collisions and trigger impact sounds.
/// Communicates with the parent CharacterAudioController.
/// Filters out collisions with other body parts of the same character.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BodyPartCollisionTrigger : MonoBehaviour
{
    private CharacterAudioController audioController;
    private Rigidbody rb;
    private float lastImpactTime = -999f;
    private float impactCooldown = 0.15f;
    private Transform characterRoot;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        RefreshAudioController();
    }
    
    /// <summary>
    /// Manually refresh the audio controller reference (useful after costume swaps)
    /// </summary>
    public void RefreshAudioController()
    {
        // Find the CharacterAudioController in parent hierarchy
        audioController = GetComponentInParent<CharacterAudioController>();
        
        // if (audioController == null)
        // {
        //     Debug.LogWarning($"BodyPartCollisionTrigger on {gameObject.name}: No CharacterAudioController found in parent!");
        // }
        if (audioController != null)
        {
            characterRoot = audioController.transform;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (audioController == null)
            return;

        // Ignore collisions with other body parts of this character
        if (IsOwnBodyPart(collision.gameObject))
            return;

        // Check cooldown
        if (Time.time - lastImpactTime < impactCooldown)
            return;

        // Calculate impact velocity from this body part's rigidbody
        float impactVelocity = collision.relativeVelocity.magnitude;
        
        // Notify the audio controller to play impact sound
        audioController.PlayImpactSoundFromBodyPart(impactVelocity, collision.gameObject);
        
        lastImpactTime = Time.time;
    }

    private bool IsOwnBodyPart(GameObject other)
    {
        // Check if the colliding object is part of the same character hierarchy
        if (characterRoot == null)
            return false;
        
        // Check if it's a child of our character root
        Transform current = other.transform;
        while (current != null)
        {
            if (current == characterRoot)
                return true;
            current = current.parent;
        }
        
        return false;
    }
}

