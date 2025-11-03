using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to a TextMeshPro component to display ammo count from the player's gun.
/// MULTIPLAYER SUPPORT: Automatically finds the player associated with this UI's camera.
/// In split-screen, each camera should have its own canvas with this component.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class AmmoDisplayText : MonoBehaviour
{
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "Ammo: {0}";
    [Tooltip("Use {0} for the ammo count. Example: 'Ammo: {0}' or '{0} shots left'")]
    
    [Header("--- LOW AMMO WARNING ---")]
    [SerializeField] private bool enableLowAmmoWarning = true;
    [SerializeField] private int lowAmmoThreshold = 5;
    [Tooltip("Ammo count at which text turns to warning color")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowAmmoColor = Color.red;
    
    [Header("--- AUTO HIDE ---")]
    [SerializeField] private bool hideWhenInfiniteAmmo = true;
    [Tooltip("If true, hides the text when player has infinite ammo")]
    
    [Header("--- MULTIPLAYER ---")]
    [SerializeField] private bool autoDetectPlayerFromCamera = true;
    [Tooltip("If true, automatically finds the player that owns this UI's camera (for split-screen)")]
    
    private TMP_Text textComponent;
    private DefaultBehaviour playerBehaviour;
    private Color originalColor;
    private RectTransform rectTransform;
    private Camera associatedCamera;
    
    private void Awake()
    {
        // Get the text component on this GameObject
        textComponent = GetComponent<TMP_Text>();
        originalColor = textComponent.color;
        
        if (textComponent == null)
        {
            Debug.LogError("[AmmoDisplayText] No TextMeshPro component found!");
        }
        
        // Get RectTransform and position in bottom-right corner
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Set anchor to bottom-right
            rectTransform.anchorMin = new Vector2(1, 0); // Bottom-right
            rectTransform.anchorMax = new Vector2(1, 0); // Bottom-right
            rectTransform.pivot = new Vector2(1, 0); // Pivot at bottom-right
            
            // Position at bottom of stack (lowest position)
            rectTransform.anchoredPosition = new Vector2(-20, 20); // 20 pixels from right, 20 pixels from bottom
            
            // Set text alignment to right
            if (textComponent != null)
            {
                textComponent.alignment = TMPro.TextAlignmentOptions.BottomRight;
                textComponent.enableWordWrapping = false; // Prevent text wrapping
                textComponent.overflowMode = TMPro.TextOverflowModes.Overflow; // Allow overflow instead of wrapping
            }
        }
    }
    
    private void Update()
    {
        // Find player if not found yet (lazy loading)
        if (playerBehaviour == null)
        {
            FindPlayer();
            if (playerBehaviour == null)
            {
                // No player found - hide text
                if (textComponent != null)
                {
                    textComponent.enabled = false;
                }
                return;
            }
        }
        
        // Check if player has limited ammo
        bool hasLimitedAmmo = playerBehaviour.HasLimitedAmmo();
        
        // Hide/show based on ammo type
        if (hideWhenInfiniteAmmo && !hasLimitedAmmo)
        {
            // Infinite ammo - hide the display
            if (textComponent.enabled)
            {
                textComponent.enabled = false;
            }
            return;
        }
        
        // Show the display
        if (!textComponent.enabled)
        {
            textComponent.enabled = true;
        }
        
        // Get ammo count
        int ammoCount = playerBehaviour.GetCurrentAmmo();
        
        // Special case: -1 means infinite (show ∞ symbol)
        if (ammoCount == -1)
        {
            textComponent.text = "Ammo: ∞";
            textComponent.color = normalColor;
        }
        else
        {
            // Display ammo count
            textComponent.text = string.Format(displayFormat, ammoCount);
            
            // Apply color coding based on ammo level
            if (enableLowAmmoWarning && ammoCount <= lowAmmoThreshold)
            {
                textComponent.color = lowAmmoColor;
            }
            else
            {
                textComponent.color = normalColor;
            }
        }
    }
    
    /// <summary>
    /// Find the player's DefaultBehaviour component
    /// In multiplayer, finds the player associated with this UI's camera
    /// </summary>
    private void FindPlayer()
    {
        if (autoDetectPlayerFromCamera)
        {
            // Get the camera associated with this UI's canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                // Check if this canvas has a specific camera assigned
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
                {
                    associatedCamera = canvas.worldCamera;
                }
                else if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    // Overlay mode - try to find camera from hierarchy or use Camera.main
                    associatedCamera = GetCameraFromHierarchy();
                    if (associatedCamera == null)
                    {
                        associatedCamera = Camera.main;
                    }
                }
            }
            
            // Find player that owns this camera
            if (associatedCamera != null)
            {
                playerBehaviour = FindPlayerByCamera(associatedCamera);
                
                if (playerBehaviour != null)
                {
                    Debug.Log($"<color=cyan>[AmmoDisplay]</color> Found player for camera '{associatedCamera.name}'");
                    return;
                }
            }
        }
        
        // Fallback: Find first player (single-player mode or if camera detection failed)
        RespawnablePlayer respawnablePlayer = FindFirstObjectByType<RespawnablePlayer>();
        if (respawnablePlayer != null)
        {
            playerBehaviour = respawnablePlayer.GetComponent<DefaultBehaviour>();
            
            if (playerBehaviour == null)
            {
                Debug.LogWarning("[AmmoDisplayText] Found player but no DefaultBehaviour component!");
            }
        }
    }
    
    /// <summary>
    /// Try to find a camera in the hierarchy (for overlay canvases)
    /// </summary>
    private Camera GetCameraFromHierarchy()
    {
        // Check if this UI is a child of a camera
        Transform current = transform.parent;
        while (current != null)
        {
            Camera cam = current.GetComponent<Camera>();
            if (cam != null)
            {
                return cam;
            }
            current = current.parent;
        }
        return null;
    }
    
    /// <summary>
    /// Find the player that owns a specific camera
    /// </summary>
    private DefaultBehaviour FindPlayerByCamera(Camera targetCamera)
    {
        // Find all players in the scene
        RespawnablePlayer[] allPlayers = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        
        foreach (RespawnablePlayer player in allPlayers)
        {
            // Get the camera module from this player
            ActiveRagdoll.CameraModule cameraModule = player.GetComponent<ActiveRagdoll.CameraModule>();
            if (cameraModule != null && cameraModule.Camera != null)
            {
                Camera playerCamera = cameraModule.Camera.GetComponent<Camera>();
                
                // Check if this player's camera matches our target camera
                if (playerCamera == targetCamera)
                {
                    DefaultBehaviour behaviour = player.GetComponent<DefaultBehaviour>();
                    if (behaviour != null)
                    {
                        return behaviour;
                    }
                }
            }
        }
        
        return null;
    }
}

