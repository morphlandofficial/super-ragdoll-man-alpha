using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ActiveRagdoll;

/// <summary> Title Screen version of Default behaviour - Safe copy for modifications </summary>
public class TitlePlayerDefaultBehavior : MonoBehaviour {
    // Author: Sergio Abreu García | https://sergioabreu.me
    // Modified for Title Screen use

    [Header("Modules")]
    [SerializeField] private ActiveRagdoll.ActiveRagdoll _activeRagdoll;
    [SerializeField] private PhysicsModule _physicsModule;
    [SerializeField] private AnimationModule _animationModule;
    [SerializeField] private GripModule _gripModule;
    [SerializeField] private CameraModule _cameraModule;
    [SerializeField] private TimeRewindController _timeRewindController;
    private CharacterAudioController _audioController;

    [Header("Movement")]
    [SerializeField] private bool _enableMovement = true;
    [SerializeField] 
    [Tooltip("Use fixed world-space controls for title screen (no camera dependency)")]
    private bool _useFixedWorldControls = true;
    [SerializeField]
    [Tooltip("Forward direction in world space (what W does)")]
    private Vector3 _worldForward = new Vector3(0, 0, 1);
    [SerializeField]
    [Tooltip("Right direction in world space (what D does)")]  
    private Vector3 _worldRight = new Vector3(1, 0, 0);
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

    [Header("Ragdoll Settings")]
    [SerializeField] private bool _ragdollWhileJumping = true;
    private bool _manualRagdoll = false;
    private PhysicsModule.BALANCE_MODE _previousBalanceMode;
    
    [Header("Level Complete")]
    private bool _levelCompleted = false;
    
    [Header("Air Steering (Additive Layer)")]
    [SerializeField]
    [Tooltip("Enable gentle steering during falls without affecting core physics")]
    private bool _enableAirSteering = true;
    [SerializeField]
    [Tooltip("Gentle steering force - keeps physics natural (50-200 recommended)")]
    private float _airSteeringForce = 1250f;
    
    [Header("Arm Control While Jumping")]
    private float _leftArmInput = 0f;
    private float _rightArmInput = 0f;
    
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
        
