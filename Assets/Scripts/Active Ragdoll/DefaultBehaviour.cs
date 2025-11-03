using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using ActiveRagdoll;

/// <summary> Default behaviour of an Active Ragdoll </summary>
public class DefaultBehaviour : MonoBehaviour {
    // Author: Sergio Abreu García | https://sergioabreu.me

    [Header("Modules")]
    [SerializeField] private ActiveRagdoll.ActiveRagdoll _activeRagdoll;
    [SerializeField] private PhysicsModule _physicsModule;
    [SerializeField] private AnimationModule _animationModule;
    [SerializeField] private GripModule _gripModule;
    [SerializeField] private CameraModule _cameraModule;
    [SerializeField] private TimeRewindController _timeRewindController;
    private CharacterAudioController _audioController;
    private PauseMenuManager _pauseMenuManager;
    private AimIndicatorUI _aimIndicatorUI;

    [Header("Movement")]
    [SerializeField] private bool _enableMovement = true;
    private Vector2 _movement;

    [Header("Speed Settings")]
    [SerializeField] private float _walkSpeed = 1.0f;
    [SerializeField] private float _runSpeedMultiplier = 1.8f;
    [SerializeField] private bool _enableRun = true;
    private bool _isRunning = false;

    [Header("Progressive Speed Boost (While Running)")]
    [SerializeField] 
    [Tooltip("Time you must run before speed boost starts (seconds)")]
    private float _speedBoostBufferTime = 1.0f;
    [SerializeField] 
    [Tooltip("Speed increase percentage per interval (e.g., 0.1 = 10% increase)")]
    private float _speedBoostPercentage = 0.1f;
    [SerializeField] 
    [Tooltip("Time interval in seconds for each speed boost")]
    private float _speedBoostInterval = 1.0f;
    [SerializeField] 
    [Tooltip("Maximum speed boost multiplier (e.g., 2.0 = double speed max)")]
    private float _maxSpeedBoostMultiplier = 2.5f;
    private float _runningTimer = 0f;
    private float _speedBoostTimer = 0f;
    private float _currentSpeedBoostMultiplier = 1f;

    [Header("Jump Settings")]
    [SerializeField] private bool _enableJump = true;
    [SerializeField] private float _jumpForce = 500f;
    [SerializeField] private float _jumpCooldown = 0.5f;
    private float _lastJumpTime = -1f;
    
    [Header("Momentum Preservation on Landing")]
    [SerializeField] 
    [Tooltip("Enable momentum preservation when landing upright after a jump")]
    private bool _enableMomentumPreservation = true;
    [SerializeField]
    [Tooltip("Minimum horizontal velocity required to preserve momentum (units/second)")]
    private float _minVelocityToPreserve = 3f;
    [SerializeField]
    [Tooltip("Maximum torso tilt angle (degrees) to consider an upright landing")]
    private float _maxUprightTiltAngle = 35f;
    [SerializeField]
    [Tooltip("Time window after leaving ground to track as jump (seconds)")]
    private float _jumpTrackingWindow = 2f;
    
    // Momentum tracking
    private bool _wasJumping = false;
    private float _preJumpSpeedBoostMultiplier = 1f;
    private float _preJumpRunningTimer = 0f;
    private float _preJumpSpeedBoostTimer = 0f;
    private Vector3 _preJumpHorizontalVelocity = Vector3.zero;
    private float _timeLeftGround = -1f;

    [Header("Air Control Settings")]
    [SerializeField]
    [Tooltip("Enable movement control while airborne")]
    private bool _enableAirControl = true;
    [SerializeField]
    [Tooltip("Force applied for air movement (lower than ground movement)")]
    private float _airControlForce = 50f;
    [SerializeField]
    [Tooltip("Maximum air speed (prevents infinite acceleration)")]
    private float _maxAirSpeed = 10f;
    [SerializeField]
    [Tooltip("Apply force to torso vs applying torque for rotation")]
    private bool _useDirectionalForce = true;
    [SerializeField]
    [Tooltip("Torque strength for rotating towards movement direction while airborne")]
    private float _airRotationTorque = 20f;
    
    [Header("Air Steering (Additive Layer)")]
    [SerializeField]
    [Tooltip("Enable gentle steering during falls without affecting core physics")]
    private bool _enableAirSteering = true;
    [SerializeField]
    [Tooltip("Base steering force for short jumps (always active)")]
    private float _baseAirSteeringForce = 150f;
    [SerializeField]
    [Tooltip("Full steering force for long falls (kicks in after delay)")]
    private float _fullAirSteeringForce = 1250f;
    [SerializeField]
    [Tooltip("Time airborne (seconds) before full steering kicks in")]
    private float _steeringPowerUpDelay = 2f;
    
    private float _timeAirborne = 0f;
    
    [Header("Ragdoll Settings")]
    [SerializeField] private bool _ragdollWhileJumping = true;
    private bool _manualRagdoll = false;
    private bool _levelCompleted = false; // Permanent flag for level completion
    private PhysicsModule.BALANCE_MODE _previousBalanceMode;
    
    // Public getter for ragdoll state (used by RedLightGreenLightManager)
    public bool IsInRagdollMode => _manualRagdoll;
    
    [Header("Race Mode")]
    [SerializeField] 
    [Tooltip("Enable race mode - player must complete all laps before reaching finish")]
    private bool _raceMode = false;
    
    // Public getter for race mode
    public bool IsRaceMode => _raceMode;
    
    [Header("Arm Control While Jumping")]
    private float _leftArmInput = 0f;
    private float _rightArmInput = 0f;
    
