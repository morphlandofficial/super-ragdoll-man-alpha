using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Gun pickup that enables shooting for the Default Character
/// Attach this to a gun GameObject with a Rigidbody
/// In multiplayer: Gun stays visible until ALL players have collected it
/// Each player can only collect the gun once (prevents infinite ammo farming)
/// When player respawns, the gun reappears and can be collected again
/// </summary>
public class GunPickup : MonoBehaviour
{
    [Header("Hand Item Selection")]
    [Tooltip("Reference to the item prefab to show in LEFT hand (drag from player's hand children). Leave null for no item.")]
    [SerializeField] private GameObject leftHandItemPrefab;
    
    [Tooltip("Reference to the item prefab to show in RIGHT hand (drag from player's hand children). Leave null for no item.")]
    [SerializeField] private GameObject rightHandItemPrefab;
    
    [Header("Pickup Settings")]
    [Tooltip("Speed at which the gun shrinks to zero when picked up")]
    [SerializeField] private float shrinkSpeed = 5f;
    
    [Tooltip("Optional: Custom sound for gun pickup (if null, uses default CharacterAudioController gun pickup sound)")]
    [SerializeField] private AudioClip pickupSound;
    
    [Header("Ammo Settings")]
    [Tooltip("Enable limited ammo (if false, infinite ammo)")]
    [SerializeField] private bool limitedAmmo = false;
    
    [Tooltip("Number of shots available when picked up (only used if Limited Ammo is enabled)")]
    [SerializeField] private int ammoCount = 30;
    
    [Header("Debug")]
    // [SerializeField] private bool showDebugLogs = true; // Unused after debug log cleanup
    
    // Track which players have collected this gun (by player ID)
    private HashSet<int> _playersWhoCollected = new HashSet<int>();
    private bool _hasBeenPickedUp = false; // True when ALL players have collected it
    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _isShrinking = false;
    private Collider _collider;
    private Rigidbody _rigidbody;
    private bool _isMultiplayerMode = false;
    
    // Static registry of all guns in the scene for respawn management
    private static List<GunPickup> _allGuns = new List<GunPickup>();
    
    private void Awake()
    {
        _originalScale = transform.localScale;
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        
        // Detect multiplayer mode (2+ controllers)
        _isMultiplayerMode = (Gamepad.all.Count >= 2);
        
        if (_isMultiplayerMode)
        {
            Debug.Log($"<color=cyan>[GunPickup]</color> {gameObject.name} - Multiplayer mode detected! Gun will stay visible until all players collect it.");
        }
        
        // Cache components
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
        
        // Validate collider
        if (_collider == null)
        {
            // Debug.LogError($"<color=red>[GunPickup]</color> {gameObject.name} needs a Collider component for trigger detection!");
        }
        else if (!_collider.isTrigger)
        {
            // Debug.LogWarning($"<color=yellow>[GunPickup]</color> {gameObject.name} collider should be set to 'Is Trigger'!");
        }
        else
        {
            if (false) // showDebugLogs
            {
            }
        }
        
        // Register this gun in the static list
        if (!_allGuns.Contains(this))
        {
            _allGuns.Add(this);
        }
    }
    
    private void OnDestroy()
    {
        // Unregister this gun when destroyed
        _allGuns.Remove(this);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenPickedUp) return; // Already fully collected (all players have it)
        
        if (false) // showDebugLogs
        {
        }
        
        // Check if this is the player (root GameObject)
        RespawnablePlayer player = other.GetComponent<RespawnablePlayer>();
        
        // If not found on the colliding object, check parent hierarchy
        // This handles triggers with the player's body parts (torso, limbs, etc.)
        if (player == null)
        {
            player = other.GetComponentInParent<RespawnablePlayer>();
        }
        
