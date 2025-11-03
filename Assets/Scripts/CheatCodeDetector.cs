using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Detects controller cheat code sequences for unlocking levels and costumes
/// Attach to the Level Manager GameObject
/// </summary>
public class CheatCodeDetector : MonoBehaviour
{
    [Header("--- Cheat Code Configuration ---")]
    [SerializeField] 
    [Tooltip("How long the player has to complete the LEVELS sequence (seconds)")]
    private float _levelsSequenceTimeLimit = 2.0f;
    
    [SerializeField] 
    [Tooltip("How long the player has to complete the COSTUMES sequence (seconds)")]
    private float _costumesSequenceTimeLimit = 3.0f;
    
    [SerializeField]
    [Tooltip("Enable debug logging to console")]
    private bool _debugMode = false;
    
    [Header("--- Audio ---")]
    [SerializeField]
    [Tooltip("Sound effect to play when cheat code is successfully activated")]
    private AudioClip _unlockSound = null;
    
    // Cheat button types
    private enum CheatButton
    {
        L2,  // Left Trigger
        L1,  // Left Shoulder
        R2,  // Right Trigger
        R1,  // Right Shoulder
        L3,  // Left Stick Click
        R3   // Right Stick Click
    }
    
    // CHEAT 1: Unlock All Levels - L2-L1-R2-R1 (x3)
    private readonly CheatButton[] _levelsSequence = new CheatButton[]
    {
        CheatButton.L2, CheatButton.L1, CheatButton.R2, CheatButton.R1,
        CheatButton.L2, CheatButton.L1, CheatButton.R2, CheatButton.R1,
        CheatButton.L2, CheatButton.L1, CheatButton.R2, CheatButton.R1
    };
    
    // CHEAT 2: Unlock Costume Cycling - L1-R1-L1-R1-L1-R1
    private readonly CheatButton[] _costumesSequence = new CheatButton[]
    {
        CheatButton.L1, CheatButton.R1, CheatButton.L1, CheatButton.R1,
        CheatButton.L1, CheatButton.R1
    };
    
    // State tracking
    private List<CheatButton> _currentSequence = new List<CheatButton>();
    private float _sequenceStartTime = 0f;
    private bool _isTrackingSequence = false;
    private bool _levelsCheatActivated = false;
    private bool _costumesCheatActivated = false;
    
    // Button state tracking (to detect single presses, not holds)
    private bool _l2WasPressed = false;
    private bool _l1WasPressed = false;
    private bool _r2WasPressed = false;
    private bool _r1WasPressed = false;
    private bool _l3WasPressed = false;
    private bool _r3WasPressed = false;
    
    // References
    private Gamepad _gamepad;
    
    // Singleton-like behavior for session state
    private static CheatCodeDetector _instance;
    
    // Track current scene to detect scene changes
    private string _currentSceneName = "";
    
