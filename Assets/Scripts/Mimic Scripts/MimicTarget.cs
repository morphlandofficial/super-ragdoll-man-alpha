using UnityEngine;

namespace MimicSpace
{
    /// <summary>
    /// Simple marker component. Attach this to any GameObject you want the Mimic to detect and chase.
    /// Just place this on the player's torso or main body.
    /// </summary>
    public class MimicTarget : MonoBehaviour
    {
        private void Start()
        {
            // Verify this GameObject has colliders
            Collider[] colliders = GetComponents<Collider>();
            if (colliders.Length == 0)
            {
// Debug.LogWarning($"[MimicTarget] {gameObject.name} has MimicTarget but NO COLLIDERS! Add a collider for detection to work.", this);
            }
            else
            {
                string layerName = LayerMask.LayerToName(gameObject.layer);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.Append($"[MimicTarget] ✓ Initialized on '{gameObject.name}' | Layer: {layerName} | {colliders.Length} Collider(s): ");
                
                foreach (Collider col in colliders)
                {
                    sb.Append($"{col.GetType().Name}");
                    if (col is SphereCollider sphere)
                    {
                        sb.Append($"(R:{sphere.radius:F1}m)");
                    }
                    else if (col is BoxCollider box)
                    {
                        sb.Append($"({box.size.x:F2}x{box.size.y:F2}x{box.size.z:F2})");
                    }
                    sb.Append($"[Trigger:{col.isTrigger}], ");
                }
                
                
                // Show trigger sphere info if exists
                // SphereCollider triggerSphere = GetComponent<SphereCollider>();
                // if (triggerSphere != null && triggerSphere.isTrigger)
                // {
                // }
            }
        }

        // Draw gizmo so you can see where MimicTarget is in the scene
        private void OnDrawGizmos()
        {
            // Draw detection sphere (if it exists)
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f); // Semi-transparent magenta
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius * 0.5f);
            }
            else
            {
                // Fallback small sphere
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
            
            Gizmos.DrawIcon(transform.position, "sv_icon_dot0_pix16_gizmo", true);
        }
    }
}

