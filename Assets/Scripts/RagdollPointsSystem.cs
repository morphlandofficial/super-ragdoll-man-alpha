using UnityEngine;
using ActiveRagdoll;

/// <summary>
/// Global points tracker that receives data from RagdollPointsCollector.
/// This component lives on a permanent GameObject and persists through respawns.
/// </summary>
public class RagdollPointsSystem : MonoBehaviour
{
    [Header("--- POINTS DISPLAY ---")]
    [SerializeField] private float _currentPoints = 0f;
    [SerializeField] private float _totalPointsEarned = 0f;
    
    [Header("--- SPEED POINTS ---")]
    [Tooltip("Speed below this earns 0 points")]
    [SerializeField] private float _minSpeed = 2f;
    [Tooltip("Speed above this earns max points")]
    [SerializeField] private float _maxSpeed = 20f;
    [Tooltip("Points per second at max speed")]
    [SerializeField] private float _speedPointsPerSecond = 10f;
    
    [Header("--- SPEED STREAK ---")]
    [Tooltip("Speed required to build streak (usually close to maxSpeed)")]
    [SerializeField] private float _speedStreakThreshold = 18f;
    [Tooltip("Seconds at high speed before streak bonuses start applying")]
    [SerializeField] private float _speedStreakFloor = 1f;
    [Tooltip("Multiplier increase per second (0.1 = +10% per second)")]
    [SerializeField] private float _speedStreakIncrementPerSecond = 0.1f;
    [Tooltip("Maximum streak multiplier cap")]
    [SerializeField] private float _maxSpeedStreakMultiplier = 2.5f;
    
    [Header("--- IMPACT POINTS ---")]
    [Tooltip("Impact force below this earns 0 points")]
    [SerializeField] private float _minImpactForce = 5f;
    [Tooltip("Impact force above this earns max points")]
    [SerializeField] private float _maxImpactForce = 100f;
    [Tooltip("Points per max-force impact")]
    [SerializeField] private float _impactPointsPerHit = 5f;
    
    [Header("--- RAGDOLL BONUS ---")]
    [Tooltip("Multiplier when in manual ragdoll mode (Tab/ButtonNorth)")]
    [SerializeField] private float _ragdollMultiplier = 3f;
    
    [Header("--- KILL POINTS ---")]
    [Tooltip("Points awarded for killing an AI ragdoll (set to 0 to disable)")]
    [SerializeField] private float _killPoints = 100f;
    
    [Tooltip("Bonus points for headshot kills (added on top of kill points)")]
    [SerializeField] private float _headshotBonusPoints = 50f;
    
    [Header("--- TIME REWIND PENALTY ---")]
    [SerializeField] private float _timeRewindPointsPerSecond = 5f;
    [Tooltip("Points deducted per second while time rewinding is active")]
    
    [Header("--- SPAWN BUFFER ---")]
    [SerializeField] private float _spawnBufferTime = 3f;
    [Tooltip("Time in seconds after detecting a new active object before points can be accrued")]
    
    [Header("--- RESPAWN PENALTY ---")]
    [SerializeField] private bool _enableRespawnPenalty = true;
    [SerializeField] private float _respawnPenaltyAmount = 1000f;
    [Tooltip("Points deducted when a respawn is detected (new collector replaces old one)")]
    
    [Header("--- POINT LIMITS ---")]
    [SerializeField] private bool _enablePointRateCap = true;
    [SerializeField] private float _maxPointsPerSecond = 100f;
    [Tooltip("Maximum points that can be earned per second (anti-exploit protection)")]
    
    
    [Header("--- FREEZE STATE ---")]
    [SerializeField] private bool _pointsFrozen = false;
    [Tooltip("When true, points will not accumulate (used when level is complete)")]
    
    [Header("--- DEBUG INFO ---")]
    [SerializeField] private float _currentSpeed = 0f;
    [SerializeField] private float _currentImpactForce = 0f;
    [SerializeField] private float _currentSpeedStreakMultiplier = 1f;
    [SerializeField] private float _speedStreakTime = 0f;
    [SerializeField] private float _pointsPerSecond = 0f;
    [SerializeField] private float _spawnBufferRemaining = 0f;
    
    // Singleton instance
    public static RagdollPointsSystem Instance { get; private set; }
    
    // References - now external
    private RagdollPointsCollector _currentCollector;
    private float _collectorSpawnTime = -1f;
    
    // Properties for external access
    public float CurrentPoints => _currentPoints;
    public float TotalPointsEarned => _totalPointsEarned;
    public float PointsPerSecond => _pointsPerSecond;
    public float SmoothedPointsPerSecond => _pointsPerSecond; // No smoothing needed in simplified system
    
    private void Awake()
    {
        // Setup singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
// Debug.LogWarning("Multiple RagdollPointsSystem instances found! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        
        // Check if there's a CollisionToggleTrigger that will start points tracking
        CheckForPointsTrigger();
        
        // Find the active collector in the scene
        FindActiveCollector();
    }
    
