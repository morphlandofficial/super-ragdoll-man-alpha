using UnityEngine;
using System.Collections.Generic;

public class SkyboxRandomizer : MonoBehaviour
{
    [Header("Skybox Materials (Legacy)")]
    [Tooltip("Old system: Drag and drop skybox materials here. Use 'Populate From Array' button below to migrate.")]
    public Material[] skyboxMaterials;

    [Header("Rarity Buckets")]
    [Tooltip("Enable/disable common skyboxes")]
    public bool enableCommon = true;
    
    [Tooltip("Drag skyboxes here that should appear frequently")]
    public Material[] commonSkyboxes = new Material[0];
    
    [Tooltip("Enable/disable uncommon skyboxes")]
    public bool enableUncommon = true;
    
    [Tooltip("Drag skyboxes here that should appear occasionally")]
    public Material[] uncommonSkyboxes = new Material[0];
    
    [Tooltip("Enable/disable rare skyboxes")]
    public bool enableRare = true;
    
    [Tooltip("Drag skyboxes here that should appear rarely")]
    public Material[] rareSkyboxes = new Material[0];
    
    [Header("Rarity Weights")]
    [Tooltip("Weight multiplier for common skyboxes (default: 5x more likely than uncommon)")]
    [Range(1f, 20f)]
    public float commonWeight = 5f;
    
    [Tooltip("Weight multiplier for uncommon skyboxes (default: 1x baseline)")]
    [Range(0.1f, 10f)]
    public float uncommonWeight = 1f;
    
    [Tooltip("Weight multiplier for rare skyboxes (default: 0.2x less likely than uncommon)")]
    [Range(0.05f, 5f)]
    public float rareWeight = 0.2f;
    
    [Header("Special Skybox (All Levels Unlocked)")]
    [Tooltip("Special skybox to display when ALL levels are unlocked (overrides random selection)")]
    public Material allLevelsUnlockedSkybox = null;
    
    [Header("Special Skybox (First Time / New Game)")]
    [Tooltip("Special skybox to display the FIRST TIME the title screen loads (overrides everything else)")]
    public Material firstTimeSkybox = null;
    
    [Tooltip("PlayerPrefs key to track if the game has been launched before")]
    public string firstTimePrefKey = "HasLaunchedBefore";
    
    [Tooltip("If TRUE, every editor play session will be treated as a new game (first-time skybox shows every time in editor)")]
    public bool treatEditorPlayAsNewGame = true;
    
    [Header("Settings")]
    [Tooltip("If true, will randomize on Start(). If false, call RandomizeSkybox() manually.")]
    public bool randomizeOnStart = true;
    
    [Header("Debug Controls")]
    [Tooltip("Enable debug controls to randomize skyboxes manually (C key or Right Stick Click).")]
    public bool enableDebugControls = true;
    
    [Tooltip("C key cycling ONLY works when all levels are unlocked")]
    public bool requireAllLevelsUnlockedForCycling = true;
    
    [Tooltip("Show weight distribution info in console on start")]
    public bool showWeightInfo = false;
    
    private Material lastSelectedSkybox = null;
    private bool isUsingSpecialSkybox = false;
    
    // Static flag to track if we've shown the first-time skybox this session
    private static bool hasShownFirstTimeSkyboxThisSession = false;

    void OnValidate()
    {
        // Auto-populate from legacy array if buckets are empty (Editor mode)
        if (IsAllBucketsEmpty() && skyboxMaterials != null && skyboxMaterials.Length > 0)
        {
            PopulateFromSimpleArray();
        }
    }

    private bool IsAllBucketsEmpty()
    {
        return (commonSkyboxes == null || commonSkyboxes.Length == 0) &&
               (uncommonSkyboxes == null || uncommonSkyboxes.Length == 0) &&
               (rareSkyboxes == null || rareSkyboxes.Length == 0);
    }

