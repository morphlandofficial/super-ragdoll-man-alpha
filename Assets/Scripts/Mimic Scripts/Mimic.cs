using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    public class Mimic : MonoBehaviour
    {
        [Header("Animation")]
        public GameObject legPrefab;

        [Range(2, 20)]
        public int numberOfLegs = 5;
        [Tooltip("The number of splines per leg")]
        [Range(1, 10)]
        public int partsPerLeg = 4;
        int maxLegs;

        public int legCount;
        public int deployedLegs;
        [Range(0, 19)]
        public int minimumAnchoredLegs = 2;
        public int minimumAnchoredParts;

        [Tooltip("Minimum duration before leg is replaced")]
        public float minLegLifetime = 5;
        [Tooltip("Maximum duration before leg is replaced")]
        public float maxLegLifetime = 15;

        public Vector3 legPlacerOrigin = Vector3.zero;
        [Tooltip("Leg placement radius offset")]
        public float newLegRadius = 3;

        public float minLegDistance = 4.5f;
        public float maxLegDistance = 6.3f;

        [Range(2, 50)]
        [Tooltip("Number of spline samples per legpart")]
        public int legResolution = 40;

        [Tooltip("Minimum lerp coeficient for leg growth smoothing")]
        public float minGrowCoef = 4.5f;
        [Tooltip("MAximum lerp coeficient for leg growth smoothing")]
        public float maxGrowCoef = 6.5f;

        [Tooltip("Minimum duration before a new leg can be placed")]
        public float newLegCooldown = 0.3f;

        bool canCreateLeg = true;

        List<GameObject> availableLegPool = new List<GameObject>();

        [Tooltip("This must be updates as the Mimin moves to assure great leg placement")]
        public Vector3 velocity;

        [Header("Surface Detection")]
        [Tooltip("Enable reaching for walls and ceiling, not just ground")]
        public bool enableWallAndCeilingGrab = true;
        [Tooltip("Maximum raycast distance to find surfaces")]
        public float surfaceSearchDistance = 15f;
        [Tooltip("Chance to prioritize non-ground surfaces (0-1)")]
        [Range(0f, 1f)]
        public float wallCeilingPriority = 0.4f;
        [Tooltip("Layers to ignore when raycasting for leg placement (put respawn trigger on this layer)")]
        public LayerMask raycastIgnoreLayers = 1 << 2; // Default: Ignore "Ignore Raycast" layer (layer 2)

        void Start()
        {
            ResetMimic();
        }

        private void OnValidate()
        {
            ResetMimic();
        }

        private void ResetMimic()
        {
            foreach (Leg g in GameObject.FindObjectsByType<Leg>(FindObjectsSortMode.None))
            {
                Destroy(g.gameObject);
            }
            legCount = 0;
            deployedLegs = 0;

            maxLegs = numberOfLegs * partsPerLeg;
            float rot = 360f / maxLegs;
            Vector2 randV = Random.insideUnitCircle;
            velocity = new Vector3(randV.x, 0, randV.y);
            minimumAnchoredParts = minimumAnchoredLegs * partsPerLeg;
            maxLegDistance = newLegRadius * 2.1f;

        }

        IEnumerator NewLegCooldown()
        {
            canCreateLeg = false;
            yield return new WaitForSeconds(newLegCooldown);
            canCreateLeg = true;
        }

        // Update is called once per frame
        void Update()
        {
            if (!canCreateLeg)
                return;

            // New leg origin is placed in front of the mimic
            legPlacerOrigin = transform.position + velocity.normalized * newLegRadius;

            if (legCount <= maxLegs - partsPerLeg)
            {
                // Offset The leg origin by a random vector
                Vector2 offset = Random.insideUnitCircle * newLegRadius;
                Vector3 newLegPosition = legPlacerOrigin + new Vector3(offset.x, 0, offset.y);

                // If the mimic is moving and the new leg position is behind it, mirror it to make
                // it reach in front of the mimic.
                if (velocity.magnitude > 1f)
                {
                    float newLegAngle = Vector3.Angle(velocity, newLegPosition - transform.position);

                    if (Mathf.Abs(newLegAngle) > 90)
                    {
                        newLegPosition = transform.position - (newLegPosition - transform.position);
                    }
                }

                if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(legPlacerOrigin.x, 0, legPlacerOrigin.z)) < minLegDistance)
                    newLegPosition = ((newLegPosition - transform.position).normalized * minLegDistance) + transform.position;

                // if the angle is too big, adjust the new leg position towards the velocity vector
                if (Vector3.Angle(velocity, newLegPosition - transform.position) > 45)
                    newLegPosition = transform.position + ((newLegPosition - transform.position) + velocity.normalized * (newLegPosition - transform.position).magnitude) / 2f;

                // Find surface to attach leg to (ground, walls, or ceiling)
                Vector3 myHit = FindNearestSurface(newLegPosition);

                float lifeTime = Random.Range(minLegLifetime, maxLegLifetime);

                StartCoroutine("NewLegCooldown");
                for (int i = 0; i < partsPerLeg; i++)
                {
                    RequestLeg(myHit, legResolution, maxLegDistance, Random.Range(minGrowCoef, maxGrowCoef), this, lifeTime);
                    if (legCount >= maxLegs)
                        return;
                }
            }
        }

        // object pooling to limit leg instantiation
        void RequestLeg(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            GameObject newLeg;
            if (availableLegPool.Count > 0)
            {
                newLeg = availableLegPool[availableLegPool.Count - 1];
                availableLegPool.RemoveAt(availableLegPool.Count - 1);
            }
            else
            {
                newLeg = Instantiate(legPrefab, transform.position, Quaternion.identity);
            }
            newLeg.SetActive(true);
            newLeg.GetComponent<Leg>().Initialize(footPosition, legResolution, maxLegDistance, growCoef, myMimic, lifeTime);
            newLeg.transform.SetParent(myMimic.transform);
        }

        public void RecycleLeg(GameObject leg)
        {
            availableLegPool.Add(leg);
            leg.SetActive(false);
        }

        /// <summary>
        /// Searches for nearest surface in multiple directions (ground, walls, ceiling)
        /// Returns the hit point where the leg should attach
        /// </summary>
        Vector3 FindNearestSurface(Vector3 searchOrigin)
        {
            RaycastHit hit;
            List<RaycastHit> validHits = new List<RaycastHit>();

            // Create inverse layer mask (raycast everything EXCEPT ignored layers)
            int layerMask = ~raycastIgnoreLayers.value;

            // Always check downward first (ground)
            if (Physics.Raycast(searchOrigin + Vector3.up * surfaceSearchDistance, -Vector3.up, out hit, surfaceSearchDistance * 2f, layerMask))
            {
                // Make sure there's line of sight from body to hit point
                RaycastHit obstacleCheck;
                if (!Physics.Linecast(transform.position, hit.point, out obstacleCheck, layerMask))
                {
                    validHits.Add(hit);
                }
                else
                {
                    // If there's an obstacle, use the obstacle as the hit point
                    validHits.Add(obstacleCheck);
                }
            }

            // If wall/ceiling detection is enabled, search in more directions
            if (enableWallAndCeilingGrab)
            {
                // Define search directions: forward, back, left, right, up, and diagonals
                Vector3[] searchDirections = new Vector3[]
                {
                    Vector3.forward,
                    Vector3.back,
                    Vector3.left,
                    Vector3.right,
                    Vector3.up,
                    (Vector3.forward + Vector3.right).normalized,
                    (Vector3.forward + Vector3.left).normalized,
                    (Vector3.back + Vector3.right).normalized,
                    (Vector3.back + Vector3.left).normalized,
                    (Vector3.forward + Vector3.up).normalized,
                    (Vector3.back + Vector3.up).normalized,
                    (Vector3.left + Vector3.up).normalized,
                    (Vector3.right + Vector3.up).normalized
                };

                // Cast rays in all directions from the search origin
                foreach (Vector3 direction in searchDirections)
                {
                    if (Physics.Raycast(searchOrigin, direction, out hit, surfaceSearchDistance, layerMask))
                    {
                        // Verify line of sight from body
                        RaycastHit obstacleCheck;
                        if (!Physics.Linecast(transform.position, hit.point, out obstacleCheck, layerMask))
                        {
                            validHits.Add(hit);
                        }
                        else if (Vector3.Distance(transform.position, obstacleCheck.point) > 0.5f)
                        {
                            // Use the obstacle if it's not too close to the body
                            validHits.Add(obstacleCheck);
                        }
                    }
                }
            }

            // Choose the best surface from valid hits
            if (validHits.Count > 0)
            {
                // Sometimes prioritize non-ground surfaces for more interesting movement
                bool preferWallOrCeiling = Random.value < wallCeilingPriority;

                if (preferWallOrCeiling && enableWallAndCeilingGrab)
                {
                    // Try to find a wall or ceiling hit
                    foreach (RaycastHit validHit in validHits)
                    {
                        // Check if surface normal is not pointing up (not ground)
                        if (Vector3.Dot(validHit.normal, Vector3.up) < 0.7f)
                        {
                            return validHit.point;
                        }
                    }
                }

                // Default: Choose the closest surface
                RaycastHit closestHit = validHits[0];
                float closestDistance = Vector3.Distance(transform.position, validHits[0].point);

                foreach (RaycastHit validHit in validHits)
                {
                    float distance = Vector3.Distance(transform.position, validHit.point);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestHit = validHit;
                    }
                }

                return closestHit.point;
            }

            // Fallback: return the search origin if no surface found
            return searchOrigin;
        }
    }

}