using UnityEngine;

namespace MimicSpace
{
    /// <summary>
    /// Automatically sets up a respawn trigger for the Mimic that won't interfere with leg generation.
    /// ONLY ACTIVE during proximity homing mode - prevents killing player through walls during exploration.
    /// Just add this component to your Mimic GameObject!
    /// </summary>
    [RequireComponent(typeof(Mimic))]
    [RequireComponent(typeof(Movement))]
    public class MimicRespawnTrigger : MonoBehaviour
    {
        [Header("Respawn Trigger Settings")]
        [Tooltip("Radius of the trigger sphere around the Mimic")]
        public float triggerRadius = 0.38f;
        
        [Tooltip("Offset from Mimic's center (Y axis)")]
        public float yOffset = 0f;
        
        [Tooltip("Only activate trigger during proximity homing mode (prevents wall kills)")]
        public bool onlyActiveInHomingMode = true;
        
        [Tooltip("Show debug visualization in scene view")]
        public bool showDebugGizmo = true;
        
        [Tooltip("Color when trigger is ACTIVE (homing mode)")]
        public Color activeGizmoColor = new Color(1f, 1f, 0f, 0.5f); // Yellow
        
        [Tooltip("Color when trigger is INACTIVE (other modes)")]
        public Color inactiveGizmoColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gray
        
        [Header("Audio Settings")]
        [Tooltip("Sound to play when Mimic kills the player (2D sound)")]
        public AudioClip mimicKillSound;
        
        [Tooltip("Volume for Mimic kill sound (0-1)")]
        [Range(0f, 1f)]
        public float soundVolume = 1f;
        
        [Space(10)]
        [Header("Hunt Mode Audio")]
        [Tooltip("Sound to play when Mimic first detects player (one-shot)")]
        public AudioClip detectionSound;
        
        [Tooltip("Music to loop while in hunt/homing mode")]
        public AudioClip huntMusic;
        
        [Tooltip("Volume for detection sound (0-1)")]
        [Range(0f, 1f)]
        public float detectionVolume = 1f;
        
        [Tooltip("Volume for hunt music (0-1)")]
        [Range(0f, 1f)]
        public float huntMusicVolume = 0.5f;
        
        [Tooltip("How fast the hunt music fades in (higher = faster)")]
        public float fadeInSpeed = 3f;
        
        [Tooltip("How fast the hunt music fades out (higher = faster)")]
        public float fadeOutSpeed = 1f;
        
        private GameObject triggerObject;
        private SphereCollider triggerCollider;
        private RespawnTrigger respawnTriggerComponent;
        private Movement movementComponent;
        private AudioSource killAudioSource;
        private AudioSource musicAudioSource;
        
        private bool wasChasing = false;
        private bool hasPlayedDetectionSound = false;
        private float targetMusicVolume = 0f;
        private float currentMusicVolume = 0f;
        
        void Start()
        {
            SetupRespawnTrigger();
            
            // Get Movement component to track homing state
            movementComponent = GetComponent<Movement>();
            if (movementComponent == null)
            {
                // Debug.LogError("[MimicRespawnTrigger] Movement component not found! Trigger will always be active.", this);
            }
            
            // Setup audio
            SetupAudio();
        }
        
        void SetupAudio()
        {
            // AudioSource 1: Kill sound (one-shot)
            killAudioSource = gameObject.AddComponent<AudioSource>();
            killAudioSource.playOnAwake = false;
            killAudioSource.loop = false;
            killAudioSource.spatialBlend = 0f; // 2D
            killAudioSource.volume = soundVolume;
            
            // AudioSource 2: Hunt music (looping with fade)
            musicAudioSource = gameObject.AddComponent<AudioSource>();
            musicAudioSource.playOnAwake = false;
            musicAudioSource.loop = true;
            musicAudioSource.spatialBlend = 0f; // 2D
            musicAudioSource.volume = 0f; // Start silent
            musicAudioSource.clip = huntMusic;
        }
        
        /// <summary>
        /// Play the Mimic kill sound (called by RespawnTrigger when Mimic kills player)
        /// </summary>
        public void PlayMimicKillSound()
        {
            if (mimicKillSound != null && killAudioSource != null)
            {
                killAudioSource.PlayOneShot(mimicKillSound, soundVolume);
            }
        }
        
        void Update()
        {
            if (movementComponent == null) return;
            
            bool isHoming = movementComponent.IsProximityHoming;
            bool isChasing = movementComponent.IsChasing;
            
            // --- UPDATE TRIGGER COLLIDER ---
            if (triggerCollider != null && onlyActiveInHomingMode)
            {
                // Enable trigger ONLY during proximity homing mode
                if (triggerCollider.enabled != isHoming)
                {
                    triggerCollider.enabled = isHoming;
                }
            }
            else if (triggerCollider != null && !onlyActiveInHomingMode)
            {
                // If homing-only mode is disabled, always keep trigger active
                if (!triggerCollider.enabled)
                {
                    triggerCollider.enabled = true;
                }
            }
            
            // --- HANDLE MODE-BASED AUDIO ---
            
            // DETECTION: Play one-shot sound when FIRST entering CHASE mode (explore → hunt transition)
            if (isChasing && !wasChasing)
            {
                // Just entered chase/hunt mode - player first detected!
                if (detectionSound != null && !hasPlayedDetectionSound)
                {
                    AudioSource.PlayClipAtPoint(detectionSound, transform.position, detectionVolume);
                    hasPlayedDetectionSound = true;
                }
                
                // Start hunt music
                targetMusicVolume = huntMusicVolume;
                if (musicAudioSource != null && huntMusic != null && !musicAudioSource.isPlaying)
                {
                    musicAudioSource.Play();
                }
            }
            // STOP HUNTING: Fade out music when player is lost
            else if (!isChasing && wasChasing)
            {
                // Just exited chase/hunt mode - player lost
                targetMusicVolume = 0f;
                hasPlayedDetectionSound = false; // Reset for next detection
            }
            
            // FADE MUSIC
            if (musicAudioSource != null)
            {
                // Lerp towards target volume
                float fadeSpeed = (targetMusicVolume > currentMusicVolume) ? fadeInSpeed : fadeOutSpeed;
                currentMusicVolume = Mathf.MoveTowards(currentMusicVolume, targetMusicVolume, fadeSpeed * Time.deltaTime);
                musicAudioSource.volume = currentMusicVolume;
                
                // Stop playback when fully faded out
                if (currentMusicVolume <= 0.001f && musicAudioSource.isPlaying)
                {
                    musicAudioSource.Stop();
                }
            }
            
            wasChasing = isChasing;
        }
        
