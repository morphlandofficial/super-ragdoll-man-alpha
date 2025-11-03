using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ActiveRagdoll;

/// <summary>
/// Collects ragdoll physics data and sends it to the global RagdollPointsTracker.
/// This component lives on the Character prefab and gets destroyed/recreated on respawn.
/// </summary>
[RequireComponent(typeof(ActiveRagdoll.ActiveRagdoll))]
public class RagdollPointsCollector : MonoBehaviour
{
    // References
    private ActiveRagdoll.ActiveRagdoll _activeRagdoll;
    private DefaultBehaviour _defaultBehaviour;
    private PhysicsModule _physicsModule;
    private TimeRewindController _timeRewindController;
    
    // Cached values for performance
    private int _rigidbodyCount = 0;
    
    // Tracking variables
    private Vector3[] _previousVelocities;
    private Vector3[] _previousAngularVelocities;
    
    // Impact tracking
    private List<CollisionListener> _collisionListeners = new List<CollisionListener>();
    private float _lastImpactPoints = 0f;
    
    // Speed streak tracking
    private float _speedStreakTime = 0f;
    private float _speedStreakMultiplier = 1f;
    
    // Airborne streak tracking
    private float _airborneStreakTime = 0f;
    private float _airborneStreakMultiplier = 1f;
    
    // Debug values (exposed for tracker to read)
    public float CurrentSpeed { get; private set; }
    public float CurrentAngularVelocity { get; private set; }
    public float CurrentJointStress { get; private set; }
    public float CurrentAccelerationChange { get; private set; }
    public float LastImpactForce { get; private set; }
    public float LastImpactPoints => _lastImpactPoints;
    public float SpeedStreakTime => _speedStreakTime;
    public float SpeedStreakMultiplier => _speedStreakMultiplier;
    public float AirborneStreakTime => _airborneStreakTime;
    public float AirborneStreakMultiplier => _airborneStreakMultiplier;
    
    private void Awake()
    {
        _activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        _defaultBehaviour = GetComponent<DefaultBehaviour>();
        _physicsModule = GetComponent<PhysicsModule>();
        _timeRewindController = GetComponent<TimeRewindController>();
    }
    
    private void OnEnable()
    {
        // MULTIPLAYER FIX: Register immediately when enabled
        // This ensures points tracking starts even if Start() timing is off
        RegisterWithPointsSystem();
    }
    
    private void Start()
    {
        // Initialize velocity tracking arrays and cache rigidbody count FIRST
        if (_activeRagdoll.Rigidbodies != null)
        {
            _rigidbodyCount = _activeRagdoll.Rigidbodies.Length;
            _previousVelocities = new Vector3[_rigidbodyCount];
            _previousAngularVelocities = new Vector3[_rigidbodyCount];
            
            // Initialize with current velocities and setup collision listeners
            for (int i = 0; i < _rigidbodyCount; i++)
            {
                Rigidbody rb = _activeRagdoll.Rigidbodies[i];
                if (rb != null)
                {
                    _previousVelocities[i] = rb.linearVelocity;
                    _previousAngularVelocities[i] = rb.angularVelocity;
                    
                    // Try to find existing listener first (may be pre-attached)
                    CollisionListener listener = rb.gameObject.GetComponent<CollisionListener>();
                    if (listener == null)
                    {
                        // Only add if not already present
                        listener = rb.gameObject.AddComponent<CollisionListener>();
                    }
                    listener.OnCollisionDetected += HandleCollision;
                    _collisionListeners.Add(listener);
                }
            }
        }
        
        // Register with points system AFTER initialization
        // This is a backup in case OnEnable() registration didn't work
        RegisterWithPointsSystem();
    }
    
