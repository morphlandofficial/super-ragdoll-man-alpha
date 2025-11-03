using UnityEngine;

/// <summary>
/// Per-level achievement configuration and goal tracking.
/// Place one of these in each gameplay level scene.
/// Communicates with LevelManager when level is completed.
/// </summary>
public class LevelGoalSettings : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    // LEVEL IDENTIFICATION
    // ═══════════════════════════════════════════════════════
    
    [Header("--- LEVEL ID ---")]
    [Tooltip("Leave empty to auto-detect from scene name")]
    [SerializeField] private string levelID = "";
    
    // ═══════════════════════════════════════════════════════
    // GOAL TYPE
    // ═══════════════════════════════════════════════════════
    
    [Header("--- GOAL TYPE ---")]
    [Tooltip("What metric is used for achievements?")]
    [SerializeField] private GoalType goalType = GoalType.PhysicsPoints;
    
    // ═══════════════════════════════════════════════════════
    // ACHIEVEMENT THRESHOLDS
    // ═══════════════════════════════════════════════════════
    
    [Header("--- PHYSICS POINTS THRESHOLDS ---")]
    [Tooltip("Points required for Bronze (only for PhysicsPoints or Both goal types)")]
    [SerializeField] private float bronzePhysicsThreshold = 1000f;
    
    [Tooltip("Points required for Silver")]
    [SerializeField] private float silverPhysicsThreshold = 3000f;
    
    [Tooltip("Points required for Gold")]
    [SerializeField] private float goldPhysicsThreshold = 5000f;
    
    [Header("--- TIME THRESHOLDS ---")]
    [Tooltip("Seconds required for Bronze (only for Time or Both goal types)")]
    [SerializeField] private float bronzeTimeThreshold = 120f;
    
    [Tooltip("Seconds required for Silver")]
    [SerializeField] private float silverTimeThreshold = 90f;
    
    [Tooltip("Seconds required for Gold")]
    [SerializeField] private float goldTimeThreshold = 60f;
    
    // ═══════════════════════════════════════════════════════
    // LEVEL UNLOCKING (Achievement-Based)
    // ═══════════════════════════════════════════════════════
    
    [Header("--- LEVEL UNLOCKING ---")]
    [Tooltip("Drag scene assets from Project window to unlock at Bronze")]
    [SerializeField] private Object[] unlockScenesAtBronze = new Object[0];
    
    [Tooltip("Drag scene assets from Project window to unlock at Silver")]
    [SerializeField] private Object[] unlockScenesAtSilver = new Object[0];
    
    [Tooltip("Drag scene assets from Project window to unlock at Gold")]
    [SerializeField] private Object[] unlockScenesAtGold = new Object[0];
    
    // ═══════════════════════════════════════════════════════
    // DEBUG INFO
    // ═══════════════════════════════════════════════════════
    
    [Header("--- DEBUG INFO (Read-Only) ---")]
    [SerializeField] private float lastPhysicsScore = 0f;
    [SerializeField] private float lastTimeScore = 0f;
    [SerializeField] private Achievement lastAchievement = Achievement.None;
    
    // ═══════════════════════════════════════════════════════
    // SINGLETON (Scene-Specific)
    // ═══════════════════════════════════════════════════════
    
    public static LevelGoalSettings Instance { get; private set; }
    
    // ═══════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════
    
    void Awake()
    {
        // Scene-specific singleton (one per level)
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Auto-detect level ID from scene name if empty
        if (string.IsNullOrEmpty(levelID))
        {
            levelID = LevelManager.GetCurrentLevelID();
        }
    }
    
    // ═══════════════════════════════════════════════════════
    // LEVEL COMPLETION HANDLING
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Called by LevelFinishTrigger when level is completed.
    /// Evaluates achievement and sends data to LevelManager.
    /// </summary>
    public void OnLevelComplete(float physicsPoints, float timeRemaining)
    {
        // Store for debug display
        lastPhysicsScore = physicsPoints;
        lastTimeScore = timeRemaining;
        
        // Evaluate achievement based on goal type
        Achievement achievement = EvaluateAchievement(physicsPoints, timeRemaining);
        lastAchievement = achievement;
        
        // Send to LevelManager (if it exists)
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.UpdateLevelProgress(levelID, physicsPoints, timeRemaining, achievement);
            
            // AUTOMATIC LEVEL UNLOCKING based on achievement
            UnlockLevelsBasedOnAchievement(achievement);
        }
        else
        {
            Debug.LogWarning("[LevelGoalSettings] LevelManager not found! Progress will not be saved. Make sure LevelManager exists in the title screen scene.");
        }
    }
    
    /// <summary>
    /// Unlock levels based on achievement earned
    /// </summary>
    private void UnlockLevelsBasedOnAchievement(Achievement achievement)
    {
        if (LevelManager.Instance == null) return;
        
        Debug.Log($"[LevelGoalSettings] 🎯 Achievement earned: {achievement}");
        Debug.Log($"[LevelGoalSettings] Bronze array size: {unlockScenesAtBronze.Length}");
        Debug.Log($"[LevelGoalSettings] Silver array size: {unlockScenesAtSilver.Length}");
        Debug.Log($"[LevelGoalSettings] Gold array size: {unlockScenesAtGold.Length}");
        
        // Unlock levels at Gold (if specified)
        if (achievement >= Achievement.Gold && unlockScenesAtGold.Length > 0)
        {
            Debug.Log($"[LevelGoalSettings] Processing GOLD unlocks...");
            foreach (Object sceneAsset in unlockScenesAtGold)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    Debug.Log($"[LevelGoalSettings] 🏆 GOLD - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock levels at Silver (if specified)
        if (achievement >= Achievement.Silver && unlockScenesAtSilver.Length > 0)
        {
            Debug.Log($"[LevelGoalSettings] Processing SILVER unlocks...");
            foreach (Object sceneAsset in unlockScenesAtSilver)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    Debug.Log($"[LevelGoalSettings] 🥈 SILVER - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
            }
        }
        
        // Unlock levels at Bronze (if specified)
        if (achievement >= Achievement.Bronze && unlockScenesAtBronze.Length > 0)
        {
            Debug.Log($"[LevelGoalSettings] Processing BRONZE unlocks...");
            foreach (Object sceneAsset in unlockScenesAtBronze)
            {
                if (sceneAsset != null)
                {
                    string sceneName = sceneAsset.name;
                    Debug.Log($"[LevelGoalSettings] 🥉 BRONZE - Unlocking: {sceneName}");
                    LevelManager.Instance.UnlockLevel(sceneName);
                }
                else
                {
                    Debug.LogWarning($"[LevelGoalSettings] NULL scene asset in Bronze array!");
                }
            }
        }
        
        Debug.Log($"[LevelGoalSettings] ✅ Finished processing achievement unlocks.");
    }
    
    // ═══════════════════════════════════════════════════════
    // ACHIEVEMENT EVALUATION
    // ═══════════════════════════════════════════════════════
    
    /// <summary>
    /// Evaluate achievement based on goal type and thresholds
    /// </summary>
    private Achievement EvaluateAchievement(float physicsPoints, float timeRemaining)
    {
        switch (goalType)
        {
            case GoalType.PhysicsPoints:
                return EvaluatePhysicsAchievement(physicsPoints);
                
            case GoalType.Time:
                return EvaluateTimeAchievement(timeRemaining);
                
            case GoalType.Both:
                // For "Both", player must meet BOTH thresholds to get the achievement
                Achievement physicsAch = EvaluatePhysicsAchievement(physicsPoints);
                Achievement timeAch = EvaluateTimeAchievement(timeRemaining);
                // Return the lower of the two (must meet both to advance)
                return (Achievement)Mathf.Min((int)physicsAch, (int)timeAch);
                
            default:
                return Achievement.None;
        }
    }
    
    /// <summary>
    /// Evaluate achievement based on physics points (higher is better)
    /// </summary>
    private Achievement EvaluatePhysicsAchievement(float physicsPoints)
    {
        if (physicsPoints >= goldPhysicsThreshold)
            return Achievement.Gold;
        else if (physicsPoints >= silverPhysicsThreshold)
            return Achievement.Silver;
        else if (physicsPoints >= bronzePhysicsThreshold)
            return Achievement.Bronze;
        else
            return Achievement.None;
    }
    
    /// <summary>
    /// Evaluate achievement based on time (lower is better)
    /// </summary>
    private Achievement EvaluateTimeAchievement(float timeRemaining)
    {
        // For time challenges, we want HIGHER time remaining (faster completion)
        // So thresholds are: Gold = most time remaining, Bronze = least time remaining
        
        if (timeRemaining >= goldTimeThreshold)
            return Achievement.Gold;
        else if (timeRemaining >= silverTimeThreshold)
            return Achievement.Silver;
        else if (timeRemaining >= bronzeTimeThreshold)
            return Achievement.Bronze;
        else
            return Achievement.None;
    }
    
    // ═══════════════════════════════════════════════════════
    // PUBLIC API (For UI / Debugging)
    // ═══════════════════════════════════════════════════════
    
    public GoalType GetGoalType() => goalType;
    public string GetLevelID() => levelID;
    public Achievement GetLastAchievement() => lastAchievement;
    
    /// <summary>
    /// Get achievement thresholds for display (UI use)
    /// </summary>
    public void GetPhysicsThresholds(out float bronze, out float silver, out float gold)
    {
        bronze = bronzePhysicsThreshold;
        silver = silverPhysicsThreshold;
        gold = goldPhysicsThreshold;
    }
    
    /// <summary>
    /// Get time thresholds for display (UI use)
    /// </summary>
    public void GetTimeThresholds(out float bronze, out float silver, out float gold)
    {
        bronze = bronzeTimeThreshold;
        silver = silverTimeThreshold;
        gold = goldTimeThreshold;
    }
}

// ═══════════════════════════════════════════════════════
// GOAL TYPE ENUM
// ═══════════════════════════════════════════════════════

/// <summary>
/// What type of goal this level uses for achievements
/// </summary>
public enum GoalType
{
    PhysicsPoints,  // Score-based (e.g., 5000 points for gold)
    Time,           // Time-based (e.g., finish with 60s remaining for gold)
    Both            // Hybrid - must meet BOTH physics AND time thresholds
}

