using UnityEngine;

/// <summary>
/// Makes a surface slippery like ice by adjusting its physics material.
/// Add this to any GameObject with a collider to make it slippery.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlipperySurface : MonoBehaviour
{
    [Header("Slipperiness Settings")]
    [Tooltip("How slippery the surface is (0 = no friction/ice, 1 = full friction/sticky)")]
    [Range(0f, 1f)]
    public float friction = 0.1f;

    [Tooltip("How bouncy the surface is (0 = no bounce, 1 = full bounce)")]
    [Range(0f, 1f)]
    public float bounciness = 0f;

    [Header("Advanced Settings")]
    [Tooltip("Friction combine mode - Minimum is best for slippery surfaces")]
    public PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Minimum;

    [Tooltip("Bounce combine mode")]
    public PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average;

    private PhysicsMaterial physicMaterial;
    private Collider surfaceCollider;

    private void Awake()
    {
        SetupSlipperySurface();
    }

    private void OnValidate()
    {
        // Update in editor when values change
        if (Application.isPlaying)
        {
            SetupSlipperySurface();
        }
    }

    private void SetupSlipperySurface()
    {
        surfaceCollider = GetComponent<Collider>();
        
        if (surfaceCollider == null)
        {
// Debug.LogError("SlipperySurface: No collider found on " + gameObject.name);
            return;
        }

        // Create a new physics material if one doesn't exist
        if (physicMaterial == null)
        {
            physicMaterial = new PhysicsMaterial("Slippery Surface");
        }

        // Set the physics material properties
        physicMaterial.dynamicFriction = friction;
        physicMaterial.staticFriction = friction;
        physicMaterial.bounciness = bounciness;
        physicMaterial.frictionCombine = frictionCombine;
        physicMaterial.bounceCombine = bounceCombine;

        // Apply the material to the collider
        surfaceCollider.material = physicMaterial;

    }

    /// <summary>
    /// Set the slipperiness at runtime
    /// </summary>
    public void SetSlipperiness(float newFriction)
    {
        friction = Mathf.Clamp01(newFriction);
        SetupSlipperySurface();
    }
}


