using UnityEngine;

public class SkyboxRotator : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation in degrees per second")]
    public float rotationSpeed = 1f;
    
    [Tooltip("Axis to rotate around (X, Y, Z)")]
    public Vector3 rotationAxis = Vector3.up;
    
    [Header("Advanced Settings")]
    [Tooltip("If true, rotation will be affected by Time.timeScale")]
    public bool useScaledTime = true;
    
    [Tooltip("If true, rotation will continue even when game is paused")]
    public bool rotateInRealTime = false;
    
    [Tooltip("Current rotation offset (can be set manually or will accumulate over time)")]
    public float currentRotation = 0f;
    
    [Header("Debug")]
    [Tooltip("Show debug information in console")]
    public bool showDebugInfo = false;

    private Material currentSkyboxMaterial;
    private float lastRotationValue = 0f;

    void Start()
    {
        // Normalize the rotation axis
        rotationAxis = rotationAxis.normalized;
        
        // Get the initial skybox
        UpdateCurrentSkybox();
    }

    void Update()
    {
        // Update current skybox reference
        UpdateCurrentSkybox();
        
        // Calculate rotation
        float deltaTime = rotateInRealTime ? Time.unscaledDeltaTime : 
                         (useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime);
        
        currentRotation += rotationSpeed * deltaTime;
        
        // Keep rotation in 0-360 range for cleaner values
        if (currentRotation >= 360f)
            currentRotation -= 360f;
        else if (currentRotation < 0f)
            currentRotation += 360f;
        
        // Apply rotation to skybox
        ApplyRotationToSkybox();
    }

    void UpdateCurrentSkybox()
    {
        Material newSkybox = RenderSettings.skybox;
        
        if (newSkybox != currentSkyboxMaterial)
        {
            currentSkyboxMaterial = newSkybox;
            
            if (showDebugInfo && currentSkyboxMaterial != null)
            {
            }
        }
    }

    void ApplyRotationToSkybox()
    {
        if (currentSkyboxMaterial == null)
            return;

        // Different skybox shaders use different property names for rotation
        // Try the most common ones
        if (currentSkyboxMaterial.HasProperty("_Rotation"))
        {
            currentSkyboxMaterial.SetFloat("_Rotation", currentRotation);
        }
        else if (currentSkyboxMaterial.HasProperty("_RotationX"))
        {
            // For skyboxes that have separate X, Y, Z rotation
            if (rotationAxis.x != 0) currentSkyboxMaterial.SetFloat("_RotationX", currentRotation * rotationAxis.x);
            if (rotationAxis.y != 0 && currentSkyboxMaterial.HasProperty("_RotationY")) 
                currentSkyboxMaterial.SetFloat("_RotationY", currentRotation * rotationAxis.y);
            if (rotationAxis.z != 0 && currentSkyboxMaterial.HasProperty("_RotationZ")) 
                currentSkyboxMaterial.SetFloat("_RotationZ", currentRotation * rotationAxis.z);
        }
        else if (currentSkyboxMaterial.HasProperty("_SunDisk"))
        {
            // For Unity's built-in procedural skybox, we can't rotate it directly
            // but we can log a warning
            if (showDebugInfo && Mathf.Abs(currentRotation - lastRotationValue) > 10f)
            {
// Debug.LogWarning("SkyboxRotator: Current skybox doesn't support rotation. Try using a 6-sided or panoramic skybox.");
                lastRotationValue = currentRotation;
            }
        }
        else
        {
            // Try some other common property names
            string[] possibleProperties = { "_Rotate", "_SkyRotation", "_Rotation1", "_MainRotation" };
            
            bool foundProperty = false;
            foreach (string prop in possibleProperties)
            {
                if (currentSkyboxMaterial.HasProperty(prop))
                {
                    currentSkyboxMaterial.SetFloat(prop, currentRotation);
                    foundProperty = true;
                    break;
                }
            }
            
            if (!foundProperty && showDebugInfo && Mathf.Abs(currentRotation - lastRotationValue) > 10f)
            {
// Debug.LogWarning($"SkyboxRotator: No rotation property found for skybox '{currentSkyboxMaterial.name}'. Shader: {currentSkyboxMaterial.shader.name}");
                lastRotationValue = currentRotation;
            }
        }
    }

    /// <summary>
    /// Manually set the rotation to a specific value
    /// </summary>
    /// <param name="rotation">Rotation in degrees</param>
    public void SetRotation(float rotation)
    {
        currentRotation = rotation;
        ApplyRotationToSkybox();
    }

    /// <summary>
    /// Reset rotation to zero
    /// </summary>
    public void ResetRotation()
    {
        SetRotation(0f);
    }

    /// <summary>
    /// Pause/resume rotation
    /// </summary>
    /// <param name="pause">True to pause, false to resume</param>
    public void PauseRotation(bool pause)
    {
        enabled = !pause;
    }

    /// <summary>
    /// Get the current rotation value
    /// </summary>
    /// <returns>Current rotation in degrees</returns>
    public float GetCurrentRotation()
    {
        return currentRotation;
    }

    /// <summary>
    /// Check if the current skybox supports rotation
    /// </summary>
    /// <returns>True if rotation is supported</returns>
    public bool IsRotationSupported()
    {
        if (currentSkyboxMaterial == null)
            return false;

        return currentSkyboxMaterial.HasProperty("_Rotation") ||
               currentSkyboxMaterial.HasProperty("_RotationX") ||
               currentSkyboxMaterial.HasProperty("_Rotate") ||
               currentSkyboxMaterial.HasProperty("_SkyRotation") ||
               currentSkyboxMaterial.HasProperty("_Rotation1") ||
               currentSkyboxMaterial.HasProperty("_MainRotation");
    }

    void OnValidate()
    {
        // Ensure rotation axis is normalized
        if (rotationAxis != Vector3.zero)
            rotationAxis = rotationAxis.normalized;
    }
}