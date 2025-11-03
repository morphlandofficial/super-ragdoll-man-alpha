using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Enhanced ProximitySceneLoader that works with ProximityScaleAnimator for smooth grow/shrink effects
/// </summary>
public class ProximitySceneLoaderAnimated : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private Object sceneToLoad; // Drag scene asset here
    
    [Header("Object Toggle with Animation")]
    [SerializeField] private GameObject objectToToggle; // Object to activate/deactivate with animation
    
    [Header("Animation Settings")]
    [SerializeField] private bool useScaleAnimation = true;
    [SerializeField] private float growDuration = 0.3f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false; // Set to false for release
    
    private bool playerInProximity = false;
    private ActiveRagdollActions inputActions;
    private bool originalObjectState;
    private Collider proximityCollider;
    private ProximityScaleAnimator scaleAnimator;
    
    private void Awake()
    {
        inputActions = new ActiveRagdollActions();
        
        // Get the collider component on this GameObject
        proximityCollider = GetComponent<Collider>();
        if (proximityCollider == null)
        {
// Debug.LogError("ProximitySceneLoaderAnimated: No Collider component found on " + gameObject.name + ". Please add a Collider and set it as a trigger.");
        }
        else
        {
            // Ensure it's set as a trigger
            proximityCollider.isTrigger = true;
        }
        
        // Set up the scale animator on the toggle object if needed
        SetupScaleAnimator();
    }
    
    private void SetupScaleAnimator()
    {
        if (objectToToggle != null && useScaleAnimation)
        {
            // Check if the object already has a ProximityScaleAnimator
            scaleAnimator = objectToToggle.GetComponent<ProximityScaleAnimator>();
            
            if (scaleAnimator == null)
            {
                // Add the ProximityScaleAnimator component
                scaleAnimator = objectToToggle.AddComponent<ProximityScaleAnimator>();
                
                if (debugMode)
                {
                }
            }
            
            // Configure the animator with our settings
            ConfigureScaleAnimator();
        }
    }
    
    private void ConfigureScaleAnimator()
    {
        if (scaleAnimator != null)
        {
            // Configure the animator with our settings
            scaleAnimator.GrowDuration = growDuration;
            scaleAnimator.GrowCurve = growCurve;
            
            if (debugMode)
            {
            }
        }
    }
    
    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.UI.Submit.performed += OnSubmitPressed;
    }
    
    private void OnDisable()
    {
        inputActions.UI.Submit.performed -= OnSubmitPressed;
        inputActions.Disable();
    }
    
    private void Start()
    {
        // Store the original state of the object to toggle
        if (objectToToggle != null)
        {
            originalObjectState = objectToToggle.activeSelf;
            
            if (debugMode)
            {
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is a player
        if (IsPlayer(other.gameObject))
        {
            playerInProximity = true;
            
            if (debugMode)
            {
            }
            
            // Activate the toggle object with animation
            ActivateToggleObject();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is a player
        if (IsPlayer(other.gameObject))
        {
            playerInProximity = false;
            
            if (debugMode)
            {
            }
            
            // Deactivate the toggle object (instant for now, as requested)
            DeactivateToggleObject();
        }
    }
    
    private void ActivateToggleObject()
    {
        if (objectToToggle != null)
        {
            if (useScaleAnimation && scaleAnimator != null)
            {
                // Activate the object first, then the animator will handle the scaling
                objectToToggle.SetActive(true);
                // The ProximityScaleAnimator will automatically start the grow animation in OnEnable
            }
            else
            {
                // Simple activation without animation
                objectToToggle.SetActive(true);
            }
            
            if (debugMode)
            {
            }
        }
    }
    
    private void DeactivateToggleObject()
    {
        if (objectToToggle != null)
        {
            // Simple deactivation (instant as requested)
            objectToToggle.SetActive(originalObjectState);
            
            if (debugMode)
            {
            }
        }
    }
    
    private bool IsPlayer(GameObject obj)
    {
        // Check if the object or any of its parents has the LevelTraveler component
        Transform current = obj.transform;
        while (current != null)
        {
            if (current.GetComponent<LevelTraveler>() != null)
            {
                return true;
            }
            current = current.parent;
        }
        
        return false;
    }
    
    private void OnSubmitPressed(InputAction.CallbackContext context)
    {
        // Only load scene if player is in proximity and we have a scene to load
        if (playerInProximity && sceneToLoad != null)
        {
            string sceneName = sceneToLoad.name;
            
            if (debugMode)
            {
            }
            
            // CHECK: Is level unlocked? (Level gating system)
            if (LevelManager.Instance != null)
            {
                if (!LevelManager.Instance.IsLevelUnlocked(sceneName))
                {
                    Debug.Log($"[ProximitySceneLoaderAnimated] Level '{sceneName}' is LOCKED! Cannot load.");
                    // TODO: Play "locked" sound effect here
                    return; // Don't load locked levels
                }
            }
            
            // Check if scene is in build settings
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                
                if (sceneNameFromPath == sceneName)
                {
                    SceneManager.LoadScene(sceneName);
                    return;
                }
            }
            
            // If not found in build settings, try loading by name anyway
// Debug.LogWarning($"Scene '{sceneName}' not found in build settings. Attempting to load anyway...");
            SceneManager.LoadScene(sceneName);
        }
    }
    
    /// <summary>
    /// Manually set the object to toggle (useful for runtime setup)
    /// </summary>
    public void SetToggleObject(GameObject newToggleObject)
    {
        objectToToggle = newToggleObject;
        originalObjectState = newToggleObject != null ? newToggleObject.activeSelf : false;
        
        if (useScaleAnimation)
        {
            SetupScaleAnimator();
        }
        
        if (debugMode)
        {
        }
    }
    
    /// <summary>
    /// Get the current toggle object
    /// </summary>
    public GameObject GetToggleObject()
    {
        return objectToToggle;
    }
}