        if (player != null)
        {
            // Get player ID (default to 1 for single-player)
            int playerID = player.playerID > 0 ? player.playerID : 1;
            
            // Check if this specific player already collected this gun
            if (_playersWhoCollected.Contains(playerID))
            {
                Debug.Log($"<color=yellow>[GunPickup]</color> Player {playerID} already collected {gameObject.name} - ignoring (prevents infinite ammo farming)");
                return; // This player already got this gun - prevent infinite ammo farming
            }
            
            if (false) // showDebugLogs
            {
            }
            
            // Enable shooting on the player
            DefaultBehaviour playerBehaviour = player.GetComponent<DefaultBehaviour>();
            if (playerBehaviour != null)
            {
                // Show specified hand items by prefab name
                string leftItemName = leftHandItemPrefab != null ? leftHandItemPrefab.name : "None";
                string rightItemName = rightHandItemPrefab != null ? rightHandItemPrefab.name : "None";
                
                playerBehaviour.ShowSpecificHandItems(leftItemName, rightItemName);
                
                // Pass ammo settings to player
                if (limitedAmmo)
                {
                    playerBehaviour.EnableShooting(ammoCount);
                }
                else
                {
                    playerBehaviour.EnableShooting(); // Infinite ammo
                }
                
                // Play pickup sound on the player
                CharacterAudioController audioController = player.GetComponent<CharacterAudioController>();
                if (audioController != null)
                {
                    if (pickupSound != null)
                    {
                        audioController.PlayGunPickupSound(pickupSound);
                    }
                    else
                    {
                        audioController.PlayGunPickupSound();
                    }
                }
                
                // Mark this player as having collected this gun
                _playersWhoCollected.Add(playerID);
                Debug.Log($"<color=green>[GunPickup]</color> Player {playerID} collected {gameObject.name} ({_playersWhoCollected.Count} player(s) have collected it)");
                
                // Check if ALL players have collected this gun
                int totalPlayers = GetTotalActivePlayers();
                
                if (_playersWhoCollected.Count >= totalPlayers)
                {
                    // All players have collected this gun - now hide it permanently (until respawn)
                    _hasBeenPickedUp = true;
                    _isShrinking = true;
                    Debug.Log($"<color=cyan>[GunPickup]</color> ALL {totalPlayers} player(s) collected {gameObject.name} - hiding gun");
                }
                else
                {
                    Debug.Log($"<color=yellow>[GunPickup]</color> {gameObject.name} stays visible - waiting for {totalPlayers - _playersWhoCollected.Count} more player(s)");
                }
                
                if (false) // showDebugLogs
                {
                }
            }
            else
            {
                // Debug.LogError($"<color=red>[GunPickup]</color> Player has no DefaultBehaviour component!");
            }
        }
        else
        {
            if (false) // showDebugLogs
            {
            }
        }
    }
    
    /// <summary>
    /// Get the total number of active players in the scene
    /// </summary>
    private int GetTotalActivePlayers()
    {
        RespawnablePlayer[] allPlayers = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        return allPlayers.Length;
    }
    
    private void Update()
    {
        if (_isShrinking)
        {
            // Shrink the gun to zero scale
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, shrinkSpeed * Time.deltaTime);
            
            // Hide when very small (instead of destroying)
            if (transform.localScale.magnitude < 0.01f)
            {
                if (false) // showDebugLogs
                {
                }
                
                // Disable visual and physics instead of destroying
                _isShrinking = false;
                if (_collider != null) _collider.enabled = false;
                if (_rigidbody != null) _rigidbody.isKinematic = true;
                
                // Make completely invisible
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    renderer.enabled = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Reset the gun (for respawn or scene reset)
    /// Called automatically when any player respawns
    /// </summary>
    public void ResetGun()
    {
        _hasBeenPickedUp = false;
        _isShrinking = false;
        
        // Clear the collection tracking - all players can collect again
        _playersWhoCollected.Clear();
        
        // Restore original transform
        transform.localScale = _originalScale;
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
        
        // Re-enable collider and physics
        if (_collider != null) _collider.enabled = true;
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
        
        // Re-enable renderers
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = true;
        }
        
        gameObject.SetActive(true);
        
        Debug.Log($"<color=magenta>[GunPickup]</color> {gameObject.name} reset - all players can collect again");
        
        if (false) // showDebugLogs
        {
        }
    }
    
    /// <summary>
    /// Static method to reset all guns in the scene
    /// Called by RespawnablePlayer when respawning
    /// </summary>
    public static void ResetAllGuns()
    {
        
        // Create a copy of the list to avoid modification during iteration issues
        List<GunPickup> gunsCopy = new List<GunPickup>(_allGuns);
        
        foreach (GunPickup gun in gunsCopy)
        {
            if (gun != null)
            {
                gun.ResetGun();
            }
        }
    }
}

