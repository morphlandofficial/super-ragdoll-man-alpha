using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class ProximitySceneLoader : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private Object sceneToLoad; // Drag scene asset here
    
    [Header("Object Toggle")]
    [SerializeField] private GameObject[] objectsToToggle; // Objects to activate/deactivate
    [SerializeField] private bool useScaleAnimation = true; // Scale into existence or just pop in
    [SerializeField] private bool scaleDownOnExit = true; // Scale down when exiting proximity
    [SerializeField] private float growDuration = 0.3f;
    [SerializeField] private float shrinkDuration = 0.3f;
    [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float startScale = 0.001f; // Very small but not zero
    
    private bool playerInProximity = false;
    private ActiveRagdollActions inputActions;
    private bool[] originalObjectStates;
    private Collider proximityCollider;
    private Vector3[] originalScales;
    private Coroutine[] scaleAnimations;
    
    private void Awake()
    {
        inputActions = new ActiveRagdollActions();
        
        // Get the collider component on this GameObject
        proximityCollider = GetComponent<Collider>();
        if (proximityCollider == null)
        {
// Debug.LogError("ProximitySceneLoader: No Collider component found on " + gameObject.name + ". Please add a Collider and set it as a trigger.");
        }
        else
        {
            // Ensure it's set as a trigger
            proximityCollider.isTrigger = true;
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
        // Store the original state of the objects to toggle
        if (objectsToToggle != null && objectsToToggle.Length > 0)
        {
            originalObjectStates = new bool[objectsToToggle.Length];
            originalScales = new Vector3[objectsToToggle.Length];
            scaleAnimations = new Coroutine[objectsToToggle.Length];
            
            for (int i = 0; i < objectsToToggle.Length; i++)
            {
                if (objectsToToggle[i] != null)
                {
                    originalObjectStates[i] = objectsToToggle[i].activeSelf;
                    
                    // Store original scale for animation
                    if (useScaleAnimation)
                    {
                        originalScales[i] = objectsToToggle[i].transform.localScale;
                        
                        // If object starts inactive, set it to tiny scale
                        if (!objectsToToggle[i].activeSelf)
                        {
                            objectsToToggle[i].transform.localScale = Vector3.one * startScale;
                        }
                    }
                }
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is a player
        if (IsPlayer(other.gameObject))
        {
            playerInProximity = true;
            
            // Activate the toggle objects
            if (objectsToToggle != null && objectsToToggle.Length > 0)
            {
                for (int i = 0; i < objectsToToggle.Length; i++)
                {
                    if (objectsToToggle[i] != null)
                    {
                        if (useScaleAnimation)
                        {
                            // Start with tiny scale and activate
                            objectsToToggle[i].transform.localScale = Vector3.one * startScale;
                            objectsToToggle[i].SetActive(true);
                            
                            // Start grow animation
                            if (scaleAnimations[i] != null)
                            {
                                StopCoroutine(scaleAnimations[i]);
                            }
                            scaleAnimations[i] = StartCoroutine(GrowAnimation(i));
                        }
                        else
                        {
                            // Simple pop-in activation
                            objectsToToggle[i].SetActive(true);
                        }
                    }
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is a player
        if (IsPlayer(other.gameObject))
        {
            playerInProximity = false;
            
            // Restore the toggle objects to their original state
            if (objectsToToggle != null && objectsToToggle.Length > 0)
            {
                for (int i = 0; i < objectsToToggle.Length; i++)
                {
                    if (objectsToToggle[i] != null)
                    {
                        // Stop any running animation
                        if (scaleAnimations[i] != null)
                        {
                            StopCoroutine(scaleAnimations[i]);
                            scaleAnimations[i] = null;
                        }
                        
                        // If using scale animation and scale down on exit is enabled
                        if (useScaleAnimation && scaleDownOnExit && !originalObjectStates[i])
                        {
                            // Start shrink animation
                            scaleAnimations[i] = StartCoroutine(ShrinkAnimation(i));
                        }
                        else
                        {
                            // Instant disappear
                            // Reset scale if we were using animation
                            if (useScaleAnimation && !originalObjectStates[i])
                            {
                                objectsToToggle[i].transform.localScale = Vector3.one * startScale;
                            }
                            
                            // Deactivate
                            objectsToToggle[i].SetActive(originalObjectStates[i]);
                        }
                    }
                }
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
            
            // CHECK: Is level unlocked? (Level gating system)
            if (LevelManager.Instance != null)
            {
                if (!LevelManager.Instance.IsLevelUnlocked(sceneName))
                {
                    Debug.Log($"[ProximitySceneLoader] Level '{sceneName}' is LOCKED! Cannot load.");
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
    
    private IEnumerator GrowAnimation(int index)
    {
        if (objectsToToggle[index] == null) yield break;
        
        float elapsedTime = 0f;
        Vector3 startScaleVec = Vector3.one * startScale;
        
        while (elapsedTime < growDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / growDuration;
            
            // Apply the animation curve
            float curveValue = growCurve.Evaluate(progress);
            
            // Interpolate between tiny scale and original scale
            Vector3 currentScale = Vector3.Lerp(startScaleVec, originalScales[index], curveValue);
            objectsToToggle[index].transform.localScale = currentScale;
            
            yield return null;
        }
        
        // Ensure we end at exactly the original scale
        objectsToToggle[index].transform.localScale = originalScales[index];
        scaleAnimations[index] = null;
    }
    
    private IEnumerator ShrinkAnimation(int index)
    {
        if (objectsToToggle[index] == null) yield break;
        
        float elapsedTime = 0f;
        Vector3 endScaleVec = Vector3.one * startScale;
        Vector3 currentStartScale = objectsToToggle[index].transform.localScale;
        
        while (elapsedTime < shrinkDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / shrinkDuration;
            
            // Apply the animation curve
            float curveValue = shrinkCurve.Evaluate(progress);
            
            // Interpolate from current scale to tiny scale
            Vector3 currentScale = Vector3.Lerp(currentStartScale, endScaleVec, curveValue);
            
            // Safety check in case object was destroyed during animation
            if (objectsToToggle[index] == null) yield break;
            
            objectsToToggle[index].transform.localScale = currentScale;
            
            yield return null;
        }
        
        // Ensure we end at exactly the tiny scale and deactivate
        if (objectsToToggle[index] != null)
        {
            objectsToToggle[index].transform.localScale = endScaleVec;
            objectsToToggle[index].SetActive(originalObjectStates[index]);
        }
        
        scaleAnimations[index] = null;
    }
}