    [Header("Shooting Settings")]
    [SerializeField]
    [Tooltip("Enable shooting mechanic (press L2/R2 to fire once, must release and press again for next shot)")]
    private bool _enableShooting = false;
    [SerializeField]
    [Tooltip("Bullet visual prefab (should have Trail Renderer)")]
    private GameObject _bulletPrefab;
    [SerializeField]
    [Tooltip("Optional: Gun model objects to show/hide in hands (will auto-find if not set). Should be children of the hand bones.")]
    private GameObject[] _handGunModels;
    [SerializeField]
    [Tooltip("Time required to hold trigger before firing (seconds)")]
    private float _shootChargeTime = 0.2f;
    [SerializeField]
    [Tooltip("Time between continuous shots when holding trigger (seconds)")]
    private float _shootCooldown = 1.0f;
    [SerializeField]
    [Tooltip("Speed of the bullet projectile (units per second)")]
    private float _bulletSpeed = 20f;
    [SerializeField]
    [Tooltip("Radius of the shot sphere (larger = easier to hit)")]
    private float _shotRadius = 0.4f;
    [SerializeField]
    [Tooltip("Maximum distance the shot rays travel")]
    private float _shotMaxDistance = 50f;
    [SerializeField]
    [Tooltip("Layer mask for what the shot can hit")]
    private LayerMask _shotLayerMask = ~0; // Everything by default
    
    [Header("Ammo (Read-Only)")]
    [SerializeField]
    [Tooltip("Current ammo remaining (-1 = infinite)")]
    private int _currentAmmo = -1;
    [SerializeField]
    [Tooltip("Has limited ammo? (false = infinite ammo)")]
    private bool _hasLimitedAmmo = false;
    
    // Shooting state tracking
    private bool _leftTriggerPressed = false;
    private bool _rightTriggerPressed = false;
    
    [Header("Visual Effects")]
    [SerializeField] 
    [Tooltip("Enable Datamosh glitch effect when entering pure ragdoll mode (hold Tab/ButtonNorth)")]
    private bool _datamoshGlitchOnRagdoll = false;
    private Kino.Datamosh _datamoshEffect;
    
    [SerializeField]
    [Tooltip("Enable Analog Glitch effect when rewinding time (hold R/ButtonEast)")]
    private bool _analogGlitchOnRewind = true;
    
    private Kino.AnalogGlitch _analogGlitchEffect;

    private Vector3 _aimDirection;

    private void OnValidate() {
        if (_activeRagdoll == null) _activeRagdoll = GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (_physicsModule == null) _physicsModule = GetComponent<PhysicsModule>();
        if (_animationModule == null) _animationModule = GetComponent<AnimationModule>();
        if (_gripModule == null) _gripModule = GetComponent<GripModule>();
        if (_cameraModule == null) _cameraModule = GetComponent<CameraModule>();
        if (_timeRewindController == null) _timeRewindController = GetComponent<TimeRewindController>();
    }

    private void Start() {
        // Link all the functions to its input to define how the ActiveRagdoll will behave.
        // This is a default implementation, where the input player is binded directly to
        // the ActiveRagdoll actions in a very simple way. But any implementation is
        // possible, such as assigning those same actions to the output of an AI system.

        _activeRagdoll.Input.OnMoveDelegates += MovementInput;
        _activeRagdoll.Input.OnMoveDelegates += _physicsModule.ManualTorqueInput;
        _activeRagdoll.Input.OnFloorChangedDelegates += ProcessFloorChanged;

        _activeRagdoll.Input.OnLeftArmDelegates += LeftArmInput;
        _activeRagdoll.Input.OnRightArmDelegates += RightArmInput;

        _activeRagdoll.Input.OnRunDelegates += RunInput;
        _activeRagdoll.Input.OnJumpDelegates += JumpInput;
        _activeRagdoll.Input.OnRagdollDelegates += RagdollInput;
        _activeRagdoll.Input.OnRewindDelegates += RewindInput;
        
        // Get audio controller component
        _audioController = GetComponent<CharacterAudioController>();
        
        // Find pause menu manager (if it exists in the scene)
        _pauseMenuManager = FindFirstObjectByType<PauseMenuManager>();
        
        // Find aim indicator UI (if it exists in the scene)
        _aimIndicatorUI = FindFirstObjectByType<AimIndicatorUI>();
        
        // Pass our camera to the aim indicator UI
        if (_aimIndicatorUI != null && _cameraModule != null && _cameraModule.Camera != null)
        {
            _aimIndicatorUI.SetCamera(_cameraModule.Camera.GetComponent<Camera>());
        }
        
        // Hide gun models by default (will show when gun pickup is collected)
        HideHandGuns();
    }
    
    private void InitializeDatamoshEffect() {
        // Try to get the Datamosh component if we haven't already
        if (_datamoshGlitchOnRagdoll && _datamoshEffect == null && _cameraModule != null && _cameraModule.Camera != null) {
            _datamoshEffect = _cameraModule.Camera.GetComponent<Kino.Datamosh>();
        }
    }
    
    private void InitializeAnalogGlitchEffect() {
        // Try to get the AnalogGlitch component if we haven't already
        if (_analogGlitchOnRewind && _analogGlitchEffect == null && _cameraModule != null && _cameraModule.Camera != null) {
            _analogGlitchEffect = _cameraModule.Camera.GetComponent<Kino.AnalogGlitch>();
            // Start with the effect disabled
            if (_analogGlitchEffect != null) {
                _analogGlitchEffect.enabled = false;
            }
        }
    }

    private void Update() {
        // Try to initialize effects if not done yet
        if (_datamoshGlitchOnRagdoll && _datamoshEffect == null) {
            InitializeDatamoshEffect();
        }
        if (_analogGlitchOnRewind && _analogGlitchEffect == null) {
            InitializeAnalogGlitchEffect();
        }
        
        // Handle level completion scene controls
        if (_levelCompleted) {
            HandleLevelCompleteInputs();
        }
        
        // Get aim direction from camera (with null check)
        if (_cameraModule != null && _cameraModule.Camera != null) {
            _aimDirection = _cameraModule.Camera.transform.forward;
        } else {
            // Fallback: use Camera.main or default forward
            Camera mainCam = Camera.main;
            _aimDirection = (mainCam != null) ? mainCam.transform.forward : Vector3.forward;
        }
        
        if (_animationModule != null) {
            _animationModule.AimDirection = _aimDirection;
        }

        UpdateMovement();
        UpdateAirControl();
        UpdateAirSteering(); // Gentle steering layer on top of physics
        UpdateShooting(); // Handle shooting mechanics
    }
    
