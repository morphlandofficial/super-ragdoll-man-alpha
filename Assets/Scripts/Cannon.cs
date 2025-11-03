#pragma warning disable 649

using System.Collections;
using UnityEngine;

public class Cannon : MonoBehaviour
{
    [SerializeField] private Rigidbody _cannonBallPrefab;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private float _launchForce = 100, _timeInterval = 3;
    [SerializeField] private int _cannonBallSolverIterations = 8,
                                 _cannonBallVelSolverIterations = 8;
    [SerializeField] private float _cannonBallLifetime = 5f;
    
    [Header("Firing Effects")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private Transform _animationRoot; // The root object to animate (leave empty to use this object)
    [SerializeField] private float _fireAnimationScale = 1.15f;
    [SerializeField] private float _fireAnimationDuration = 0.2f;

    private float timer;
    private AudioSource _audioSource;
    private Vector3 _originalScale;
    private bool _isAnimating = false;
    private Transform _targetTransform;

    // Start is called before the first frame update
    void Start() {
        // Get or add AudioSource component
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure AudioSource for 3D sound
        _audioSource.spatialBlend = 1.0f; // Full 3D
        _audioSource.playOnAwake = false;
        
        // Determine which transform to animate
        _targetTransform = _animationRoot != null ? _animationRoot : transform;
        
        // Store original scale of the target transform
        _originalScale = _targetTransform.localScale;
    }

    // Update is called once per frame
    void Update()
    {
        if (timer < _timeInterval) timer += Time.deltaTime;
        else {
            var cannonBall = SpawnCannonBall();
            cannonBall.linearVelocity = cannonBall.angularVelocity = Vector3.zero;
            cannonBall.MovePosition(_spawnPoint.position);
            cannonBall.MoveRotation(_spawnPoint.rotation);
            cannonBall.AddForce(_spawnPoint.forward * _launchForce, ForceMode.Impulse);

            // Destroy cannonball after lifetime
            Destroy(cannonBall.gameObject, _cannonBallLifetime);

            // Play firing effects
            PlayFireEffects();

            timer = 0;
        }
    }

    private void PlayFireEffects() {
        // Play fire sound if assigned
        if (_fireSound != null && _audioSource != null) {
            _audioSource.PlayOneShot(_fireSound);
        }
        
        // Trigger fire animation
        if (!_isAnimating) {
            StartCoroutine(FireAnimationCoroutine());
        }
    }

    private Rigidbody SpawnCannonBall() {
        // Always create a new cannonball since they get destroyed after lifetime
        var cannonBall = Instantiate(_cannonBallPrefab);
        cannonBall.solverIterations = _cannonBallSolverIterations;
        cannonBall.solverVelocityIterations = _cannonBallVelSolverIterations;
        return cannonBall;
    }

    private IEnumerator FireAnimationCoroutine() {
        _isAnimating = true;
        
        float halfDuration = _fireAnimationDuration / 2f;
        float elapsed = 0f;
        
        // Scale up phase
        while (elapsed < halfDuration) {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            _targetTransform.localScale = Vector3.Lerp(_originalScale, _originalScale * _fireAnimationScale, t);
            yield return null;
        }
        
        elapsed = 0f;
        
        // Scale down phase
        while (elapsed < halfDuration) {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            _targetTransform.localScale = Vector3.Lerp(_originalScale * _fireAnimationScale, _originalScale, t);
            yield return null;
        }
        
        // Ensure we end at the exact original scale
        _targetTransform.localScale = _originalScale;
        _isAnimating = false;
    }

    private void OnDrawGizmos() {
        if (_spawnPoint == null) return;

        // Draw arrow showing firing direction
        Gizmos.color = Color.red;
        Vector3 startPos = _spawnPoint.position;
        Vector3 direction = _spawnPoint.forward;
        float arrowLength = 2f;
        Vector3 endPos = startPos + direction * arrowLength;

        // Draw main arrow line
        Gizmos.DrawLine(startPos, endPos);

        // Draw arrowhead
        float arrowHeadLength = 0.3f;
        float arrowHeadAngle = 25f;
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;
        Vector3 up = Quaternion.LookRotation(direction) * Quaternion.Euler(180 + arrowHeadAngle, 0, 0) * Vector3.forward;
        Vector3 down = Quaternion.LookRotation(direction) * Quaternion.Euler(180 - arrowHeadAngle, 0, 0) * Vector3.forward;

        Gizmos.DrawLine(endPos, endPos + right * arrowHeadLength);
        Gizmos.DrawLine(endPos, endPos + left * arrowHeadLength);
        Gizmos.DrawLine(endPos, endPos + up * arrowHeadLength);
        Gizmos.DrawLine(endPos, endPos + down * arrowHeadLength);

        // Draw spawn point sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(startPos, 0.1f);
    }
}
