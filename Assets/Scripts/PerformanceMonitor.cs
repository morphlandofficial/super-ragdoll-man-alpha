using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple performance monitor - attach to LevelManager.
/// Logs warnings when FPS drops, GC occurs, or systems bog down.
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    [SerializeField] private bool enableMonitoring = true;
    [SerializeField] private float logInterval = 3f; // Log stats every N seconds
    [SerializeField] private float fpsWarningCooldown = 2f; // Don't spam FPS warnings
    [SerializeField] private bool enableDetailedCounting = true; // Count objects to identify bottlenecks
    
    private Queue<float> frameTimes = new Queue<float>();
    private float lastLogTime = 0f;
    private float lastFPSWarningTime = 0f;
    private int lastGCCount = 0;
    private float currentFPS = 60f;
    
    // Cached references to avoid allocations
    private BattleRoyaleManager cachedBattleManager;
    
    // Object counts (updated periodically, not every frame)
    private int activeRagdollCount = 0;
    private int rigidbodyCount = 0;
    private int colliderCount = 0;
    private int animatorCount = 0;
    private int activeGameObjectCount = 0;
    
    private void Start()
    {
        if (enableMonitoring)
        {
            lastGCCount = System.GC.CollectionCount(0);
            cachedBattleManager = FindFirstObjectByType<BattleRoyaleManager>();
            Debug.Log("<color=cyan>[Performance]</color> Monitoring enabled");
        }
    }
    
    private void Update()
    {
        if (!enableMonitoring) return;
        
        // Track FPS
        float deltaTime = Time.unscaledDeltaTime;
        frameTimes.Enqueue(deltaTime);
        if (frameTimes.Count > 30) frameTimes.Dequeue();
        
        // Calculate average FPS
        float avgDelta = 0f;
        foreach (float t in frameTimes) avgDelta += t;
        avgDelta /= frameTimes.Count;
        currentFPS = 1f / avgDelta;
        
        // Check for GC
        int gcCount = System.GC.CollectionCount(0);
        if (gcCount > lastGCCount)
        {
            Debug.LogWarning($"<color=orange>[Performance]</color> 🗑️ GC occurred! Frame time: {deltaTime * 1000f:F1}ms");
            lastGCCount = gcCount;
        }
        
        // Log periodic stats
        if (Time.time - lastLogTime >= logInterval)
        {
            LogStats();
            lastLogTime = Time.time;
        }
        
        // Instant warnings (with cooldown to reduce spam)
        if (currentFPS < 20f && Time.time - lastFPSWarningTime >= fpsWarningCooldown)
        {
            Debug.LogWarning($"<color=yellow>[Performance]</color> ⚠️ FPS: {currentFPS:F0}");
            lastFPSWarningTime = Time.time;
        }
        
        if (deltaTime > 0.1f) // Only log severe spikes (100ms+) to reduce spam
        {
            Debug.LogWarning($"<color=yellow>[Performance]</color> ⚠️ Lag spike: {deltaTime * 1000f:F0}ms");
            
            // Show object counts at time of spike
            if (enableDetailedCounting && rigidbodyCount > 0)
            {
                Debug.LogWarning($"<color=red>[SPIKE CONTEXT]</color> Active: {activeGameObjectCount} GameObjects | {rigidbodyCount} Rigidbodies | {colliderCount} Colliders | {animatorCount} Animators | {activeRagdollCount} Ragdolls");
            }
        }
    }
    
    private void LogStats()
    {
        // Re-cache manager if it's null (might not exist on title screen)
        if (cachedBattleManager == null)
        {
            cachedBattleManager = FindFirstObjectByType<BattleRoyaleManager>();
        }
        
        // Use cached manager to get global counts
        int globalCount = 0;
        int globalMax = 50;
        if (cachedBattleManager != null)
        {
            globalCount = cachedBattleManager.GetGlobalActiveCount();
            globalMax = cachedBattleManager.GetGlobalMaxRagdolls();
        }
        
        // Memory
        float memoryMB = System.GC.GetTotalMemory(false) / (1024f * 1024f);
        
        // Count objects (expensive, only do periodically)
        if (enableDetailedCounting)
        {
            CountSceneObjects();
        }
        
        // Build log string once (reduces allocations)
        Debug.Log($"<color=cyan>[Performance]</color> FPS: {currentFPS:F0} | Global: {globalCount}/{globalMax} | Memory: {memoryMB:F0}MB");
        
        // Show detailed breakdown if enabled
        if (enableDetailedCounting)
        {
            Debug.Log($"<color=cyan>[Object Counts]</color> Ragdolls: {activeRagdollCount} | Rigidbodies: {rigidbodyCount} | Colliders: {colliderCount} | Animators: {animatorCount} | Active GameObjects: {activeGameObjectCount}");
            
            // Warn about specific bottlenecks
            if (rigidbodyCount > 400)
                Debug.LogWarning($"<color=red>[BOTTLENECK]</color> ⚠️ Too many Rigidbodies! ({rigidbodyCount}) - Physics simulation is expensive");
            
            if (colliderCount > 800)
                Debug.LogWarning($"<color=red>[BOTTLENECK]</color> ⚠️ Too many Colliders! ({colliderCount}) - Collision detection is expensive");
            
            if (animatorCount > 60)
                Debug.LogWarning($"<color=red>[BOTTLENECK]</color> ⚠️ Too many Animators! ({animatorCount}) - Animation updates are expensive");
            
            if (activeGameObjectCount > 2000)
                Debug.LogWarning($"<color=red>[BOTTLENECK]</color> ⚠️ Too many active GameObjects! ({activeGameObjectCount}) - Scene is too complex");
        }
        
        // Only warn if near limit
        if (globalCount > globalMax * 0.8f)
        {
            Debug.LogWarning($"<color=yellow>[Performance]</color> ⚠️ Near global limit: {globalCount}/{globalMax}");
        }
    }
    
    private void CountSceneObjects()
    {
        // Count active ragdolls
        activeRagdollCount = 0;
        var ragdolls = FindObjectsByType<ActiveRagdoll.ActiveRagdoll>(FindObjectsSortMode.None);
        foreach (var ragdoll in ragdolls)
        {
            if (ragdoll.gameObject.activeInHierarchy)
                activeRagdollCount++;
        }
        
        // Count physics objects
        rigidbodyCount = 0;
        var rbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
        foreach (var rb in rbs)
        {
            if (rb.gameObject.activeInHierarchy)
                rigidbodyCount++;
        }
        
        // Count colliders
        colliderCount = 0;
        var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (var col in colliders)
        {
            if (col.gameObject.activeInHierarchy)
                colliderCount++;
        }
        
        // Count animators
        animatorCount = 0;
        var animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        foreach (var anim in animators)
        {
            if (anim.gameObject.activeInHierarchy && anim.enabled)
                animatorCount++;
        }
        
        // Count all active GameObjects (expensive!)
        activeGameObjectCount = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length;
    }
}

