using UnityEngine;

/// <summary>
/// Spawn point marker for multiplayer games.
/// Place 2-4 of these in your scene to define where each player spawns.
/// These are automatically used by MultiplayerManager.
/// </summary>
public class MultiplayerSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    [Tooltip("Which player uses this spawn point (0 = P1, 1 = P2, etc.)")]
    public int playerIndex = 0;
    
    [Header("Gizmo Settings")]
    public Color gizmoColor = Color.cyan;
    public float gizmoSize = 1f;
    
    private void OnDrawGizmos()
    {
        // Draw spawn point visualization
        Gizmos.color = gizmoColor;
        
        // Draw wireframe sphere
        Gizmos.DrawWireSphere(transform.position, gizmoSize * 0.5f);
        
        // Draw arrow showing forward direction
        Vector3 forward = transform.forward * gizmoSize;
        Gizmos.DrawLine(transform.position, transform.position + forward);
        Gizmos.DrawWireSphere(transform.position + forward, gizmoSize * 0.1f);
        
        // Draw player number above spawn point
        Vector3 labelPos = transform.position + Vector3.up * (gizmoSize + 0.5f);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"P{playerIndex + 1}", new GUIStyle()
        {
            normal = new GUIStyleState() { textColor = gizmoColor },
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        });
        #endif
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw more detailed gizmo when selected
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoSize * 0.3f);
        
        // Draw coordinate axes
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * gizmoSize);
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * gizmoSize);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoSize);
    }
}







