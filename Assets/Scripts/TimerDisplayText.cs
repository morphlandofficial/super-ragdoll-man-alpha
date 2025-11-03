using UnityEngine;
using TMPro; // Remove this line if using regular UI.Text

/// <summary>
/// Attach this to a TextMeshPro component to display a countdown timer.
/// </summary>
[RequireComponent(typeof(TMP_Text))] // Change to Text if using regular UI
public class TimerDisplayText : MonoBehaviour
{
    [Header("--- COUNTDOWN SETTINGS ---")]
    [SerializeField] private float countdownSeconds = 60f;
    [Tooltip("Total seconds to countdown from")]
    
    [Header("--- BONUS POINTS ---")]
    [SerializeField] private float pointsPerSecond = 0.25f;
    [Tooltip("Points awarded for each second remaining when level completes")]
    
    [Header("--- DISPLAY FORMAT ---")]
    [SerializeField] private string displayFormat = "{0:00}:{1:00}";
    [Tooltip("Use {0} for minutes, {1} for seconds. Example: '00:00' or 'Time: {0}m {1}s'")]
    
    [SerializeField] private bool showMilliseconds = false;
    [SerializeField] private string displayFormatWithMilliseconds = "{0:00}:{1:00}.{2:00}";
    [Tooltip("Use {0} for minutes, {1} for seconds, {2} for centiseconds (1/100th of second)")]
    
    private TMP_Text textComponent; // Change to Text if using regular UI
    private float startTime;
    private float remainingTime;
    private bool isStopped = false;
    private bool hasExpiredEventFired = false;
    
    // Public properties
    public float RemainingTime => remainingTime;
    public bool HasTimeExpired => remainingTime <= 0f;
    public bool IsStopped => isStopped;
    
    // Event fired when timer reaches 0
    public event System.Action OnTimerExpired;
    
    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>(); // Change to GetComponent<Text>() if using regular UI
        startTime = Time.time;
        remainingTime = countdownSeconds;
        
        if (textComponent == null)
        {
// Debug.LogError("TimerDisplayText: No TextMeshPro component found!");
        }
        
        // Check if there's a CollisionToggleTrigger that will start this timer
        CheckForTimerTrigger();
    }
    
    private void CheckForTimerTrigger()
    {
        // Find all CollisionToggleTriggers in the scene
        CollisionToggleTrigger[] triggers = FindObjectsByType<CollisionToggleTrigger>(FindObjectsSortMode.None);
        
        foreach (var trigger in triggers)
        {
            // Check if this trigger is set to start the timer
            if (trigger.IsTimerControlEnabled() && trigger.GetTimerControlMode() == CollisionToggleTrigger.TimerControlMode.StartTimer)
            {
                // Start the timer in stopped state
                isStopped = true;
                return; // Only need to find one trigger that starts the timer
            }
        }
    }
    
    private void Update()
    {
        if (textComponent != null)
        {
            if (!isStopped)
            {
                float elapsedTime = Time.time - startTime;
                remainingTime = Mathf.Max(0f, countdownSeconds - elapsedTime); // Stops at 0
            }
            
            // Always update display (even when stopped, so we can see the starting time)
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            
            if (showMilliseconds)
            {
                int centiseconds = Mathf.FloorToInt((remainingTime * 100f) % 100f);
                textComponent.text = string.Format(displayFormatWithMilliseconds, minutes, seconds, centiseconds);
            }
            else
            {
                textComponent.text = string.Format(displayFormat, minutes, seconds);
            }
            
            // Check if timer has reached 0 and fire event once
            if (remainingTime <= 0f && !hasExpiredEventFired && !isStopped)
            {
                hasExpiredEventFired = true;
                OnTimerExpired?.Invoke();
            }
        }
    }
    
    /// <summary>
    /// Stop the timer and freeze at current time
    /// </summary>
    public void StopTimer()
    {
        isStopped = true;
    }
    
    /// <summary>
    /// Restart the timer from the beginning
    /// </summary>
    public void RestartTimer()
    {
        startTime = Time.time;
        remainingTime = countdownSeconds;
        isStopped = false;
        hasExpiredEventFired = false;
    }
    
    /// <summary>
    /// Calculate bonus points based on remaining time
    /// </summary>
    public float CalculateTimeBonus()
    {
        return remainingTime * pointsPerSecond;
    }
    
    /// <summary>
    /// Add time to the countdown timer (for power-ups, bounce pads, etc.)
    /// </summary>
    /// <param name="seconds">Seconds to add (can be negative to subtract)</param>
    public void AddTime(float seconds)
    {
        if (isStopped)
        {
// Debug.LogWarning("TimerDisplayText: Cannot add time - timer is stopped!");
            return;
        }
        
        // Adjust the start time to effectively add more time remaining
        // Adding to startTime means the elapsed time will be less, giving more remaining time
        startTime += seconds;
        
        // Ensure timer doesn't go negative
        float elapsedTime = Time.time - startTime;
        float newRemainingTime = countdownSeconds - elapsedTime;
        
        if (newRemainingTime < 0)
        {
            // If adding time would make it negative, just set remaining to 0
            startTime = Time.time - countdownSeconds;
        }
        
    }
}