    private void UpdateMovement() {
        // Check if animation module is assigned
        if (_animationModule == null || _animationModule.Animator == null) return;
        
        // Disable all movement if manual ragdoll is active
        if (_manualRagdoll) {
            _animationModule.Animator.SetBool("moving", false);
            ResetSpeedBoost();
            return;
        }
        
        if (_movement == Vector2.zero || !_enableMovement) {
            _animationModule.Animator.SetBool("moving", false);
            ResetSpeedBoost();
            return;
        }

        _animationModule.Animator.SetBool("moving", true);
        
        // Progressive speed boost - only while running and on floor
        if (_isRunning && _enableRun && _activeRagdoll.Input.IsOnFloor && _speedBoostInterval > 0) {
            _runningTimer += Time.deltaTime;
            
            // Only start boosting after buffer period
            if (_runningTimer >= _speedBoostBufferTime) {
                _speedBoostTimer += Time.deltaTime;
                
                // Apply boost every interval
                while (_speedBoostTimer >= _speedBoostInterval) {
                    _currentSpeedBoostMultiplier += _speedBoostPercentage;
                    _speedBoostTimer -= _speedBoostInterval;
                }
                
                // Clamp to max speed
                _currentSpeedBoostMultiplier = Mathf.Min(_currentSpeedBoostMultiplier, _maxSpeedBoostMultiplier);
            }
        } else if (!_isRunning) {
            // Reset if not running
            ResetSpeedBoost();
        }
        
        // Calculate speed based on whether running or walking
        float currentSpeed = _walkSpeed;
        if (_isRunning && _enableRun) {
            currentSpeed *= _runSpeedMultiplier;
            currentSpeed *= _currentSpeedBoostMultiplier; // Apply progressive boost
        }
        
        _animationModule.Animator.SetFloat("speed", _movement.magnitude * currentSpeed);        

        float angleOffset = Vector2.SignedAngle(_movement, Vector2.up);
        Vector3 targetForward = Quaternion.AngleAxis(angleOffset, Vector3.up) * Auxiliary.GetFloorProjection(_aimDirection);
        _physicsModule.TargetDirection = targetForward;
    }
    
    private void UpdateAirControl() {
        // Only apply air control when:
        // 1. Air control is enabled
        // 2. Character is airborne (not on floor)
        // 3. Manual ragdoll is NOT active
        // 4. There is movement input
        if (!_enableAirControl || _activeRagdoll.Input.IsOnFloor || _manualRagdoll || _movement == Vector2.zero)
            return;
        
        // Get current velocity
        Vector3 currentVelocity = _activeRagdoll.PhysicalTorso.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        
        // Check if we're below max air speed
        if (horizontalVelocity.magnitude >= _maxAirSpeed)
            return;
        
        // Calculate desired movement direction based on input and camera
        float angleOffset = Vector2.SignedAngle(_movement, Vector2.up);
        Vector3 desiredDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * Auxiliary.GetFloorProjection(_aimDirection);
        desiredDirection.Normalize();
        
        if (_useDirectionalForce) {
            // Apply directional force to the torso
            Vector3 force = desiredDirection * _airControlForce * _movement.magnitude;
            _activeRagdoll.PhysicalTorso.AddForce(force, ForceMode.Force);
        }
        
        // Apply rotational torque to help orient the character
        if (_airRotationTorque > 0) {
            // Calculate the rotation needed to face the desired direction
            Vector3 currentForward = _activeRagdoll.PhysicalTorso.transform.forward;
            Vector3 currentForwardFlat = new Vector3(currentForward.x, 0, currentForward.z).normalized;
            Vector3 desiredDirectionFlat = new Vector3(desiredDirection.x, 0, desiredDirection.z).normalized;
            
            // Calculate torque perpendicular to both directions
            Vector3 torqueDirection = Vector3.Cross(currentForwardFlat, desiredDirectionFlat);
            float torqueMagnitude = Vector3.Angle(currentForwardFlat, desiredDirectionFlat) / 180f; // Normalize to 0-1
            
            Vector3 torque = torqueDirection * _airRotationTorque * torqueMagnitude * _movement.magnitude;
            _activeRagdoll.PhysicalTorso.AddTorque(torque, ForceMode.Force);
        }
    }
    
    /// <summary> 
    /// Gentle steering layer that works on top of existing physics.
    /// Applies a constant, gentle force without interfering with momentum or ragdoll behavior.
    /// Think of it like leaning in the air or using a parachute to steer.
    /// Progressive system: starts weak for short jumps, ramps up for long falls.
    /// </summary>
    private void UpdateAirSteering() {
        // Reset timer when on floor
        if (_activeRagdoll.Input.IsOnFloor) {
            _timeAirborne = 0f;
            return;
        }
        
        // Only steer when:
        // 1. Steering is enabled
        // 2. Character is airborne
        // 3. Manual ragdoll is NOT active
        // 4. There is movement input
        if (!_enableAirSteering || _manualRagdoll || _movement == Vector2.zero)
            return;
        
        // Track time airborne
        _timeAirborne += Time.deltaTime;
        
        // Calculate current steering force based on time airborne
        // Smoothly ramps from base to full over the delay period
        float steeringProgress = Mathf.Clamp01(_timeAirborne / _steeringPowerUpDelay);
        float currentSteeringForce = Mathf.Lerp(_baseAirSteeringForce, _fullAirSteeringForce, steeringProgress);
        
        // Calculate steering direction based on input and camera
        float angleOffset = Vector2.SignedAngle(_movement, Vector2.up);
        Vector3 steeringDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * Auxiliary.GetFloorProjection(_aimDirection);
        steeringDirection.Normalize();
        
        // Apply steering force (progressively stronger the longer you're airborne)
        // This is purely additive and doesn't check velocity or modify existing physics
        Vector3 steeringForce = steeringDirection * currentSteeringForce * _movement.magnitude;
        _activeRagdoll.PhysicalTorso.AddForce(steeringForce, ForceMode.Force);
    }

