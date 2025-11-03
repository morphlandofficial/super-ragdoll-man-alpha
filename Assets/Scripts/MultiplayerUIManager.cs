using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Manages UI in multiplayer split-screen mode
/// Automatically duplicates Screen Space - Overlay canvases into per-camera canvases
/// Attach this to your global Canvas GameObject (the one in Level Systems 2 prefab)
/// 
/// HOW IT WORKS:
/// - Single-player: Uses the original overlay canvas (no changes)
/// - Multiplayer: Creates a separate canvas for each player camera with copied UI elements
/// </summary>
public class MultiplayerUIManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The canvas to duplicate for each player (usually 'this' canvas)")]
    [SerializeField] private Canvas sourceCanvas;
    
    [Tooltip("If true, hides the original canvas in multiplayer (only show per-player canvases)")]
    [SerializeField] private bool hideOriginalInMultiplayer = true;
    
    [Header("Global UI Exclusions")]
    [Tooltip("UI elements with these names will NOT be duplicated per-player (remain global and centered)")]
    [SerializeField] private string[] globalUINames = new string[] { "PausePanel", "Pause Panel", "LevelFinishUI", "Level Finish UI", "Final Score Text", "FinalScoreText" };
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool isMultiplayerMode = false;
    private List<Canvas> playerCanvases = new List<Canvas>();
    private bool hasSetupMultiplayer = false;
    private HashSet<int> trackedPlayerIDs = new HashSet<int>(); // Track which players we've created canvases for
    private Dictionary<int, Canvas> playerIDToCanvas = new Dictionary<int, Canvas>(); // Map player ID to their canvas
    private HashSet<GameObject> globalUIElements = new HashSet<GameObject>(); // Track global UI elements to keep visible
    private List<GameObject> hiddenUIElements = new List<GameObject>(); // Track UI elements we hid (to restore on cleanup)
    
    private void Awake()
    {
        // Auto-assign source canvas if not set
        if (sourceCanvas == null)
        {
            sourceCanvas = GetComponent<Canvas>();
        }
        
        // Detect multiplayer mode (2+ controllers)
        isMultiplayerMode = (Gamepad.all.Count >= 2);
        
        if (isMultiplayerMode)
        {
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[MultiplayerUI]</color> Multiplayer detected - will create per-player UI canvases");
            }
        }
    }
    
    private void Start()
    {
        if (isMultiplayerMode)
        {
            // Delay setup slightly to ensure players are spawned
            Invoke(nameof(SetupMultiplayerUI), 0.5f);
        }
    }
    
    private void Update()
    {
        // Initial setup - create UI for Player 1 when they spawn
        if (isMultiplayerMode && !hasSetupMultiplayer)
        {
            // Check if we have Player 1
            RespawnablePlayer[] players = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
            if (players.Length > 0)
            {
                SetupMultiplayerUI();
            }
        }
    }
    
    /// <summary>
    /// Called by MultiplayerManagerSimple when a new player joins OR respawns
    /// Creates a UI canvas for that specific player, or updates camera reference if canvas exists
    /// </summary>
    public void OnPlayerJoined(GameObject playerObject, int playerID)
    {
        Debug.Log($"<color=magenta>[MultiplayerUI]</color> OnPlayerJoined called for Player {playerID}");
        
        if (!isMultiplayerMode)
        {
            Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Not in multiplayer mode - ignoring OnPlayerJoined for Player {playerID}");
            return; // Not in multiplayer, ignore
        }
        
        // Check if we already have a canvas for this player
        if (trackedPlayerIDs.Contains(playerID))
        {
            // Player already has a canvas - just update the camera reference (for respawns)
            UpdateCanvasCamera(playerObject, playerID);
            if (showDebugLogs)
            {
                Debug.Log($"<color=yellow>[MultiplayerUI]</color> Player {playerID} already has UI canvas - updated camera reference");
            }
            return;
        }
        
        Debug.Log($"<color=cyan>[MultiplayerUI]</color> Creating UI canvas for Player {playerID}...");
        
        // Get player's camera
        ActiveRagdoll.CameraModule cameraModule = playerObject.GetComponent<ActiveRagdoll.CameraModule>();
        if (cameraModule == null || cameraModule.Camera == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {playerID} has no camera - skipping UI creation");
            }
            return;
        }
        
        Camera playerCamera = cameraModule.Camera.GetComponent<Camera>();
        if (playerCamera == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {playerID} camera has no Camera component - skipping");
            }
            return;
        }
        
        // Create canvas for this player
        CreatePlayerCanvas(playerCamera, playerID);
        
        // NOTE: We DON'T disable the source canvas anymore
        // Non-global UI elements are hidden individually in CopyUIElements()
        // Global UI elements (pause menu, level finish) stay visible on source canvas
        if (showDebugLogs && hideOriginalInMultiplayer)
        {
            Debug.Log("<color=cyan>[MultiplayerUI]</color> Using per-player canvases (global UI remains on source canvas)");
        }
    }
    
    /// <summary>
    /// Create per-camera canvases for each player in multiplayer
    /// </summary>
    private void SetupMultiplayerUI()
    {
        if (hasSetupMultiplayer) return;
        
        // Find all players
        RespawnablePlayer[] players = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        
        if (players.Length == 0)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("<color=yellow>[MultiplayerUI]</color> No players found yet - will retry");
            }
            return;
        }
        
        hasSetupMultiplayer = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>[MultiplayerUI]</color> Setting up UI for {players.Length} player(s)");
        }
        
        // Create a canvas for each player
        foreach (RespawnablePlayer player in players)
        {
            // Get player's camera
            ActiveRagdoll.CameraModule cameraModule = player.GetComponent<ActiveRagdoll.CameraModule>();
            if (cameraModule == null || cameraModule.Camera == null)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {player.playerID} has no camera - skipping");
                }
                continue;
            }
            
            Camera playerCamera = cameraModule.Camera.GetComponent<Camera>();
            if (playerCamera == null)
            {
                if (showDebugLogs)
                {
                    Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {player.playerID} camera has no Camera component - skipping");
                }
                continue;
            }
            
            // Create a new canvas for this player
            CreatePlayerCanvas(playerCamera, player.playerID);
        }
        
        // NOTE: We DON'T disable the source canvas anymore
        // Non-global UI elements are hidden individually in CopyUIElements()
        // Global UI elements (pause menu, level finish) stay visible on source canvas
        if (showDebugLogs && hideOriginalInMultiplayer)
        {
            Debug.Log("<color=cyan>[MultiplayerUI]</color> Using per-player canvases (global UI remains on source canvas)");
        }
    }
    
    /// <summary>
    /// Create a canvas for a specific player's camera
    /// </summary>
    private void CreatePlayerCanvas(Camera playerCamera, int playerID)
    {
        // Create a new GameObject for the canvas (NOT parented to camera - it needs to persist across respawns!)
        GameObject canvasObj = new GameObject($"PlayerUI_P{playerID}");
        // DON'T parent to camera - that would destroy the UI when the player respawns!
        
        // Add Canvas component
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = playerCamera;
        canvas.planeDistance = 0.5f; // Close to camera
        canvas.sortingOrder = 100; // High priority
        
        // Add CanvasScaler (copy settings from original)
        UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        if (sourceCanvas != null)
        {
            UnityEngine.UI.CanvasScaler sourceScaler = sourceCanvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (sourceScaler != null)
            {
                scaler.uiScaleMode = sourceScaler.uiScaleMode;
                scaler.referenceResolution = sourceScaler.referenceResolution;
                scaler.screenMatchMode = sourceScaler.screenMatchMode;
                scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            }
        }
        
        // Add GraphicRaycaster (for UI interactions)
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        
        // Copy all UI elements from source canvas
        if (sourceCanvas != null)
        {
            CopyUIElements(sourceCanvas.transform, canvasObj.transform);
        }
        
        playerCanvases.Add(canvas);
        trackedPlayerIDs.Add(playerID); // Mark this player as tracked
        playerIDToCanvas[playerID] = canvas; // Map player ID to canvas
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>[MultiplayerUI]</color> ✅ Created UI canvas for Player {playerID} on camera '{playerCamera.name}'");
        }
    }
    
    /// <summary>
    /// Update the camera reference for an existing player canvas (called on respawn)
    /// </summary>
    private void UpdateCanvasCamera(GameObject playerObject, int playerID)
    {
        // Find the canvas for this player
        if (!playerIDToCanvas.ContainsKey(playerID))
        {
            Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> No canvas found for Player {playerID} - cannot update camera");
            return;
        }
        
        Canvas canvas = playerIDToCanvas[playerID];
        if (canvas == null)
        {
            Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Canvas for Player {playerID} was destroyed!");
            return;
        }
        
        // Get the new camera from the respawned player
        ActiveRagdoll.CameraModule cameraModule = playerObject.GetComponent<ActiveRagdoll.CameraModule>();
        if (cameraModule == null || cameraModule.Camera == null)
        {
            Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {playerID} has no camera after respawn!");
            return;
        }
        
        Camera playerCamera = cameraModule.Camera.GetComponent<Camera>();
        if (playerCamera == null)
        {
            Debug.LogWarning($"<color=yellow>[MultiplayerUI]</color> Player {playerID} camera has no Camera component!");
            return;
        }
        
        // Update the canvas's camera reference
        canvas.worldCamera = playerCamera;
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>[MultiplayerUI]</color> Updated canvas camera for Player {playerID} to '{playerCamera.name}'");
        }
    }
    
    /// <summary>
    /// Copy UI elements from source to target (recursively)
    /// EXCLUDES global UI elements (pause menu, level finish) to keep them centered
    /// </summary>
    private void CopyUIElements(Transform source, Transform target)
    {
        foreach (Transform child in source)
        {
            // MULTIPLAYER: Skip global UI elements (pause, level finish, etc.)
            if (IsGlobalUIElement(child.gameObject))
            {
                // Track this as a global element and keep it on source canvas
                globalUIElements.Add(child.gameObject);
                
                if (showDebugLogs)
                {
                    Debug.Log($"<color=magenta>[MultiplayerUI]</color> Skipping '{child.name}' - marked as global UI (will remain centered)");
                }
                continue;
            }
            
            // Duplicate the child
            GameObject copy = Instantiate(child.gameObject, target);
            copy.name = child.name; // Remove (Clone) suffix
            
            // Make sure RectTransform is properly set
            RectTransform sourceRect = child.GetComponent<RectTransform>();
            RectTransform copyRect = copy.GetComponent<RectTransform>();
            
            if (sourceRect != null && copyRect != null)
            {
                // Copy all RectTransform properties
                copyRect.anchorMin = sourceRect.anchorMin;
                copyRect.anchorMax = sourceRect.anchorMax;
                copyRect.anchoredPosition = sourceRect.anchoredPosition;
                copyRect.sizeDelta = sourceRect.sizeDelta;
                copyRect.pivot = sourceRect.pivot;
                copyRect.localScale = sourceRect.localScale;
                copyRect.localRotation = sourceRect.localRotation;
            }
            
            // MULTIPLAYER: Hide the original element (since it's now duplicated per-player)
            // This prevents duplicate UI rendering
            child.gameObject.SetActive(false);
            hiddenUIElements.Add(child.gameObject); // Track for cleanup
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[MultiplayerUI]</color> Copied and hid original '{child.name}' (now per-player)");
            }
        }
    }
    
    /// <summary>
    /// Check if a UI element should be kept global (not duplicated per-player)
    /// </summary>
    private bool IsGlobalUIElement(GameObject uiElement)
    {
        if (uiElement == null || globalUINames == null || globalUINames.Length == 0)
            return false;
        
        string elementName = uiElement.name;
        
        foreach (string globalName in globalUINames)
        {
            // Case-insensitive comparison, ignoring spaces
            if (elementName.Replace(" ", "").Equals(globalName.Replace(" ", ""), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Clean up player canvases when this object is destroyed
    /// </summary>
    private void OnDestroy()
    {
        // Restore hidden UI elements (in case we're switching scenes or restarting)
        foreach (GameObject hiddenElement in hiddenUIElements)
        {
            if (hiddenElement != null)
            {
                hiddenElement.SetActive(true);
            }
        }
        hiddenUIElements.Clear();
        
        // Destroy per-player canvases
        foreach (Canvas canvas in playerCanvases)
        {
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }
        playerCanvases.Clear();
    }
}
