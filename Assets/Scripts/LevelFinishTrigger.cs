using UnityEngine;

/// <summary>
/// Attach this to the final platform (must have Rigidbody + Collider).
/// When the player touches this platform, the level completes.
/// Platform Rigidbody should be set to Kinematic so it doesn't fall.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class LevelFinishTrigger : MonoBehaviour
{
    [Header("--- REFERENCES ---")]
    [SerializeField] private TimerDisplayText timer;
    [SerializeField] private LevelFinishUI finishUI;
    
    [Header("--- AUDIO ---")]
    [Tooltip("Sound played when level is completed (2D)")]
    [SerializeField] private AudioClip levelCompleteSound;
    
    [Tooltip("Volume for level complete sound (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;
    
    [Header("--- DEBUG ---")]
    [SerializeField] private bool levelCompleted = false;
    
    [Header("--- Race Tracking ---")]
    private bool aiWonTheRace = false; // Track if AI beat the player in Race to Finish mode
    
    // Audio
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Reset the level completed flag
        levelCompleted = false;
        
        // Reset race tracking
        aiWonTheRace = false;
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.volume = soundVolume;
        
        // Ensure the rigidbody on this platform is kinematic (so it doesn't fall)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
// Debug.LogWarning("LevelFinishTrigger: Platform Rigidbody should be Kinematic! Setting it now.");
            rb.isKinematic = true;
        }
        
        // Cache references if not assigned - these are only called once at startup
        if (timer == null)
        {
            timer = FindFirstObjectByType<TimerDisplayText>();
            if (timer == null)
            {
// Debug.LogWarning("LevelFinishTrigger: No TimerDisplayText found in scene. Consider assigning it manually.");
            }
        }
        
        // Subscribe to timer expiration event
        if (timer != null)
        {
            timer.OnTimerExpired += OnTimerExpired;
        }
        
        if (finishUI == null)
        {
            finishUI = FindFirstObjectByType<LevelFinishUI>();
            if (finishUI == null)
            {
// Debug.LogWarning("LevelFinishTrigger: No LevelFinishUI found in scene. Consider assigning it manually.");
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from timer event to prevent memory leaks
        if (timer != null)
        {
            timer.OnTimerExpired -= OnTimerExpired;
        }
    }
    
    /// <summary>
    /// Called when the timer reaches 0
    /// </summary>
    private void OnTimerExpired()
    {
        CompleteLevel();
    }
    
    /// <summary>
    /// Detects when the player (or AI in Race to Finish mode) collides with this platform
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // Don't trigger multiple times
        if (levelCompleted) return;
        
        // Check if this is an AI ragdoll
        ActiveRagdoll.RagdollAIController aiController = collision.gameObject.GetComponent<ActiveRagdoll.RagdollAIController>();
        
        if (aiController == null)
        {
            // Try to get it from parent (in case collision is from a body part)
            aiController = collision.gameObject.GetComponentInParent<ActiveRagdoll.RagdollAIController>();
        }
        
        if (aiController != null)
        {
            // This is an AI ragdoll - check if it can trigger level complete
            bool canTriggerComplete = false;
            
            if (aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.RaceToFinish)
            {
                // Race to Finish mode - always triggers (player loses)
                canTriggerComplete = true;
                aiWonTheRace = true; // AI beat the player!
            }
            else if (aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.RedLightGreenLight)
            {
                // Red Light Green Light mode - AI reached finish first (player loses)
                canTriggerComplete = true;
                aiWonTheRace = true; // AI beat the player to the flag - ZERO points!
            }
            else if (aiController.currentMode == ActiveRagdoll.RagdollAIController.AIMode.PathMovement)
            {
                // Path Movement mode - only triggers if "End at Finish Trigger" is enabled
                if (aiController.pathEndAtFinishTrigger)
                {
                    canTriggerComplete = true;
                    aiWonTheRace = true; // AI reached finish first - player gets ZERO points
                }
                else
                {
                }
            }
            else
            {
            }
            
            if (canTriggerComplete)
            {
                CompleteLevel();
            }
        }
        else
        {
            // Not an AI ragdoll - assume it's the player or valid trigger
            aiWonTheRace = false; // Player reached first (or no race happening)
            
            // Check if this is the player and if race mode is active
            DefaultBehaviour playerBehaviour = collision.gameObject.GetComponent<DefaultBehaviour>();
            if (playerBehaviour == null)
            {
                playerBehaviour = collision.gameObject.GetComponentInParent<DefaultBehaviour>();
            }
            
            // Validate race completion if player has race mode enabled
            if (playerBehaviour != null && playerBehaviour.IsRaceMode)
            {
                // Player is in race mode - check if all laps are complete
                RaceLapManager lapManager = RaceLapManager.Instance;
                if (lapManager != null)
                {
                    if (lapManager.AllLapsComplete())
                    {
                        // All laps complete - allow finish!
                        CompleteLevel();
                    }
                    else
                    {
                        // Laps not complete - reject finish
                        // Don't complete the level
                        return;
                    }
                }
                else
                {
                    // Race mode enabled but no lap manager found - warn and allow finish anyway
                    // Debug.LogWarning("<color=orange>[Race Warning]</color> Player has race mode enabled but no RaceLapManager found in scene! Allowing finish anyway.");
                    CompleteLevel();
                }
            }
            else
            {
                // Not in race mode - complete normally
                CompleteLevel();
            }
        }
    }
    
    private void CompleteLevel()
    {
        levelCompleted = true;
        
        // Play level complete sound
        PlayLevelCompleteSound();
        
        // Get the points system singleton
        RagdollPointsSystem pointsSystem = RagdollPointsSystem.Instance;
        
        if (pointsSystem == null)
        {
// Debug.LogError("LevelFinishTrigger: RagdollPointsSystem.Instance is NULL! Cannot calculate physics score. Make sure there's a RagdollPointsSystem GameObject in the scene.");
        }
        // else
        // {
        // }
        
        // FIRST: Stop timer and freeze points
        if (timer != null)
        {
            timer.StopTimer();
        }
        
        if (pointsSystem != null)
        {
            pointsSystem.FreezePoints();
        }
        
        // THEN: Calculate final score (using frozen values)
        float physicsPoints = pointsSystem != null ? pointsSystem.CurrentPoints : 0f;
        float timeBonus = timer != null ? timer.CalculateTimeBonus() : 0f;
        float timeRemaining = timer != null ? timer.RemainingTime : 0f;
        float finalScore = physicsPoints + timeBonus;
        
        // CHECK: If AI won the race, set ALL scores to ZERO
        if (aiWonTheRace)
        {
            physicsPoints = 0f;
            timeBonus = 0f;
            finalScore = 0f;
            timeRemaining = 0f;
        }
        // CHECK: If Battle Royale Manager ran out of lives, set ALL scores to ZERO
        else if (BattleRoyaleManager.ForceZeroPointsOnFinish)
        {
            physicsPoints = 0f;
            timeBonus = 0f;
            finalScore = 0f;
            timeRemaining = 0f;
            Debug.Log("<color=red>[Level Finish]</color> Game Over - Out of lives! Score set to ZERO.");
        }
        else
        {
        }
        
        
        // Level complete - scores calculated
        
        // NOTIFY LEVEL GOAL SETTINGS (for achievement tracking)
        LevelGoalSettings levelGoals = LevelGoalSettings.Instance;
        if (levelGoals != null)
        {
            levelGoals.OnLevelComplete(physicsPoints, timeRemaining);
        }
        
        // Display final score
        if (finishUI != null)
        {
            Debug.Log($"<color=green>[LevelFinishTrigger]</color> Showing level finish UI - Score: {finalScore:F0}");
            finishUI.ShowFinalScore(finalScore, physicsPoints, timeBonus);
        }
        else
        {
            Debug.LogWarning("<color=red>[LevelFinishTrigger]</color> finishUI is NULL - cannot show level finish UI!");
        }
        
        // Optional: Add time bonus to the points system total (only if player won)
        if (pointsSystem != null && timer != null && !aiWonTheRace)
        {
            pointsSystem.AddPoints(timeBonus);
        }
        
        // FINALLY: Enable permanent ragdoll/datamosh mode
        EnablePermanentRagdollMode();
        
        // AND: Disable all respawn triggers so player can't die
        RespawnTrigger.DisableAllRespawnTriggers();
    }
    
    /// <summary>
    /// Enable permanent ragdoll mode on the player character(s)
    /// </summary>
    private void EnablePermanentRagdollMode()
    {
        Debug.Log("<color=yellow>[LevelFinishTrigger]</color> EnablePermanentRagdollMode() called - searching for player(s)...");
        
        // IMPORTANT: Find ALL PLAYERS (supports multiplayer)
        // We do this by finding all RespawnablePlayer instances (which only exist on players, not AI)
        RespawnablePlayer[] respawnablePlayers = FindObjectsByType<RespawnablePlayer>(FindObjectsSortMode.None);
        
        if (respawnablePlayers.Length > 0)
        {
            Debug.Log($"<color=green>[LevelFinishTrigger]</color> Found {respawnablePlayers.Length} player(s) - applying permanent ragdoll mode to all");
            
            foreach (RespawnablePlayer respawnablePlayer in respawnablePlayers)
            {
                Debug.Log($"<color=green>[LevelFinishTrigger]</color> Processing player: {respawnablePlayer.gameObject.name}");
                
                // Try DefaultBehaviour first (regular levels)
                DefaultBehaviour defaultBehaviour = respawnablePlayer.GetComponent<DefaultBehaviour>();
                
                if (defaultBehaviour != null)
                {
                    Debug.Log("<color=cyan>[LevelFinishTrigger]</color> Found DefaultBehaviour - calling EnablePermanentRagdollMode()");
                    defaultBehaviour.EnablePermanentRagdollMode();
                }
                else
                {
                    // Try TitlePlayerDefaultBehavior (2.5D levels like Flat Land)
                    TitlePlayerDefaultBehavior titleBehaviour = respawnablePlayer.GetComponent<TitlePlayerDefaultBehavior>();
                    
                    if (titleBehaviour != null)
                    {
                        Debug.Log("<color=cyan>[LevelFinishTrigger]</color> Found TitlePlayerDefaultBehavior - calling EnablePermanentRagdollMode()");
                        titleBehaviour.EnablePermanentRagdollMode();
                    }
                    else
                    {
                        Debug.LogWarning("<color=red>[LevelFinishTrigger]</color> Player found but NO behaviour component (DefaultBehaviour or TitlePlayerDefaultBehavior)!");
                    }
                }
                
                // Disable manual respawn (Q/R1) after level completion
                respawnablePlayer.allowManualRespawn = false;
            }
        }
        else
        {
            Debug.LogWarning("<color=red>[LevelFinishTrigger]</color> Could not find any RespawnablePlayer (player characters) to enable ragdoll mode!");
        }
        
        // Disable pause menu (P/Start) after level completion
        PauseMenuManager pauseMenuManager = FindFirstObjectByType<PauseMenuManager>();
        if (pauseMenuManager != null)
        {
            pauseMenuManager.DisablePauseMenu();
        }
    }
    
    // ==================== AUDIO METHODS ====================
    
    /// <summary>
    /// Play level complete sound (2D)
    /// </summary>
    private void PlayLevelCompleteSound()
    {
        if (levelCompleteSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(levelCompleteSound, soundVolume);
        }
    }
}

