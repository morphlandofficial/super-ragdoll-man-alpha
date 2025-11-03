using UnityEngine;
using UnityEngine.InputSystem;

public class TitleScreenEarthRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [SerializeField] private bool invertRotation = false;
    
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
    [Tooltip("How quickly rotation slows down when no input (higher = faster stop)")]
    private float deceleration = 0.15f;
    
    [Header("Safe Dial Click Sounds")]
    [Tooltip("Click sound to play when rotating (like a safe dial)")]
    [SerializeField] private AudioClip clickSound;
    
    [Tooltip("Degrees of rotation required for each click")]
    [SerializeField] private float degreesPerClick = 15f;
    
    [Tooltip("Volume of click sounds")]
    [SerializeField] private float clickVolume = 0.5f;
    
    [Tooltip("Minimum rotation speed to trigger clicks")]
    [SerializeField] private float minRotationSpeedForClick = 1f;
    
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }
    
    private ActiveRagdollActions inputActions;
    private Vector2 lookInput;
    private float targetRotationVelocity;
    private float currentRotationVelocity;
    
    // Click sound system
    private AudioSource audioSource;
    private float rotationAccumulator = 0f;
    
    // Progressive speed boost tracking
    private float currentSpeedMultiplier = 1f;
    private float rotationTimer = 0f;
    private float lastInputDirection = 0f; // -1, 0, or 1
    
    private void Awake()
    {
        inputActions = new ActiveRagdollActions();
        
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
        HandleRotation();
    }
    
    private void HandleRotation()
    {
        // Get arrow key input (direct keyboard reading)
        float arrowKeyInput = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.isPressed)
                arrowKeyInput += 1f;
            if (Keyboard.current.leftArrowKey.isPressed)
                arrowKeyInput -= 1f;
        }
        
        // Get gamepad input from Look action (right stick)
        float gamepadInput = 0f;
        if (Gamepad.current != null)
        {
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    gamepadInput = lookInput.y; // Vertical input for X-axis rotation
                    break;
                case RotationAxis.Y:
                    gamepadInput = lookInput.x; // Horizontal input for Y-axis rotation
                    break;
                case RotationAxis.Z:
                    gamepadInput = lookInput.x; // Horizontal input for Z-axis rotation
                    break;
            }
        }
        
        // Combine arrow key and gamepad inputs with their respective sensitivities
        float inputValue = (arrowKeyInput * arrowKeySensitivity) + (gamepadInput * gamepadSensitivity);
        
        // Invert if needed
        if (invertRotation)
            inputValue = -inputValue;
        
        // Determine current direction (normalize to -1, 0, or 1)
        float currentDirection = 0f;
        if (Mathf.Abs(inputValue) > 0.01f)
            currentDirection = Mathf.Sign(inputValue);
        
        // Progressive speed boost logic
        if (currentDirection != 0f && currentDirection == lastInputDirection)
        {
            // Same direction - accumulate time and apply exponential boost
            rotationTimer += Time.deltaTime;
            
            // Apply multiplier every 0.5 seconds (exponential growth)
            int intervals = Mathf.FloorToInt(rotationTimer / 0.5f);
            currentSpeedMultiplier = Mathf.Pow(speedUpMultiplier, intervals);
            
            // Cap at speed limit
            currentSpeedMultiplier = Mathf.Min(currentSpeedMultiplier, speedLimit);
        }
        else
        {
            // Direction changed or stopped - reset
            rotationTimer = 0f;
            currentSpeedMultiplier = 1f;
        }
        
        lastInputDirection = currentDirection;
        
        // Calculate target rotation velocity with speed boost applied
        targetRotationVelocity = inputValue * rotationSpeed * currentSpeedMultiplier;
        
        // Physics-based momentum system
        if (Mathf.Abs(inputValue) > 0.01f)
        {
            // There is input - accelerate toward target velocity
            if (enableSmoothing)
            {
                currentRotationVelocity = Mathf.Lerp(currentRotationVelocity, targetRotationVelocity, 
                    smoothingSpeed * Time.deltaTime);
            }
            else
            {
                currentRotationVelocity = targetRotationVelocity;
            }
        }
        else
        {
            // No input - apply deceleration (coast to stop)
            currentRotationVelocity = Mathf.Lerp(currentRotationVelocity, 0f, deceleration * Time.deltaTime);
            
            // Stop completely when very slow to avoid infinite tiny rotation
            if (Mathf.Abs(currentRotationVelocity) < 0.1f)
                currentRotationVelocity = 0f;
        }
        
        // Apply rotation
        if (Mathf.Abs(currentRotationVelocity) > 0.01f)
        {
            Vector3 rotationVector = Vector3.zero;
            
            switch (rotationAxis)
            {
                case RotationAxis.X:
                    rotationVector = Vector3.right;
                    break;
                case RotationAxis.Y:
                    rotationVector = Vector3.up;
                    break;
                case RotationAxis.Z:
                    rotationVector = Vector3.forward;
                    break;
            }
            
            float rotationThisFrame = currentRotationVelocity * Time.deltaTime;
            transform.Rotate(rotationVector * rotationThisFrame, Space.World);
            
            // Handle click sounds
            HandleClickSounds(rotationThisFrame);
        }
        else
        {
            // Reset accumulator when not rotating
            rotationAccumulator = 0f;
        }
    }
    
    private void HandleClickSounds(float rotationAmount)
    {
        if (clickSound == null || audioSource == null)
            return;
        
        // Only trigger clicks if rotating fast enough
        if (Mathf.Abs(currentRotationVelocity) < minRotationSpeedForClick)
        {
            rotationAccumulator = 0f;
            return;
        }
        
        // Accumulate rotation (use absolute value so direction doesn't matter)
        rotationAccumulator += Mathf.Abs(rotationAmount);
        
        // Play click every time we cross the threshold
        while (rotationAccumulator >= degreesPerClick)
        {
            rotationAccumulator -= degreesPerClick;
            
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