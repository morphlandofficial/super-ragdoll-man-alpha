using UnityEngine;

/// <summary>
/// PERFORMANCE OPTIMIZATION: Singleton that caches the player reference to avoid expensive
/// FindFirstObjectByType calls every frame from multiple AI systems.
/// 
/// When player respawns, the new player instance automatically registers itself.
/// All systems can query CurrentPlayer instead of searching the scene.
/// </summary>
public class PlayerReferenceManager : MonoBehaviour
{
    private static PlayerReferenceManager _instance;
    public static PlayerReferenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find existing instance
                _instance = FindFirstObjectByType<PlayerReferenceManager>();
                
                // Create if none exists
                if (_instance == null)
                {
                    GameObject go = new GameObject("PlayerReferenceManager");
                    _instance = go.AddComponent<PlayerReferenceManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    // Cached player reference
    private Transform _currentPlayerTransform;
    private RespawnablePlayer _currentPlayer;
    
    /// <summary>
    /// Get the current active player transform (cached, very fast)
    /// Returns null if no player exists
    /// </summary>
    public static Transform CurrentPlayerTransform => Instance._currentPlayerTransform;
    
    /// <summary>
    /// Get the current active player RespawnablePlayer component (cached, very fast)
    /// Returns null if no player exists
    /// </summary>
    public static RespawnablePlayer CurrentPlayer => Instance._currentPlayer;
    
    private void Awake()
    {
        // Singleton pattern
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Find initial player
        RefreshPlayerReference();
    }
    
    private void OnEnable()
    {
        // Subscribe to scene loaded event to refresh player reference
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Refresh player reference when new scene loads
        RefreshPlayerReference();
    }
    
    /// <summary>
    /// Register a player instance (called by RespawnablePlayer on spawn)
    /// </summary>
    public static void RegisterPlayer(RespawnablePlayer player)
    {
        if (player != null)
        {
            Instance._currentPlayer = player;
            Instance._currentPlayerTransform = player.transform;
            // Debug.Log($"<color=cyan>[PlayerRefManager]</color> Player registered: {player.name}");
        }
    }
    
    /// <summary>
    /// Unregister a player instance (called by RespawnablePlayer on destroy)
    /// </summary>
    public static void UnregisterPlayer(RespawnablePlayer player)
    {
        if (Instance._currentPlayer == player)
        {
            Instance._currentPlayer = null;
            Instance._currentPlayerTransform = null;
            // Debug.Log("<color=cyan>[PlayerRefManager]</color> Player unregistered");
        }
    }
    
    /// <summary>
    /// Manually refresh the player reference (searches scene - slow, use sparingly)
    /// </summary>
    public static void RefreshPlayerReference()
    {
        RespawnablePlayer player = FindFirstObjectByType<RespawnablePlayer>();
        if (player != null)
        {
            RegisterPlayer(player);
        }
        else
        {
            Instance._currentPlayer = null;
            Instance._currentPlayerTransform = null;
        }
    }
    
    /// <summary>
    /// Check if player reference is valid and active
    /// </summary>
    public static bool HasValidPlayer()
    {
        return Instance._currentPlayerTransform != null && 
               Instance._currentPlayerTransform.gameObject.activeInHierarchy;
    }
}


