using UnityEngine;
using UnityEngine.InputSystem;

public class AxisMovementController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 2f;
    [SerializeField] private MovementAxis movementAxis = MovementAxis.X;
    [SerializeField] private bool invertMovement = false;
    
    [Header("Movement Bounds")]
    [Tooltip("Enable to limit movement range")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private float minPosition = -5f;
    [SerializeField] private float maxPosition = 5f;
    
    [Header("Input Settings")]
    [SerializeField] private float arrowKeySensitivity = 12f;
    [SerializeField] private float gamepadSensitivity = 1f;
    
    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingSpeed = 5f;
    
    [Header("Progressive Speed Boost")]
    [SerializeField] 
    [Tooltip("Speed multiplier applied every 0.5 seconds (exponential growth)")]
    private float speedUpMultiplier = 1.2f;
    [SerializeField]
    [Tooltip("Maximum speed multiplier limit")]
    private float speedLimit = 3f;
    
    [Header("Momentum / Deceleration")]
    [SerializeField]
    [Tooltip("How quickly movement slows down when no input (higher = faster stop)")]
    private float deceleration = 0.15f;
    
    [Header("Click Sounds")]
    [Tooltip("Click sound to play when moving")]
    [SerializeField] private AudioClip clickSound;
    
    [Tooltip("Distance units required for each click")]
    [SerializeField] private float unitsPerClick = 0.5f;
    
    [Tooltip("Volume of click sounds")]
    [SerializeField] private float clickVolume = 0.5f;
    
    [Tooltip("Minimum movement speed to trigger clicks")]
    [SerializeField] private float minMovementSpeedForClick = 0.1f;
    
    public enum MovementAxis
    {
        X,
        Y,
        Z
    }
    
    private ActiveRagdollActions inputActions;
    private Vector2 lookInput;
    private float targetMovementVelocity;
    private float currentMovementVelocity;
    
    // Click sound system
    private AudioSource audioSource;
    private float movementAccumulator = 0f;
    
    // Progressive speed boost tracking
    private float currentSpeedMultiplier = 1f;
    private float movementTimer = 0f;
    private float lastInputDirection = 0f; // -1, 0, or 1
    
    // Store initial position for bounds calculation
    private Vector3 initialPosition;
    
    private void Awake()
    {
        inputActions = new ActiveRagdollActions();
        initialPosition = transform.position;
        
        // Setup audio source for click sounds
        if (clickSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound - location independent for title screen
        }
    }
    
    private void OnEnable()
    {
        inputActions.Player.Look.performed += OnLookInput;
        inputActions.Player.Look.canceled += OnLookInputCanceled;
        inputActions.Enable();
    }
    
    private void OnDisable()
    {
        inputActions.Player.Look.performed -= OnLookInput;
        inputActions.Player.Look.canceled -= OnLookInputCanceled;
        inputActions.Disable();
    }
    
    private void OnLookInput(InputAction.CallbackContext context)
    {
        // Filter out mouse input - only accept gamepad input
        if (context.control.device is Mouse)
        {
            lookInput = Vector2.zero;
            return;
        }
        
        lookInput = context.ReadValue<Vector2>();
    }
    
    private void OnLookInputCanceled(InputAction.CallbackContext context)
    {
        // Only clear if it's from gamepad (not mouse)
        if (context.control.device is Mouse)
            return;
            
        lookInput = Vector2.zero;
    }
    
    private void Update()
    {
        HandleMovement();
    }
    
    private void HandleMovement()
    {
        // Get arrow key input (direct keyboard reading)
        float arrowKeyInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed)
                arrowKeyInput -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed)
                arrowKeyInput += 1f;
        }
        
        // Scale arrow key input
        arrowKeyInput *= arrowKeySensitivity;
        
        // Gamepad input (horizontal axis)
        float gamepadInput = lookInput.x * gamepadSensitivity;
        
        // Combine inputs
        float inputValue = arrowKeyInput + gamepadInput;
        inputValue = Mathf.Clamp(inputValue, -1f, 1f);
        
        // Apply inversion if set
        if (invertMovement)
            inputValue = -inputValue;
        
        // Determine current direction for speed boost tracking
        float currentDirection = 0f;
        if (inputValue > 0.1f)
            currentDirection = 1f;
        else if (inputValue < -0.1f)
            currentDirection = -1f;
        
        // Progressive speed boost system
        if (currentDirection != 0f && currentDirection == lastInputDirection)
        {
            // Same direction - accumulate time and apply exponential boost
            movementTimer += Time.deltaTime;
            
            // Apply multiplier every 0.5 seconds (exponential growth)
            int intervals = Mathf.FloorToInt(movementTimer / 0.5f);
            currentSpeedMultiplier = Mathf.Pow(speedUpMultiplier, intervals);
            
            // Cap at speed limit
            currentSpeedMultiplier = Mathf.Min(currentSpeedMultiplier, speedLimit);
        }
        else
        {
            // Direction changed or stopped - reset
            movementTimer = 0f;
            currentSpeedMultiplier = 1f;
        }
        
        lastInputDirection = currentDirection;
        
        // Calculate target movement velocity with speed boost applied
        targetMovementVelocity = inputValue * movementSpeed * currentSpeedMultiplier;
        
        // Physics-based momentum system
        if (Mathf.Abs(inputValue) > 0.01f)
        {
            // There is input - accelerate toward target velocity
            if (enableSmoothing)
            {
                currentMovementVelocity = Mathf.Lerp(currentMovementVelocity, targetMovementVelocity, 
                    smoothingSpeed * Time.deltaTime);
            }
            else
            {
                currentMovementVelocity = targetMovementVelocity;
            }
        }
        else
        {
            // No input - apply deceleration (coast to stop)
            currentMovementVelocity = Mathf.Lerp(currentMovementVelocity, 0f, deceleration * Time.deltaTime);
            
            // Stop completely when very slow to avoid infinite tiny movement
            if (Mathf.Abs(currentMovementVelocity) < 0.01f)
                currentMovementVelocity = 0f;
        }
        
        // Apply movement
        if (Mathf.Abs(currentMovementVelocity) > 0.001f)
        {
            Vector3 movementVector = Vector3.zero;
            
            switch (movementAxis)
            {
                case MovementAxis.X:
                    movementVector = Vector3.right;
                    break;
                case MovementAxis.Y:
                    movementVector = Vector3.up;
                    break;
                case MovementAxis.Z:
                    movementVector = Vector3.forward;
                    break;
            }
            
            float movementThisFrame = currentMovementVelocity * Time.deltaTime;
            Vector3 newPosition = transform.position + movementVector * movementThisFrame;
            
            // Apply bounds if enabled
            if (useBounds)
            {
                float axisValue = GetAxisValue(newPosition);
                float clampedValue = Mathf.Clamp(axisValue, minPosition, maxPosition);
                newPosition = SetAxisValue(newPosition, clampedValue);
                
                // Stop velocity if we hit a boundary
                float currentAxisValue = GetAxisValue(transform.position);
                if ((clampedValue == minPosition && currentAxisValue <= minPosition) ||
                    (clampedValue == maxPosition && currentAxisValue >= maxPosition))
                {
                    currentMovementVelocity = 0f;
                }
            }
            
            transform.position = newPosition;
            
            // Handle click sounds
            HandleClickSounds(movementThisFrame);
        }
        else
        {
            // Reset accumulator when not moving
            movementAccumulator = 0f;
        }
    }
    
    private float GetAxisValue(Vector3 position)
    {
        switch (movementAxis)
        {
            case MovementAxis.X:
                return position.x;
            case MovementAxis.Y:
                return position.y;
            case MovementAxis.Z:
                return position.z;
            default:
                return 0f;
        }
    }
    
    private Vector3 SetAxisValue(Vector3 position, float value)
    {
        switch (movementAxis)
        {
            case MovementAxis.X:
                position.x = value;
                break;
            case MovementAxis.Y:
                position.y = value;
                break;
            case MovementAxis.Z:
                position.z = value;
                break;
        }
        return position;
    }
    
    private void HandleClickSounds(float movementAmount)
    {
        if (clickSound == null || audioSource == null)
            return;
        
        // Only trigger clicks if moving fast enough
        if (Mathf.Abs(currentMovementVelocity) < minMovementSpeedForClick)
        {
            movementAccumulator = 0f;
            return;
        }
        
        // Accumulate movement (use absolute value so direction doesn't matter)
        movementAccumulator += Mathf.Abs(movementAmount);
        
        // Play click every time we cross the threshold
        while (movementAccumulator >= unitsPerClick)
        {
            movementAccumulator -= unitsPerClick;
            
            // Play click sound
            audioSource.pitch = Random.Range(0.95f, 1.05f); // Slight pitch variation for realism
            audioSource.PlayOneShot(clickSound, clickVolume);
        }
    }
    
    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
}


