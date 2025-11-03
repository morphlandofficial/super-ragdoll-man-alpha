using UnityEngine;

public class BillboardTilt : MonoBehaviour
{
    [Header("Tilt Billboard Settings")]
    public bool reverseDirection = false; // Reverse the tilt direction if needed
    
    [Header("Pitch Constraints")]
    [Tooltip("Minimum pitch angle in degrees (looking down)")]
    public float minPitch = -90f;
    
    [Tooltip("Maximum pitch angle in degrees (looking up)")]
    public float maxPitch = 90f;
    
    private Camera playerCamera;
    private Transform cameraTransform;
    
    void Start()
    {
        // Find the player camera - first try to find it by tag, then by name, then use main camera
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerCamera = player.GetComponentInChildren<Camera>();
        }
        
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        if (playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
        }
    }
    
    void LateUpdate()
    {
        if (cameraTransform == null) return;
        
        // Calculate direction to camera
        Vector3 direction = cameraTransform.position - transform.position;
        
        if (reverseDirection)
        {
            direction = -direction;
        }
        
        // Only process if there's a meaningful direction
        if (direction.sqrMagnitude > 0.001f)
        {
            // Calculate the vertical angle (tilt/pitch)
            float horizontalDistance = new Vector2(direction.x, direction.z).magnitude;
            float verticalAngle = Mathf.Atan2(direction.y, horizontalDistance) * Mathf.Rad2Deg;
            
            // Clamp the angle within the pitch constraints
            verticalAngle = Mathf.Clamp(verticalAngle, minPitch, maxPitch);
            
            // Apply only the X rotation (tilt), keep current Y and Z rotations
            transform.rotation = Quaternion.Euler(-verticalAngle, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z);
        }
    }
}

