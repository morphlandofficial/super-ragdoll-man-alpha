using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines a boundary box for AI ragdolls.
/// Auto-discovers and manages AIs spawned inside the box.
/// Handles player detection to trigger AI awareness.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AIBoundaryBox : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Audio clip to play when player first enters the box (plays once)")]
    [SerializeField] private AudioClip playerDetectionSound;
    
    [Tooltip("Volume for the detection sound (0-1)")]
    [SerializeField] private float detectionSoundVolume = 0.7f;
    
    [Header("Gizmo Settings")]
    [Tooltip("Show the boundary box wireframe in the scene")]
    [SerializeField] private bool showGizmo = true;
    
    [Tooltip("Color of the boundary box gizmo")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.5f); // Orange
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    // Internal state
    private BoxCollider boxCollider;
    private HashSet<ActiveRagdoll.RagdollAIController> registeredAIs = new HashSet<ActiveRagdoll.RagdollAIController>();
    
    // MULTIPLAYER: Track ALL players inside box (not just first player)
    private HashSet<RespawnablePlayer> playersInBox = new HashSet<RespawnablePlayer>();
    private bool hasPlayedDetectionSound = false; // Track if we've played sound at least once
    
    private void Awake()
    {
        // Setup box collider as trigger
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        
        // Register AIs immediately in Awake to ensure it happens before AI Start() methods
        // This prevents AIs from detecting player before being registered with the box
        Invoke(nameof(DiscoverAndRegisterAIs), 0.1f); // Small delay to ensure AIs have spawned
    }
    
    private void Update()
    {
        // MULTIPLAYER: Continuously check if any players left the box (handles respawns/teleports)
        if (playersInBox.Count > 0)
        {
            // Create temporary list to avoid modifying collection during iteration
            List<RespawnablePlayer> playersToRemove = new List<RespawnablePlayer>();
            
            foreach (RespawnablePlayer player in playersInBox)
            {
                if (player == null || !player.gameObject.activeInHierarchy || !IsPlayerInsideBox(player.transform))
                {
                    playersToRemove.Add(player);
                }
            }
            
            // Remove players that left
            foreach (RespawnablePlayer player in playersToRemove)
            {
                playersInBox.Remove(player);
                OnPlayerExitedBox(player);
            }
        }
    }
    
    #region AI Registration
    
    /// <summary>
    /// Discover all AI ragdolls in the scene and register those inside this box
    /// </summary>
    private void DiscoverAndRegisterAIs()
    {
        // Find all AI controllers in the scene
        ActiveRagdoll.RagdollAIController[] allAIs = FindObjectsByType<ActiveRagdoll.RagdollAIController>(FindObjectsSortMode.None);
        
        int registeredCount = 0;
        foreach (var ai in allAIs)
        {
            // Check if AI's position is inside this box
            if (IsPositionInsideBox(ai.transform.position))
            {
                RegisterAI(ai);
                registeredCount++;
            }
        }
        
        if (showDebugLogs)
        {
            // Debug.Log($"[AIBoundaryBox] '{gameObject.name}' registered {registeredCount} AI ragdolls");
        }
    }
    
    /// <summary>
    /// Register an AI with this boundary box
    /// </summary>
    public void RegisterAI(ActiveRagdoll.RagdollAIController ai)
    {
        if (ai == null) return;
        
        if (registeredAIs.Add(ai))
        {
            // Tell the AI to use this box for boundary checks
            ai.SetBoundaryBox(this);
            
            if (showDebugLogs)
            {
                // Debug.Log($"[AIBoundaryBox] Registered AI: {ai.gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Unregister an AI from this boundary box
    /// </summary>
    public void UnregisterAI(ActiveRagdoll.RagdollAIController ai)
    {
        if (ai == null) return;
        
        if (registeredAIs.Remove(ai))
        {
            ai.SetBoundaryBox(null);
            
            if (showDebugLogs)
            {
                // Debug.Log($"[AIBoundaryBox] Unregistered AI: {ai.gameObject.name}");
            }
        }
    }
    
    #endregion
    
    #region Player Detection
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if this is an AI ragdoll
        ActiveRagdoll.RagdollAIController ai = other.GetComponentInParent<ActiveRagdoll.RagdollAIController>();
        if (ai != null)
        {
            // Auto-register AI when it enters the box
            RegisterAI(ai);
            return;
        }
        
        // MULTIPLAYER: Check if this is A player (not THE player)
        RespawnablePlayer player = other.GetComponentInParent<RespawnablePlayer>();
        if (player != null && !playersInBox.Contains(player))
        {
            // Add player to tracking set
            playersInBox.Add(player);
            OnPlayerEnteredBox(player);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Check if this is an AI ragdoll leaving the box
        ActiveRagdoll.RagdollAIController ai = other.GetComponentInParent<ActiveRagdoll.RagdollAIController>();
        if (ai != null)
        {
            // Optional: Unregister AI when it leaves (or keep it registered for boundary clamping)
            // For now, we'll keep them registered so they can't escape
            // UnregisterAI(ai);
            return;
        }
        
        // MULTIPLAYER: Check if this is a tracked player leaving
        RespawnablePlayer player = other.GetComponentInParent<RespawnablePlayer>();
        if (player != null && playersInBox.Contains(player))
        {
            playersInBox.Remove(player);
            OnPlayerExitedBox(player);
        }
    }
    
    /// <summary>
    /// MULTIPLAYER: Called when A player enters the box
    /// </summary>
    private void OnPlayerEnteredBox(RespawnablePlayer player)
    {
        // Play detection sound ONCE (only on first player entry)
        if (!hasPlayedDetectionSound && playerDetectionSound != null)
        {
            AudioSource.PlayClipAtPoint(playerDetectionSound, transform.position, detectionSoundVolume);
            hasPlayedDetectionSound = true;
        }
        
        // Notify all registered AIs that a player entered the box
        foreach (var ai in registeredAIs)
        {
            if (ai != null)
            {
                ai.OnPlayerEnteredBoundaryBox();
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=orange>[AIBoundaryBox]</color> {player.gameObject.name} entered box '{gameObject.name}' - {playersInBox.Count} player(s) inside, {registeredAIs.Count} AIs notified");
        }
    }
    
    /// <summary>
    /// MULTIPLAYER: Called when A player exits the box
    /// </summary>
    private void OnPlayerExitedBox(RespawnablePlayer player)
    {
        // Reset detection sound flag if ALL players have left
        if (playersInBox.Count == 0)
        {
            hasPlayedDetectionSound = false;
        }
        
        // Notify all registered AIs that a player left the box
        foreach (var ai in registeredAIs)
        {
            if (ai != null)
            {
                ai.OnPlayerExitedBoundaryBox();
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=orange>[AIBoundaryBox]</color> {player.gameObject.name} exited box '{gameObject.name}' - {playersInBox.Count} player(s) remaining");
        }
    }
    
    /// <summary>
    /// MULTIPLAYER: Get the closest player inside this box to the given AI position.
    /// Returns null if no players are in the box.
    /// Used by AI to commit to a target when entering detection range.
    /// </summary>
    public RespawnablePlayer GetClosestPlayerInBox(Vector3 aiPosition)
    {
        if (playersInBox.Count == 0)
        {
            return null;
        }
        
        if (playersInBox.Count == 1)
        {
            // Only one player - return them
            foreach (var player in playersInBox)
            {
                return player;
            }
        }
        
        // Multiple players - find closest
        RespawnablePlayer closestPlayer = null;
        float closestDistance = Mathf.Infinity;
        
        foreach (RespawnablePlayer player in playersInBox)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
                continue;
            
            float distance = Vector3.Distance(aiPosition, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }
        
        return closestPlayer;
    }
    
    /// <summary>
    /// Check if player is currently inside the box (handles manual checks)
    /// </summary>
    private bool IsPlayerInsideBox(Transform player)
    {
        if (player == null) return false;
        
        // Get player's torso position
        ActiveRagdoll.ActiveRagdoll playerRagdoll = player.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        Vector3 playerPos;
        
        if (playerRagdoll != null && playerRagdoll.PhysicalTorso != null)
        {
            playerPos = playerRagdoll.PhysicalTorso.position;
        }
        else
        {
            playerPos = player.position;
        }
        
        return IsPositionInsideBox(playerPos);
    }
    
    #endregion
    
    #region Boundary Checking
    
    /// <summary>
    /// Check if a position is inside this box
    /// </summary>
    public bool IsPositionInsideBox(Vector3 position)
    {
        // Transform position to local space of the box
        Vector3 localPos = transform.InverseTransformPoint(position);
        
        // Check if within box bounds
        Vector3 halfSize = boxCollider.size * 0.5f;
        
        return Mathf.Abs(localPos.x) <= halfSize.x &&
               Mathf.Abs(localPos.y) <= halfSize.y &&
               Mathf.Abs(localPos.z) <= halfSize.z;
    }
    
    /// <summary>
    /// Clamp a position to be inside the box
    /// </summary>
    public Vector3 ClampPositionToBox(Vector3 position)
    {
        // Transform to local space
        Vector3 localPos = transform.InverseTransformPoint(position);
        
        // Clamp to box bounds
        Vector3 halfSize = boxCollider.size * 0.5f;
        localPos.x = Mathf.Clamp(localPos.x, -halfSize.x, halfSize.x);
        localPos.y = Mathf.Clamp(localPos.y, -halfSize.y, halfSize.y);
        localPos.z = Mathf.Clamp(localPos.z, -halfSize.z, halfSize.z);
        
        // Transform back to world space
        return transform.TransformPoint(localPos);
    }
    
    /// <summary>
    /// Get the box bounds in world space
    /// </summary>
    public Bounds GetWorldBounds()
    {
        return new Bounds(transform.position, boxCollider.size);
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmos()
    {
        if (!showGizmo) return;
        
        // Get or create box collider reference
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) return;
        }
        
        // Draw wireframe box
        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        
        // Draw filled box (more transparent)
        Color fillColor = gizmoColor;
        fillColor.a *= 0.2f;
        Gizmos.color = fillColor;
        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        
        // Get or create box collider reference
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) return;
        }
        
        // Draw brighter wireframe when selected
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
    }
    
    #endregion
}