    void Start()
    {
        // Ensure we have skyboxes populated at runtime too
        if (IsAllBucketsEmpty() && skyboxMaterials != null && skyboxMaterials.Length > 0)
        {
            PopulateFromSimpleArray();
        }
        
        if (showWeightInfo && !IsAllBucketsEmpty())
        {
            LogWeightDistribution();
        }
        
        // Priority 1: Check if this is the first time loading in THIS SESSION (NEW GAME)
        // Only show first-time skybox once per play session
        if (!hasShownFirstTimeSkyboxThisSession && IsFirstTimeLaunch() && firstTimeSkybox != null)
        {
            SetFirstTimeSkybox();
            MarkAsLaunched();
            hasShownFirstTimeSkyboxThisSession = true; // Mark as shown for this session
        }
        // Priority 2: Check if all levels are unlocked and apply special skybox
        else if (AreAllLevelsUnlocked() && allLevelsUnlockedSkybox != null)
        {
            SetSpecialSkybox();
        }
        // Priority 3: Use normal randomization
        else if (randomizeOnStart)
        {
            RandomizeSkybox();
        }
    }
    
    /// <summary>
    /// Check if all levels are unlocked (connects to Level Manager)
    /// </summary>
    private bool AreAllLevelsUnlocked()
    {
        if (LevelManager.Instance == null)
        {
            return false;
        }
        
        return LevelManager.Instance.AreAllLevelsUnlocked();
    }
    
    /// <summary>
    /// Set the special "all levels unlocked" skybox (called when cheat code triggers or on start)
    /// </summary>
    public void SetSpecialSkybox()
    {
        if (allLevelsUnlockedSkybox == null)
        {
            Debug.LogWarning("[SkyboxRandomizer] All levels unlocked but no special skybox assigned!");
            return;
        }
        
        Debug.Log("[SkyboxRandomizer] 🌟 All levels unlocked! Setting special skybox...");
        SetSkybox(allLevelsUnlockedSkybox);
        isUsingSpecialSkybox = true;
    }
    
    /// <summary>
    /// Check if this is the first time the game has been launched
    /// </summary>
    /// <returns>True if this is the first launch</returns>
    private bool IsFirstTimeLaunch()
    {
        #if UNITY_EDITOR
        // In editor, if treatEditorPlayAsNewGame is true, always treat as first launch
        if (treatEditorPlayAsNewGame)
        {
            return true;
        }
        #endif
        
        return !PlayerPrefs.HasKey(firstTimePrefKey) || PlayerPrefs.GetInt(firstTimePrefKey, 0) == 0;
    }
    
