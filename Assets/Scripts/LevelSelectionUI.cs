using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class LevelSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private GameObject levelSelectionPanel;
    [SerializeField] private Transform levelButtonsParent;
    [SerializeField] private Button exitButton; // Used as template for level buttons
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TextMeshProUGUI interactionPromptText;
    
    [Header("Settings")]
    [SerializeField] private string npcDialogue = "What event are you here to register for?";
    [SerializeField] private float hoverScaleMultiplier = 1.2f;
    [SerializeField] private float scaleAnimationSpeed = 5f;
    
    // Events
    public event Action<int> OnLevelSelected;
    public event Action OnDialogueClosed;
    
    private Button[] levelButtons;
    private Button[] allSelectableButtons; // Includes level buttons + exit button
    private int currentSelectedIndex = 0;
    private bool isDialogueActive = false;
    private ActiveRagdollActions inputActions;
    private Vector3[] originalButtonScales;
    private Vector3 exitButtonOriginalScale;
    private bool buttonsCreated = false; // Track if buttons have been created
    private Gamepad player1Gamepad = null; // Only Player 1's gamepad can control this UI
    
    // For digital navigation from analog stick
    private float navigationCooldown = 0f;
    private const float NAVIGATION_COOLDOWN_TIME = 0.2f; // Time between navigation inputs
    private Vector2 lastNavigationInput = Vector2.zero;
    private const float NAVIGATION_THRESHOLD = 0.5f; // Stick must be pushed this far to count
    
    void Awake()
    {
        inputActions = new ActiveRagdollActions();
    }
    
    void Start()
    {
        // Initialize UI state
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (interactionPrompt != null) interactionPrompt.SetActive(false);
        
        // Set dialogue text
        if (dialogueText != null)
            dialogueText.text = npcDialogue;
        
        // Setup exit button
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(CloseDialogue);
            
            // Fix exit button size and text wrapping
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            if (exitRect != null)
            {
                exitRect.anchorMin = new Vector2(0, 0);
                exitRect.anchorMax = new Vector2(1, 0);
                exitRect.sizeDelta = new Vector2(0, 60);
                exitRect.anchoredPosition = Vector2.zero;
            }
            
            TextMeshProUGUI exitText = exitButton.GetComponentInChildren<TextMeshProUGUI>();
            if (exitText != null)
            {
                exitText.text = "Exit";
                exitText.enabled = true; // ENABLE the text component!
                exitText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            }
            
            // Exit button visibility is controlled by the panel being active/inactive
            // So we don't need to deactivate it here
        }
        
        // Set interaction prompt text
        if (interactionPromptText != null)
        {
            interactionPromptText.text = "Press X/Enter to interact";
        }
    }
    
    void OnEnable()
    {
        // Don't enable inputs here - only when dialogue is active
    }
    
    void OnDisable()
    {
        // Make sure to disable if dialogue was active
        if (isDialogueActive)
        {
            DisableDialogueInputs();
        }
    }
    
    void EnableDialogueInputs()
    {
        inputActions.Enable();
        inputActions.UI.Navigate.performed += OnNavigate;
        inputActions.UI.Interact.performed += OnInteract;
        inputActions.UI.Submit.performed += OnInteract; // Enter/Start also selects
        inputActions.UI.Cancel.performed += OnCancel;
    }
    
    void DisableDialogueInputs()
    {
        inputActions.UI.Navigate.performed -= OnNavigate;
        inputActions.UI.Interact.performed -= OnInteract;
        inputActions.UI.Submit.performed -= OnInteract;
        inputActions.UI.Cancel.performed -= OnCancel;
        inputActions.Disable();
    }
    
    public void ShowInteractionPrompt(bool show)
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(show);
    }
    
    public void ShowLevelSelection(string[] levelNames)
    {
        isDialogueActive = true;
        
        // MULTIPLAYER FIX: Find Player 1 and get their specific gamepad
        DetectPlayer1Gamepad();
        
        // Enable input handling for dialogue
        EnableDialogueInputs();
        
        // Show dialogue panel first
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
        else
        {
            // Debug.LogError("Dialogue panel is null!");
        }
        
        // Create level buttons only if they haven't been created yet
        if (!buttonsCreated)
        {
            CreateLevelButtons(levelNames);
            buttonsCreated = true;
        }
        else
        {
            // Just activate existing buttons
            ActivateButtons();
        }
        
        // Show level selection after a brief delay
        StartCoroutine(ShowLevelSelectionDelayed());
    }
    
    void DetectPlayer1Gamepad()
    {
        // Find Player 1 in the scene
        RespawnablePlayer[] players = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        
        foreach (RespawnablePlayer player in players)
        {
            // Player 1 always has playerID == 1 (not 0!)
            if (player.playerID == 1)
            {
                // Try to get their assigned gamepad from MultiplayerGamepadController
                MultiplayerGamepadController controller = player.GetComponent<MultiplayerGamepadController>();
                if (controller != null && controller.assignedGamepad != null)
                {
                    player1Gamepad = controller.assignedGamepad;
                    Debug.Log($"<color=cyan>[LevelSelectionUI]</color> Locked UI to Player 1's gamepad: {player1Gamepad.name}");
                    return;
                }
            }
        }
        
        // Fallback: If Player 1 not found or doesn't have assigned gamepad, use first gamepad
        if (Gamepad.all.Count > 0)
        {
            player1Gamepad = Gamepad.all[0];
            Debug.Log($"<color=yellow>[LevelSelectionUI]</color> Player 1 gamepad not found, using first available: {player1Gamepad.name}");
        }
        else
        {
            Debug.LogWarning("<color=red>[LevelSelectionUI]</color> No gamepads found! UI will only respond to keyboard.");
        }
    }
    
    IEnumerator ShowLevelSelectionDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        
        if (levelSelectionPanel != null)
        {
            levelSelectionPanel.SetActive(true);
            
            // Force layout rebuild to make buttons appear
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(levelButtonsParent as RectTransform);
        }
        else
        {
            // Debug.LogError("Level selection panel is null!");
        }
        
        // Initialize selection
        if (levelButtons != null && levelButtons.Length > 0)
        {
            currentSelectedIndex = 0;
            UpdateButtonSelection();
        }
        else
        {
            // Debug.LogError("No level buttons were created!");
        }
    }
    
    void ActivateButtons()
    {
        // Just rebuild the selectable buttons array and reset selection
        // Buttons are always active, visibility is controlled by parent panels
        if (levelButtons != null && levelButtons.Length > 0)
        {
            // Rebuild selectable buttons array
            allSelectableButtons = new Button[levelButtons.Length + 1];
            for (int i = 0; i < levelButtons.Length; i++)
            {
                allSelectableButtons[i] = levelButtons[i];
            }
            allSelectableButtons[levelButtons.Length] = exitButton;
            
            // Reset selection
            currentSelectedIndex = 0;
            UpdateButtonSelection();
            
        }
    }
    
    void CreateLevelButtons(string[] levelNames)
    {
        
        if (levelNames == null || levelNames.Length == 0)
        {
            // Debug.LogWarning("No level names provided to CreateLevelButtons!");
            return;
        }
        
        if (exitButton == null)
        {
            // Debug.LogError("Exit button is null! Cannot create level buttons.");
            return;
        }
        
        // Create new buttons
        levelButtons = new Button[levelNames.Length];
        originalButtonScales = new Vector3[levelNames.Length];
        
        for (int i = 0; i < levelNames.Length; i++)
        {
            
            // Clone the Exit Button to create a new level button
            GameObject buttonObj = Instantiate(exitButton.gameObject, levelButtonsParent);
            buttonObj.name = $"Level Button - {levelNames[i]}";
            buttonObj.SetActive(true);
            
            
            // Fix button anchors and size for proper layout
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0, 0);
                buttonRect.anchorMax = new Vector2(1, 0);  // Stretch horizontally
                buttonRect.sizeDelta = new Vector2(0, 60); // Full width, 60 height
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.SetAsFirstSibling(); // Put before the Exit button
            }
            
            Button button = buttonObj.GetComponent<Button>();
            if (button == null)
            {
                // Debug.LogError($"No Button component found on instantiated prefab!");
                continue;
            }
            
            // Get the text component (cloned from Exit button, so there should be exactly one)
            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            
            if (buttonText != null)
            {
                buttonText.text = levelNames[i];
                buttonText.enabled = true;
                buttonText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            }
            else
            {
                // Debug.LogError($"Button {i} has no TextMeshProUGUI child!");
            }
            
            levelButtons[i] = button;
            originalButtonScales[i] = button.transform.localScale;
            
            // Add click listener
            int levelIndex = i;
            button.onClick.AddListener(() => SelectLevel(levelIndex));
        }
        
        // Build complete selectable buttons array (level buttons + exit button)
        allSelectableButtons = new Button[levelButtons.Length + 1];
        for (int i = 0; i < levelButtons.Length; i++)
        {
            allSelectableButtons[i] = levelButtons[i];
        }
        allSelectableButtons[levelButtons.Length] = exitButton; // Add exit button at the end
        
        // Store exit button's original scale
        if (exitButton != null)
        {
            exitButtonOriginalScale = exitButton.transform.localScale;
        }
        
    }
    
    void Update()
    {
        // Decrease navigation cooldown
        if (navigationCooldown > 0)
        {
            navigationCooldown -= Time.deltaTime;
        }
        
        // Handle digital navigation with cooldown reset when stick returns to center
        if (isDialogueActive && navigationCooldown > 0)
        {
            // Reset cooldown when stick returns near center
            if (Mathf.Abs(lastNavigationInput.y) < 0.3f)
            {
                navigationCooldown = 0f;
            }
        }
    }
    
    void OnNavigate(InputAction.CallbackContext context)
    {
        if (!isDialogueActive || allSelectableButtons == null) return;
        
        // MULTIPLAYER FIX: Only accept input from Player 1's gamepad or keyboard
        if (!IsInputFromPlayer1(context))
        {
            return;
        }
        
        Vector2 input = context.ReadValue<Vector2>();
        
        // Store input for cooldown reset detection
        lastNavigationInput = input;
        
        // Digital/Binary navigation - only trigger once per stick movement
        // Check if we're in cooldown (prevents rapid-fire navigation)
        if (navigationCooldown > 0)
            return;
        
        // Check if stick is pushed past threshold
        if (input.y > NAVIGATION_THRESHOLD) // Up - move to previous button (higher on screen)
        {
            currentSelectedIndex = (currentSelectedIndex + 1) % allSelectableButtons.Length;
            UpdateButtonSelection();
            navigationCooldown = NAVIGATION_COOLDOWN_TIME;
        }
        else if (input.y < -NAVIGATION_THRESHOLD) // Down - move to next button (lower on screen)
        {
            currentSelectedIndex = (currentSelectedIndex - 1 + allSelectableButtons.Length) % allSelectableButtons.Length;
            UpdateButtonSelection();
            navigationCooldown = NAVIGATION_COOLDOWN_TIME;
        }
    }
    
    void OnInteract(InputAction.CallbackContext context)
    {
        if (!isDialogueActive || allSelectableButtons == null) return;
        
        // MULTIPLAYER FIX: Only accept input from Player 1's gamepad or keyboard
        if (!IsInputFromPlayer1(context))
        {
            return;
        }
        
        // Check if we're selecting the exit button (last in array)
        if (currentSelectedIndex == allSelectableButtons.Length - 1)
        {
            CloseDialogue();
        }
        else
        {
            SelectLevel(currentSelectedIndex);
        }
    }
    
    void OnCancel(InputAction.CallbackContext context)
    {
        if (!isDialogueActive) return;
        
        // MULTIPLAYER FIX: Only accept input from Player 1's gamepad or keyboard
        if (!IsInputFromPlayer1(context))
        {
            return;
        }
        
        CloseDialogue();
    }
    
    /// <summary>
    /// Check if the input is coming from Player 1's specific gamepad or keyboard
    /// </summary>
    bool IsInputFromPlayer1(InputAction.CallbackContext context)
    {
        // Keyboard is always allowed (Player 1's input)
        if (context.control.device is Keyboard)
        {
            return true;
        }
        
        // If it's a gamepad, check if it's Player 1's assigned gamepad
        if (context.control.device is Gamepad gamepad)
        {
            // If we have Player 1's gamepad detected, only allow that one
            if (player1Gamepad != null)
            {
                bool isPlayer1 = (gamepad == player1Gamepad);
                if (!isPlayer1)
                {
                    Debug.Log($"<color=yellow>[LevelSelectionUI]</color> Blocked input from {gamepad.name} - only Player 1's gamepad ({player1Gamepad.name}) is allowed");
                }
                return isPlayer1;
            }
            else
            {
                // If we couldn't detect Player 1's gamepad, allow the first gamepad
                bool isFirstGamepad = (Gamepad.all.Count > 0 && gamepad == Gamepad.all[0]);
                if (!isFirstGamepad)
                {
                    Debug.Log($"<color=yellow>[LevelSelectionUI]</color> Blocked input from {gamepad.name} - only first gamepad is allowed");
                }
                return isFirstGamepad;
            }
        }
        
        // Unknown device - block it
        return false;
    }
    
    void UpdateButtonSelection()
    {
        if (allSelectableButtons == null) return;
        
        for (int i = 0; i < allSelectableButtons.Length; i++)
        {
            Button button = allSelectableButtons[i];
            Vector3 originalScale;
            
            // Get the appropriate original scale
            if (i < levelButtons.Length)
            {
                originalScale = originalButtonScales[i];
            }
            else
            {
                originalScale = exitButtonOriginalScale;
            }
            
            if (i == currentSelectedIndex)
            {
                // Highlight selected button
                StartCoroutine(AnimateButtonScale(button, originalScale * hoverScaleMultiplier));
                
                // Change button color or add visual feedback
                ColorBlock colors = button.colors;
                colors.normalColor = Color.yellow;
                button.colors = colors;
            }
            else
            {
                // Reset other buttons
                StartCoroutine(AnimateButtonScale(button, originalScale));
                
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                button.colors = colors;
            }
        }
    }
    
    IEnumerator AnimateButtonScale(Button button, Vector3 targetScale)
    {
        Vector3 startScale = button.transform.localScale;
        float elapsedTime = 0f;
        float duration = 1f / scaleAnimationSpeed;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            button.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        
        button.transform.localScale = targetScale;
    }
    
    void SelectLevel(int levelIndex)
    {
        OnLevelSelected?.Invoke(levelIndex);
        CloseDialogue();
    }
    
    void CloseDialogue()
    {
        isDialogueActive = false;
        
        // Disable input handling for dialogue
        DisableDialogueInputs();
        
        // Hide panels (this hides all buttons inside them)
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
        
        if (levelSelectionPanel != null)
            levelSelectionPanel.SetActive(false);
        
        OnDialogueClosed?.Invoke();
    }
    
    void OnDestroy()
    {
        if (exitButton != null)
            exitButton.onClick.RemoveAllListeners();
        
        if (levelButtons != null)
        {
            foreach (Button button in levelButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }
    }
}