using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The player controller.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("First person camera")]
    public Camera firstPersonCamera;
    [Tooltip("Is AI?")]
    public bool isAI = false;

    //[Header("AI")]
    

    [Header("Input")]
    [Tooltip("Angles per unit of mouse movement")]
    public Vector2 mouseSensitivity = new Vector2(1, 1);
    private float cameraRotX;
    [Tooltip("Meters per second")]
    public Vector2 speed = new Vector2(5, 5);
    [Tooltip("Meters per second")]
    public float jumpSpeed = 5;
    [Tooltip("More weighty or more floaty?")]
    public float gravityMultiplier = 1;
    private float ySpeed;

    [Header("Shooting")]
    [Tooltip("The transform where the raycast will come from. Forward is the direction")]
    public Transform shootingRaycastOrigin;
    [Tooltip("Shooting raycast's layer mask. Useful for things like preventing player from shooting itself")]
    public LayerMask shootingRaycastMask;
    [Tooltip("Limits recursion in portals for performance and prevents infinite loops")]
    public int shootingPortalRecursions = 2;
    [Tooltip("Prevents raycast from shooting a target portal's trigger")]
    public int shootingRaycastTempPortalIgnoreLayer = 10;

    private CharacterController characterController;
    private NavMeshAgent navMeshAgent;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (isAI) navMeshAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (isAI)
            return;

        // HACKHACK: Disable cursor when player comes into play
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (isAI) UpdateAI();
        else UpdatePlayerInput();
    }

    private void UpdateAI()
    {
        if (Time.frameCount % 30 == 0 && GameManager.instance.player)
            navMeshAgent.SetDestination(GameManager.instance.player.transform.position);
    }

    private void UpdatePlayerInput()
    {
        // Mouse up/down, moves camera up and down (around X axis)
        // Clamp camera from -90 (straight up) to 90 (straight down)
        cameraRotX += Input.GetAxis("Mouse Y") * -mouseSensitivity.y;
        cameraRotX = Mathf.Clamp(cameraRotX, -90, 90);
        firstPersonCamera.transform.localRotation = Quaternion.Euler(cameraRotX, 0, 0);

        // Mouse left/right, moves player left and right (around Y axis)
        transform.Rotate(0, Input.GetAxis("Mouse X") * mouseSensitivity.x, 0);

        if (characterController.isGrounded)
        {
            // Cancel out any Y speed
            ySpeed = 0;

            // Apply jump
            if (Input.GetButton("Jump"))
            {
                ySpeed = jumpSpeed;
            }
        }
        else
        {
            // Apply gravity when not grounded
            ySpeed += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }

        // WASD movement
        characterController.Move(
            Input.GetAxis("Vertical") * speed.y * transform.forward * Time.deltaTime +
            Input.GetAxis("Horizontal") * speed.x * transform.right * Time.deltaTime +
            ySpeed * Vector3.up * Time.deltaTime);

        // Shoot
        if (Input.GetButtonDown("Fire1"))
        {
            ShootRecursive(shootingRaycastOrigin.position, shootingRaycastOrigin.forward);
        }
    }

    private void ShootRecursive(Vector3 position, Vector3 direction, int currentRecursion = 0, GameObject ignoreObject = null)
    {
        // Ignore a specific object when raycasting.
        // Useful for preventing a raycast through a portal from hitting the target portal from the back,
        // which makes a raycast unable to go through a portal since it'll just be absorbed by the target portal's trigger.
        int ignoreObjectOriginalLayer = 0;
        if (ignoreObject)
        {
            ignoreObjectOriginalLayer = ignoreObject.layer;
            ignoreObject.layer = shootingRaycastTempPortalIgnoreLayer;
        }

        // Shoot raycast.
        bool raycastHitSomething = Physics.Raycast(
            position,
            direction,
            out RaycastHit hit,
            Mathf.Infinity,
            shootingRaycastMask.value);

        // Reset ignore
        if (ignoreObject)
        {
            ignoreObject.layer = ignoreObjectOriginalLayer;
        }

        // If no objects are hit, the recursion ends here, with no effect.
        if (raycastHitSomething)
        {
            Portal portal = hit.collider.GetComponent<Portal>();
            if (portal)
            {
                // If the object hit is a portal, recurse, unless we are already at max recursions
                if (currentRecursion < shootingPortalRecursions)
                {
                    ShootRecursive(
                        Portal.TransformPositionBetweenPortals(portal, portal.target, hit.point),
                        Portal.TransformDirectionBetweenPortals(portal, portal.target, direction),
                        currentRecursion + 1,
                        portal.target.gameObject);
                }
            }
            else
            {
                // If the object hit is not a portal, recursion ends here with an OnDamage
                hit.collider.SendMessageUpwards("OnDamage", SendMessageOptions.DontRequireReceiver);
            }
        }
    }
}
