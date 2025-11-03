using UnityEngine;
using TMPro;

/// <summary>
/// Attach this to a TextMeshPro component to display ragdoll count from Battle Royale Manager.
/// - Battle Royale Mode: Shows ragdolls remaining
/// - Infinite Spawn Mode: Shows total kills
/// Automatically finds the Battle Royale Manager and hides if not present.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class RagdollCounterUI : MonoBehaviour
{
    [Header("--- DISPLAY FORMATS ---")]
    [SerializeField] private string battleRoyaleFormat = "Enemies: {0}";
    [Tooltip("Format for Battle Royale mode. {0} = remaining ragdolls")]
    
    [SerializeField] private string infiniteSpawnFormat = "Kills: {0}";
    [Tooltip("Format for Infinite Spawn mode. {0} = total kills")]
    
    [SerializeField] private string waveFormat = "Wave {0}: {1} Left";
    [Tooltip("Format for Wave mode. {0} = wave number, {1} = enemies remaining in wave")]
    
    [Header("--- COLOR CODING ---")]
    [SerializeField] private bool enableColorCoding = true;
    [SerializeField] private Color highCountColor = Color.red;
    [Tooltip("Color when many enemies remain (Battle Royale) or few kills (Infinite Spawn)")]
    [SerializeField] private Color midCountColor = Color.yellow;
    [Tooltip("Color at mid-point")]
    [SerializeField] private Color lowCountColor = Color.green;
    [Tooltip("Color when few enemies remain (Battle Royale) or many kills (Infinite Spawn)")]
    
    [Header("--- AUTO HIDE ---")]
    [SerializeField] private bool hideWhenNoManager = true;
    [Tooltip("If true, hides when no Battle Royale Manager is found in scene")]
    
    private TMP_Text textComponent;
    private BattleRoyaleManager battleRoyaleManager;
    private bool hasManager = false;
    private RectTransform rectTransform;
    
    private void Awake()
    {
        // Get the text component on this GameObject
        textComponent = GetComponent<TMP_Text>();
        
        if (textComponent == null)
        {
            Debug.LogError("[RagdollCounterUI] No TextMeshPro component found!");
        }
        
        // Get RectTransform and position in bottom-right corner (middle of stack)
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // Set anchor to bottom-right
            rectTransform.anchorMin = new Vector2(1, 0); // Bottom-right
            rectTransform.anchorMax = new Vector2(1, 0); // Bottom-right
            rectTransform.pivot = new Vector2(1, 0); // Pivot at bottom-right
            
            // Position above ammo display with proper spacing
            rectTransform.anchoredPosition = new Vector2(-20, 70); // 20 pixels from right, 70 pixels from bottom
            
            // Set text alignment to right
            if (textComponent != null)
            {
                textComponent.alignment = TMPro.TextAlignmentOptions.BottomRight;
                textComponent.enableWordWrapping = false; // Prevent text wrapping
                textComponent.overflowMode = TMPro.TextOverflowModes.Overflow; // Allow overflow instead of wrapping
            }
        }
    }
    
    private void Start()
    {
        // Find Battle Royale Manager
        FindBattleRoyaleManager();
    }
    
    private void Update()
    {
        // Find manager if not found yet (lazy loading)
        if (!hasManager)
        {
            FindBattleRoyaleManager();
            
            if (!hasManager)
            {
                // No manager found - hide text if enabled
                if (hideWhenNoManager && textComponent != null)
                {
                    if (textComponent.enabled)
                    {
                        textComponent.enabled = false;
                    }
                }
                return;
            }
        }
        
        // Show the display
        if (!textComponent.enabled)
        {
            textComponent.enabled = true;
        }
        
        // Update display based on game mode
        UpdateDisplay();
    }
    
    /// <summary>
    /// Find the Battle Royale Manager in the scene
    /// </summary>
    private void FindBattleRoyaleManager()
    {
        battleRoyaleManager = FindFirstObjectByType<BattleRoyaleManager>();
        hasManager = (battleRoyaleManager != null);
        
        if (!hasManager && !hideWhenNoManager)
        {
            Debug.LogWarning("[RagdollCounterUI] No Battle Royale Manager found in scene!");
        }
    }
    
    /// <summary>
    /// Update the text display based on current game mode
    /// </summary>
    private void UpdateDisplay()
    {
        if (battleRoyaleManager == null || textComponent == null) return;
        
        BattleRoyaleManager.GameMode currentMode = battleRoyaleManager.GetGameMode();
        bool wavesEnabled = battleRoyaleManager.IsWaveSystemEnabled();
        
        // WAVE SYSTEM: Show current wave info (overrides normal display)
        if (wavesEnabled)
        {
            int currentWave = battleRoyaleManager.GetCurrentWaveNumber();
            int waveRemaining = battleRoyaleManager.GetCurrentWaveRemaining();
            int waveTotalRagdolls = battleRoyaleManager.GetCurrentWaveTotalRagdolls();
            
            textComponent.text = string.Format(waveFormat, currentWave, waveRemaining);
            
            // Apply color coding based on wave progress
            if (enableColorCoding && waveTotalRagdolls > 0)
            {
                float percentage = (float)waveRemaining / waveTotalRagdolls;
                
                if (percentage > 0.66f)
                {
                    textComponent.color = highCountColor; // Many enemies left
                }
                else if (percentage > 0.33f)
                {
                    textComponent.color = midCountColor; // Mid-way
                }
                else
                {
                    textComponent.color = lowCountColor; // Almost done!
                }
            }
        }
        // NO WAVES: Normal display based on game mode
        else if (currentMode == BattleRoyaleManager.GameMode.BattleRoyale)
        {
            // BATTLE ROYALE MODE: Show enemies remaining
            int remaining = battleRoyaleManager.GetTotalRemaining();
            int total = battleRoyaleManager.GetTotalPossibleRagdolls();
            
            textComponent.text = string.Format(battleRoyaleFormat, remaining);
            
            // Apply color coding (red when many left, green when few left)
            if (enableColorCoding && total > 0)
            {
                float percentage = (float)remaining / total;
                
                if (percentage > 0.66f)
                {
                    textComponent.color = highCountColor; // Many enemies left
                }
                else if (percentage > 0.33f)
                {
                    textComponent.color = midCountColor; // Mid-way
                }
                else
                {
                    textComponent.color = lowCountColor; // Almost done!
                }
            }
        }
        else if (currentMode == BattleRoyaleManager.GameMode.InfiniteSpawn)
        {
            // INFINITE SPAWN MODE: Show total kills
            int kills = battleRoyaleManager.GetTotalKills();
            
            textComponent.text = string.Format(infiniteSpawnFormat, kills);
            
            // Apply color coding (red when few kills, green when many kills)
            if (enableColorCoding)
            {
                if (kills < 10)
                {
                    textComponent.color = highCountColor; // Just starting
                }
                else if (kills < 50)
                {
                    textComponent.color = midCountColor; // Getting there
                }
                else
                {
                    textComponent.color = lowCountColor; // On a roll!
                }
            }
        }
    }
}