        void OnValidate()
        {
            // If in play mode and trigger exists, update its properties
            if (Application.isPlaying && triggerCollider != null)
            {
                triggerCollider.radius = triggerRadius;
                triggerObject.transform.localPosition = new Vector3(0, yOffset, 0);
            }
        }
        
        /// <summary>
        /// Creates a child GameObject with a trigger collider and RespawnTrigger component
        /// </summary>
        void SetupRespawnTrigger()
        {
            // Check if trigger already exists
            triggerObject = transform.Find("MimicRespawnTrigger")?.gameObject;
            
            if (triggerObject == null)
            {
                // Create new trigger object
                triggerObject = new GameObject("MimicRespawnTrigger");
                triggerObject.transform.SetParent(transform);
                triggerObject.transform.localPosition = new Vector3(0, yOffset, 0);
                triggerObject.transform.localRotation = Quaternion.identity;
                triggerObject.transform.localScale = Vector3.one;
                
            }
            else
            {
            }
            
            // Set layer to "Ignore Raycast" (layer 2)
            // This ensures the Mimic's leg raycasts won't hit this collider
            triggerObject.layer = 2;
            
            // Add or get SphereCollider
            triggerCollider = triggerObject.GetComponent<SphereCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = triggerObject.AddComponent<SphereCollider>();
            }
            
            // Configure collider
            triggerCollider.isTrigger = true;
            triggerCollider.radius = triggerRadius;
            
            // Add or get RespawnTrigger component
            respawnTriggerComponent = triggerObject.GetComponent<RespawnTrigger>();
            if (respawnTriggerComponent == null)
            {
                respawnTriggerComponent = triggerObject.AddComponent<RespawnTrigger>();
            }
            // RespawnTrigger now handles retreat triggering automatically by checking for Movement component
            
            // Verify parent Mimic has the correct layer mask configured
            Mimic mimicScript = GetComponent<Mimic>();
            if (mimicScript != null)
            {
                // Check if "Ignore Raycast" layer (layer 2) is in the ignore mask
                int ignoreRaycastLayer = 2;
                bool isLayerIgnored = ((mimicScript.raycastIgnoreLayers.value & (1 << ignoreRaycastLayer)) != 0);
                
                if (!isLayerIgnored)
                {
                    // Debug.LogWarning($"[MimicRespawnTrigger] Mimic's 'Raycast Ignore Layers' should include 'Ignore Raycast' layer. " +
                    //                $"Please check the Mimic component inspector and ensure layer 2 (Ignore Raycast) is selected.", this);
                }
                else
                {
                }
            }
        }
        
        void OnDrawGizmos()
        {
            if (!showDebugGizmo) return;
            
            // Draw sphere at runtime or in editor
            Vector3 center = transform.position + new Vector3(0, yOffset, 0);
            
            // Choose color based on active state (runtime) or default (editor)
            Color currentColor;
            if (Application.isPlaying && onlyActiveInHomingMode && movementComponent != null)
            {
                // Show yellow when active (homing), gray when inactive
                currentColor = movementComponent.IsProximityHoming ? activeGizmoColor : inactiveGizmoColor;
            }
            else if (Application.isPlaying && !onlyActiveInHomingMode)
            {
                // Always show active color if homing-only mode is disabled
                currentColor = activeGizmoColor;
            }
            else
            {
                // In editor, show active color as default
                currentColor = activeGizmoColor;
            }
            
            Gizmos.color = currentColor;
            Gizmos.DrawSphere(center, triggerRadius);
            
            // Draw wireframe for better visibility
            Gizmos.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f);
            Gizmos.DrawWireSphere(center, triggerRadius);
        }
        
        void OnDrawGizmosSelected()
        {
            // Draw brighter gizmo when selected
            Vector3 center = transform.position + new Vector3(0, yOffset, 0);
            
            // Show current active state color when selected
            Color currentColor;
            if (Application.isPlaying && onlyActiveInHomingMode && movementComponent != null)
            {
                currentColor = movementComponent.IsProximityHoming ? Color.yellow : Color.gray;
            }
            else
            {
                currentColor = Color.yellow;
            }
            
            Gizmos.color = currentColor;
            Gizmos.DrawWireSphere(center, triggerRadius);
            Gizmos.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.3f);
            Gizmos.DrawSphere(center, triggerRadius);
        }
    }
}