    /// <summary>
    /// Mark that the game has been launched (called after showing first time skybox)
    /// </summary>
    private void MarkAsLaunched()
    {
        PlayerPrefs.SetInt(firstTimePrefKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[SkyboxRandomizer] ✨ First launch detected! Marked as launched.");
    }
    
    /// <summary>
    /// Set the special "first time" skybox for new game experience
    /// </summary>
    public void SetFirstTimeSkybox()
    {
        if (firstTimeSkybox == null)
        {
            Debug.LogWarning("[SkyboxRandomizer] First time launch but no first time skybox assigned!");
            return;
        }
        
        Debug.Log("[SkyboxRandomizer] 🎮 Welcome! Setting first-time skybox...");
        SetSkybox(firstTimeSkybox);
        isUsingSpecialSkybox = true;
    }
    
    /// <summary>
    /// Reset the first launch flag (useful for testing or resetting the game)
    /// </summary>
    public void ResetFirstLaunchFlag()
    {
        PlayerPrefs.DeleteKey(firstTimePrefKey);
        PlayerPrefs.Save();
        hasShownFirstTimeSkyboxThisSession = false; // Also reset the session flag
        Debug.Log("[SkyboxRandomizer] First launch flag reset. Next launch will be treated as first time.");
    }
    
    /// <summary>
    /// Reset the session flag (so first-time skybox can show again when returning to title screen)
    /// </summary>
    public void ResetSessionFlag()
    {
        hasShownFirstTimeSkyboxThisSession = false;
        Debug.Log("[SkyboxRandomizer] Session flag reset. Next title screen load will show first-time skybox.");
    }
    
    /// <summary>
    /// Check if the first-time skybox has been shown this session
    /// </summary>
    public bool HasShownFirstTimeSkyboxThisSession()
    {
        return hasShownFirstTimeSkyboxThisSession;
    }

    /// <summary>
    /// Populates the rarity buckets from the simple skybox materials array.
    /// Distributes evenly: first third = common, second third = uncommon, last third = rare.
    /// </summary>
    public void PopulateFromSimpleArray()
    {
        if (skyboxMaterials == null || skyboxMaterials.Length == 0)
        {
            return;
        }

        // Distribute skyboxes across three buckets
        int totalCount = skyboxMaterials.Length;
        int commonCount = Mathf.CeilToInt(totalCount / 3f);
        int uncommonCount = Mathf.CeilToInt((totalCount - commonCount) / 2f);
        int rareCount = totalCount - commonCount - uncommonCount;

        commonSkyboxes = new Material[commonCount];
        uncommonSkyboxes = new Material[uncommonCount];
        rareSkyboxes = new Material[rareCount];

        int index = 0;
        
        // Fill common
        for (int i = 0; i < commonCount && index < totalCount; i++, index++)
        {
            commonSkyboxes[i] = skyboxMaterials[index];
        }
        
        // Fill uncommon
        for (int i = 0; i < uncommonCount && index < totalCount; i++, index++)
        {
            uncommonSkyboxes[i] = skyboxMaterials[index];
        }
        
        // Fill rare
        for (int i = 0; i < rareCount && index < totalCount; i++, index++)
        {
            rareSkyboxes[i] = skyboxMaterials[index];
        }
    }

    void Update()
    {
        if (enableDebugControls)
        {
            // Check for C key press or right stick click
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.JoystickButton11))
            {
                // Check if cycling is restricted to all levels unlocked
                if (requireAllLevelsUnlockedForCycling)
                {
                    if (AreAllLevelsUnlocked())
                    {
                        // All levels unlocked - allow cycling
                        RandomizeSkybox();
                    }
                    else
                    {
                        Debug.Log("[SkyboxRandomizer] C key cycling disabled until all levels are unlocked!");
                    }
                }
                else
                {
                    // No restriction - always allow cycling
                    RandomizeSkybox();
                }
            }
        }
    }

    /// <summary>
    /// Randomly selects and applies one of the skybox materials based on rarity buckets.
    /// </summary>
    public void RandomizeSkybox()
    {
        // Clear special skybox flag when manually randomizing
        isUsingSpecialSkybox = false;
        
        // Build a weighted list of all skyboxes
        List<Material> allSkyboxes = new List<Material>();
        List<float> weights = new List<float>();

        // Add common skyboxes (if enabled)
        if (enableCommon && commonSkyboxes != null)
        {
            foreach (Material mat in commonSkyboxes)
            {
                if (mat != null)
                {
                    allSkyboxes.Add(mat);
                    weights.Add(commonWeight);
                }
            }
        }

        // Add uncommon skyboxes (if enabled)
        if (enableUncommon && uncommonSkyboxes != null)
        {
            foreach (Material mat in uncommonSkyboxes)
            {
                if (mat != null)
                {
                    allSkyboxes.Add(mat);
                    weights.Add(uncommonWeight);
                }
            }
        }

        // Add rare skyboxes (if enabled)
        if (enableRare && rareSkyboxes != null)
        {
            foreach (Material mat in rareSkyboxes)
            {
                if (mat != null)
                {
                    allSkyboxes.Add(mat);
                    weights.Add(rareWeight);
                }
            }
        }

        if (allSkyboxes.Count == 0)
        {
// Debug.LogWarning("SkyboxRandomizer: No skyboxes found in any rarity bucket!");
            return;
        }

        // Calculate total weight
        float totalWeight = 0f;
        foreach (float weight in weights)
        {
            totalWeight += weight;
        }

        // Select random skybox based on weights
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < allSkyboxes.Count; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight)
            {
                Material selectedSkybox = allSkyboxes[i];
                lastSelectedSkybox = selectedSkybox;
                
                // Apply the skybox
                RenderSettings.skybox = selectedSkybox;
                DynamicGI.UpdateEnvironment();
                
                if (showWeightInfo)
                {
                    string rarity = GetRarityOfMaterial(selectedSkybox);
                    float probability = (weights[i] / totalWeight) * 100f;
                }
                
                return;
            }
        }
    }

    private string GetRarityOfMaterial(Material mat)
    {
        if (commonSkyboxes != null && System.Array.IndexOf(commonSkyboxes, mat) >= 0)
            return "COMMON";
        if (uncommonSkyboxes != null && System.Array.IndexOf(uncommonSkyboxes, mat) >= 0)
            return "UNCOMMON";
        if (rareSkyboxes != null && System.Array.IndexOf(rareSkyboxes, mat) >= 0)
            return "RARE";
        return "UNKNOWN";
    }

    /// <summary>
    /// Manually set a specific skybox by material reference.
    /// </summary>
    /// <param name="skybox">The skybox material to apply</param>
    public void SetSkybox(Material skybox)
    {
        if (skybox == null)
        {
// Debug.LogWarning("SkyboxRandomizer: Skybox material is null!");
            return;
        }

        RenderSettings.skybox = skybox;
        lastSelectedSkybox = skybox;
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Get the currently active skybox material.
    /// </summary>
    /// <returns>The current skybox material</returns>
    public Material GetCurrentSkybox()
    {
        return RenderSettings.skybox;
    }

    /// <summary>
    /// Get the total number of skyboxes across all rarity buckets.
    /// </summary>
    /// <returns>Total number of skybox materials</returns>
    public int GetSkyboxCount()
    {
        int count = 0;
        if (commonSkyboxes != null) count += commonSkyboxes.Length;
        if (uncommonSkyboxes != null) count += uncommonSkyboxes.Length;
        if (rareSkyboxes != null) count += rareSkyboxes.Length;
        return count;
    }

    /// <summary>
    /// Logs the weight distribution of all skyboxes across rarity buckets to the console.
    /// </summary>
    private void LogWeightDistribution()
    {
        int commonCount = (enableCommon && commonSkyboxes != null) ? System.Array.FindAll(commonSkyboxes, m => m != null).Length : 0;
        int uncommonCount = (enableUncommon && uncommonSkyboxes != null) ? System.Array.FindAll(uncommonSkyboxes, m => m != null).Length : 0;
        int rareCount = (enableRare && rareSkyboxes != null) ? System.Array.FindAll(rareSkyboxes, m => m != null).Length : 0;
        
        int totalCount = commonCount + uncommonCount + rareCount;
        
        if (totalCount == 0)
        {
// Debug.LogWarning("SkyboxRandomizer: No skyboxes enabled in any bucket!");
            return;
        }

        // Calculate total weight
        float totalWeight = (commonCount * commonWeight) + 
                           (uncommonCount * uncommonWeight) + 
                           (rareCount * rareWeight);


        // Log common skyboxes
        if (enableCommon && commonCount > 0)
        {
            float perSkyboxChance = (commonWeight / totalWeight) * 100f;
            float bucketChance = (commonCount * commonWeight / totalWeight) * 100f;
            
            if (commonSkyboxes != null)
            {
                foreach (Material mat in commonSkyboxes)
                {
                    // if (mat != null)
                }
            }
        }
        else if (!enableCommon && commonSkyboxes != null && commonSkyboxes.Length > 0)
        {
        }

        // Log uncommon skyboxes
        if (enableUncommon && uncommonCount > 0)
        {
            float perSkyboxChance = (uncommonWeight / totalWeight) * 100f;
            float bucketChance = (uncommonCount * uncommonWeight / totalWeight) * 100f;
            
            if (uncommonSkyboxes != null)
            {
                foreach (Material mat in uncommonSkyboxes)
                {
                    // if (mat != null)
                }
            }
        }
        else if (!enableUncommon && uncommonSkyboxes != null && uncommonSkyboxes.Length > 0)
        {
        }

        // Log rare skyboxes
        if (enableRare && rareCount > 0)
        {
            float perSkyboxChance = (rareWeight / totalWeight) * 100f;
            float bucketChance = (rareCount * rareWeight / totalWeight) * 100f;
            
            if (rareSkyboxes != null)
            {
                foreach (Material mat in rareSkyboxes)
                {
                    // if (mat != null)
                }
            }
        }
        else if (!enableRare && rareSkyboxes != null && rareSkyboxes.Length > 0)
        {
        }
    }
}