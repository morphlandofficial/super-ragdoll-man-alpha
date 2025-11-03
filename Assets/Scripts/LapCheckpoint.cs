using UnityEngine;
using System.Collections;

/// <summary>
/// Attach this to a GameObject with a BoxCollider (set as trigger).
/// Detects when the player crosses through it and notifies the RaceLapManager.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class LapCheckpoint : MonoBehaviour
{
    /// <summary>
    /// Visual effect to play when activating/deactivating GameObjects
    /// </summary>
    public enum ActivationEffect
    {
        None,           // Instant activation/deactivation (default)
        ScaleUp,        // Scale from 0 to 1 when activating
        ScaleDown,      // Scale from 1 to 0 when deactivating
        FadeIn,         // Fade alpha from 0 to 1 when activating (requires Renderer)
        FadeOut,        // Fade alpha from 1 to 0 when deactivating (requires Renderer)
        PopIn,          // Bouncy scale-up effect when activating
        Shrink          // Bouncy scale-down effect when deactivating
    }
    
    [Header("Checkpoint Settings")]
    [Tooltip("Which lap does this checkpoint mark? (1 = Lap 1, 2 = Lap 2, etc.)")]
    [SerializeField] private int lapNumber = 1;
    
    [Header("Activation Events")]
    [Tooltip("GameObjects to activate when this checkpoint is crossed")]
    [SerializeField] private GameObject[] objectsToActivate = new GameObject[0];
    
    [Tooltip("Effect to play when activating objects")]
    [SerializeField] private ActivationEffect activationEffect = ActivationEffect.None;
    
    [Tooltip("Duration of activation effect (seconds)")]
    [SerializeField] private float activationDuration = 0.5f;
    
    [Header("Deactivation Events")]
    [Tooltip("GameObjects to deactivate when this checkpoint is crossed")]
    [SerializeField] private GameObject[] objectsToDeactivate = new GameObject[0];
    
    [Tooltip("Effect to play when deactivating objects")]
    [SerializeField] private ActivationEffect deactivationEffect = ActivationEffect.None;
    
    [Tooltip("Duration of deactivation effect (seconds)")]
    [SerializeField] private float deactivationDuration = 0.5f;
    
    [Header("Gizmo Visualization")]
    [Tooltip("Color of the checkpoint in the editor")]
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green
    
    private BoxCollider boxCollider;
    
    // Public getter for lap number
    public int LapNumber => lapNumber;
    
    private void Awake()
    {
        // Ensure we have a box collider set as trigger
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = true;
        }
        
        // Register this checkpoint with the RaceLapManager
        RaceLapManager manager = RaceLapManager.Instance;
        if (manager != null)
        {
            manager.RegisterCheckpoint(this);
        }
        else
        {
            // Debug.LogWarning($"<color=yellow>[Lap Checkpoint]</color> {gameObject.name} (Lap {lapNumber}): No RaceLapManager found in scene!");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Check if the player entered the checkpoint
        DefaultBehaviour playerBehaviour = other.GetComponent<DefaultBehaviour>();
        
        // Try to get it from parent if not found (in case collision is from a body part)
        if (playerBehaviour == null)
        {
            playerBehaviour = other.GetComponentInParent<DefaultBehaviour>();
        }
        
        // Verify this is the player and not an AI
        if (playerBehaviour != null)
        {
            // Make sure it's not an AI ragdoll (AI doesn't have RespawnablePlayer component)
            RespawnablePlayer respawnablePlayer = playerBehaviour.GetComponent<RespawnablePlayer>();
            if (respawnablePlayer != null)
            {
                // This is the player! Notify the lap manager
                RaceLapManager manager = RaceLapManager.Instance;
                if (manager != null)
                {
                    bool wasCompleted = manager.PlayerCrossedCheckpoint(this);
                    
                    // If checkpoint was successfully completed, trigger activation events
                    if (wasCompleted)
                    {
                        TriggerActivationEvents();
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Activate/deactivate GameObjects when checkpoint is crossed
    /// </summary>
    private void TriggerActivationEvents()
    {
        // Activate objects with effect
        foreach (GameObject obj in objectsToActivate)
        {
            if (obj != null)
            {
                if (activationEffect == ActivationEffect.None)
                {
                    // Instant activation (default)
                    obj.SetActive(true);
                }
                else
                {
                    // Activate with effect
                    StartCoroutine(ActivateWithEffect(obj, activationEffect, activationDuration));
                }
            }
        }
        
        // Deactivate objects with effect
        foreach (GameObject obj in objectsToDeactivate)
        {
            if (obj != null)
            {
                if (deactivationEffect == ActivationEffect.None)
                {
                    // Instant deactivation (default)
                    obj.SetActive(false);
                }
                else
                {
                    // Deactivate with effect
                    StartCoroutine(DeactivateWithEffect(obj, deactivationEffect, deactivationDuration));
                }
            }
        }
    }
    
    /// <summary>
    /// Activate a GameObject with visual effect
    /// </summary>
    private IEnumerator ActivateWithEffect(GameObject obj, ActivationEffect effect, float duration)
    {
        if (obj == null) yield break;
        
        // Make sure object is active before applying effects
        obj.SetActive(true);
        
        Vector3 originalScale = obj.transform.localScale;
        
        switch (effect)
        {
            case ActivationEffect.ScaleUp:
                // Scale from 0 to original size
                obj.transform.localScale = Vector3.zero;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    obj.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
                    yield return null;
                }
                obj.transform.localScale = originalScale;
                break;
                
            case ActivationEffect.PopIn:
                // Bouncy scale-up effect
                obj.transform.localScale = Vector3.zero;
                elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    // Elastic easing out
                    float bounce = Mathf.Sin(t * Mathf.PI * (0.2f + 2.5f * t * t * t)) * Mathf.Pow(1f - t, 2.2f);
                    float scale = t + bounce;
                    obj.transform.localScale = originalScale * Mathf.Min(scale, 1.2f);
                    yield return null;
                }
                obj.transform.localScale = originalScale;
                break;
                
            case ActivationEffect.FadeIn:
                // Fade renderers from transparent to opaque
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    // Store original colors
                    Color[][] originalColors = new Color[renderers.Length][];
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Material[] materials = renderers[i].materials;
                        originalColors[i] = new Color[materials.Length];
                        for (int j = 0; j < materials.Length; j++)
                        {
                            originalColors[i][j] = materials[j].color;
                        }
                    }
                    
                    // Fade in
                    elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = elapsed / duration;
                        
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            Material[] materials = renderers[i].materials;
                            for (int j = 0; j < materials.Length; j++)
                            {
                                Color c = originalColors[i][j];
                                c.a = Mathf.Lerp(0f, originalColors[i][j].a, t);
                                materials[j].color = c;
                            }
                        }
                        yield return null;
                    }
                    
                    // Restore original colors
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Material[] materials = renderers[i].materials;
                        for (int j = 0; j < materials.Length; j++)
                        {
                            materials[j].color = originalColors[i][j];
                        }
                    }
                }
                break;
        }
        
    }
    
    /// <summary>
    /// Deactivate a GameObject with visual effect
    /// </summary>
    private IEnumerator DeactivateWithEffect(GameObject obj, ActivationEffect effect, float duration)
    {
        if (obj == null || !obj.activeSelf) yield break;
        
        Vector3 originalScale = obj.transform.localScale;
        
        switch (effect)
        {
            case ActivationEffect.ScaleDown:
                // Scale from original size to 0
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    obj.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
                    yield return null;
                }
                obj.transform.localScale = Vector3.zero;
                break;
                
            case ActivationEffect.Shrink:
                // Bouncy scale-down effect
                elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    // Bounce effect while shrinking
                    float bounce = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 0.1f;
                    float scale = Mathf.Lerp(1f, 0f, t) + bounce;
                    obj.transform.localScale = originalScale * Mathf.Max(scale, 0f);
                    yield return null;
                }
                obj.transform.localScale = Vector3.zero;
                break;
                
            case ActivationEffect.FadeOut:
                // Fade renderers from opaque to transparent
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    // Store original colors
                    Color[][] originalColors = new Color[renderers.Length][];
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Material[] materials = renderers[i].materials;
                        originalColors[i] = new Color[materials.Length];
                        for (int j = 0; j < materials.Length; j++)
                        {
                            originalColors[i][j] = materials[j].color;
                        }
                    }
                    
                    // Fade out
                    elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = elapsed / duration;
                        
                        for (int i = 0; i < renderers.Length; i++)
                        {
                            Material[] materials = renderers[i].materials;
                            for (int j = 0; j < materials.Length; j++)
                            {
                                Color c = originalColors[i][j];
                                c.a = Mathf.Lerp(originalColors[i][j].a, 0f, t);
                                materials[j].color = c;
                            }
                        }
                        yield return null;
                    }
                    
                    // Restore original colors (for next activation)
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        Material[] materials = renderers[i].materials;
                        for (int j = 0; j < materials.Length; j++)
                        {
                            materials[j].color = originalColors[i][j];
                        }
                    }
                }
                break;
        }
        
        // Restore original scale before deactivating (so it's ready for next activation)
        obj.transform.localScale = originalScale;
        
        // Deactivate after effect completes
        obj.SetActive(false);
        
    }
    
    private void OnDrawGizmos()
    {
        // Draw the checkpoint area in the editor
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(col.center, col.size);
            
            // Draw wireframe for better visibility
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(col.center, col.size);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw a more visible checkpoint when selected
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(col.center, col.size);
            
            // Draw lap number label position
            Vector3 labelPos = transform.position + Vector3.up * (col.size.y * 0.5f + 1f);
            Gizmos.DrawWireSphere(labelPos, 0.3f);
        }
    }
}