    private void Awake()
    {
        // Singleton pattern - prevent duplicates
        if (_instance != null && _instance != this)
        {
            if (_debugMode)
                Debug.Log("[CheatCodeDetector] Duplicate detected - destroying this one");
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        
        // CRITICAL: Make this persist across scenes so cheat states are maintained!
        DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene change events
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        
        if (_debugMode)
            Debug.Log("[CheatCodeDetector] Cheat code detector initialized (persists across scenes, only listens on title screen)");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from scene change events
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _currentSceneName = scene.name;
        
        // Only enable on title screen
        if (_currentSceneName == "_Title Screen")
        {
            this.enabled = true;
            
            // Reset costume selection when returning to title screen (fresh game session)
            CostumeCycler.ClearStoredCostumeIndex();
            
            if (_debugMode)
                Debug.Log("[CheatCodeDetector] 🎮 Title screen loaded - cheat detection ENABLED, costume preference reset");
        }
        else
        {
            this.enabled = false;
            if (_debugMode)
                Debug.Log($"[CheatCodeDetector] Scene '{_currentSceneName}' loaded - cheat detection DISABLED");
        }
    }
    
    private void Start()
    {
        // Initial scene check
        _currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (_currentSceneName != "_Title Screen")
        {
            this.enabled = false;
            if (_debugMode)
                Debug.Log($"[CheatCodeDetector] Not on title screen (current: {_currentSceneName}) - disabling cheat detection");
        }
        else
        {
            // Reset costume selection on game start (fresh session)
            CostumeCycler.ClearStoredCostumeIndex();
            
            if (_debugMode)
                Debug.Log("[CheatCodeDetector] 🎮 Title screen start - costume preference reset");
        }
    }
    
    private void Update()
    {
        // Get current gamepad (if any)
        _gamepad = Gamepad.current;
        if (_gamepad == null)
            return; // No gamepad connected
        
        // Check if ButtonNorth (ragdoll button) is held
        bool ragdollHeld = _gamepad.buttonNorth.isPressed;
        
        if (ragdollHeld)
        {
            // Start tracking sequence when ragdoll is first held
            if (!_isTrackingSequence)
            {
                StartSequenceTracking();
            }
            
            // Check for button presses
            DetectButtonPresses();
            
            // Determine which time limit to use based on current sequence
            float currentTimeLimit = DetermineTimeLimit();
            
            // Check if sequence timed out
            if (Time.time - _sequenceStartTime > currentTimeLimit)
            {
                // if (_debugMode && _currentSequence.Count > 0)
                // {
                //     Debug.Log($"[CheatCode] ⏱️ Sequence timed out! Got {_currentSequence.Count} inputs.");
                // }
                ResetSequence();
            }
        }
        else
        {
            // Ragdoll released - reset sequence
            if (_isTrackingSequence && _currentSequence.Count > 0)
            {
                // if (_debugMode)
                // {
                //     Debug.Log($"[CheatCode] ❌ Ragdoll released! Sequence reset. Had {_currentSequence.Count} inputs.");
                // }
                ResetSequence();
            }
        }
    }
    
    private float DetermineTimeLimit()
    {
        // If sequence matches costumes pattern so far, use costume time limit
        if (_currentSequence.Count > 0 && _currentSequence.Count <= _costumesSequence.Length)
        {
            bool matchesCostumes = true;
            for (int i = 0; i < _currentSequence.Count; i++)
            {
                if (_currentSequence[i] != _costumesSequence[i])
                {
                    matchesCostumes = false;
                    break;
                }
            }
            if (matchesCostumes) return _costumesSequenceTimeLimit;
        }
        
        // Default to levels time limit
        return _levelsSequenceTimeLimit;
    }
    
    private void StartSequenceTracking()
    {
        _isTrackingSequence = true;
        _sequenceStartTime = Time.time;
        _currentSequence.Clear();
        
        // if (_debugMode)
        // {
        //     Debug.Log("[CheatCode] 🎮 Ragdoll held! Listening for cheat sequence...");
        // }
    }
    
    private void DetectButtonPresses()
    {
        // L2 (Left Trigger)
        bool l2Pressed = _gamepad.leftTrigger.ReadValue() > 0.5f;
        if (l2Pressed && !_l2WasPressed)
        {
            RegisterButtonPress(CheatButton.L2);
        }
        _l2WasPressed = l2Pressed;
        
        // L1 (Left Shoulder)
        bool l1Pressed = _gamepad.leftShoulder.isPressed;
        if (l1Pressed && !_l1WasPressed)
        {
            RegisterButtonPress(CheatButton.L1);
        }
        _l1WasPressed = l1Pressed;
        
        // R2 (Right Trigger)
        bool r2Pressed = _gamepad.rightTrigger.ReadValue() > 0.5f;
        if (r2Pressed && !_r2WasPressed)
        {
            RegisterButtonPress(CheatButton.R2);
        }
        _r2WasPressed = r2Pressed;
        
        // R1 (Right Shoulder)
        bool r1Pressed = _gamepad.rightShoulder.isPressed;
        if (r1Pressed && !_r1WasPressed)
        {
            RegisterButtonPress(CheatButton.R1);
        }
        _r1WasPressed = r1Pressed;
        
        // L3 (Left Stick Click)
        bool l3Pressed = _gamepad.leftStickButton.isPressed;
        if (l3Pressed && !_l3WasPressed)
        {
            RegisterButtonPress(CheatButton.L3);
        }
        _l3WasPressed = l3Pressed;
        
        // R3 (Right Stick Click)
        bool r3Pressed = _gamepad.rightStickButton.isPressed;
        if (r3Pressed && !_r3WasPressed)
        {
            RegisterButtonPress(CheatButton.R3);
        }
        _r3WasPressed = r3Pressed;
    }
    
    private void RegisterButtonPress(CheatButton button)
    {
        _currentSequence.Add(button);
        
        // if (_debugMode)
        // {
        //     Debug.Log($"[CheatCode] Button pressed: {button} ({_currentSequence.Count} inputs)");
        // }
        
        int index = _currentSequence.Count - 1;
        
        // Check if it matches the LEVELS cheat
        bool matchesLevels = index < _levelsSequence.Length && _currentSequence[index] == _levelsSequence[index];
        
        // Check if it matches the COSTUMES cheat
        bool matchesCostumes = index < _costumesSequence.Length && _currentSequence[index] == _costumesSequence[index];
        
        // if (_debugMode)
        // {
        //     Debug.Log($"[CheatCode]   -> matchesLevels: {matchesLevels}, matchesCostumes: {matchesCostumes}");
        //     Debug.Log($"[CheatCode]   -> Current count: {_currentSequence.Count}, Costumes length: {_costumesSequence.Length}, Already activated: {_costumesCheatActivated}");
        // }
        
        // Check if COSTUMES sequence is complete FIRST (it's shorter)
        if (matchesCostumes && _currentSequence.Count == _costumesSequence.Length && !_costumesCheatActivated)
        {
            // Debug.Log($"[CheatCode] 🎨 COSTUMES SEQUENCE COMPLETE!");
            ActivateCostumesCheat();
            return;
        }
        // else if (_debugMode && _currentSequence.Count == _costumesSequence.Length)
        // {
        //     Debug.Log($"[CheatCode] ⚠️ Count matches but didn't activate: matchesCostumes={matchesCostumes}, alreadyActivated={_costumesCheatActivated}");
        // }
        
        // Check if LEVELS sequence is complete
        if (matchesLevels && _currentSequence.Count == _levelsSequence.Length && !_levelsCheatActivated)
        {
            if (_debugMode)
                Debug.Log($"[CheatCode] 🎮 LEVELS SEQUENCE COMPLETE!");
            ActivateLevelsCheat();
            return;
        }
        
        // If doesn't match any pattern, reset
        if (!matchesLevels && !matchesCostumes)
        {
            // Doesn't match any cheat pattern - reset
            // if (_debugMode)
            // {
            //     Debug.Log($"[CheatCode] ❌ Button doesn't match any cheat pattern! Resetting...");
            // }
            ResetSequence();
            return;
        }
    }
    
    private void ResetSequence()
    {
        _currentSequence.Clear();
        _isTrackingSequence = false;
        
        // Reset button states
        _l2WasPressed = false;
        _l1WasPressed = false;
        _r2WasPressed = false;
        _r1WasPressed = false;
        _l3WasPressed = false;
        _r3WasPressed = false;
    }
    
    private void ActivateLevelsCheat()
    {
        _levelsCheatActivated = true;
        
        Debug.Log("🎉 [CheatCode] ✅ LEVELS CHEAT ACTIVATED! Unlocking all levels...");
        
        // Play unlock sound effect (2D audio)
        if (_unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(_unlockSound, Camera.main.transform.position);
        }
        
        // Unlock all levels via Level Manager
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.UnlockAllLevels();
            // Debug.Log("[CheatCode] 🌟 All levels unlocked!");
        }
        else
        {
            Debug.LogWarning("[CheatCode] ⚠️ LevelManager not found! Cannot unlock levels.");
        }
        
        // Reset sequence
        ResetSequence();
    }
    
