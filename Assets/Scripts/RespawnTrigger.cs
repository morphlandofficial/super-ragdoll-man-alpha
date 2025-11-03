using UnityEngine;

/// <summary>
/// Add this component to a GameObject with a trigger collider.
/// Anything with a RespawnablePlayer or RespawnableAIRagdoll component that touches this trigger will respawn.
/// Useful for kill zones, fall triggers, etc.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RespawnTrigger : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Sound played when player hits respawn trigger (2D)")]
    [SerializeField] private AudioClip respawnSound;
    
    [Tooltip("Volume for respawn sound (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;
    
    // Audio
    private AudioSource audioSource;
    
    // Static flag to disable all respawn triggers (e.g., when level is complete)
    private static bool _respawnsDisabled = false;
    
    /// <summary>
    /// Disable all respawn triggers globally
    /// </summary>
    public static void DisableAllRespawnTriggers()
    {
        _respawnsDisabled = true;
    }
    
    /// <summary>
    /// Re-enable all respawn triggers globally
    /// </summary>
    public static void EnableAllRespawnTriggers()
    {
        _respawnsDisabled = false;
    }
    
    private void Awake()
    {
        // Reset the static flag when the scene loads
        _respawnsDisabled = false;
        
        // Setup audio
        SetupAudio();
        
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            // Debug.LogWarning("RespawnTrigger: Collider on " + gameObject.name + " is not set as a trigger. Setting it now.");
            col.isTrigger = true;
        }
    }
    
    private void SetupAudio()
    {
        // ALWAYS create a NEW dedicated AudioSource for respawn sounds
        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configure exactly like jump sounds - simple 2D one-shot
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.volume = soundVolume;
    }

    private void OnTriggerEnter(Collider other)
    {
        // DIAGNOSTIC LOGGING
        
        // Don't respawn if globally disabled
        if (_respawnsDisabled)
        {
            return;
        }
        
        // Check for RespawnablePlayer component
        RespawnablePlayer player = other.GetComponent<RespawnablePlayer>();
        
        if (player == null)
        {
            player = other.GetComponentInParent<RespawnablePlayer>();
            
            if (player != null)
            {
            }
        }

        // Check for RespawnableAIRagdoll component
        RespawnableAIRagdoll aiRagdoll = other.GetComponent<RespawnableAIRagdoll>();
        
        if (aiRagdoll == null)
        {
            aiRagdoll = other.GetComponentInParent<RespawnableAIRagdoll>();
        }

        // Handle player respawn
        if (player != null)
        {
            // SPECIAL CASE: If this is a Mimic trigger, notify the Mimic to retreat first
            MimicSpace.Movement mimicMovement = GetComponentInParent<MimicSpace.Movement>();
            if (mimicMovement != null)
            {
                Vector3 killPosition = player.transform.position;
                
                // Trigger retreat BEFORE respawning
                mimicMovement.TriggerRetreat(killPosition);
                
                
                // Play Mimic-specific kill sound from the MimicRespawnTrigger (it has proper audio setup)
                MimicSpace.MimicRespawnTrigger mimicTrigger = GetComponentInParent<MimicSpace.MimicRespawnTrigger>();
                if (mimicTrigger != null)
                {
                    mimicTrigger.PlayMimicKillSound();
                }
                else
                {
                    // Debug.LogWarning($"<color=red>[RespawnTrigger]</color> Mimic kill but no MimicRespawnTrigger found!");
                }
            }
            else
            {
                // Play regular respawn sound (not a Mimic kill)
                PlayRespawnSound();
            }
            
            // Now respawn the player
            player.Respawn();
            return;
        }
        // Note: Removed warning for non-player/AI objects entering trigger - this is normal behavior
        // (dead ragdoll parts, environment objects, etc. can enter triggers harmlessly)
        
        // Handle AI ragdoll respawn
        if (aiRagdoll != null)
        {
            aiRagdoll.Respawn();
        }
    }

    // Draw a gizmo in the editor to visualize the trigger
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider boxCollider)
            {
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            }
            else if (col is SphereCollider sphereCollider)
            {
                Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
            }
            else if (col is CapsuleCollider capsuleCollider)
            {
                Gizmos.DrawSphere(capsuleCollider.center, capsuleCollider.radius);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (col is BoxCollider boxCollider)
            {
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            }
            else if (col is SphereCollider sphereCollider)
            {
                Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
            }
        }
    }
    
    // ==================== AUDIO METHODS ====================
    
    /// <summary>
    /// Play respawn sound (2D)
    /// </summary>
    private void PlayRespawnSound()
    {
        if (respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound, soundVolume);
        }
    }
}

