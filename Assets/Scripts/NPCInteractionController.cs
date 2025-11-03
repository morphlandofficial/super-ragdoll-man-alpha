using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NPCInteractionController : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private Transform player;
    [SerializeField] private LevelSelectionUI levelSelectionUI;
    
    [Header("Level Scenes")]
    [SerializeField] private SceneAsset[] levelScenes = new SceneAsset[3];
    
    // Runtime scene names and paths
    private string[] levelNames;
    private string[] levelPaths;
    
    private bool playerInRange = false;
    private bool dialogueActive = false;
    private ActiveRagdollActions inputActions;
    
    void Awake()
    {
        inputActions = new ActiveRagdollActions();
    }
    
    void Start()
    {
        // Initialize scene data from SceneAssets
        InitializeSceneData();
        
        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        
        // Find level selection UI if not assigned
        if (levelSelectionUI == null)
        {
            levelSelectionUI = FindFirstObjectByType<LevelSelectionUI>();
        }
        
        if (levelSelectionUI != null)
        {
            levelSelectionUI.OnLevelSelected += LoadLevel;
            levelSelectionUI.OnDialogueClosed += OnDialogueClosed;
        }
    }
    
    void InitializeSceneData()
    {
        
        // Count valid scenes
        int validSceneCount = 0;
        for (int i = 0; i < levelScenes.Length; i++)
        {
            if (levelScenes[i] != null)
            {
                validSceneCount++;
            }
            else
            {
                // Debug.LogWarning($"Scene at index {i} is null!");
            }
        }
        
        // Initialize arrays
        levelNames = new string[validSceneCount];
        levelPaths = new string[validSceneCount];
        
        // Extract scene names and paths
        int index = 0;
        for (int i = 0; i < levelScenes.Length; i++)
        {
            if (levelScenes[i] != null)
            {
#if UNITY_EDITOR
                string scenePath = AssetDatabase.GetAssetPath(levelScenes[i]);
                levelPaths[index] = scenePath;
                levelNames[index] = levelScenes[i].name;
#else
                // In build, use the scene name directly
                levelNames[index] = levelScenes[i].name;
                levelPaths[index] = levelScenes[i].name;
#endif
                index++;
            }
        }
        
    }
    
    void OnEnable()
    {
        inputActions.Enable();
        inputActions.UI.Interact.performed += OnInteractPressed;
    }
    
    void OnDisable()
    {
        inputActions.UI.Interact.performed -= OnInteractPressed;
        inputActions.Disable();
    }
    
    void Update()
    {
        // Continuously look for player if not found
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        
        CheckPlayerProximity();
    }
    
    void CheckPlayerProximity()
    {
        if (player == null) return;
        
        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRange;
        
        // Show/hide interaction prompt based on proximity
        if (playerInRange != wasInRange)
        {
            if (levelSelectionUI != null)
            {
                if (playerInRange && !dialogueActive)
                    levelSelectionUI.ShowInteractionPrompt(true);
                else if (!playerInRange)
                    levelSelectionUI.ShowInteractionPrompt(false);
            }
        }
    }
    
    void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (playerInRange && !dialogueActive)
        {
            StartDialogue();
        }
    }
    
    void StartDialogue()
    {
        dialogueActive = true;
        
        // Disable player movement input FIRST
        DisablePlayerInput();
        
        if (levelSelectionUI != null)
        {
            if (levelNames != null)
            {
                for (int i = 0; i < levelNames.Length; i++)
                {
                }
            }
            else
            {
                // Debug.LogError("levelNames array is null!");
            }
            
            levelSelectionUI.ShowInteractionPrompt(false);
            levelSelectionUI.ShowLevelSelection(levelNames);
        }
        else
        {
            // Debug.LogError("LevelSelectionUI is null!");
        }
    }
    
    void OnDialogueClosed()
    {
        dialogueActive = false;
        
        // Re-enable player movement input
        EnablePlayerInput();
        
        // Show interaction prompt again if still in range
        if (playerInRange && levelSelectionUI != null)
        {
            levelSelectionUI.ShowInteractionPrompt(true);
        }
    }
    
    void LoadLevel(int levelIndex)
    {
        if (levelIndex >= 0 && levelIndex < levelPaths.Length)
        {
#if UNITY_EDITOR
            string scenePath = levelPaths[levelIndex];
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            SceneManager.LoadScene(sceneName);
#else
            // In build, use the scene name directly
            string sceneName = levelPaths[levelIndex];
            SceneManager.LoadScene(sceneName);
#endif
        }
        else
        {
            // Debug.LogError($"Invalid level index: {levelIndex}");
        }
    }
    
    void DisablePlayerInput()
    {
        
        // Find and disable player input components
        if (player == null)
        {
            Debug.LogWarning("<color=red>[NPCInteraction]</color> Player reference is NULL! Cannot disable input.");
            return;
        }
        
        // Find the ROOT player object (PlayerInput is likely on parent)
        Transform rootPlayer = FindPlayerRoot(player);
        
        Debug.Log("<color=cyan>[NPCInteraction]</color> Disabling player input for NPC interaction...");
        
        // NUCLEAR OPTION: Just disable/destroy everything that can make the player move
        
        // 1. DISABLE PlayerInput COMPONENT ENTIRELY (if it exists - may not in multiplayer)
        // This stops ALL input processing at the source
        PlayerInput playerInput = rootPlayer.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
            Debug.Log("<color=green>[NPCInteraction]</color> Disabled PlayerInput");
        }
        
        // 1.5. DISABLE MultiplayerGamepadController (if it exists - used in multiplayer)
        MultiplayerGamepadController gamepadController = rootPlayer.GetComponent<MultiplayerGamepadController>();
        if (gamepadController != null)
        {
            gamepadController.enabled = false;
            Debug.Log("<color=green>[NPCInteraction]</color> Disabled MultiplayerGamepadController");
        }
        
        // 2. DISABLE InputModule
        ActiveRagdoll.InputModule inputModule = rootPlayer.GetComponent<ActiveRagdoll.InputModule>();
        if (inputModule != null)
        {
            inputModule.enabled = false;
            Debug.Log("<color=green>[NPCInteraction]</color> Disabled InputModule");
        }
        
        // 3. DISABLE PhysicsModule - THIS WAS THE CULPRIT!
        // PhysicsModule keeps applying forces in FixedUpdate based on cached TargetDirection
        ActiveRagdoll.PhysicsModule physicsModule = rootPlayer.GetComponent<ActiveRagdoll.PhysicsModule>();
        if (physicsModule != null)
        {
            physicsModule.TargetDirection = Vector3.zero; // Clear target first
            physicsModule.enabled = false;
            Debug.Log("<color=green>[NPCInteraction]</color> Disabled PhysicsModule");
        }
        
        // 4. DISABLE AnimationModule
        ActiveRagdoll.AnimationModule animationModule = rootPlayer.GetComponent<ActiveRagdoll.AnimationModule>();
        if (animationModule != null)
        {
            animationModule.enabled = false;
            Debug.Log("<color=green>[NPCInteraction]</color> Disabled AnimationModule");
        }
        
        // 5. DISABLE ALL BEHAVIOR SCRIPTS
        MonoBehaviour[] behaviors = rootPlayer.GetComponents<MonoBehaviour>();
        foreach (var behavior in behaviors)
        {
            string behaviorName = behavior.GetType().Name;
            if (behaviorName.Contains("DefaultBehaviour") || 
                behaviorName.Contains("TitlePlayerDefaultBehavior"))
            {
                behavior.enabled = false;
            }
        }
        
        // 6. STOP ALL VELOCITY
        Rigidbody[] rigidbodies = rootPlayer.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
        
        // 7. Show cursor for UI navigation
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
    }
    
    Transform FindPlayerRoot(Transform current)
    {
        // Search up the hierarchy for PlayerInput OR MultiplayerGamepadController component
        Transform checkTransform = current;
        while (checkTransform != null)
        {
            if (checkTransform.GetComponent<PlayerInput>() != null || 
                checkTransform.GetComponent<MultiplayerGamepadController>() != null)
            {
                return checkTransform;
            }
            checkTransform = checkTransform.parent;
        }
        
        // If no input component found, return the original (fallback)
        Debug.LogWarning("<color=yellow>[NPCInteraction]</color> Could not find PlayerInput or MultiplayerGamepadController in hierarchy, using original reference");
        return current;
    }
    
    void EnablePlayerInput()
    {
        // Re-enable player input components
        if (player == null)
        {
            Debug.LogWarning("<color=red>[NPCInteraction]</color> Player reference is null in EnablePlayerInput");
            return;
        }
        
        // Find the ROOT player object
        Transform rootPlayer = FindPlayerRoot(player);
        
        Debug.Log("<color=cyan>[NPCInteraction]</color> Re-enabling player input...");
        
        // 1. RE-ENABLE PlayerInput component (if it exists - may not in multiplayer)
        PlayerInput playerInput = rootPlayer.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = true;
            Debug.Log("<color=green>[NPCInteraction]</color> Re-enabled PlayerInput");
        }
        
        // 1.5. RE-ENABLE MultiplayerGamepadController (if it exists - used in multiplayer)
        MultiplayerGamepadController gamepadController = rootPlayer.GetComponent<MultiplayerGamepadController>();
        if (gamepadController != null)
        {
            gamepadController.enabled = true;
            Debug.Log("<color=green>[NPCInteraction]</color> Re-enabled MultiplayerGamepadController");
        }
        
        // 2. Re-enable InputModule
        ActiveRagdoll.InputModule inputModule = rootPlayer.GetComponent<ActiveRagdoll.InputModule>();
        if (inputModule != null)
        {
            inputModule.enabled = true;
            Debug.Log("<color=green>[NPCInteraction]</color> Re-enabled InputModule");
        }
        
        // 3. Re-enable PhysicsModule
        ActiveRagdoll.PhysicsModule physicsModule = rootPlayer.GetComponent<ActiveRagdoll.PhysicsModule>();
        if (physicsModule != null)
        {
            physicsModule.enabled = true;
            Debug.Log("<color=green>[NPCInteraction]</color> Re-enabled PhysicsModule");
        }
        
        // 4. Re-enable AnimationModule
        ActiveRagdoll.AnimationModule animationModule = rootPlayer.GetComponent<ActiveRagdoll.AnimationModule>();
        if (animationModule != null)
        {
            animationModule.enabled = true;
            Debug.Log("<color=green>[NPCInteraction]</color> Re-enabled AnimationModule");
        }
        
        // 5. Re-enable default behavior scripts
        MonoBehaviour[] behaviors = rootPlayer.GetComponents<MonoBehaviour>();
        foreach (var behavior in behaviors)
        {
            if (behavior.GetType().Name.Contains("DefaultBehaviour") || 
                behavior.GetType().Name.Contains("TitlePlayerDefaultBehavior"))
            {
                behavior.enabled = true;
            }
        }
        
        // 6. Lock cursor again for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Debug.Log("<color=green>[NPCInteraction]</color> Player input fully restored!");
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw interaction range in scene view
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
    
    void OnDestroy()
    {
        if (levelSelectionUI != null)
        {
            levelSelectionUI.OnLevelSelected -= LoadLevel;
            levelSelectionUI.OnDialogueClosed -= OnDialogueClosed;
        }
    }
}