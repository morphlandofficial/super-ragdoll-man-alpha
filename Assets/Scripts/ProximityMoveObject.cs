using UnityEngine;

public enum ProximityShape
{
    Sphere,
    Box
}

/// <summary>
/// Binary proximity trigger - when the player enters, snaps a target object to this object's center.
/// Can be a sphere or box shape.
/// </summary>
public class ProximityMoveObject : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("The object that will snap to this object's position when player enters")]
    public GameObject targetObject;

    [Header("Proximity Settings")]
    [Tooltip("Shape of the proximity trigger")]
    public ProximityShape shape = ProximityShape.Sphere;

    [Tooltip("Radius of the proximity sphere")]
    public float proximityRadius = 5f;

    [Tooltip("Size of the proximity box")]
    public Vector3 proximityBoxSize = new Vector3(5f, 5f, 5f);

    [Tooltip("Should match the target's rotation to this object's rotation?")]
    public bool matchRotation = false;

    [Tooltip("Should only trigger once per player instance?")]
    public bool triggerOnce = true;

    [Tooltip("Should disable the trigger after snapping? (prevents exit events)")]
    public bool disableAfterSnap = true;

    [Header("Audio Settings")]
    [Tooltip("Sound played when checkpoint is triggered (2D)")]
    public AudioClip checkpointSound;
    
    [Tooltip("Volume for checkpoint sound (0-1)")]
    [Range(0f, 1f)]
    public float soundVolume = 1f;
    
    [Header("Lives Restoration")]
    [Tooltip("Restores player lives to max when checkpoint is triggered (Only works with Battle Royale Manager)")]
    public bool restoreLives = true;
    
    [Tooltip("Debug: Has this checkpoint already restored lives once?")]
    [SerializeField] private bool hasRestoredLives = false;
    
    [Header("Gizmo Settings")]
    [Tooltip("Color of the proximity gizmo")]
    public Color gizmoColor = new Color(0f, 1f, 1f, 0.3f);

    private Collider proximityCollider;
    private RespawnablePlayer lastTriggeredPlayer;
    private bool hasTriggered = false;
    private AudioSource audioSource;

    private void Awake()
    {
        SetupCollider();
        SetupAudio();
    }
    
    private void SetupAudio()
    {
        // ALWAYS create a NEW dedicated AudioSource for checkpoint sounds
        audioSource = gameObject.AddComponent<AudioSource>();
        
        // Configure exactly like jump sounds - simple 2D one-shot
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.volume = soundVolume;
    }

    private void OnValidate()
    {
        SetupCollider();
    }

    private void SetupCollider()
    {
        if (shape == ProximityShape.Sphere)
        {
            // Remove box collider if it exists
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                if (Application.isPlaying)
                    Destroy(box);
                else
                    DestroyImmediate(box);
            }

            // Get or add sphere collider
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = gameObject.AddComponent<SphereCollider>();
            
            sphere.isTrigger = true;
            sphere.radius = proximityRadius;
            proximityCollider = sphere;
        }
        else // Box
        {
            // Remove sphere collider if it exists
            SphereCollider sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                if (Application.isPlaying)
                    Destroy(sphere);
                else
                    DestroyImmediate(sphere);
            }

            // Get or add box collider
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();
            
            box.isTrigger = true;
            box.size = proximityBoxSize;
            proximityCollider = box;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // DIAGNOSTIC LOGGING
        
        // If already triggered and should disable, do nothing
        if (hasTriggered && disableAfterSnap)
        {
            return;
        }

        // Check if the player entered
        RespawnablePlayer player = other.GetComponent<RespawnablePlayer>();

        // If no player component found, try to find it in parent
        if (player == null)
        {
            player = other.GetComponentInParent<RespawnablePlayer>();
            
            if (player != null)
            {
            }
        }

        // If player entered and we have a target object
        if (player != null && targetObject != null)
        {
            // If triggerOnce is enabled, check if this is a new player instance
            if (triggerOnce && lastTriggeredPlayer == player)
            {
                return; // Already triggered for this player instance
            }

            // Snap the target object to this object's center
            targetObject.transform.position = transform.position;

            if (matchRotation)
            {
                targetObject.transform.rotation = transform.rotation;
            }
            
            // Play checkpoint sound
            PlayCheckpointSound();
            
            // Restore lives if enabled and not already done
            RestoreLivesIfApplicable();

            // Remember this player instance
            lastTriggeredPlayer = player;
            hasTriggered = true;


            // Disable the trigger to prevent any exit events or further interactions
            if (disableAfterSnap && proximityCollider != null)
            {
                proximityCollider.enabled = false;
            }
        }
        else
        {
            // DIAGNOSTIC: Why didn't it trigger?
            if (player == null)
            {
                // Debug.LogWarning($"<color=red>[ProximityMoveObject]</color> ✗ FAILED: No RespawnablePlayer component found!");
            }
            else if (targetObject == null)
            {
                // Debug.LogWarning($"<color=red>[ProximityMoveObject]</color> ✗ FAILED: No target object assigned!");
            }
        }
    }

    // Draw gizmo to visualize the proximity trigger
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (shape == ProximityShape.Sphere)
        {
            Gizmos.DrawWireSphere(Vector3.zero, proximityRadius);
        }
        else // Box
        {
            Gizmos.DrawWireCube(Vector3.zero, proximityBoxSize);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw a more visible gizmo when selected
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (shape == ProximityShape.Sphere)
        {
            Gizmos.DrawSphere(Vector3.zero, proximityRadius);
        }
        else // Box
        {
            Gizmos.DrawCube(Vector3.zero, proximityBoxSize);
        }
        
        // Draw a line to the target object if assigned
        if (targetObject != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetObject.transform.position);
        }
    }
    
    // ==================== AUDIO METHODS ====================
    
    /// <summary>
    /// Play checkpoint sound (2D)
    /// </summary>
    private void PlayCheckpointSound()
    {
        if (checkpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(checkpointSound, soundVolume);
        }
    }
    
    // ==================== LIVES RESTORATION ====================
    
    /// <summary>
    /// Restore player lives if Battle Royale Manager is present with Limited Lives mode
    /// Each checkpoint can only restore lives once
    /// </summary>
    private void RestoreLivesIfApplicable()
    {
        // Skip if lives restoration is disabled
        if (!restoreLives)
            return;
        
        // Skip if already restored lives
        if (hasRestoredLives)
            return;
        
        // Find Battle Royale Manager in scene
        BattleRoyaleManager battleRoyaleManager = FindFirstObjectByType<BattleRoyaleManager>();
        
        // Skip if no Battle Royale Manager found
        if (battleRoyaleManager == null)
            return;
        
        // Only restore lives if in Limited Lives mode
        if (battleRoyaleManager.GetLivesMode() != BattleRoyaleManager.LivesMode.Limited)
            return;
        
        // Get max lives and current lives
        int maxLives = battleRoyaleManager.GetMaxLives();
        int currentLives = battleRoyaleManager.GetCurrentLives();
        
        // Only restore if player has lost lives
        if (currentLives < maxLives)
        {
            // Restore lives to max using reflection (since the field is private)
            System.Type type = battleRoyaleManager.GetType();
            System.Reflection.FieldInfo currentLivesField = type.GetField("currentLives", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (currentLivesField != null)
            {
                currentLivesField.SetValue(battleRoyaleManager, maxLives);
                
                int livesRestored = maxLives - currentLives;
                Debug.Log($"<color=green>[Checkpoint]</color> ❤️ Lives restored! {currentLives} → {maxLives} (+{livesRestored} lives)");
                
                // Also reset the PreventRespawning flag since we're back to full health
                BattleRoyaleManager.PreventRespawning = false;
            }
        }
        
        // Mark as restored (even if no lives were needed, so we don't check again)
        hasRestoredLives = true;
    }
}

