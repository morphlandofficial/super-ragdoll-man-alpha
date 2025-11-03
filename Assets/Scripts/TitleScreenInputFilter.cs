using UnityEngine;
using UnityEngine.InputSystem;
using ActiveRagdoll;

/// <summary>
/// Title screen specific input filter that restricts movement to X-axis only.
/// This component sits between the Input System and the ActiveRagdoll InputModule,
/// filtering out Z-axis movement while preserving all other functionality.
/// </summary>
public class TitleScreenInputFilter : MonoBehaviour
{
    [Header("Movement Restriction")]
    [SerializeField] private bool restrictToXAxisOnly = true;
    
    private InputModule _inputModule;
    
    void Start()
    {
        // Get the InputModule component
        _inputModule = GetComponent<InputModule>();
        if (_inputModule == null)
        {
// Debug.LogError("TitleScreenInputFilter requires an InputModule component on the same GameObject!");
            enabled = false;
        }
    }
    
    /// <summary>
    /// Intercepts movement input and filters it to X-axis only if restriction is enabled.
    /// Arrow keys are now separate from WASD - they control rotation/axis movement instead.
    /// </summary>
    public void OnMove(InputValue value)
    {
        Vector2 originalMovement = value.Get<Vector2>();
        
        if (restrictToXAxisOnly)
        {
            // Keep only X-axis movement, zero out Y (which maps to Z in world space)
            Vector2 filteredMovement = new Vector2(originalMovement.x, 0f);
            
            // Create a new InputValue with the filtered movement
            // We need to manually invoke the InputModule's delegates with our filtered input
            _inputModule.OnMoveDelegates?.Invoke(filteredMovement);
        }
        else
        {
            // Pass through unfiltered if restriction is disabled
            _inputModule.OnMoveDelegates?.Invoke(originalMovement);
        }
    }
    
    // Pass through all other input methods unchanged
    public void OnLeftArm(InputValue value)
    {
        _inputModule.OnLeftArmDelegates?.Invoke(value.Get<float>());
    }
    
    public void OnRightArm(InputValue value)
    {
        _inputModule.OnRightArmDelegates?.Invoke(value.Get<float>());
    }
    
    public void OnRun(InputValue value)
    {
        _inputModule.OnRunDelegates?.Invoke(value.Get<float>() > 0.5f);
    }
    
    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            _inputModule.OnJumpDelegates?.Invoke();
    }
    
    public void OnRagdoll(InputValue value)
    {
        _inputModule.OnRagdollDelegates?.Invoke(value.Get<float>() > 0.5f);
    }
    
    public void OnRewind(InputValue value)
    {
        _inputModule.OnRewindDelegates?.Invoke(value.Get<float>() > 0.5f);
    }
}