    private void ActivateCostumesCheat()
    {
        _costumesCheatActivated = true;
        
        Debug.Log("👕 [CheatCode] ✅ COSTUMES CHEAT ACTIVATED! Costume cycling unlocked!");
        
        // Play unlock sound effect (2D audio)
        if (_unlockSound != null)
        {
            AudioSource.PlayClipAtPoint(_unlockSound, Camera.main.transform.position);
        }
        
        // Debug.Log("[CheatCode] 🎨 Costume cycling unlocked for this session! Hold ButtonNorth (Tab/Y) + Press ButtonWest (X/Square) to cycle costumes.");
        
        // Notify all active players to enable costume cycling
        DefaultBehaviour[] players = FindObjectsByType<DefaultBehaviour>(FindObjectsSortMode.None);
        foreach (DefaultBehaviour player in players)
        {
            var costumeCycler = player.GetComponent<CostumeCycler>();
            if (costumeCycler != null)
            {
                costumeCycler.UnlockCostumeCycling();
            }
            else
            {
                Debug.LogWarning("[CheatCode] Player found but no CostumeCycler component! Add it to the prefab.");
            }
        }
        
        // Reset sequence
        ResetSequence();
    }
    
    /// <summary>
    /// Check if costume cycling has been unlocked THIS SESSION (like levels cheat)
    /// </summary>
    public static bool IsCostumeCyclingUnlocked()
    {
        // Check if the instance exists and the flag is set
        return _instance != null && _instance._costumesCheatActivated;
    }
}