        // Lock cursor when game starts (title screen)
        LockCursor();
    }
    
    private void InitializeDatamoshEffect() {
        // Try to get the Datamosh component if we haven't already
        if (_datamoshGlitchOnRagdoll && _datamoshEffect == null) {
            // Find the currently active camera in the scene
            Camera activeCamera = Camera.main;
            
            // If Camera.main isn't set, find any enabled camera
            if (activeCamera == null) {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera cam in cameras) {
                    if (cam.enabled && cam.gameObject.activeInHierarchy) {
                        activeCamera = cam;
                        break;
                    }
                }
            }
            
            // Try to get the Datamosh component from the active camera
            if (activeCamera != null) {
                _datamoshEffect = activeCamera.GetComponent<Kino.Datamosh>();
                
                if (_datamoshEffect != null) {
                }
                // else {
                //     Debug.LogWarning($"[Title Screen] Datamosh enabled but camera '{activeCamera.gameObject.name}' doesn't have Datamosh component!");
                // }
            }
        }
    }
    
    private void InitializeAnalogGlitchEffect() {
        // Try to get the AnalogGlitch component if we haven't already
        if (_analogGlitchOnRewind && _analogGlitchEffect == null) {
            // Find the currently active camera in the scene
            Camera activeCamera = Camera.main;
            
            // If Camera.main isn't set, find any enabled camera
            if (activeCamera == null) {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (Camera cam in cameras) {
                    if (cam.enabled && cam.gameObject.activeInHierarchy) {
                        activeCamera = cam;
                        break;
                    }
                }
            }
            
            // Try to get the AnalogGlitch component from the active camera
            if (activeCamera != null) {
                _analogGlitchEffect = activeCamera.GetComponent<Kino.AnalogGlitch>();
                
                // Start with the effect disabled
                if (_analogGlitchEffect != null) {
                    _analogGlitchEffect.enabled = false;
                }
            }
        }
    }

    private void Update() {
        // Handle level complete scene navigation
        if (_levelCompleted) {
            Debug.Log("<color=yellow>[TitlePlayerDefaultBehavior]</color> Level complete - handling navigation inputs");
            HandleLevelCompleteInputs();
            return; // Skip normal update when level is complete
        }
        
        // Handle cursor locking
        HandleCursorLocking();
        
        // Try to initialize effects if not done yet
        if (_datamoshGlitchOnRagdoll && _datamoshEffect == null) {
            InitializeDatamoshEffect();
        }
        if (_analogGlitchOnRewind && _analogGlitchEffect == null) {
            InitializeAnalogGlitchEffect();
        }
        
        // Set aim direction
        if (_useFixedWorldControls) {
            // Fixed world controls: use preset world forward
            _aimDirection = _worldForward.normalized;
        } else if (_cameraModule != null && _cameraModule.Camera != null) {
            // Camera-relative: use camera forward
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
        UpdateAirSteering(); // Gentle steering layer on top of physics

#if UNITY_EDITOR
        // TEST
        // if (Input.GetKeyDown(KeyCode.F1))
        //     Debug.Break();
#endif
    }
    
    private void UpdateMovement() {
        // Disable all movement if manual ragdoll is active
        if (_manualRagdoll) {
            if (_animationModule != null && _animationModule.Animator != null) {
                _animationModule.Animator.SetBool("moving", false);
            }
            ResetSpeedBoost();
            return;
        }
        
        if (_movement == Vector2.zero || !_enableMovement) {
            if (_animationModule != null && _animationModule.Animator != null) {
                _animationModule.Animator.SetBool("moving", false);
            }
            ResetSpeedBoost();
            return;
        }

        if (_animationModule != null && _animationModule.Animator != null) {
            _animationModule.Animator.SetBool("moving", true);
        }
        
        // Progressive speed boost - only while running and on floor
        if (_isRunning && _enableRun && _activeRagdoll != null && _activeRagdoll.Input.IsOnFloor && _speedBoostInterval > 0) {
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
        
        if (_animationModule != null && _animationModule.Animator != null) {
            _animationModule.Animator.SetFloat("speed", _movement.magnitude * currentSpeed);        
        }

        // Calculate target direction
        Vector3 targetForward;
        
        if (_useFixedWorldControls) {
            // FIXED WORLD CONTROLS: Use preset world-space directions
            // Completely independent of camera - perfect for title screen!
            Vector3 forward = _worldForward.normalized;
            Vector3 right = _worldRight.normalized;
            
            // Combine based on input
            Vector3 moveDirection = (right * _movement.x + forward * _movement.y);
            
            if (moveDirection.sqrMagnitude > 0.001f) {
                targetForward = moveDirection.normalized;
            } else {
                targetForward = forward;
            }
        } else {
            // Traditional movement: input rotates around aim direction
            float angleOffset = Vector2.SignedAngle(_movement, Vector2.up);
            targetForward = Quaternion.AngleAxis(angleOffset, Vector3.up) * Auxiliary.GetFloorProjection(_aimDirection);
        }
        
        if (_physicsModule != null) {
            _physicsModule.TargetDirection = targetForward;
        }
    }
    
    /// <summary> 
    /// Gentle steering layer that works on top of existing physics.
    /// Applies a constant, gentle force without interfering with momentum or ragdoll behavior.
    /// Think of it like leaning in the air or using a parachute to steer.
    /// </summary>
    private void UpdateAirSteering() {
        // Only steer when:
        // 1. Steering is enabled
        // 2. Character is airborne
        // 3. Manual ragdoll is NOT active
        // 4. There is movement input
        if (!_enableAirSteering || _activeRagdoll == null || _activeRagdoll.Input.IsOnFloor || _manualRagdoll || _movement == Vector2.zero)
            return;
        
        // Calculate steering direction
        Vector3 steeringDirection;
        
        if (_useFixedWorldControls) {
            // FIXED WORLD CONTROLS: Use preset world-space directions
            Vector3 forward = _worldForward.normalized;
            Vector3 right = _worldRight.normalized;
            
            // Combine based on input
            steeringDirection = (right * _movement.x + forward * _movement.y);
            
            if (steeringDirection.sqrMagnitude > 0.001f) {
                steeringDirection.Normalize();
            } else {
                return; // No valid direction
            }
        } else {
            // Traditional: based on input and camera
            float angleOffset = Vector2.SignedAngle(_movement, Vector2.up);
            steeringDirection = Quaternion.AngleAxis(angleOffset, Vector3.up) * Auxiliary.GetFloorProjection(_aimDirection);
            steeringDirection.Normalize();
        }
        
        // Apply gentle, constant steering force
        // This is purely additive and doesn't check velocity or modify existing physics
        Vector3 steeringForce = steeringDirection * _airSteeringForce * _movement.magnitude;
        _activeRagdoll.PhysicalTorso.AddForce(steeringForce, ForceMode.Force);
    }

    private void ProcessFloorChanged(bool onFloor) {
        // Don't change balance mode if manual ragdoll is active
        if (!_manualRagdoll && _physicsModule != null && _activeRagdoll != null) {
            if (onFloor) {
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.STABILIZER_JOINT);
                _enableMovement = true;
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(1);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(1);
                if (_animationModule != null) {
                    _animationModule.PlayAnimation("Idle");
                }
                
                // Exit ragdoll mode when landing
                if (_ragdollWhileJumping) {
                    _activeRagdoll.SetStrengthScaleForAllBodyParts(1f);
                }
            }
            else {
                // Reset speed boost when leaving floor (falling/losing balance)
                ResetSpeedBoost();
                
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.MANUAL_TORQUE);
                _enableMovement = false;
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0.1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0.05f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0.05f);
                if (_animationModule != null) {
                    _animationModule.PlayAnimation("InTheAir");
                }
                
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
        // Disable jump if manual ragdoll is active
        if (_manualRagdoll || _activeRagdoll == null)
            return;
            
        if (!_enableJump || !_activeRagdoll.Input.IsOnFloor)
            return;

        // Check cooldown
        if (Time.time - _lastJumpTime < _jumpCooldown)
            return;

        // Reset speed boost when jumping
        ResetSpeedBoost();

        // Apply jump force to the torso
        if (_activeRagdoll.PhysicalTorso != null) {
            _activeRagdoll.PhysicalTorso.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        _lastJumpTime = Time.time;
        
        // Play jump sound
        if (_audioController != null) {
            _audioController.PlayJumpSound();
        }
    }

    /// <summary> Handle ragdoll mode toggle </summary>
    private void RagdollInput(bool isRagdoll) {
        if (_physicsModule == null || _activeRagdoll == null) return;
        
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
        if (_activeRagdoll == null || _animationModule == null || _gripModule == null) return;
        
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
        if (_activeRagdoll == null || _animationModule == null || _gripModule == null) return;
        
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
    /// Enable permanent ragdoll mode (for level completion)
    /// </summary>
    public void EnablePermanentRagdollMode() {
        Debug.Log($"<color=cyan>[TitlePlayerDefaultBehavior]</color> EnablePermanentRagdollMode() called! _physicsModule={_physicsModule != null}, _activeRagdoll={_activeRagdoll != null}");
        
        if (_physicsModule == null || _activeRagdoll == null) {
            Debug.LogError("<color=red>[TitlePlayerDefaultBehavior]</color> Cannot enable permanent ragdoll - missing modules!");
            return;
        }
        
        // Set level completed flag - this blocks ragdoll toggle and enables scene controls
        _levelCompleted = true;
        Debug.Log("<color=green>[TitlePlayerDefaultBehavior]</color> _levelCompleted set to TRUE");
        
        // Force ragdoll mode on
        _manualRagdoll = true;
        
        // Save current balance mode and disable ALL physics balancing
        _previousBalanceMode = _physicsModule.BalanceMode;
        _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.NONE);
        Debug.Log($"<color=green>[TitlePlayerDefaultBehavior]</color> Balance mode set to NONE (was {_previousBalanceMode})");
        
        // Go full ragdoll - disable all body part strength completely
        _activeRagdoll.SetStrengthScaleForAllBodyParts(0f);
        Debug.Log("<color=green>[TitlePlayerDefaultBehavior]</color> All body parts strength set to 0");
        
        // Explicitly override individual body parts
        _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0f);
        _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0f);
        _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0f);
        
        // Activate Datamosh glitch effect if enabled
        if (_datamoshGlitchOnRagdoll && _datamoshEffect != null) {
            Debug.Log("<color=magenta>[TitlePlayerDefaultBehavior]</color> Activating Datamosh effect!");
            _datamoshEffect.Glitch();
        } else {
            Debug.Log($"<color=orange>[TitlePlayerDefaultBehavior]</color> Datamosh NOT activated: _datamoshGlitchOnRagdoll={_datamoshGlitchOnRagdoll}, _datamoshEffect={_datamoshEffect != null}");
        }
    }
    
    /// <summary>
    /// Handle scene control inputs when level is completed
    /// </summary>
    private void HandleLevelCompleteInputs() {
        // Square (gamepad) or Spacebar (keyboard) - Reload current scene
        bool reloadPressed = (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonWest.wasPressedThisFrame) ||
                             (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame);
        
        if (reloadPressed) {
            Debug.Log("<color=cyan>[Navigation]</color> SPACEBAR pressed - reloading level...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }
        
        // Circle (gamepad) or Enter (keyboard) - Return to title screen
        bool titlePressed = (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame) ||
                            (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame);
        
        if (titlePressed) {
            Debug.Log("<color=cyan>[Navigation]</color> ENTER pressed - going to title screen...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("_Title Screen");
            return;
        }
    }
    
    // ------------- Cursor Control -------------
    
    /// <summary> Handle cursor locking input </summary>
    private void HandleCursorLocking() {
        // Toggle cursor lock with ESC key
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (Cursor.lockState == CursorLockMode.Locked) {
                UnlockCursor();
            } else {
                LockCursor();
            }
        }
        
        // Re-lock cursor when clicking in game window
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) {
            LockCursor();
        }
    }
    
    /// <summary> Lock and hide the cursor </summary>
    private void LockCursor() {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    /// <summary> Unlock and show the cursor </summary>
    private void UnlockCursor() {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}