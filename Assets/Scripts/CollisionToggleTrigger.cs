using UnityEngine;

/// <summary>
/// Collision trigger that can activate/deactivate game objects.
/// Add this to any object with a Rigidbody and Collider to make it a trigger.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CollisionToggleTrigger : MonoBehaviour
{
    [Header("Game Objects to Activate")]
    [Tooltip("Game objects to set active (SetActive(true)) when player touches this")]
    [SerializeField] private GameObject[] objectsToActivate;
    
    [Header("Game Objects to Deactivate")]
    [Tooltip("Game objects to set inactive (SetActive(false)) when player touches this")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    
    [Header("Trigger Settings")]
    [Tooltip("Only trigger once, then disable this component")]
    [SerializeField] private bool oneShot = true;
    
    [Tooltip("Delay before executing actions (seconds)")]
    [SerializeField] private float actionDelay = 0f;
    
    [Header("Audio")]
    [SerializeField]
    [Tooltip("Sound to play when triggered (optional)")]
    private AudioClip triggerSound;
    
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Volume of the trigger sound")]
    private float triggerSoundVolume = 0.8f;
    
    [Header("Timer Control")]
    [Tooltip("Enable automatic timer control when triggered")]
    [SerializeField] private bool enableTimerControl = false;
    
    [Tooltip("Timer control mode - choose one action to perform when triggered")]
    [SerializeField] private TimerControlMode timerControlMode = TimerControlMode.None;
    
    [Tooltip("Reference to the TimerDisplayText component to control (leave empty to auto-find in scene)")]
    [SerializeField] private TimerDisplayText timerToControl;
    
    [Header("Points System Control")]
    [Tooltip("Enable automatic points system control when triggered")]
    [SerializeField] private bool enablePointsControl = false;
    
    [Tooltip("Points control mode - choose one action to perform when triggered")]
    [SerializeField] private PointsControlMode pointsControlMode = PointsControlMode.None;
    
    // [Header("Debug")]
    // [SerializeField]
    // private bool showDebugMessages = false; // Unused after debug log cleanup // Set to false for release
    
    // Timer control options
    public enum TimerControlMode
    {
        None,           // No timer control
        StartTimer,     // Start timer and let it run forever
        StopTimer       // Stop timer at current time
    }
    
    // Points control options
    public enum PointsControlMode
    {
        None,                  // No points control
        StartPointsTracking,   // Start/resume points accumulation
        StopPointsTracking     // Stop/freeze points accumulation
    }
    
    // Internal state
    private bool hasBeenTriggered = false;
    private bool timerActionExecuted = false; // Track if timer action was already executed (one-use only)
    private bool pointsActionExecuted = false; // Track if points action was already executed (one-use only)
    private AudioSource audioSource;
    private RagdollPointsSystem pointsSystem; // Cached reference to points system
    
    private void Awake()
    {
        // Ensure rigidbody is kinematic (so the trigger doesn't fall)
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.isKinematic = true;
            // if (false) // showDebugMessages
        }
        
        // Setup audio source if trigger sound is assigned
        if (triggerSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.clip = triggerSound;
            audioSource.volume = triggerSoundVolume;
        }
        
        // Auto-find timer in scene if not manually assigned and timer control is enabled
        if (enableTimerControl && timerToControl == null && timerControlMode != TimerControlMode.None)
        {
            timerToControl = FindFirstObjectByType<TimerDisplayText>();
            if (timerToControl != null && false) // showDebugMessages
            {
            }
            else if (timerToControl == null)
            {
                // Debug.LogWarning($"CollisionToggleTrigger on {gameObject.name}: Timer control is enabled but no TimerDisplayText found in scene!");
            }
        }
        
        // Auto-find points system via singleton if points control is enabled
        if (enablePointsControl && pointsControlMode != PointsControlMode.None)
        {
            pointsSystem = RagdollPointsSystem.Instance;
            if (pointsSystem != null && false) // showDebugMessages
            {
            }
            else if (pointsSystem == null)
            {
                // Debug.LogWarning($"CollisionToggleTrigger on {gameObject.name}: Points control is enabled but no RagdollPointsSystem found in scene!");
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Only trigger once if one-shot enabled
        if (oneShot && hasBeenTriggered)
            return;
        
        // Try to find the player
        Rigidbody collidedRigidbody = collision.rigidbody;
        if (collidedRigidbody == null)
            return;
        
        // Check if this is part of the player's ragdoll
        DefaultBehaviour playerBehaviour = collidedRigidbody.GetComponentInParent<DefaultBehaviour>();
        if (playerBehaviour == null)
        {
            // Not the player
            return;
        }
        
        // TRIGGER IT!
        if (actionDelay > 0)
        {
            StartCoroutine(TriggerAfterDelay());
        }
        else
        {
            ExecuteTrigger();
        }
        
        hasBeenTriggered = true;
    }
    
    private System.Collections.IEnumerator TriggerAfterDelay()
    {
        yield return new WaitForSeconds(actionDelay);
        ExecuteTrigger();
    }
    
    private void ExecuteTrigger()
    {
        // Play audio
        if (audioSource != null)
        {
            audioSource.Play();
        }
        
        // Execute timer control (one-use only)
        if (enableTimerControl && !timerActionExecuted && timerToControl != null && timerControlMode != TimerControlMode.None)
        {
            switch (timerControlMode)
            {
                case TimerControlMode.StartTimer:
                    // Start timer by resetting it (restart from beginning)
                    timerToControl.RestartTimer();
                    break;
                    
                case TimerControlMode.StopTimer:
                    // Stop timer at current time
                    timerToControl.StopTimer();
                    break;
            }
            timerActionExecuted = true; // Mark as executed (one-use only)
        }
        
        // Execute points control (one-use only)
        if (enablePointsControl && !pointsActionExecuted && pointsSystem != null && pointsControlMode != PointsControlMode.None)
        {
            switch (pointsControlMode)
            {
                case PointsControlMode.StartPointsTracking:
                    pointsSystem.UnfreezePoints();
                    break;
                    
                case PointsControlMode.StopPointsTracking:
                    pointsSystem.FreezePoints();
                    break;
            }
            pointsActionExecuted = true; // Mark as executed (one-use only)
        }
        
        // Activate game objects
        if (objectsToActivate != null && objectsToActivate.Length > 0)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    // if (false) // showDebugMessages

                }
            }
        }
        
        // Deactivate game objects
        if (objectsToDeactivate != null && objectsToDeactivate.Length > 0)
        {
            foreach (GameObject obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    // if (false) // showDebugMessages

                }
            }
        }
        
        if (false) // showDebugMessages
        {
        }
        
        // Disable this component if one-shot
        if (oneShot)
        {
            enabled = false;
        }
    }
    
    /// <summary>
    /// Manually reset the trigger to allow it to be triggered again
    /// </summary>
    public void ResetTrigger()
    {
        hasBeenTriggered = false;
        timerActionExecuted = false; // Reset timer action as well
        pointsActionExecuted = false; // Reset points action as well
        enabled = true;
    }
    
    /// <summary>
    /// Manually execute the trigger actions without collision
    /// </summary>
    public void ManualTrigger()
    {
        if (oneShot && hasBeenTriggered)
        {
// Debug.LogWarning($"CollisionToggleTrigger on {gameObject.name}: Already triggered and one-shot is enabled!");
            return;
        }
        
        ExecuteTrigger();
        hasBeenTriggered = true;
    }
    
    /// <summary>
    /// Check if timer control is enabled (for external systems to query)
    /// </summary>
    public bool IsTimerControlEnabled()
    {
        return enableTimerControl;
    }
    
    /// <summary>
    /// Get the timer control mode (for external systems to query)
    /// </summary>
    public TimerControlMode GetTimerControlMode()
    {
        return timerControlMode;
    }
    
    /// <summary>
    /// Check if points control is enabled (for external systems to query)
    /// </summary>
    public bool IsPointsControlEnabled()
    {
        return enablePointsControl;
    }
    
    /// <summary>
    /// Get the points control mode (for external systems to query)
    /// </summary>
    public PointsControlMode GetPointsControlMode()
    {
        return pointsControlMode;
    }
}

