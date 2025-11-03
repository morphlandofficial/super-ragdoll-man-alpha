using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Singleton manager that tracks lap progress for race mode.
/// Automatically detects all LapCheckpoint components in the scene.
/// </summary>
public class RaceLapManager : MonoBehaviour
{
    [Header("Race Configuration (Auto-Detected)")]
    [SerializeField] 
    [Tooltip("Total number of laps (AUTO-DETECTED from LapCheckpoint count in scene) - READ ONLY")]
    private int totalLaps = 0;
    
    [Header("Debug Info")]
    [SerializeField] 
    [Tooltip("Current lap the player is on (read-only)")]
    private int currentLap = 0;
    
    [SerializeField]
    [Tooltip("Next checkpoint the player must cross (read-only)")]
    private int nextCheckpointNumber = 1;
    
    [SerializeField]
    [Tooltip("Is the race complete? (read-only)")]
    private bool raceComplete = false;
    
    [SerializeField]
    [Tooltip("Number of checkpoints found in scene (read-only)")]
    private int checkpointsFound = 0;
    
    [Header("Persistence Settings")]
    [Tooltip("Lap progress persists through respawns (binary - once completed, stays completed)")]
    [SerializeField] private bool persistProgressThroughRespawns = true;
    
    [Header("Audio Settings")]
    [Tooltip("Sound played when the race starts (2D)")]
    [SerializeField] private AudioClip raceStartSound;
    
    [Tooltip("Sound played when completing a lap checkpoint (2D)")]
    [SerializeField] private AudioClip checkpointSound;
    
    [Tooltip("Sound played when completing the final lap (2D)")]
    [SerializeField] private AudioClip finalCheckpointSound;
    