    private void CheckForPointsTrigger()
    {
        // Find all CollisionToggleTriggers in the scene
        CollisionToggleTrigger[] triggers = FindObjectsByType<CollisionToggleTrigger>(FindObjectsSortMode.None);
        
        foreach (var trigger in triggers)
        {
            // Check if this trigger is set to start points tracking
            if (trigger.IsPointsControlEnabled() && trigger.GetPointsControlMode() == CollisionToggleTrigger.PointsControlMode.StartPointsTracking)
            {
                // Start with points frozen
                _pointsFrozen = true;
                return; // Only need to find one trigger that starts points tracking
            }
        }
    }
    
    private void FindActiveCollector()
    {
        // Find active collector in scene (fallback only - prefer RegisterCollector)
        RagdollPointsCollector[] collectors = FindObjectsByType<RagdollPointsCollector>(FindObjectsSortMode.None);
        foreach (var collector in collectors)
        {
            if (collector.gameObject.activeInHierarchy)
            {
                RegisterCollector(collector);
                break;
            }
        }
    }
    
    /// <summary>
    /// Public method for collectors to register themselves (avoids expensive FindObjectsOfType calls)
    /// </summary>
    public void RegisterCollector(RagdollPointsCollector collector)
    {
        // Only record spawn time if this is a NEW collector (different from current)
        if (_currentCollector != collector)
        {
            // Determine if this is first spawn or respawn BEFORE changing _currentCollector
            bool isFirstSpawn = (_currentCollector == null);
            
            // Check if this is a manual respawn (no penalty)
            bool isManualRespawn = RespawnablePlayer.NextRespawnIsManual;
            
            // Reset the flag immediately after reading it
            RespawnablePlayer.NextRespawnIsManual = false;
            
            // Apply respawn penalty if this is a respawn (not the first spawn) AND not manual
            if (!isFirstSpawn && _enableRespawnPenalty && !isManualRespawn)
            {
                _currentPoints -= _respawnPenaltyAmount;
                Debug.Log($"<color=yellow>[PointsSystem]</color> Applied respawn penalty: -{_respawnPenaltyAmount}");
            }
            
            _currentCollector = collector;
            _collectorSpawnTime = Time.time;
            _spawnBufferRemaining = _spawnBufferTime;
            _speedStreakTime = 0f; // Reset streak on respawn
            _currentSpeedStreakMultiplier = 1f;
            
            string spawnType = isFirstSpawn ? " (FIRST SPAWN)" : " (RESPAWN)";
            Debug.Log($"<color=green>[PointsSystem]</color> Collector registered{spawnType} - Spawn buffer: {_spawnBufferTime}s, Points frozen: {_pointsFrozen}");
        }
        else
        {
            Debug.Log($"<color=yellow>[PointsSystem]</color> Collector re-registered (same instance) - ignoring duplicate registration");
        }
    }
    
    // Initialization moved to RagdollPointsCollector
    
