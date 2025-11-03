using UnityEngine;

/// <summary>
/// Add this component to a GameObject with a trigger collider.
/// Anything with a RespawnableAIRagdoll component that touches this trigger will respawn.
/// This is separate from player respawn triggers.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AIRespawnTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("Also affect player characters (uses RespawnablePlayer component)")]
    public bool alsoAffectPlayers = true;
    
    private void Awake()
    {
        // Ensure the collider is set to trigger
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check for AI ragdoll
        RespawnableAIRagdoll aiRagdoll = other.GetComponent<RespawnableAIRagdoll>();

        // If no component found, try to find it in parent
        if (aiRagdoll == null)
        {
            aiRagdoll = other.GetComponentInParent<RespawnableAIRagdoll>();
        }

        // If we found an AI ragdoll, respawn it
        if (aiRagdoll != null)
        {
            aiRagdoll.Respawn();
            return; // Don't check for player if we already handled AI
        }
        
        // Optionally also handle player respawns
        if (alsoAffectPlayers)
        {
            RespawnablePlayer player = other.GetComponent<RespawnablePlayer>();

            if (player == null)
            {
                player = other.GetComponentInParent<RespawnablePlayer>();
            }

            if (player != null)
            {
                player.Respawn();
            }
        }
    }

    // Draw a gizmo in the editor to visualize the trigger (cyan for AI)
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Cyan with transparency
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
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f); // Brighter cyan
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
}


