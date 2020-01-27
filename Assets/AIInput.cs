using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(PlayerController))]
public class AIInput : MonoBehaviour
{
    [Header("Shooting")]
    [Tooltip("In degrees. If the horizontal aim degree offset is lower than this, do not rotate")]
    public float aimDeadZone = 0;
    [Tooltip("In degrees. If the horizontal aim degree offset is lower than this, rotate by a slower amount")]
    public float aimSlowZone = 45;
    [Tooltip("In degrees. If the horizontal aim degree offset is lower than this, the AI will shoot")]
    public float aimShootZone = 20;
    [Tooltip("In degrees. When horizontal aim degree offset is higher than slow zone, rotate by X degrees per second")]
    public float aimHorizontalRotationSpeed = 180;
    [Tooltip("In degrees. When horizontal aim degree offset is lower than slow zone but higher than dead zone, rotate by X degrees per second")]
    public float aimHorizontalSlowRotationSpeed = 30;
    [Tooltip("Layer mask used to determine line of sight. Should only interesect with solid objects. Hitboxes not included")]
    public LayerMask aimLineOfSightMask;

    [Header("Moving")]
    [Tooltip("How long to wait before recalculating path? Happens ALL the time, even if destination has not changed")]
    public float pathfindingInterval = 1;
    private float pathfindingIntervalLeft;
    [Tooltip("What areas can this AI walk through?")]
    public int pathfindingAreaMask = NavMesh.AllAreas;
    [Tooltip("How close must this AI be to a corner before it'll go to the next corner? If too small, it may get stuck")]
    public float pathfindingCornerRange = 0.05f;
    private NavMeshPath path;
    private Vector3[] pathCorners;
    private int pathCurrentCornerIndex;
    private PlayerController movingCurrentTarget;

    // Cache
    private List<PlayerController> aliveEnemies = new List<PlayerController>();