    [Tooltip("Volume for race sounds (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;
    
    // Audio
    private AudioSource audioSource;
    
    // Singleton instance
    public static RaceLapManager Instance { get; private set; }
    
    // Track all checkpoints in the scene
    private List<LapCheckpoint> checkpoints = new List<LapCheckpoint>();
    
    // Events for lap changes
    public delegate void LapChangedHandler(int currentLap, int totalLaps);
    public event LapChangedHandler OnLapChanged;
    
    public delegate void RaceCompletedHandler();
    public event RaceCompletedHandler OnRaceCompleted;
    
    // Public properties
    public int CurrentLap => currentLap;
    public int TotalLaps => totalLaps;
    public bool IsRaceComplete => raceComplete;
    public int TotalCheckpoints => checkpoints.Count;
    
    private void Awake()
    {
        // Setup singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            // Debug.LogWarning("<color=yellow>[Race Lap Manager]</color> Multiple RaceLapManager instances found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
        audioSource.volume = soundVolume;
        
        // AUTO-DETECT: Find all LapCheckpoint objects in the scene (including inactive)
        
        LapCheckpoint[] existingCheckpoints = FindObjectsByType<LapCheckpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var checkpoint in existingCheckpoints)
        {
            RegisterCheckpoint(checkpoint);
        }
        
        // Update checkpoint count for inspector
        checkpointsFound = checkpoints.Count;
        
        // AUTO-DETECT: Set total laps from checkpoint count
        if (checkpoints.Count > 0)
        {
            totalLaps = checkpoints.Count;
            
            // Verify sequential lap numbers
            bool hasValidSequence = ValidateLapSequence();
            
            if (hasValidSequence)
            {
            }
            else
            {
                // Debug.LogWarning($"<color=orange>[Race Lap Manager]</color> ⚠ AUTO-DETECTED {totalLaps} lap(s) but sequence may have gaps! Check lap numbers.");
            }
        }
        else
        {
            totalLaps = 0;
            // Debug.LogWarning("<color=red>[Race Lap Manager]</color> ⚠ NO LapCheckpoint objects found in scene! Add LapCheckpoint components to set up race.");
        }
        
        // Start at lap 0 (race hasn't started)
        currentLap = 0;
        nextCheckpointNumber = 1;
        
        // Play race start sound to signify the race has begun
        PlayRaceStartSound();
    }
    
    /// <summary>
    /// Validate that lap numbers form a proper sequence (1, 2, 3, etc.)
    /// </summary>
    private bool ValidateLapSequence()
    {
        if (checkpoints.Count == 0) return false;
        
        // Check that we have lap numbers 1 through totalLaps
        for (int i = 1; i <= totalLaps; i++)
        {
            bool found = false;
            foreach (var checkpoint in checkpoints)
            {
                if (checkpoint.LapNumber == i)
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                // Debug.LogWarning($"<color=orange>[Race Lap Manager]</color> Missing Lap {i} checkpoint! Lap sequence has gaps.");
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Register a checkpoint with the manager (called by LapCheckpoint.Awake)
    /// </summary>
    public void RegisterCheckpoint(LapCheckpoint checkpoint)
    {
        if (checkpoint == null) return;
        
        if (!checkpoints.Contains(checkpoint))
        {
            checkpoints.Add(checkpoint);
            
            // Update count
            checkpointsFound = checkpoints.Count;
        }
    }
    
    /// <summary>
    /// Called when the player crosses a checkpoint
    /// Returns true if the checkpoint was successfully completed (for triggering events)
    /// </summary>
    public bool PlayerCrossedCheckpoint(LapCheckpoint checkpoint)
    {
        if (checkpoint == null) return false;
        if (raceComplete) return false; // Race already finished
        
        int checkpointLapNumber = checkpoint.LapNumber;
        
        // STRICT SEQUENTIAL VALIDATION: Must cross checkpoints in exact order
        if (checkpointLapNumber == nextCheckpointNumber)
        {
            // Valid checkpoint crossed! This is the next required checkpoint.
            
            // Update current lap (BINARY - once set, never resets unless manually cleared)
            currentLap = checkpointLapNumber;
            
            // Notify listeners
            OnLapChanged?.Invoke(currentLap, totalLaps);
            
            // Check if this was the final lap
            if (currentLap >= totalLaps)
            {
                CompleteRace();
            }
            else
            {
                // Set next checkpoint (sequential progression)
                nextCheckpointNumber = checkpointLapNumber + 1;
                
                // Play checkpoint sound (not final lap)
                PlayCheckpointSound();
            }
            
            return true; // Successfully completed - trigger events
        }
        else if (checkpointLapNumber < nextCheckpointNumber)
        {
            // Player crossed an already-completed checkpoint (going backwards or re-crossing)
            return false; // Don't trigger events for already-completed laps
        }
        else
        {
            // Player tried to skip ahead! (e.g., crossing Lap 3 before Lap 1)
            // Debug.LogWarning($"<color=red>[Lap Progress - REJECTED]</color> Player crossed Lap {checkpointLapNumber} but must complete Lap {nextCheckpointNumber} FIRST! (Sequential order required)");
            return false; // Don't trigger events for skipped laps
        }
    }
    
    /// <summary>
    /// Mark the race as complete
    /// </summary>
    private void CompleteRace()
    {
        if (raceComplete) return;
        
        raceComplete = true;
        
        // Play final checkpoint sound
        PlayFinalCheckpointSound();
        
        // Notify listeners
        OnRaceCompleted?.Invoke();
    }
    
    /// <summary>
    /// Check if all required laps are complete
    /// </summary>
    public bool AllLapsComplete()
    {
        return raceComplete;
    }
    
    /// <summary>
    /// Reset the race progress (ONLY use for full level restart - NOT for respawns!)
    /// </summary>
    public void ResetRace()
    {
        // Only reset if persistence is disabled, otherwise lap progress is permanent
        if (!persistProgressThroughRespawns)
        {
            currentLap = 0;
            nextCheckpointNumber = 1;
            raceComplete = false;
            
            
            // Notify listeners of reset
            OnLapChanged?.Invoke(currentLap, totalLaps);
        }
        else
        {
        }
    }
    
    /// <summary>
    /// Force reset the race (ignores persistence setting - use for full level restart)
    /// </summary>
    public void ForceResetRace()
    {
        currentLap = 0;
        nextCheckpointNumber = 1;
        raceComplete = false;
        
        
        // Notify listeners of reset
        OnLapChanged?.Invoke(currentLap, totalLaps);
    }
    
    // ==================== AUDIO METHODS ====================
    
    /// <summary>
    /// Play checkpoint completion sound (2D)
    /// </summary>
    private void PlayCheckpointSound()
    {
        if (checkpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(checkpointSound, soundVolume);
        }
    }
    
    /// <summary>
    /// Play final checkpoint completion sound (2D)
    /// </summary>
    private void PlayFinalCheckpointSound()
    {
        if (finalCheckpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(finalCheckpointSound, soundVolume);
        }
    }
    
    /// <summary>
    /// Play race start sound (2D)
    /// </summary>
    private void PlayRaceStartSound()
    {
        if (raceStartSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(raceStartSound, soundVolume);
        }
    }
}

