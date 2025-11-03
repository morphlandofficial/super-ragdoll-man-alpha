using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages collectible requirements for level completion.
/// Attach this to an empty GameObject called "Collectible Finish Manager".
/// Add collectible GameObjects to the list and set how many need to be collected to activate the finish trigger.
/// </summary>
public class CollectibleFinishManager : MonoBehaviour
{
    [Header("Required Collectibles")]
    [Tooltip("Drag and drop all collectible GameObjects available in the level")]
    [SerializeField] private List<GameObject> requiredCollectibles = new List<GameObject>();
    
    [Tooltip("How many collectibles need to be collected to activate the finish trigger (0 = all of them)")]
    [SerializeField] private int numberOfCollectiblesRequired = 0;
    
    [Header("Finish Trigger")]
    [Tooltip("Optional: Manually assign the Level Finish Trigger. If left empty, it will be found automatically.")]
    [SerializeField] private LevelFinishTrigger levelFinishTrigger;
    
    [Header("Audio")]
    [Tooltip("Sound played when all collectibles are collected (2D)")]
    [SerializeField] private AudioClip allCollectedSound;
    
    [Tooltip("Volume for the completion sound (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.8f;
    
    [Header("Animation")]
    [Tooltip("Duration of the scale-up animation when finish trigger appears (seconds)")]
    [SerializeField] private float scaleUpDuration = 0.5f;
    
    [Tooltip("Animation curve for the scale-up effect")]
    [SerializeField] private AnimationCurve scaleUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Debug Info")]
    [SerializeField] private int collectiblesCollected = 0;
    [SerializeField] private int totalCollectibles = 0;
    [SerializeField] private int actualRequiredCount = 0; // The actual number required after validation
    [SerializeField] private bool allCollected = false;
    
    // Internal state
    private List<Collectible> collectibleComponents = new List<Collectible>();
    private HashSet<int> collectedIndices = new HashSet<int>(); // Track which collectibles have been collected by index
    private bool hasPlayedSound = false;
    
    // Animation state
    private bool isAnimatingScaleUp = false;
    private float scaleUpTimer = 0f;
    private Vector3 originalFinishTriggerScale;
    
    private void Awake()
    {
        // Validate collectibles list
        if (requiredCollectibles == null || requiredCollectibles.Count == 0)
        {
            Debug.LogWarning("[CollectibleFinishManager] No collectibles assigned! This manager will have no effect.");
            enabled = false;
            return;
        }
    }
    
    private void Start()
    {
        // Find and cache Collectible components
        FindCollectibleComponents();
        
        // Calculate and validate the actual required count
        if (numberOfCollectiblesRequired <= 0 || numberOfCollectiblesRequired > totalCollectibles)
        {
            // If 0 or invalid, require all collectibles
            actualRequiredCount = totalCollectibles;
            Debug.Log($"[CollectibleFinishManager] Required count set to ALL collectibles ({actualRequiredCount})");
        }
        else
        {
            actualRequiredCount = numberOfCollectiblesRequired;
            Debug.Log($"[CollectibleFinishManager] Required count set to {actualRequiredCount} out of {totalCollectibles} collectibles");
        }
        
        // Find the level finish trigger if not assigned
        if (levelFinishTrigger == null)
        {
            levelFinishTrigger = FindFirstObjectByType<LevelFinishTrigger>();
            
            if (levelFinishTrigger == null)
            {
                Debug.LogError("[CollectibleFinishManager] No LevelFinishTrigger found in scene! Cannot manage finish trigger.");
                enabled = false;
                return;
            }
        }
        
        // Store the original scale
        originalFinishTriggerScale = levelFinishTrigger.transform.localScale;
        
        // Deactivate the level finish trigger
        levelFinishTrigger.gameObject.SetActive(false);
        Debug.Log($"[CollectibleFinishManager] Level Finish Trigger deactivated. Collect {actualRequiredCount} out of {totalCollectibles} items to activate.");
    }
    
    private void FindCollectibleComponents()
    {
        collectibleComponents.Clear();
        collectedIndices.Clear();
        totalCollectibles = 0;
        
        Debug.Log($"[CollectibleFinishManager] Finding collectible components from {requiredCollectibles.Count} GameObjects...");
        
        for (int i = 0; i < requiredCollectibles.Count; i++)
        {
            GameObject collectibleObj = requiredCollectibles[i];
            
            if (collectibleObj == null)
            {
                Debug.LogWarning("[CollectibleFinishManager] Null GameObject in required collectibles list. Skipping.");
                continue;
            }
            
            Collectible collectible = collectibleObj.GetComponentInChildren<Collectible>();
            
            if (collectible == null)
            {
                Debug.LogWarning($"[CollectibleFinishManager] GameObject '{collectibleObj.name}' does not have a Collectible component in itself or its children! Skipping.");
                continue;
            }
            
            collectibleComponents.Add(collectible);
            int index = totalCollectibles; // Capture index for lambda
            totalCollectibles++;
            
            // Subscribe to collection event with lambda that captures the index
            collectible.OnCollected += () => OnCollectibleCollected(index);
            
            Debug.Log($"[CollectibleFinishManager] ✓ Registered: {collectibleObj.name} (Index: {index})");
        }
        
        Debug.Log($"[CollectibleFinishManager] Total registered: {totalCollectibles} collectibles");
        
        if (totalCollectibles == 0)
        {
            Debug.LogError("[CollectibleFinishManager] No valid Collectible components found! This manager will have no effect.");
            enabled = false;
        }
    }
    
    private void Update()
    {
        // Handle scale-up animation
        if (isAnimatingScaleUp)
        {
            scaleUpTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(scaleUpTimer / scaleUpDuration);
            float curveValue = scaleUpCurve.Evaluate(progress);
            
            // Apply scale
            if (levelFinishTrigger != null)
            {
                levelFinishTrigger.transform.localScale = originalFinishTriggerScale * curveValue;
            }
            
            // End animation when complete
            if (progress >= 1f)
            {
                isAnimatingScaleUp = false;
                Debug.Log("[CollectibleFinishManager] Scale-up animation complete!");
            }
        }
    }
    
    private void OnDestroy()
    {
        // Note: We can't easily unsubscribe from lambda events
        // But since this manager is being destroyed, the references will be cleaned up
        // The collectibles will also be destroyed when the scene unloads
        collectibleComponents.Clear();
        collectedIndices.Clear();
    }
    
    /// <summary>
    /// Called when any collectible is collected
    /// </summary>
    private void OnCollectibleCollected(int index)
    {
        // Don't process if already complete
        if (allCollected) return;
        
        // Mark this collectible as collected (only if not already collected)
        if (!collectedIndices.Contains(index))
        {
            collectedIndices.Add(index);
            Debug.Log($"[CollectibleFinishManager] Collectible at index {index} collected! ({collectedIndices.Count}/{actualRequiredCount} required, {totalCollectibles} total)");
            
            // Check if enough are now collected
            CheckCollectionProgress();
        }
        else
        {
            Debug.LogWarning($"[CollectibleFinishManager] Collectible at index {index} already marked as collected (duplicate event?)");
        }
    }
    
    /// <summary>
    /// Check if enough collectibles have been collected
    /// </summary>
    private void CheckCollectionProgress()
    {
        // Use the tracked indices instead of checking components (which might be destroyed)
        collectiblesCollected = collectedIndices.Count;
        
        Debug.Log($"[CollectibleFinishManager] Collection Progress: {collectiblesCollected}/{actualRequiredCount} (Total Available: {totalCollectibles})");
        
        // Check if required amount has been collected
        if (collectiblesCollected >= actualRequiredCount)
        {
            OnAllCollectiblesCollected();
        }
        else
        {
            Debug.Log($"[CollectibleFinishManager] Still need {actualRequiredCount - collectiblesCollected} more collectible(s)");
        }
    }
    
    /// <summary>
    /// Creates a temporary GameObject to play a sound in 2D space, then destroys it when done
    /// </summary>
    private void PlaySoundAndDestroy(AudioClip clip, float volume)
    {
        if (clip == null) return;
        
        // Create empty GameObject for the sound
        GameObject soundObject = new GameObject($"TempSound_{clip.name}");
        soundObject.transform.position = transform.position; // Position doesn't matter for 2D sound but set it anyway
        
        // Add and configure AudioSource
        AudioSource tempAudioSource = soundObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 0f; // 2D sound
        tempAudioSource.playOnAwake = false;
        tempAudioSource.Play();
        
        // Destroy the GameObject after the clip finishes playing
        // Add a small buffer (0.1s) to ensure the sound fully completes
        Destroy(soundObject, clip.length + 0.1f);
    }
    
    /// <summary>
    /// Called when enough collectibles have been collected to meet the requirement
    /// </summary>
    private void OnAllCollectiblesCollected()
    {
        allCollected = true;
        
        Debug.Log($"<color=green>[CollectibleFinishManager] REQUIRED COLLECTIBLES COLLECTED! ({collectiblesCollected}/{actualRequiredCount})</color> Activating Level Finish Trigger with scale-up animation!");
        
        // Play completion sound using temporary game object
        if (!hasPlayedSound && allCollectedSound != null)
        {
            PlaySoundAndDestroy(allCollectedSound, soundVolume);
            hasPlayedSound = true;
        }
        
        // Activate the level finish trigger with scale-up animation
        if (levelFinishTrigger != null)
        {
            // Set scale to 0 and activate
            levelFinishTrigger.transform.localScale = Vector3.zero;
            levelFinishTrigger.gameObject.SetActive(true);
            
            // Start scale-up animation
            isAnimatingScaleUp = true;
            scaleUpTimer = 0f;
        }
    }
    
    /// <summary>
    /// Get current collection progress (0-1) based on required amount
    /// </summary>
    public float GetProgress()
    {
        if (actualRequiredCount == 0) return 0f;
        return (float)collectiblesCollected / actualRequiredCount;
    }
    
    /// <summary>
    /// Check if enough collectibles have been collected to meet the requirement
    /// </summary>
    public bool AreAllCollected()
    {
        return allCollected;
    }
}

