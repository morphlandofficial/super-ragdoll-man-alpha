using UnityEngine;

public class FPSMonitor : MonoBehaviour
{
    [Header("FPS Monitor Settings")]
    public bool showFPS = true;
    public KeyCode toggleKey = KeyCode.F3;
    
    [Header("Display Settings")]
    public int fontSize = 20;
    public Color textColor = Color.white;
    public Color backgroundColor = new Color(0, 0, 0, 0.5f);
    
    private float deltaTime = 0.0f;
    private GUIStyle style;
    private Rect rect;
    private string fpsText = "";
    
    // Singleton pattern to ensure only one FPS monitor exists
    private static FPSMonitor instance;
    
    void Awake()
    {
        // Implement singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Initialize GUI style and rect
        InitializeGUI();
    }
    
    void Update()
    {
        // Toggle FPS display with key press
        if (Input.GetKeyDown(toggleKey))
        {
            showFPS = !showFPS;
        }
        
        // Calculate delta time for FPS calculation
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }
    
    void InitializeGUI()
    {
        // Create GUI style
        style = new GUIStyle();
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = fontSize;
        style.normal.textColor = textColor;
        
        // Set position to bottom right corner
        float width = 120f;
        float height = 50f;
        rect = new Rect(Screen.width - width - 10f, Screen.height - height - 10f, width, height);
    }
    
    void OnGUI()
    {
        if (!showFPS) return;
        
        // Update rect position in case screen size changed
        float width = 120f;
        float height = 50f;
        rect = new Rect(Screen.width - width - 10f, Screen.height - height - 10f, width, height);
        
        // Calculate FPS
        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        fpsText = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
        
        // Draw background
        Color originalColor = GUI.color;
        GUI.color = backgroundColor;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = originalColor;
        
        // Draw FPS text
        GUI.Label(rect, fpsText, style);
    }
    
    // Public method to toggle FPS display programmatically
    public void ToggleFPS()
    {
        showFPS = !showFPS;
    }
    
    // Public method to set FPS display state
    public void SetFPSDisplay(bool enabled)
    {
        showFPS = enabled;
    }
    
    // Static method to access the instance
    public static FPSMonitor Instance
    {
        get { return instance; }
    }
}