    private void FixedUpdate()
    {
        // Don't accumulate points if frozen
        if (_pointsFrozen)
        {
            _pointsPerSecond = 0f;
            return;
        }
        
        // Check if we have an active collector
        if (_currentCollector == null || !_currentCollector.gameObject.activeInHierarchy)
        {
            FindActiveCollector();
            if (_currentCollector == null)
            {
                _pointsPerSecond = 0f;
                return;
            }
        }
        
        // Check spawn buffer - no points during buffer period
        float previousBufferRemaining = _spawnBufferRemaining;
        _spawnBufferRemaining = Mathf.Max(0f, _spawnBufferTime - (Time.time - _collectorSpawnTime));
        
        // Log when buffer expires (only once)
        if (previousBufferRemaining > 0f && _spawnBufferRemaining <= 0f)
        {
            Debug.Log($"<color=green>[PointsSystem]</color> Spawn buffer EXPIRED - points tracking NOW ACTIVE!");
        }
        
        if (_spawnBufferRemaining > 0f)
        {
            _pointsPerSecond = 0f;
            return;
        }
        
        float deltaTime = Time.fixedDeltaTime;
        
        // Get data from the collector
        float speed = _currentCollector.GetAverageSpeed();
        float impactForce = _currentCollector.GetLastImpactForce();
        bool isRewinding = _currentCollector.IsTimeRewinding();
        bool inRagdoll = _currentCollector.IsInRagdollMode();
        
        // Update debug display
        _currentSpeed = speed;
        _currentImpactForce = impactForce;
        
        // ──────────────────────────────────────────────────
        // TIME REWIND (Penalty)
        // ──────────────────────────────────────────────────
        
        if (isRewinding)
        {
            _pointsPerSecond = -_timeRewindPointsPerSecond;
            _currentPoints -= _timeRewindPointsPerSecond * deltaTime;
            _speedStreakTime = 0f; // Reset streak while rewinding
            _currentSpeedStreakMultiplier = 1f;
            return;
        }
        
        // ──────────────────────────────────────────────────
        // SPEED STREAK TRACKING
        // ──────────────────────────────────────────────────
        
        if (speed >= _speedStreakThreshold)
        {
            _speedStreakTime += deltaTime;
        }
        else
        {
            _speedStreakTime = 0f; // Drop below threshold = reset
        }
        
        // Calculate streak multiplier (with floor)
        _currentSpeedStreakMultiplier = 1f;
        if (_speedStreakTime > _speedStreakFloor)
        {
            float bonusTime = _speedStreakTime - _speedStreakFloor;
            _currentSpeedStreakMultiplier = 1f + (bonusTime * _speedStreakIncrementPerSecond);
            _currentSpeedStreakMultiplier = Mathf.Min(_currentSpeedStreakMultiplier, _maxSpeedStreakMultiplier);
        }
        
        // ──────────────────────────────────────────────────
        // SPEED POINTS
        // ──────────────────────────────────────────────────
        
        float speedNormalized = Mathf.Clamp01((speed - _minSpeed) / (_maxSpeed - _minSpeed));
        float speedPoints = speedNormalized * _speedPointsPerSecond;
        
        // Apply speed streak multiplier
        speedPoints *= _currentSpeedStreakMultiplier;
        
        // ──────────────────────────────────────────────────
        // IMPACT POINTS
        // ──────────────────────────────────────────────────
        
        float impactNormalized = Mathf.Clamp01((impactForce - _minImpactForce) / (_maxImpactForce - _minImpactForce));
        float impactPoints = impactNormalized * _impactPointsPerHit;
        
        // ──────────────────────────────────────────────────
        // RAGDOLL MULTIPLIER
        // ──────────────────────────────────────────────────
        
        float multiplier = inRagdoll ? _ragdollMultiplier : 1f;
        
        // ──────────────────────────────────────────────────
        // FINAL CALCULATION
        // ──────────────────────────────────────────────────
        
        float totalPoints = (speedPoints + impactPoints) * multiplier;
        
        // Anti-exploit cap
        if (_enablePointRateCap)
        {
            totalPoints = Mathf.Min(totalPoints, _maxPointsPerSecond);
        }
        
        // Apply points
        _pointsPerSecond = totalPoints;
        _currentPoints += totalPoints * deltaTime;
        _totalPointsEarned += Mathf.Max(0f, totalPoints * deltaTime);
    }
    
    // All calculation methods moved to RagdollPointsCollector
    
    // Public methods for external control
    public void AddPoints(float points)
    {
        _currentPoints += points;
        _totalPointsEarned += points;
    }
    
    /// <summary>
    /// Award points for killing an AI ragdoll (called by DefaultBehaviour when player kills a ragdoll)
    /// </summary>
    /// <param name="isHeadshot">If true, awards bonus points for headshot</param>
    public void AddKillPoints(bool isHeadshot = false)
    {
        if (_pointsFrozen) return;
        
        float pointsToAward = _killPoints;
        
        if (isHeadshot && _headshotBonusPoints > 0f)
        {
            pointsToAward += _headshotBonusPoints;
            _currentPoints += pointsToAward;
            _totalPointsEarned += pointsToAward;
            Debug.Log($"<color=yellow>[💀 HEADSHOT!]</color> +{pointsToAward} points ({_killPoints} kill + {_headshotBonusPoints} headshot bonus)! Total: {_currentPoints:F0}");
        }
        else if (_killPoints > 0f)
        {
            _currentPoints += _killPoints;
            _totalPointsEarned += _killPoints;
            // Debug.Log($"<color=green>[Kill Points]</color> +{_killPoints} points for kill! Total: {_currentPoints:F0}");
        }
    }
    
    /// <summary>
    /// Get the kill points value (for UI display)
    /// </summary>
    public float GetKillPointsValue()
    {
        return _killPoints;
    }
    
    /// <summary>
    /// Get the headshot bonus points value (for UI display)
    /// </summary>
    public float GetHeadshotBonusValue()
    {
        return _headshotBonusPoints;
    }
    
    public void ResetPoints()
    {
        _currentPoints = 0f;
        _speedStreakTime = 0f;
        _currentSpeedStreakMultiplier = 1f;
    }
    
    public void ResetTotalPoints()
    {
        _totalPointsEarned = 0f;
        _currentPoints = 0f;
        _speedStreakTime = 0f;
        _currentSpeedStreakMultiplier = 1f;
    }
    
    /// <summary>
    /// Freeze points accumulation (used when level is complete)
    /// </summary>
    public void FreezePoints()
    {
        _pointsFrozen = true;
    }
    
    /// <summary>
    /// Unfreeze points accumulation
    /// </summary>
    public void UnfreezePoints()
    {
        _pointsFrozen = false;
    }
}