using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ProximityAudioLoop : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip audioClip;
    [SerializeField, Range(0f, 1f)] private float maxVolume = 1.0f;
    
    [Header("Proximity Settings")]
    [SerializeField] private float minDistance = 2.0f;  // Center - full volume
    [SerializeField] private float maxDistance = 10.0f; // Outer sphere - zero volume
    
    [Header("Player Reference")]
    [SerializeField] private GameObject playerPrefab; // Assign your Default Character prefab here
    [SerializeField] private string torsoObjectName = "torso"; // Name of the torso child object
    [SerializeField] private Transform playerOverride; // Optional: manually assign specific transform
    
    private AudioSource audioSource;
    private Transform playerTransform;
    
    void Start()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f; // 2D audio - we control volume manually
        audioSource.volume = 0f; // Start at zero
        audioSource.Play();
        
        FindPlayerInstance();
    }
    
    void FindPlayerInstance()
    {
        // Priority 1: Manual override
        if (playerOverride != null)
        {
            playerTransform = playerOverride;
            return;
        }
        
        // Priority 2: Find spawned instance of assigned prefab
        if (playerPrefab != null)
        {
            // Search for all GameObjects and find one matching the prefab name
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allObjects)
            {
                // Check if the object's name starts with the prefab name (handles "(Clone)" suffix)
                if (obj.name.StartsWith(playerPrefab.name) && obj.activeInHierarchy)
                {
                    // Try to find the torso child object
                    Transform torsoTransform = FindChildRecursive(obj.transform, torsoObjectName);
                    if (torsoTransform != null)
                    {
                        playerTransform = torsoTransform;
                        return;
                    }
                    else
                    {
                        // Fallback to root if torso not found
                        playerTransform = obj.transform;
                        return;
                    }
                }
            }
        }
        
        // Priority 3: Try Camera.main
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerTransform = mainCamera.transform;
            return;
        }
        
        // Priority 4: Try Player tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            return;
        }
    }
    
    Transform FindChildRecursive(Transform parent, string childName)
    {
        // Check direct children first
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }
        
        // If not found, search recursively in grandchildren
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        
        return null;
    }
    
    void Update()
    {
        // If we haven't found the player yet, keep trying
        if (playerTransform == null)
        {
            FindPlayerInstance();
            return;
        }
        
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        float targetVolume = 0f;
        
        // Calculate volume based on distance
        if (distance <= minDistance)
        {
            targetVolume = maxVolume;
        }
        else if (distance >= maxDistance)
        {
            targetVolume = 0f;
        }
        else
        {
            // Smooth fade between min and max distance
            float t = (distance - minDistance) / (maxDistance - minDistance);
            targetVolume = Mathf.Lerp(maxVolume, 0f, t);
        }
        
        audioSource.volume = targetVolume;
    }
    
    void OnDrawGizmos()
    {
        // Draw center sphere (full volume) in green
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minDistance);
        
        // Draw outer sphere (zero volume) in red
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        
        // Draw line to player if found
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            Gizmos.color = distance <= maxDistance ? Color.yellow : Color.gray;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }
}