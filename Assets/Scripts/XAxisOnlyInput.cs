using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple component that blocks W/S input and forward/backward stick movement.
/// Just add this to any character to make them only respond to A/D and left/right stick.
/// 
/// This component should be placed ABOVE the InputModule in the component list
/// so it processes input first and prevents W/S from reaching the InputModule.
/// </summary>
[DefaultExecutionOrder(-100)] // Execute before other components
public class XAxisOnlyInput : MonoBehaviour
{
    [Header("Input Filtering")]
    [SerializeField] private bool filterMovementInput = true;
    
    private ActiveRagdoll.InputModule _inputModule;
    private bool _inputModuleDisabled = false;
    
    void Start()
    {
        _inputModule = GetComponent<ActiveRagdoll.InputModule>();
        if (_inputModule == null)
        {
// Debug.LogWarning("XAxisOnlyInput: No InputModule found on this GameObject!");
            enabled = false;
        }
    }
    
    /// <summary>
    /// Intercepts movement input and removes forward/backward components
    /// This method will be called by PlayerInput's SendMessage system BEFORE InputModule.OnMove
    /// </summary>
    public void OnMove(InputValue value)
    {
        if (!filterMovementInput || _inputModule == null) return;
        
        Vector2 originalInput = value.Get<Vector2>();
        
        // Keep only X-axis input (A/D keys, left/right stick)
        // Remove Y-axis input (W/S keys, forward/backward stick)
        Vector2 filteredInput = new Vector2(originalInput.x, 0f);
        
        // Temporarily disable the InputModule to prevent it from processing the original input
        bool wasEnabled = _inputModule.enabled;
        _inputModule.enabled = false;
        _inputModuleDisabled = true;
        
        // Call the InputModule's movement delegates directly with our filtered input
        _inputModule.OnMoveDelegates?.Invoke(filteredInput);
        
        // Re-enable the InputModule for other inputs
        _inputModule.enabled = wasEnabled;
        _inputModuleDisabled = false;
    }
    
    void LateUpdate()
    {
        // Safety check to re-enable InputModule if something went wrong
        if (_inputModuleDisabled && _inputModule != null)
        {
            _inputModule.enabled = true;
            _inputModuleDisabled = false;
        }
    }
}