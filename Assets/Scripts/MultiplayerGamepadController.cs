using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Replaces PlayerInput for multiplayer players
/// Directly reads from assigned gamepad (and optionally keyboard for Player 1) and calls InputModule delegates
/// </summary>
public class MultiplayerGamepadController : MonoBehaviour
{
    public Gamepad assignedGamepad;
    public bool allowKeyboardInput = false; // Enable for Player 1 only
    
    private ActiveRagdoll.InputModule inputModule;
    private ActiveRagdoll.CameraModule cameraModule;
    
    // Camera rotation input
    private System.Reflection.FieldInfo inputDeltaField;
    
    // Track button states to only fire on change (for effects that need state change events)
    private bool _lastRagdollState = false;
    private bool _lastRewindState = false;
    
    private void Start()
    {
        inputModule = GetComponent<ActiveRagdoll.InputModule>();
        cameraModule = GetComponent<ActiveRagdoll.CameraModule>();
        
        if (inputModule == null)
        {
            Debug.LogError("[MultiplayerGamepad] No InputModule found!");
        }
        if (cameraModule == null)
        {
            Debug.LogError("[MultiplayerGamepad] No CameraModule found!");
        }
        else
        {
            // Get the private _inputDelta field from CameraModule using reflection
            inputDeltaField = typeof(ActiveRagdoll.CameraModule).GetField("_inputDelta", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }
    }
    
    private void Update()
    {
        if (inputModule == null) return;
        if (assignedGamepad == null && !allowKeyboardInput) return;
        
        // Movement (combine gamepad and keyboard if allowed)
        Vector2 movement = Vector2.zero;
        if (assignedGamepad != null)
        {
            movement = assignedGamepad.leftStick.ReadValue();
        }
        if (allowKeyboardInput && Keyboard.current != null)
        {
            // Add WASD keyboard input
            Vector2 keyboardMovement = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) keyboardMovement.y += 1f;
            if (Keyboard.current.sKey.isPressed) keyboardMovement.y -= 1f;
            if (Keyboard.current.aKey.isPressed) keyboardMovement.x -= 1f;
            if (Keyboard.current.dKey.isPressed) keyboardMovement.x += 1f;
            
            // Combine inputs (keyboard takes priority if both pressed)
            if (keyboardMovement.magnitude > 0.1f)
            {
                movement = keyboardMovement;
            }
        }
        inputModule.OnMoveDelegates?.Invoke(movement);
        
        // Camera look - directly set the _inputDelta field (gamepad + mouse if allowed)
        if (cameraModule != null && inputDeltaField != null)
        {
            Vector2 look = Vector2.zero;
            
            // Gamepad look
            if (assignedGamepad != null)
            {
                look = assignedGamepad.rightStick.ReadValue();
                look = (look * 12f) / 10f; // Scale like OnLook does
            }
            
            // Mouse look (only for Player 1 with keyboard enabled)
            if (allowKeyboardInput && Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                if (mouseDelta.magnitude > 0.1f)
                {
                    // Mouse sensitivity scaling (similar to OnLook)
                    look = mouseDelta / 10f;
                }
            }
            
            inputDeltaField.SetValue(cameraModule, look);
        }
        
        // Left Arm (LT + Q key)
        float leftArm = 0f;
        if (assignedGamepad != null)
        {
            leftArm = assignedGamepad.leftTrigger.ReadValue();
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.qKey.isPressed)
        {
            leftArm = 1f;
        }
        inputModule.OnLeftArmDelegates?.Invoke(leftArm);
        
        // Right Arm (RT + E key)
        float rightArm = 0f;
        if (assignedGamepad != null)
        {
            rightArm = assignedGamepad.rightTrigger.ReadValue();
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.eKey.isPressed)
        {
            rightArm = 1f;
        }
        inputModule.OnRightArmDelegates?.Invoke(rightArm);
        
        // Run (LB + Shift key)
        bool isRunning = false;
        if (assignedGamepad != null)
        {
            isRunning = assignedGamepad.leftShoulder.isPressed;
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
        {
            isRunning = true;
        }
        inputModule.OnRunDelegates?.Invoke(isRunning);
        
        // Jump (A button + Space key)
        bool jumpPressed = false;
        if (assignedGamepad != null && assignedGamepad.buttonSouth.wasPressedThisFrame)
        {
            jumpPressed = true;
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpPressed = true;
        }
        if (jumpPressed)
        {
            inputModule.OnJumpDelegates?.Invoke();
        }
        
        // Ragdoll (Y button + R key)
        // CRITICAL: Only invoke when state CHANGES (not every frame) to prevent datamosh effect from freezing
        bool isRagdoll = false;
        if (assignedGamepad != null)
        {
            isRagdoll = assignedGamepad.buttonNorth.isPressed;
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.rKey.isPressed)
        {
            isRagdoll = true;
        }
        if (isRagdoll != _lastRagdollState)
        {
            inputModule.OnRagdollDelegates?.Invoke(isRagdoll);
            _lastRagdollState = isRagdoll;
        }
        
        // Rewind (B button + T key)
        // CRITICAL: Only invoke when state CHANGES (not every frame) to prevent analog glitch from flickering
        bool isRewinding = false;
        if (assignedGamepad != null)
        {
            isRewinding = assignedGamepad.buttonEast.isPressed;
        }
        if (allowKeyboardInput && Keyboard.current != null && Keyboard.current.tKey.isPressed)
        {
            isRewinding = true;
        }
        if (isRewinding != _lastRewindState)
        {
            inputModule.OnRewindDelegates?.Invoke(isRewinding);
            _lastRewindState = isRewinding;
        }
    }
}