    private void ProcessFloorChanged(bool onFloor) {
        // Don't change balance mode if manual ragdoll is active
        if (!_manualRagdoll) {
            if (onFloor) {
                // Check for momentum preservation on landing
                bool shouldPreserveMomentum = CheckShouldPreserveMomentum();
                
                if (shouldPreserveMomentum) {
                    // Restore pre-jump momentum
                    RestoreMomentum();
                    // Debug.Log($"Momentum preserved! Speed boost: {_currentSpeedBoostMultiplier:F2}x");
                }
                
                // Clear jump tracking
                _wasJumping = false;
                
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.STABILIZER_JOINT);
                _enableMovement = true;
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(1);
                _animationModule.PlayAnimation("Idle");
                
                // Exit ragdoll mode when landing
                if (_ragdollWhileJumping) {
                    _activeRagdoll.SetStrengthScaleForAllBodyParts(1f);
                }
            }
            else {
                // Store state when leaving floor
                StorePreJumpState();
                
                // Reset speed boost when leaving floor (falling/losing balance)
                // BUT only if momentum preservation is disabled
                if (!_enableMomentumPreservation) {
                    ResetSpeedBoost();
                }
                
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.MANUAL_TORQUE);
                _enableMovement = false;
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0.1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0.05f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0.05f);
                _animationModule.PlayAnimation("InTheAir");
                
