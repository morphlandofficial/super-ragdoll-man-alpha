using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the in-game pause menu.
/// Press P (keyboard) or Start (gamepad) to pause/unpause.
/// When paused, press Enter (keyboard) or Button South (gamepad) to return to title screen.
/// 
/// IMPORTANT: This only works with DefaultBehaviour, not TitlePlayerDefaultBehavior.
/// Add this component to a GameObject in your level scene (can be on the UI Canvas).
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [Header("--- UI REFERENCES ---")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("The UI panel that contains the pause menu (will be shown/hidden)")]
    
    [SerializeField] private TMP_Text pauseText;
    [Tooltip("Text component to display pause instructions. Leave empty to auto-find.")]
    
    [Header("--- PAUSE TEXT ---")]
    [SerializeField] private string pauseMessage = "PAUSED\n\nPress ENTER (Keyboard) or BUTTON SOUTH (Controller) to return to Title Screen\n\nPress P (Keyboard) or START (Controller) to Resume";
    
    [Header("--- STATE ---")]
    [SerializeField] private bool isPaused = false;
    
    // MULTIPLAYER: Track which player paused the game
    private bool isMultiplayerMode = false;
    private Gamepad pausingPlayerGamepad = null; // The gamepad of the player who paused
    
    private void Start()
    {
        // MULTIPLAYER: Detect multiplayer mode
        isMultiplayerMode = (Gamepad.all.Count >= 2);
        
        // Auto-find pause text if not assigned
        if (pauseText == null && pausePanel != null)
        {
            pauseText = pausePanel.GetComponentInChildren<TMP_Text>();
        }
        
        // Validate references
        if (pausePanel == null)
        {
// Debug.LogError("PauseMenuManager: Pause Panel reference is NULL! Please assign it in the Inspector.");
        }
        
        if (pauseText == null)
        {
// Debug.LogWarning("PauseMenuManager: Pause Text is NULL! Auto-find failed.");
        }
        else
        {
            // Force center alignment (horizontal and vertical)
            pauseText.alignment = TextAlignmentOptions.Center;
            
            // Position the text in the center of the screen
            RectTransform textRect = pauseText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                // Set anchors to center of screen
                textRect.anchorMin = new Vector2(0.5f, 0.5f);
                textRect.anchorMax = new Vector2(0.5f, 0.5f);
                
                // Set pivot to center of the text box
                textRect.pivot = new Vector2(0.5f, 0.5f);
                
                // Position at center (0,0 relative to center anchor)
                textRect.anchoredPosition = Vector2.zero;
                
                // Set a reasonable size for the text box (covers most of screen with padding)
                textRect.sizeDelta = new Vector2(800f, 400f);
            }
        }
        
        // Ensure game starts unpaused
        UnpauseGame();
    }
    
    private void Update()
    {
        // IMPORTANT: Check input every frame regardless of Time.timeScale
        // We use unscaled time so input works even when paused
        
        // Check if Keyboard is available
        if (Keyboard.current == null)
        {
            return;
        }
        
        // Check for pause toggle input (P key or Start button)
        bool pauseTogglePressed = CheckPauseToggleInput();
        
        if (pauseTogglePressed)
        {
            if (isPaused)
            {
                UnpauseGame();
            }
            else
            {
                PauseGame();
            }
        }
        
        // When paused, check for title screen input
        if (isPaused)
        {
            bool titleScreenPressed = CheckTitleScreenInput();
            
            if (titleScreenPressed)
            {
                GoToTitleScreen();
            }
        }
    }
    
    /// <summary>
    /// MULTIPLAYER: Check if pause toggle input was pressed (P or Start button)
    /// Tracks WHICH gamepad paused so only that player can control the menu
    /// </summary>
    private bool CheckPauseToggleInput()
    {
        bool pressed = false;
        
        // Keyboard: P key (always allowed)
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            pressed = true;
            pausingPlayerGamepad = null; // Keyboard user paused
        }
        
        // MULTIPLAYER: Check each gamepad individually to track who paused
        if (isMultiplayerMode && Gamepad.all.Count >= 2)
        {
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad != null && pad.startButton.wasPressedThisFrame)
                {
                    pressed = true;
                    pausingPlayerGamepad = pad; // Remember which gamepad paused
                    Debug.Log($"<color=yellow>[PauseMenu]</color> Player with gamepad '{pad.name}' paused the game");
                    break;
                }
            }
        }
        else
        {
            // Single player: Use Gamepad.current
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                pressed = true;
                pausingPlayerGamepad = Gamepad.current;
            }
        }
        
        return pressed;
    }
    
    /// <summary>
    /// MULTIPLAYER: Check if title screen input was pressed (Enter or Button South)
    /// ONLY responds to the player who paused the game
    /// </summary>
    private bool CheckTitleScreenInput()
    {
        bool pressed = false;
        
        // Keyboard: Enter key (only if keyboard user paused OR single player)
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
        {
            if (!isMultiplayerMode || pausingPlayerGamepad == null)
            {
                // Single player OR keyboard user paused
                pressed = true;
            }
        }
        
        // MULTIPLAYER: Only check the SPECIFIC gamepad that paused
        if (isMultiplayerMode && pausingPlayerGamepad != null)
        {
            // Only the pausing player's gamepad can control the menu
            if (pausingPlayerGamepad.buttonSouth.wasPressedThisFrame)
            {
                pressed = true;
                Debug.Log($"<color=yellow>[PauseMenu]</color> Pausing player pressed ButtonSouth - returning to title");
            }
        }
        else if (!isMultiplayerMode)
        {
            // Single player: Use Gamepad.current
            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                pressed = true;
            }
        }
        
        return pressed;
    }
    
    /// <summary>
    /// Pause the game - freezes physics, time, points, everything
    /// </summary>
    private void PauseGame()
    {
        isPaused = true;
        
        // Freeze time - this pauses physics, animations, and all time-based systems
        Time.timeScale = 0f;
        
        // Show pause UI
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        
        if (pauseText != null)
        {
            pauseText.text = pauseMessage;
        }
    }
    
    /// <summary>
    /// Unpause the game - resumes everything exactly as it was
    /// </summary>
    private void UnpauseGame()
    {
        isPaused = false;
        
        // Restore normal time - everything resumes
        Time.timeScale = 1f;
        
        // Hide pause UI
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        
        // MULTIPLAYER: Reset pausing player tracking
        pausingPlayerGamepad = null;
    }
    
    /// <summary>
    /// Load the title screen scene
    /// </summary>
    private void GoToTitleScreen()
    {
        // Ensure time is restored before loading scene
        Time.timeScale = 1f;
        
        SceneManager.LoadScene("_Title Screen");
    }
    
    /// <summary>
    /// Public method to check if game is currently paused
    /// </summary>
    public bool IsPaused => isPaused;
    
    /// <summary>
    /// Disable the pause menu (called when level is completed)
    /// </summary>
    public void DisablePauseMenu()
    {
        // Unpause if currently paused
        if (isPaused)
        {
            UnpauseGame();
        }
        
        // Disable this component so Update() stops running
        enabled = false;
    }
}