    private PlayerController playerController;
    private Team enemyTeam;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        path = new NavMeshPath();
    }

    private void Start()
    {
        enemyTeam = GameManager.instance.GetEnemyTeam(playerController.team);
    }

    private void Update()
    {
        Vector2 movementDelta = Vector2.zero;
        bool movementJump = false;
        bool movementRelativeToWorld = false;

        // Update timers
        if (pathfindingIntervalLeft > 0)
            pathfindingIntervalLeft -= Time.deltaTime;

        // Find ALL alive enemies and ANY shootable enemy
        PlayerController shootableEnemy = null;
        aliveEnemies.Clear();
        foreach (PlayerController enemy in enemyTeam.players)
        {
            if (enemy.isAlive)
            {
                aliveEnemies.Add(enemy);
                if (HasLineOfSight(enemy.transform.position))
                {
                    shootableEnemy = enemy;
                }
            }
        }

        // If there is a shootable enemy, shoot it
        if (shootableEnemy)
        {
            // Calculate direction to enemy
            Vector3 direction = shootableEnemy.transform.position - playerController.shootingRaycastOrigin.position;
            direction.Normalize();

            // Calculate angle offsets to enemy
            float horizontalAngle = Vector3.SignedAngle(playerController.shootingRaycastOrigin.forward, direction, Vector3.up);
            float absoluteVerticalAngle = Vector3.SignedAngle(transform.forward, direction, transform.right);

            // Calculate horizontal rotation needed to reach enemy, taking slowzone into account
            float horizontalRotation;
            if (horizontalAngle < -aimSlowZone)
            {
                // Fast zone (negative)
                horizontalRotation = aimHorizontalRotationSpeed * Time.deltaTime;
                // Snap to slow zone
                if (horizontalAngle + horizontalRotation > -aimSlowZone)
                    horizontalRotation = -aimSlowZone - horizontalAngle;
            }
            else if (horizontalAngle > aimSlowZone)
            {
                // Fast zone (positive)
                horizontalRotation = -aimHorizontalRotationSpeed * Time.deltaTime;
                // Snap to slow zone
                if (horizontalAngle + horizontalRotation < aimSlowZone)
                    horizontalRotation = aimSlowZone - horizontalAngle;
            }
            else if (horizontalAngle < -aimDeadZone)
            {
                // Slow zone (negative)
                horizontalRotation = aimHorizontalSlowRotationSpeed * Time.deltaTime;
                // Snap to dead zone
                if (horizontalAngle + horizontalRotation > -aimDeadZone)
                    horizontalRotation = aimDeadZone - horizontalAngle;
            }
            else if (horizontalAngle > aimDeadZone)
            {
                // Slow zone (positive)
                horizontalRotation = -aimHorizontalSlowRotationSpeed * Time.deltaTime;
                // Snap to dead zone
                if (horizontalAngle + horizontalRotation < aimDeadZone)
                    horizontalRotation = aimDeadZone - horizontalAngle;
            }
            else
            {
                // Dead zone
                horizontalRotation = 0;
            }

            // Rotate player
            playerController.RotateCamera(new Vector2(-horizontalRotation, absoluteVerticalAngle), true);

            // Shoot if within shoot zone
            if (horizontalAngle > -aimShootZone && horizontalAngle < aimShootZone)
            {
                playerController.Shoot();
            }
            //if (Physics.Raycast(
            //    playerController.shootingRaycastOrigin.position,
            //    playerController.shootingRaycastOrigin.forward,
            //    out RaycastHit hit,
            //    Mathf.Infinity,
            //    playerController.shootingRaycastMask))
            //{
            //    if (hit.collider.GetComponentInParent<PlayerController>() == shootableEnemy)
            //        playerController.Shoot();
            //}
        }
        // If there is no shootable enemy, and there are alive enemies
        // Pathfind a way to an alive enemy
        else if (aliveEnemies.Count != 0)
        {
            if (pathfindingIntervalLeft <= 0)
            {
                pathfindingIntervalLeft = pathfindingInterval;

                // Check if current target does not exist or is dead
                // If so, assign new target
                if (!movingCurrentTarget || !movingCurrentTarget.isAlive)
                {
                    movingCurrentTarget = aliveEnemies[Random.Range(0, aliveEnemies.Count)];
                }

                // Calculate new path
                NavMesh.CalculatePath(transform.position, movingCurrentTarget.transform.position, pathfindingAreaMask, path);

                // Set corners based on new path
                // If path is invalid, make corners null
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    pathCorners = path.corners; // Decreases allocation from path.corners
                    pathCurrentCornerIndex = 0;
                }
                else
                {
                    pathCorners = null;
                }
            }

            // Traverse corners if path is valid
            if (pathCorners != null)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2, NavMesh.AllAreas))
                {
                    Vector3 displacement = pathCorners[pathCurrentCornerIndex] - hit.position;
                    movementDelta = new Vector2(displacement.x, displacement.z).normalized;
                    movementRelativeToWorld = true;

                    // Go to next corner
                    if (displacement.magnitude <= pathfindingCornerRange)
                    {
                        // If next corner does not exist, just stay on current corner
                        if (pathCurrentCornerIndex < pathCorners.Length - 1)
                        {
                            pathCurrentCornerIndex++;
                        }
                    }
                }
            }
        }
        // If there are no alive enemies, do nothing
        else
        {
        }

        // Move playercontroller
        playerController.Move(movementDelta, movementJump, movementRelativeToWorld);
    }

    /// <summary>
    /// Attempts to change the path's current corner to a corner on the path described using a Vector3 world position.
    /// Useful when trying to change a path's current corner to an offmeshlink point or a known corner.
    /// </summary>
    /// <param name="referenceCorner">The ference corner, world position.</param>
    /// <param name="samplePosition">Should we use a sample position from the nav mesh instead of raw position?</param>
    /// <param name="indexOffset">Change the current corner to the NEXT or PREVIOUS one instead?</param>
    /// <param name="tolerance">What is the tolerance for the reference corner? Combating floating point errors.</param>
    /// <returns>True if success (corner found), false if not.</returns>
    public bool AttemptChangePathCurrentCorner(Vector3 referenceCorner, bool samplePosition = true, int indexOffset = 0, float tolerance = 0.01f)
    {
        Vector3 position;

        if (samplePosition && NavMesh.SamplePosition(referenceCorner, out NavMeshHit hit, 2, NavMesh.AllAreas))
        {
            position = hit.position;
        }
        else
        {
            position = referenceCorner;
        }

        if (pathCorners != null)
        {
            for (int i = 0; i < pathCorners.Length; i++)
            {
                if ((position - pathCorners[i]).magnitude < tolerance)
                {
                    pathCurrentCornerIndex = i + indexOffset;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Forces the AI to recalculate the next frame it has to move on a NavMesh.
    /// </summary>
    public void ForcePathRecalculate()
    {
        pathfindingIntervalLeft = 0;
    }

    private bool HasLineOfSight(Vector3 target)
    {
        return !Physics.Linecast(
                playerController.shootingRaycastOrigin.position,
                target,
                aimLineOfSightMask);
    }

    private void OnDrawGizmos()
    {
        if (Camera.current.CompareTag("No Gizmo Camera"))
            return;

        if (path == null || path.status == NavMeshPathStatus.PathInvalid)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 5);
        }
        else if (path.status == NavMeshPathStatus.PathComplete)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
            }
        }
        else if (path.status == NavMeshPathStatus.PathPartial)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);
            }
        }
    }
}