                // Enter ragdoll mode when airborne (if enabled)
                if (_ragdollWhileJumping) {
                    _activeRagdoll.SetStrengthScaleForAllBodyParts(0f);
                    // Arms will be enabled on-demand when player tries to use them
                }
            }
        }
    }

    /// <summary> Reset progressive speed boost to default </summary>
    private void ResetSpeedBoost() {
        _runningTimer = 0f;
        _speedBoostTimer = 0f;
        _currentSpeedBoostMultiplier = 1f;
    }
    
    /// <summary> Store current movement state before leaving the ground </summary>
    private void StorePreJumpState() {
        if (!_enableMomentumPreservation)
            return;
            
        _timeLeftGround = Time.time;
        _preJumpSpeedBoostMultiplier = _currentSpeedBoostMultiplier;
        _preJumpRunningTimer = _runningTimer;
        _preJumpSpeedBoostTimer = _speedBoostTimer;
        
        // Store horizontal velocity
        Vector3 velocity = _activeRagdoll.PhysicalTorso.linearVelocity;
        _preJumpHorizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
    }
    
    /// <summary> Check if the landing qualifies for momentum preservation </summary>
    private bool CheckShouldPreserveMomentum() {
        if (!_enableMomentumPreservation)
            return false;
            
        // Must have been jumping
        if (!_wasJumping)
            return false;
            
        // Check if within tracking window
        float timeSinceLeftGround = Time.time - _timeLeftGround;
        if (timeSinceLeftGround > _jumpTrackingWindow)
            return false;
            
        // Check if we had significant speed
        if (_preJumpHorizontalVelocity.magnitude < _minVelocityToPreserve)
            return false;
            
        // Check if landing is upright (torso relatively vertical)
        float tiltAngle = Vector3.Angle(_activeRagdoll.PhysicalTorso.transform.up, Vector3.up);
        if (tiltAngle > _maxUprightTiltAngle)
            return false;
            
        // Check current horizontal velocity matches pre-jump direction
        Vector3 currentVelocity = _activeRagdoll.PhysicalTorso.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        
        // Allow some deviation but ensure we're still moving in a similar direction
        if (currentHorizontalVelocity.magnitude < _minVelocityToPreserve * 0.5f)
            return false;
            
        float velocityAngle = Vector3.Angle(_preJumpHorizontalVelocity, currentHorizontalVelocity);
        if (velocityAngle > 90f) // Moving in opposite direction
            return false;
        
        return true;
    }
    
    /// <summary> Restore pre-jump momentum and speed boost </summary>
    private void RestoreMomentum() {
        _currentSpeedBoostMultiplier = _preJumpSpeedBoostMultiplier;
        _runningTimer = _preJumpRunningTimer;
        _speedBoostTimer = _preJumpSpeedBoostTimer;
    }

    /// <summary> Make the player move and rotate </summary>
    private void MovementInput(Vector2 movement) {
        _movement = movement;
    }

    /// <summary> Handle run input </summary>
    private void RunInput(bool isRunning) {
        if (_enableRun)
            _isRunning = isRunning;
    }

    /// <summary> Handle jump input </summary>
    private void JumpInput() {
        // Don't jump if game is paused (prevents jump sound in pause menu)
        if (_pauseMenuManager != null && _pauseMenuManager.IsPaused)
            return;
        
        // Disable jump if manual ragdoll is active
        if (_manualRagdoll)
            return;
            
        if (!_enableJump || !_activeRagdoll.Input.IsOnFloor)
            return;

        // Check cooldown
        if (Time.time - _lastJumpTime < _jumpCooldown)
            return;

        // Mark that we're jumping (for momentum preservation)
        _wasJumping = true;
        
        // Reset speed boost when jumping (only if momentum preservation is disabled)
        if (!_enableMomentumPreservation) {
            ResetSpeedBoost();
        }

        // Apply jump force to the torso
        _activeRagdoll.PhysicalTorso.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _lastJumpTime = Time.time;
        
        // Play jump sound
        if (_audioController != null) {
            _audioController.PlayJumpSound();
        }
    }

    /// <summary> Handle ragdoll mode toggle </summary>
    private void RagdollInput(bool isRagdoll) {
        // Block ragdoll toggle if level is completed
        if (_levelCompleted) return;
        
        _manualRagdoll = isRagdoll;
        
        if (isRagdoll) {
            // PURE RAGDOLL MODE - TOTALLY SUPERSEDES ALL OTHER RAGDOLL MODES
            // Save current balance mode and disable ALL physics balancing
            _previousBalanceMode = _physicsModule.BalanceMode;
            _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.NONE);
            
            // Go full ragdoll - disable all body part strength completely
            _activeRagdoll.SetStrengthScaleForAllBodyParts(0f);
            
            // Explicitly override individual body parts to ensure jump ragdoll is cancelled
            // (jump ragdoll sets these to non-zero values which causes balancing)
            _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0f);
            _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0f);
            _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0f);
            
            // Activate Datamosh glitch effect if enabled
            if (_datamoshGlitchOnRagdoll && _datamoshEffect != null) {
                _datamoshEffect.Glitch();
            }
            
            // Start ragdoll loop sound
            if (_audioController != null) {
                _audioController.StartRagdollSound();
            }
        } else {
            // Deactivate Datamosh glitch effect if it was active
            if (_datamoshGlitchOnRagdoll && _datamoshEffect != null) {
                _datamoshEffect.Reset();
            }
            
            // Stop ragdoll loop sound
            if (_audioController != null) {
                _audioController.StopRagdollSound();
            }
            
            // Restore appropriate balance mode based on current state
            if (_activeRagdoll.Input.IsOnFloor) {
                // On ground - use stabilizer joint
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.STABILIZER_JOINT);
                _activeRagdoll.SetStrengthScaleForAllBodyParts(1f);
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(1);
                _enableMovement = true;
            } else {
                // In air - restore jump ragdoll mode
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.MANUAL_TORQUE);
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0.1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0.05f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0.05f);
                if (_ragdollWhileJumping) {
                    _activeRagdoll.SetStrengthScaleForAllBodyParts(0f);
                    // Arms will be enabled on-demand when player tries to use them
                }
            }
        }
    }

    /// <summary> Handle left arm input - enables arm strength when jumping and grabbing </summary>
    private void LeftArmInput(float armWeight) {
        _leftArmInput = armWeight;
        
        if (!_manualRagdoll) {
            _animationModule.UseLeftArm(armWeight);
            _gripModule.UseLeftGrip(armWeight);
            
            // Enable left arm strength when jumping AND trying to grab
            if (_ragdollWhileJumping && !_activeRagdoll.Input.IsOnFloor && armWeight > 0) {
                _activeRagdoll.GetBodyPart("Left Arm")?.SetStrengthScale(1f);
            }
            // Disable arm strength when not trying to grab while jumping
            else if (_ragdollWhileJumping && !_activeRagdoll.Input.IsOnFloor && armWeight == 0) {
                _activeRagdoll.GetBodyPart("Left Arm")?.SetStrengthScale(0f);
            }
        }
    }
    
    /// <summary> Handle right arm input - enables arm strength when jumping and grabbing </summary>
    private void RightArmInput(float armWeight) {
        _rightArmInput = armWeight;
        
        if (!_manualRagdoll) {
            _animationModule.UseRightArm(armWeight);
            _gripModule.UseRightGrip(armWeight);
            
            // Enable right arm strength when jumping AND trying to grab
            if (_ragdollWhileJumping && !_activeRagdoll.Input.IsOnFloor && armWeight > 0) {
                _activeRagdoll.GetBodyPart("Right Arm")?.SetStrengthScale(1f);
            }
            // Disable arm strength when not trying to grab while jumping
            else if (_ragdollWhileJumping && !_activeRagdoll.Input.IsOnFloor && armWeight == 0) {
                _activeRagdoll.GetBodyPart("Right Arm")?.SetStrengthScale(0f);
            }
        }
    }

    /// <summary> Handle time rewind input </summary>
    private void RewindInput(bool isRewinding) {
        if (_timeRewindController == null)
            return;
        
        if (isRewinding) {
            _timeRewindController.StartRewind();
            
            // Activate analog glitch effect when rewinding
            if (_analogGlitchOnRewind && _analogGlitchEffect != null) {
                _analogGlitchEffect.enabled = true;
            }
        } else {
            _timeRewindController.StopRewind();
            
            // Deactivate analog glitch effect when stopping rewind
            if (_analogGlitchOnRewind && _analogGlitchEffect != null) {
                _analogGlitchEffect.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Handle scene control inputs when level is completed
    /// </summary>
    private void HandleLevelCompleteInputs() {
        // Square (gamepad) or Spacebar (keyboard) - Reload current scene
        bool reloadPressed = (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) ||
                             (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        
        if (reloadPressed) {
            // Debug.Log("Reloading current scene...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }
        
        // Circle (gamepad) or Enter (keyboard) - Return to title screen
        bool titlePressed = (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ||
                            (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame);
        
        if (titlePressed) {
            // Debug.Log("Returning to title screen...");
            SceneManager.LoadScene("_Title Screen");
            return;
        }
    }
    
    /// <summary>
    /// Update shooting mechanics - fire once per trigger press
    /// </summary>
    private void UpdateShooting() {
        if (!_enableShooting) return;
        
        // LEFT TRIGGER (L2) - Left Hand
        // Check if trigger was just pressed this frame
        if (_leftArmInput > 0.5f && !_leftTriggerPressed) {
            // Trigger just pressed - fire immediately!
            _leftTriggerPressed = true;
            FireShot(HumanBodyBones.LeftHand, "LEFT");
        }
        // Check if trigger was released
        else if (_leftArmInput <= 0.5f && _leftTriggerPressed) {
            // Trigger released - reset for next shot
            _leftTriggerPressed = false;
        }
        
        // RIGHT TRIGGER (R2) - Right Hand
        // Check if trigger was just pressed this frame
        if (_rightArmInput > 0.5f && !_rightTriggerPressed) {
            // Trigger just pressed - fire immediately!
            _rightTriggerPressed = true;
            FireShot(HumanBodyBones.RightHand, "RIGHT");
        }
        // Check if trigger was released
        else if (_rightArmInput <= 0.5f && _rightTriggerPressed) {
            // Trigger released - reset for next shot
            _rightTriggerPressed = false;
        }
    }
    
    /// <summary>
    /// Fire a single ray from the specified hand (in camera direction)
    /// </summary>
    private void FireShot(HumanBodyBones handBone, string handName) {
        // Check ammo before firing
        if (_hasLimitedAmmo) {
            if (_currentAmmo <= 0) {
                // Out of ammo!
                Debug.Log("<color=red>[Shooting]</color> OUT OF AMMO! Gun is empty.");
                // Disable shooting when out of ammo
                _enableShooting = false;
                // Hide gun models in hands
                HideHandGuns();
                return;
            }
            
            // Consume 1 ammo
            _currentAmmo--;
            // Debug.Log($"<color=yellow>[Ammo]</color> Shot fired! Ammo remaining: {_currentAmmo}");
            
            // Check if that was the last shot
            if (_currentAmmo <= 0) {
                Debug.Log("<color=orange>[Ammo]</color> Last shot! Gun is now empty.");
            }
        }
        
        // Get the hand transform
        Transform hand = _activeRagdoll.GetPhysicalBone(handBone);
        if (hand == null) {
            // Debug.LogWarning($"<color=red>[Shoot]</color> Could not find {handName} hand bone!");
            return;
        }
        
        // Get hand position and camera forward direction
        Vector3 handPos = hand.position;
        Vector3 shootDirection = Vector3.forward;
        if (_cameraModule != null && _cameraModule.Camera != null) {
            shootDirection = _cameraModule.Camera.transform.forward;
        } else {
            Camera mainCam = Camera.main;
            shootDirection = (mainCam != null) ? mainCam.transform.forward : Vector3.forward;
        }
        
        // Debug.Log($"<color=cyan>[SHOT FIRED!]</color> {handName} hand shot from {handPos} in direction {shootDirection}");
        
        // Play bullet fire sound
        if (_audioController != null) {
            _audioController.PlayBulletFireSound();
        }
        
        // Show aim indicator UI (Duck Hunt style red dot)
        if (_aimIndicatorUI != null) {
            // Option 1: Screen center (simple)
            _aimIndicatorUI.ShowAimIndicator();
            
            // Option 2: Show at actual aim point in 3D space (more accurate)
            // Vector3 aimPoint = handPos + shootDirection * 10f; // 10 units ahead
            // _aimIndicatorUI.ShowAimIndicator(aimPoint);
        }
        
        // Spawn and animate the bullet as a true projectile (no instant raycast)
        if (_bulletPrefab != null) {
            // Debug.Log($"<color=magenta>[Projectile]</color> Spawning projectile bullet at speed {_bulletSpeed} units/s");
            StartCoroutine(AnimateProjectileBullet(handPos, shootDirection));
        } else {
            // Debug.LogError("<color=red>[Bullet ERROR]</color> Cannot spawn bullet - prefab is NULL!");
        }
    }
    
    /// <summary>
    /// Animate bullet as a true projectile - moves forward and checks for hits each frame
    /// </summary>
    private IEnumerator AnimateProjectileBullet(Vector3 startPos, Vector3 direction) {
        if (_bulletPrefab == null) {
            // Debug.LogWarning("<color=red>[Bullet]</color> No bullet prefab assigned!");
            yield break;
        }
        
        // Instantiate the bullet at hand position
        GameObject bullet = Instantiate(_bulletPrefab, startPos, Quaternion.identity);
        
        // Make bullet larger for visibility
        bullet.transform.localScale *= 3f;
        
        // Point the bullet in the direction of travel
        bullet.transform.rotation = Quaternion.LookRotation(direction);
        
        // Track distance traveled
        float distanceTraveled = 0f;
        
        // Move bullet forward until it hits something or reaches max distance
        while (distanceTraveled < _shotMaxDistance) {
            // Calculate movement this frame
            float moveDistance = _bulletSpeed * Time.deltaTime;
            Vector3 nextPosition = bullet.transform.position + direction * moveDistance;
            
            // Check for collision along the path using SphereCast
            RaycastHit hit;
            if (Physics.SphereCast(bullet.transform.position, _shotRadius, direction, out hit, moveDistance, _shotLayerMask)) {
                // Move bullet to exact hit point
                bullet.transform.position = hit.point;
                
                // Check if we hit an AI ragdoll - search aggressively up the hierarchy
                RespawnableAIRagdoll aiRagdoll = null;
                Transform current = hit.collider.transform;
                int safetyCounter = 0;
                
                // Walk up the hierarchy until we find the component or reach the top
                while (current != null && aiRagdoll == null && safetyCounter < 20) {
                    aiRagdoll = current.GetComponent<RespawnableAIRagdoll>();
                    current = current.parent;
                    safetyCounter++;
                }
                
            if (aiRagdoll != null) {
                // Check if this was a headshot
                bool isHeadshot = IsHeadshot(hit.collider, aiRagdoll);
                
                // Spawn explosion particle effect at impact point (if enabled on this AI)
                if (aiRagdoll.enableBulletImpactEffect && aiRagdoll.bulletImpactEffectPrefab != null) {
                    GameObject explosion = Instantiate(aiRagdoll.bulletImpactEffectPrefab, hit.point, Quaternion.identity);
                    // Particle will auto-play on spawn, destroy after 1 second
                    Destroy(explosion, 1f);
                }
                
                // NEW DAMAGE SYSTEM: Use TakeDamage instead of instant death
                // This allows for limb damage and bleed out mechanics
                aiRagdoll.TakeDamage(hit.collider, isHeadshot);
                
                // Award kill points ONLY on critical hits (death)
                // Points are now awarded in the Respawn() method after actual death
                // (either instant from critical hit or after bleed out)
            }
                
                // Destroy bullet on impact
                yield return new WaitForSeconds(0.1f);
                Destroy(bullet);
                yield break;
            }
            
            // No hit - continue moving
            bullet.transform.position = nextPosition;
            distanceTraveled += moveDistance;
            
            yield return null;
        }
        
        // Reached max distance without hitting anything
        Destroy(bullet);
    }
    
    /// <summary>
    /// Check if the hit collider is the head of the AI ragdoll
    /// </summary>
    private bool IsHeadshot(Collider hitCollider, RespawnableAIRagdoll aiRagdoll) {
        // Get the ActiveRagdoll component from the AI
        ActiveRagdoll.ActiveRagdoll aiActiveRagdoll = aiRagdoll.GetComponent<ActiveRagdoll.ActiveRagdoll>();
        if (aiActiveRagdoll == null) return false;
        
        // Get the head bone transform from the AI's ragdoll
        Transform headBone = aiActiveRagdoll.GetPhysicalBone(HumanBodyBones.Head);
        if (headBone == null) return false;
        
        // Check if the hit collider is on the head bone or its children
        // Compare the hit collider's gameObject with the head bone's gameObject
        Transform currentTransform = hitCollider.transform;
        
        // Walk up the hierarchy to see if we find the head bone
        while (currentTransform != null) {
            if (currentTransform == headBone) {
                // Hit the head!
                return true;
            }
            
            // Stop if we've reached the ragdoll root (avoid infinite loop)
            if (currentTransform == aiActiveRagdoll.transform) {
                break;
            }
            
            currentTransform = currentTransform.parent;
        }
        
        return false;
    }
    
    /// <summary>
    /// Draw a debug sphere using multiple circles
    /// </summary>
    private void DrawDebugSphere(Vector3 center, float radius, Color color, float duration) {
        // Draw 3 circles to visualize the sphere
        int segments = 16;
        
        // XY plane circle
        for (int i = 0; i < segments; i++) {
            float angle1 = (i / (float)segments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);
            Debug.DrawLine(p1, p2, color, duration);
        }
        
        // XZ plane circle
        for (int i = 0; i < segments; i++) {
            float angle1 = (i / (float)segments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);
            Debug.DrawLine(p1, p2, color, duration);
        }
        
        // YZ plane circle
        for (int i = 0; i < segments; i++) {
            float angle1 = (i / (float)segments) * Mathf.PI * 2f;
            float angle2 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            Vector3 p1 = center + new Vector3(0, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
            Vector3 p2 = center + new Vector3(0, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);
            Debug.DrawLine(p1, p2, color, duration);
        }
    }
    
    /// <summary>
    /// Enable permanent ragdoll mode (for level completion)
    /// </summary>
    public void EnablePermanentRagdollMode() {
        // Set level completed flag - this blocks ragdoll toggle and enables scene controls
        _levelCompleted = true;
        
        // Stop any existing ragdoll sound (don't want it persisting through level finish)
        if (_audioController != null) {
            _audioController.StopRagdollSound();
        }
        
        // Force ragdoll mode on
        _manualRagdoll = true;
        
        // Save current balance mode and disable ALL physics balancing
        _previousBalanceMode = _physicsModule.BalanceMode;
        _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.NONE);
        
        // Go full ragdoll - disable all body part strength completely
        _activeRagdoll.SetStrengthScaleForAllBodyParts(0f);
        
        // Explicitly override individual body parts
        _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0f);
        _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0f);
        _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0f);
        
        // Activate Datamosh glitch effect if enabled
        if (_datamoshGlitchOnRagdoll && _datamoshEffect != null) {
            _datamoshEffect.Glitch();
        }
        
        // Debug.Log("Level completed! Permanent ragdoll mode enabled. Press Square/Spacebar to restart or Circle/Enter for title screen.");
    }
    
    /// <summary>
    /// Enable shooting capability with infinite ammo (called by GunPickup)
    /// NOTE: Hand items are now controlled by GunPickup calling ShowSpecificHandItems() directly
    /// </summary>
    public void EnableShooting()
    {
        _enableShooting = true;
        _hasLimitedAmmo = false;
        _currentAmmo = -1; // -1 = infinite
        
        // Debug.Log("<color=green>[Shooting]</color> Shooting ENABLED for player with INFINITE ammo!");
    }
    
    /// <summary>
    /// Enable shooting capability with limited ammo (called by GunPickup with ammo count)
    /// NOTE: Hand items are now controlled by GunPickup calling ShowSpecificHandItems() directly
    /// </summary>
    public void EnableShooting(int ammo)
    {
        _enableShooting = true;
        _hasLimitedAmmo = true;
        
        // CUMULATIVE AMMO: Add to existing ammo instead of replacing it
        // If currently disabled or out of ammo, start fresh. Otherwise, add to existing.
        if (_currentAmmo <= 0) {
            _currentAmmo = ammo;
            Debug.Log($"<color=green>[Shooting]</color> First gun picked up! Starting with {ammo} shots!");
        } else {
            _currentAmmo += ammo;
            Debug.Log($"<color=green>[Shooting]</color> Gun picked up! Added {ammo} shots. Total ammo: {_currentAmmo}!");
        }
    }
    
    /// <summary>
    /// Disable shooting capability (called on respawn)
    /// </summary>
    public void DisableShooting()
    {
        _enableShooting = false;
        _hasLimitedAmmo = false;
        _currentAmmo = -1;
        
        // Hide gun models in hands
        HideHandGuns();
        
        // Debug.Log("<color=yellow>[Shooting]</color> Shooting DISABLED for player (respawn reset)");
    }
    
    /// <summary>
    /// Show specific hand items by name (called by GunPickup with prefab references)
    /// </summary>
    public void ShowSpecificHandItems(string leftItemName, string rightItemName)
    {
        if (_activeRagdoll == null) return;
        
        // Get hand transforms
        Transform leftHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftHand);
        Transform rightHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightHand);
        
        // Debug.Log($"<color=cyan>[Hand Items]</color> Activating items - Left: '{leftItemName}', Right: '{rightItemName}'");
        
        // Process left hand
        if (leftHand != null)
        {
            foreach (Transform child in leftHand)
            {
                // Skip gripper objects
                if (child.name.ToLower().Contains("gripper")) continue;
                
                // Get clean name (remove (Clone) suffix if present)
                string childName = child.name.Replace("(Clone)", "").Trim();
                
                // Show if name matches, hide if it doesn't
                if (leftItemName == "None")
                {
                    child.gameObject.SetActive(false);
                }
                else if (childName.Equals(leftItemName, System.StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(true);
                    // Debug.Log($"<color=green>[Hand Items]</color> ✓ Showing '{childName}' in LEFT hand!");
                }
                else
                {
                    // Hide other items
                    child.gameObject.SetActive(false);
                    // Debug.Log($"<color=yellow>[Hand Items]</color> ✗ Hiding '{childName}' in LEFT hand (doesn't match '{leftItemName}')");
                }
            }
        }
        
        // Process right hand
        if (rightHand != null)
        {
            foreach (Transform child in rightHand)
            {
                // Skip gripper objects
                if (child.name.ToLower().Contains("gripper")) continue;
                
                // Get clean name (remove (Clone) suffix if present)
                string childName = child.name.Replace("(Clone)", "").Trim();
                
                // Show if name matches, hide if it doesn't
                if (rightItemName == "None")
                {
                    child.gameObject.SetActive(false);
                }
                else if (childName.Equals(rightItemName, System.StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(true);
                    // Debug.Log($"<color=green>[Hand Items]</color> ✓ Showing '{childName}' in RIGHT hand!");
                }
                else
                {
                    // Hide other items
                    child.gameObject.SetActive(false);
                    // Debug.Log($"<color=yellow>[Hand Items]</color> ✗ Hiding '{childName}' in RIGHT hand (doesn't match '{rightItemName}')");
                }
            }
        }
    }
    
    /// <summary>
    /// Show gun models in the player's hands (called when gun is picked up)
    /// LEGACY: Kept for backward compatibility, shows all items
    /// </summary>
    private void ShowHandGuns()
    {
        // If hand gun models array is not set, try to auto-find them
        if (_handGunModels == null || _handGunModels.Length == 0)
        {
            _handGunModels = FindHandGunModels();
        }
        
        // Activate all gun model GameObjects
        if (_handGunModels != null)
        {
            foreach (GameObject gunModel in _handGunModels)
            {
                if (gunModel != null)
                {
                    gunModel.SetActive(true);
                }
            }
            
            Debug.Log($"<color=green>[Gun Models]</color> Showing {_handGunModels.Length} gun model(s) in hands!");
        }
    }
    
    /// <summary>
    /// Hide all hand items (called on respawn or when shooting disabled)
    /// </summary>
    private void HideHandGuns()
    {
        if (_activeRagdoll == null) return;
        
        // Get hand transforms
        Transform leftHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftHand);
        Transform rightHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightHand);
        
        // Hide all children in left hand (except grippers)
        if (leftHand != null)
        {
            foreach (Transform child in leftHand)
            {
                if (!child.name.ToLower().Contains("gripper"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        
        // Hide all children in right hand (except grippers)
        if (rightHand != null)
        {
            foreach (Transform child in rightHand)
            {
                if (!child.name.ToLower().Contains("gripper"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
    
    /// <summary>
    /// Auto-find gun model GameObjects in the hand bones
    /// Searches for any child GameObjects under the left/right hand bones
    /// </summary>
    private GameObject[] FindHandGunModels()
    {
        List<GameObject> foundGuns = new List<GameObject>();
        
        if (_activeRagdoll == null) return foundGuns.ToArray();
        
        // Get left and right hand transforms
        Transform leftHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftHand);
        Transform rightHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightHand);
        
        // Search left hand for gun models (ignore Gripper components)
        if (leftHand != null)
        {
            foreach (Transform child in leftHand)
            {
                // Skip gripper objects and only include actual mesh/model objects
                if (child.name.ToLower().Contains("gun") || child.GetComponent<MeshRenderer>() != null || child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    if (!child.name.ToLower().Contains("gripper"))
                    {
                        foundGuns.Add(child.gameObject);
                        Debug.Log($"<color=cyan>[Gun Models]</color> Found gun model in LEFT hand: {child.name}");
                    }
                }
            }
        }
        
        // Search right hand for gun models (ignore Gripper components)
        if (rightHand != null)
        {
            foreach (Transform child in rightHand)
            {
                // Skip gripper objects and only include actual mesh/model objects
                if (child.name.ToLower().Contains("gun") || child.GetComponent<MeshRenderer>() != null || child.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    if (!child.name.ToLower().Contains("gripper"))
                    {
                        foundGuns.Add(child.gameObject);
                        Debug.Log($"<color=cyan>[Gun Models]</color> Found gun model in RIGHT hand: {child.name}");
                    }
                }
            }
        }
        
        if (foundGuns.Count == 0)
        {
            Debug.LogWarning("<color=yellow>[Gun Models]</color> No gun models found in hands! Make sure gun GameObjects are children of the hand bones.");
        }
        
        return foundGuns.ToArray();
    }
    
    /// <summary>
    /// Get current ammo count (-1 = infinite, 0 = out of ammo)
    /// </summary>
    public int GetCurrentAmmo()
    {
        return _currentAmmo;
    }
    
    /// <summary>
    /// Check if gun has limited ammo
    /// </summary>
    public bool HasLimitedAmmo()
    {
        return _hasLimitedAmmo;
    }
    
    /// <summary>
    /// Check if shooting is currently enabled
    /// </summary>
    public bool IsShootingEnabled()
    {
        return _enableShooting;
    }
    
    /// <summary>
    /// Check if player has infinite ammo
    /// </summary>
    public bool HasInfiniteAmmo()
    {
        return _enableShooting && !_hasLimitedAmmo;
    }
    
    /// <summary>
    /// Get the name of the currently active left hand item (or null if none)
    /// </summary>
    public string GetActiveLeftHandItem()
    {
        if (_activeRagdoll == null) return null;
        
        Transform leftHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftHand);
        if (leftHand == null) return null;
        
        foreach (Transform child in leftHand)
        {
            // Skip gripper objects
            if (child.name.ToLower().Contains("gripper")) continue;
            
            // Check if this child is active
            if (child.gameObject.activeSelf)
            {
                // Return clean name (remove (Clone) suffix if present)
                return child.name.Replace("(Clone)", "").Trim();
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the name of the currently active right hand item (or null if none)
    /// </summary>
    public string GetActiveRightHandItem()
    {
        if (_activeRagdoll == null) return null;
        
        Transform rightHand = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightHand);
        if (rightHand == null) return null;
        
        foreach (Transform child in rightHand)
        {
            // Skip gripper objects
            if (child.name.ToLower().Contains("gripper")) continue;
            
            // Check if this child is active
            if (child.gameObject.activeSelf)
            {
                // Return clean name (remove (Clone) suffix if present)
                return child.name.Replace("(Clone)", "").Trim();
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Disable datamosh visual effect (called by RedLightGreenLightManager)
    /// This allows ragdoll mode to work without the visual glitch effect
    /// </summary>
    public void DisableDatamoshEffect()
    {
        _datamoshGlitchOnRagdoll = false;
        
        // Also disable the component if it exists
        if (_datamoshEffect != null)
        {
            _datamoshEffect.enabled = false;
        }
        
        // Debug.Log("<color=cyan>[RLGL]</color> Datamosh effect DISABLED for Red Light Green Light mode");
    }
}