    /// <summary>
    /// Register this collector with the global points system
    /// </summary>
    private void RegisterWithPointsSystem()
    {
        if (RagdollPointsSystem.Instance != null)
        {
            RagdollPointsSystem.Instance.RegisterCollector(this);
            Debug.Log($"<color=cyan>[PointsCollector]</color> Registered {gameObject.name} with RagdollPointsSystem");
        }
        else
        {
            Debug.LogWarning($"<color=red>[PointsCollector]</color> {gameObject.name} - RagdollPointsSystem.Instance is NULL! Points will not track!");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up collision listeners
        foreach (var listener in _collisionListeners)
        {
            if (listener != null)
            {
                listener.OnCollisionDetected -= HandleCollision;
            }
        }
    }
    
    /// <summary>
    /// Handle collision events from rigidbodies
    /// </summary>
    private void HandleCollision(Collision collision)
    {
        // Calculate impact force
        float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
        LastImpactForce = impactForce;
        
        // Store for points calculation (will be picked up by RagdollPointsSystem)
        _lastImpactPoints = impactForce;
    }
    
    /// <summary>
    /// Get average speed of all rigidbodies (for simplified points system)
    /// </summary>
    public float GetAverageSpeed()
    {
        if (_activeRagdoll.Rigidbodies == null) return 0f;
        
        float totalSpeed = 0f;
        int validRigidbodies = 0;
        
        foreach (Rigidbody rb in _activeRagdoll.Rigidbodies)
        {
            if (rb != null)
            {
                totalSpeed += rb.linearVelocity.magnitude;
                validRigidbodies++;
            }
        }
        
        return validRigidbodies > 0 ? totalSpeed / validRigidbodies : 0f;
    }
    
    /// <summary>
    /// Get last recorded impact force (for simplified points system)
    /// </summary>
    public float GetLastImpactForce()
    {
        float impact = LastImpactForce;
        LastImpactForce = 0f; // Reset after reading (one-time bonus)
        return impact;
    }
    
    /// <summary>
    /// Check if player is in manual ragdoll mode (for simplified points system)
    /// </summary>
    public bool IsInRagdollMode()
    {
        if (_physicsModule == null) return false;
        return _physicsModule.BalanceMode == PhysicsModule.BALANCE_MODE.NONE;
    }
    
    /// <summary>
    /// Check if time rewind is active (for simplified points system)
    /// </summary>
    public bool IsTimeRewinding()
    {
        return _timeRewindController != null && _timeRewindController.IsRewinding;
    }
    
    public float CalculateSpeedPoints(float deltaTime, float speedMultiplier, float maxSpeed, bool useCeiling, AnimationCurve speedCurve, 
                                       bool enableStreak, float streakThreshold, float streakMultiplierRate, float maxStreakMultiplier)
    {
        float totalSpeed = 0f;
        int validRigidbodies = 0;
        
        // Calculate average speed of all rigidbodies
        foreach (Rigidbody rb in _activeRagdoll.Rigidbodies)
        {
            if (rb != null)
            {
                totalSpeed += rb.linearVelocity.magnitude;
                validRigidbodies++;
            }
        }
        
        if (validRigidbodies == 0) return 0f;
        
        float averageSpeed = totalSpeed / validRigidbodies;
        CurrentSpeed = averageSpeed;
        
        // Speed streak system
        if (enableStreak)
        {
            if (averageSpeed >= streakThreshold)
            {
                // Build up streak time
                _speedStreakTime += deltaTime;
                
                // Calculate exponential multiplier based on streak time
                _speedStreakMultiplier = 1f + (_speedStreakTime * streakMultiplierRate);
                _speedStreakMultiplier = Mathf.Min(_speedStreakMultiplier, maxStreakMultiplier);
            }
            else
            {
                // Reset streak if speed drops below threshold
                _speedStreakTime = 0f;
                _speedStreakMultiplier = 1f;
            }
        }
        else
        {
            _speedStreakMultiplier = 1f;
        }
        
        // Apply speed ceiling if enabled
        if (useCeiling)
        {
            averageSpeed = Mathf.Min(averageSpeed, maxSpeed);
        }
        
        // Normalize speed (0-1) and apply curve
        float normalizedSpeed = averageSpeed / maxSpeed;
        float curveValue = speedCurve.Evaluate(normalizedSpeed);
        
        // Apply streak multiplier to final points
        return curveValue * speedMultiplier * _speedStreakMultiplier * deltaTime;
    }
    
    public float CalculateRagdollPoints(float deltaTime, float ragdollMultiplier, float angularMultiplier, float jointMultiplier, float accelerationMultiplier)
    {
        if (_activeRagdoll.Rigidbodies == null) return 0f;
        
        float totalAngularVelocity = 0f;
        float totalJointStress = 0f;
        float totalAccelerationChange = 0f;
        
        // Calculate ragdoll violence metrics - use cached count
        float deltaTimeInv = 1f / deltaTime; // Cache division
        
        for (int i = 0; i < _rigidbodyCount; i++)
        {
            Rigidbody rb = _activeRagdoll.Rigidbodies[i];
            if (rb == null) continue;
            
            // Angular velocity (how fast body parts are spinning)
            totalAngularVelocity += rb.angularVelocity.magnitude;
            
            // Acceleration change (sudden movements)
            if (i < _previousVelocities.Length)
            {
                Vector3 acceleration = (rb.linearVelocity - _previousVelocities[i]) * deltaTimeInv;
                Vector3 previousAcceleration = (_previousVelocities[i] - rb.linearVelocity) * deltaTimeInv;
                totalAccelerationChange += (acceleration - previousAcceleration).magnitude;
                
                _previousVelocities[i] = rb.linearVelocity;
            }
            
            // Angular acceleration change
            if (i < _previousAngularVelocities.Length)
            {
                Vector3 angularAcceleration = (rb.angularVelocity - _previousAngularVelocities[i]) * deltaTimeInv;
                totalAccelerationChange += angularAcceleration.magnitude * 0.5f; // Weight angular changes less
                
                _previousAngularVelocities[i] = rb.angularVelocity;
            }
        }
        
        // Calculate joint stress (how much joints are being strained)
        if (_activeRagdoll.Joints != null)
        {
            foreach (ConfigurableJoint joint in _activeRagdoll.Joints)
            {
                if (joint != null && joint.connectedBody != null)
                {
                    // Measure the force being applied to the joint
                    Rigidbody attachedRb = joint.GetComponent<Rigidbody>();
                    if (attachedRb != null)
                    {
                        Vector3 relativeVelocity = joint.connectedBody.linearVelocity - attachedRb.linearVelocity;
                        Vector3 relativeAngularVelocity = joint.connectedBody.angularVelocity - attachedRb.angularVelocity;
                        
                        totalJointStress += relativeVelocity.magnitude + relativeAngularVelocity.magnitude;
                    }
                }
            }
        }
        
        // Store debug values
        CurrentAngularVelocity = totalAngularVelocity;
        CurrentJointStress = totalJointStress;
        CurrentAccelerationChange = totalAccelerationChange;
        
        // Calculate total ragdoll points
        float ragdollPoints = 0f;
        ragdollPoints += totalAngularVelocity * angularMultiplier;
        ragdollPoints += totalJointStress * jointMultiplier;
        ragdollPoints += totalAccelerationChange * accelerationMultiplier;
        
        return ragdollPoints * ragdollMultiplier * deltaTime;
    }
    
    public float GetRagdollStateMultiplier(float manualBonus, float jumpBonus, float airborneBonus)
    {
        float multiplier = 1f;
        
        if (_defaultBehaviour == null || _physicsModule == null)
        {
            return multiplier;
        }
        
        // Check if in manual ragdoll mode (holding triangle)
        bool isManualRagdoll = _physicsModule.BalanceMode == PhysicsModule.BALANCE_MODE.NONE;
        if (isManualRagdoll)
        {
            multiplier *= manualBonus;
        }
        
        // Check if airborne (jump ragdoll or falling)
        if (!_activeRagdoll.Input.IsOnFloor)
        {
            multiplier *= airborneBonus;
            
            // Additional bonus if this is jump ragdoll
            if (_physicsModule.BalanceMode == PhysicsModule.BALANCE_MODE.MANUAL_TORQUE)
            {
                multiplier *= jumpBonus;
            }
        }
        
        return multiplier;
    }
    
    /// <summary>
    /// Calculate airborne streak multiplier - the longer airborne, the higher the multiplier
    /// </summary>
    public float GetAirborneStreakMultiplier(float deltaTime, bool enableAirborneStreak, float airborneStreakMultiplierRate, float maxAirborneStreakMultiplier)
    {
        if (!enableAirborneStreak)
        {
            _airborneStreakMultiplier = 1f;
            return 1f;
        }
        
        // Check if airborne
        bool isAirborne = !_activeRagdoll.Input.IsOnFloor;
        
        if (isAirborne)
        {
            // Build up airborne streak time
            _airborneStreakTime += deltaTime;
            
            // Calculate exponential multiplier based on airborne time
            _airborneStreakMultiplier = 1f + (_airborneStreakTime * airborneStreakMultiplierRate);
            _airborneStreakMultiplier = Mathf.Min(_airborneStreakMultiplier, maxAirborneStreakMultiplier);
        }
        else
        {
            // Reset streak when landing
            _airborneStreakTime = 0f;
            _airborneStreakMultiplier = 1f;
        }
        
        return _airborneStreakMultiplier;
    }
    
    /// <summary>
    /// Calculate impact points based on collision force
    /// </summary>
    public float CalculateImpactPoints(float impactMultiplier, float impactThreshold, AnimationCurve impactCurve, float maxImpactForce)
    {
        // Reset impact points after reading
        float impactPoints = _lastImpactPoints;
        _lastImpactPoints = 0f; // Reset for next impact
        
        // Check threshold
        if (impactPoints < impactThreshold)
        {
            return 0f;
        }
        
        // Normalize impact force to 0-1 range
        float normalizedImpact = Mathf.Clamp01(impactPoints / maxImpactForce);
        
        // Apply curve for non-linear scaling
        float curveValue = impactCurve.Evaluate(normalizedImpact);
        
        // Return final points
        return curveValue * impactMultiplier;
    }
}

/// <summary>
/// Helper component to detect collisions on rigidbodies
/// </summary>
public class CollisionListener : MonoBehaviour
{
    public System.Action<Collision> OnCollisionDetected;
    
    private void OnCollisionEnter(Collision collision)
    {
        OnCollisionDetected?.Invoke(collision);
    }
}