using UnityEngine;

public class HaniwaWanderer : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float wanderRadius = 10f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private float waitTimeAtDestination = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private float waitTimer = 0f;
    private Animator animator;
    
    private void Start()
    {
        // Store the starting position as the center of our wander area
        startPosition = transform.position;
        
        // Get the Animator component
        animator = GetComponent<Animator>();
        
        // Pick initial target
        PickNewTarget();
    }
    
    private void Update()
    {
        if (isMoving)
        {
            MoveTowardsTarget();
        }
        else
        {
            // Wait at destination
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                PickNewTarget();
            }
        }
        
        // Update animation state
        if (animator != null)
        {
            animator.SetBool("IsWalking", isMoving);
        }
    }
    
    private void PickNewTarget()
    {
        // Pick a random point within the radius circle
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        targetPosition = startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        isMoving = true;
    }
    
    private void MoveTowardsTarget()
    {
        // Calculate direction to target (only on XZ plane)
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0; // Keep movement on the ground plane
        
        float distanceToTarget = direction.magnitude;
        
        // Check if we've reached the target
        if (distanceToTarget <= stoppingDistance)
        {
            isMoving = false;
            waitTimer = waitTimeAtDestination;
            return;
        }
        
        // Move towards target
        direction.Normalize();
        transform.position += direction * moveSpeed * Time.deltaTime;
        
        // Rotate to face movement direction
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        Vector3 center = Application.isPlaying ? startPosition : transform.position;
        
        // Draw wander radius
        Gizmos.color = Color.yellow;
        DrawCircle(center, wanderRadius, 32);
        
        // Draw target position
        if (Application.isPlaying && isMoving)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(targetPosition, 0.3f);
            
            // Draw line to target
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }
    
    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}

