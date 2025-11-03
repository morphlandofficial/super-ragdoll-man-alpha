using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Simple UI display for lap progress.
/// Shows "Lap X/Y" text that updates automatically.
/// </summary>
public class LapCounterUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("TextMeshPro component to display lap count")]
    [SerializeField] private TextMeshProUGUI lapText;
    
    [Header("Display Settings")]
    [Tooltip("Prefix text before lap count (e.g., 'Lap ')")]
    [SerializeField] private string displayPrefix = "Lap ";
    
    [Tooltip("Show UI even when race mode is not active")]
    [SerializeField] private bool alwaysShow = false;
    
    [Header("Color Settings")]
    [Tooltip("Normal lap color")]
    [SerializeField] private Color normalColor = Color.white;
    
    [Tooltip("Final lap color")]
    [SerializeField] private Color finalLapColor = Color.yellow;
    
    [Tooltip("Race complete color")]
    [SerializeField] private Color completeColor = Color.green;
    
    private RaceLapManager lapManager;
    private bool isRaceMode = false;
    private bool hasCheckedRaceMode = false;
    
    private void Start()
    {
        // Find the lap manager
        lapManager = RaceLapManager.Instance;
        
        if (lapManager != null)
        {
            // Subscribe to lap change events
            lapManager.OnLapChanged += UpdateLapDisplay;
            lapManager.OnRaceCompleted += OnRaceComplete;
            
            // Initial display update - ALWAYS show if we have a lap manager
            UpdateLapDisplay(lapManager.CurrentLap, lapManager.TotalLaps);
            
            // Make sure UI is visible initially
            gameObject.SetActive(true);
            
            
            // Check for race mode after a delay (player might not be spawned yet)
            StartCoroutine(CheckRaceModeDelayed());
        }
        else
        {
            // No lap manager found
            // Debug.LogWarning("<color=yellow>[Lap Counter UI]</color> No RaceLapManager found in scene!");
            
            // Check if we should hide or show default
            if (!alwaysShow)
            {
                HideUI();
            }
            else
            {
                ShowDefaultUI();
            }
        }
    }
    
    /// <summary>
    /// Check for race mode after a short delay (allows player to spawn)
    /// </summary>
    private IEnumerator CheckRaceModeDelayed()
    {
        // Wait for player to spawn
        yield return new WaitForSeconds(0.5f);
        
        CheckRaceMode();
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (lapManager != null)
        {
            lapManager.OnLapChanged -= UpdateLapDisplay;
            lapManager.OnRaceCompleted -= OnRaceComplete;
        }
    }
    
    /// <summary>
    /// Check if the player has race mode enabled
    /// </summary>
    private void CheckRaceMode()
    {
        if (hasCheckedRaceMode) return; // Only check once
        
        DefaultBehaviour player = FindFirstObjectByType<DefaultBehaviour>();
        if (player != null)
        {
            isRaceMode = player.IsRaceMode;
            hasCheckedRaceMode = true;
            
            if (isRaceMode)
            {
            }
            else
            {
                
                // Hide UI if not in race mode AND we have a lap manager (unless alwaysShow is true)
                if (lapManager != null && !alwaysShow)
                {
                    HideUI();
                }
            }
        }
        else
        {
            // Debug.LogWarning("<color=orange>[Lap Counter UI]</color> Could not find player to check race mode - UI will stay visible by default");
        }
    }
    
    /// <summary>
    /// Update the lap counter display
    /// </summary>
    private void UpdateLapDisplay(int currentLap, int totalLaps)
    {
        if (lapText == null) return;
        
        // Always show the UI when we have a lap manager and lap changes
        // (don't hide it until we've checked race mode)
        if (gameObject.activeSelf == false)
        {
            gameObject.SetActive(true);
        }
        
        // Update text
        if (currentLap == 0)
        {
            // Race hasn't started yet
            lapText.text = $"{displayPrefix}0/{totalLaps}";
            lapText.color = normalColor;
        }
        else if (currentLap == totalLaps)
        {
            // Final lap
            lapText.text = $"{displayPrefix}{currentLap}/{totalLaps}";
            lapText.color = finalLapColor;
        }
        else
        {
            // Normal lap
            lapText.text = $"{displayPrefix}{currentLap}/{totalLaps}";
            lapText.color = normalColor;
        }
    }
    
    /// <summary>
    /// Called when race is completed
    /// </summary>
    private void OnRaceComplete()
    {
        if (lapText == null) return;
        
        // Show completion with special color
        lapText.text = $"{displayPrefix}{lapManager.TotalLaps}/{lapManager.TotalLaps} ✓";
        lapText.color = completeColor;
    }
    
    /// <summary>
    /// Hide the UI
    /// </summary>
    private void HideUI()
    {
        if (lapText != null)
        {
            lapText.text = "";
        }
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Show default UI when no lap manager exists
    /// </summary>
    private void ShowDefaultUI()
    {
        if (lapText != null)
        {
            lapText.text = $"{displayPrefix}0/0";
            lapText.color = normalColor;
        }
    }
}

