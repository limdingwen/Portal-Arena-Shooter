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
    public Transform firstPersonCamera;

    [Header("Movement")]
    [Tooltip("Meters per second")]
    public Vector2 speed = new Vector2(5, 5);
    [Tooltip("Meters per second")]
    public float jumpSpeed = 5;
    [Tooltip("More weighty or more floaty?")]
    public float gravityMultiplier = 1;
    private float cameraRotX;
    private float ySpeed;

    [Header("Shooting")]
    [Tooltip("The transform where the raycast will come from. Forward is the direction")]
    public Transform shootingRaycastOrigin;
    [Tooltip("Shooting raycast's layer mask. Useful for things like preventing player from shooting itself. " +
        "Don't include Ignore Raycast in the mask to enable portal recursions")]
    public LayerMask shootingRaycastMask;
    [Tooltip("Limits recursion in portals for performance and prevents infinite loops")]
    public int shootingPortalRecursions = 2;
    [Tooltip("The muzzle flash VFX")]
    public ParticleSystem gunMuzzleFlash;
    [Tooltip("The gun animator")]
    public Animator gunAnimator;
    [Tooltip("The trigger parameter used in the animator to initiate the shooting animation")]
    public string gunShootTrigger = "Shoot";
    [Tooltip("Prevents the gun from shooting too fast. Semi-auto assumed")]
    public float shootingCooldown = 0.17f;
    private float shootingCooldownLeft;
    [Tooltip("Audio set for firing sounds")]
    public AudioSet shootingFiringAudioSet;
    [Tooltip("Shooting damage")]
    public int shootingDamage = 13;

    [Header("Health")]
    [Tooltip("How much health the player starts with")]
    public int initialHealth = 100;
    [Tooltip("How much health the player can heal to. Also used for health bar graphics")]
    public int maxHealth = 100;
    [System.NonSerialized]
    public int health;
    [System.NonSerialized]
    public bool isAlive = true;
    [Tooltip("The pool name of the pool manager that manages blood effects")]
    public string bloodEffectPoolManagerName = "Blood Effect";
    private PoolManager bloodEffectPoolManager;

    [Header("Teams")]
    [Tooltip("What renderer's material to change tint for team color?")]
    public Renderer teamColorRenderer;
    [System.NonSerialized]
    public Team team;

    [Header("Ragdoll")]
    [Tooltip("Rigidbodies to make non kinematic when ragdoll")]
    public Rigidbody[] ragdollRigidbodies;
    [Tooltip("Colliders to enable when ragdoll")]
    public Collider[] ragdollColliders;
    [Tooltip("GameObjects to disable when ragdoll")]
    public GameObject[] ragdollDisables;
    [Tooltip("GameObjects to change layers to Ragdoll when ragdoll")]
    public GameObject[] ragdollLayerChanges;
    [Tooltip("Ragdoll layer")]
    public int ragdollRagdollLayer;
    // WARNING: ONLY USES FIRST RAGDOLL LAYER CHANGE OBJECT!!!
    private int ragdollOriginalLayer;
    [Tooltip("First rigidbody gets a force push of X amount on death, relative to where the bullet hit the hitbox")]
    public float ragdollDeathImpulse = 10;

    [Header("Respawning")]
    [Tooltip("After death, the player will wait X amount of seconds before respawning")]
    public float respawnTime = 3;
    private float respawnTimeLeft;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        health = initialHealth;
        ragdollOriginalLayer = ragdollLayerChanges[0].layer;
    }

    private void Start()
    {
        bloodEffectPoolManager = PoolManager.instances[bloodEffectPoolManagerName];

        // Set team color (GameManager should have set team after Awake)
        teamColorRenderer.material.color = team.color;

        // Clip player out of ground
        ClipPlayerOutOfGround();

        // Disable cursor when player comes into play
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void ClipPlayerOutOfGround()
    {
        characterController.Move(new Vector3(0, 0, 0.002f));
    }

    private void Update()
    {
        if (shootingCooldownLeft > 0)
            shootingCooldownLeft -= Time.deltaTime;

        // Respawn after certain amount of time
        if (!isAlive)
        {
            if (respawnTimeLeft > 0)
            {
                respawnTimeLeft -= Time.deltaTime;
            }
            else
            {
                Respawn();
            }
        }

        // Kill if below KillY
        if (transform.position.y < GameManager.instance.killY)
        {
            SendMessageUpwards("OnDamage", new OnDamageOptions(1000, transform.position, Vector3.up, Vector3.zero, this, false));
        }
    }

    private void Respawn()
    {
        // Reset variables
        isAlive = true;
        health = initialHealth;
        SetRagdoll(false);

        // Teleport to new spawn
        Transform spawn = GameManager.instance.GetRandomDeathmatchSpawn();
        transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        Physics.SyncTransforms();
        ClipPlayerOutOfGround();
    }

    /// <summary>
    /// Rotates the FPS camera, rotating the model appropriately.
    /// Can be called multiple times a frame.
    /// </summary>
    /// <param name="delta">X represents horizontal rotation, Y vertical.</param>
    public void RotateCamera(Vector2 delta, bool absoluteY = false)
    {
        if (!isAlive)
            return;

        // Mouse up/down, moves camera up and down (around X axis)
        // Clamp camera from -90 (straight up) to 90 (straight down)
        if (absoluteY) cameraRotX = delta.y;
        else cameraRotX += delta.y;
        cameraRotX = Mathf.Clamp(cameraRotX, -90, 90);
        firstPersonCamera.localRotation = Quaternion.Euler(cameraRotX, 0, 0);

        // Mouse left/right, moves player left and right (around Y axis)
        transform.Rotate(0, delta.x, 0);
    }

    /// <summary>
    /// Shoots the gun. Will not shoot if the cooldown is still active.
    /// </summary>
    public void Shoot()
    {
        if (!isAlive)
            return;
        if (shootingCooldownLeft > 0)
            return;

        // Actually shoot
        ShootRecursive(shootingRaycastOrigin.position, shootingRaycastOrigin.forward);

        // Show effect
        gunAnimator.SetTrigger(gunShootTrigger);
        gunMuzzleFlash.Clear();
        gunMuzzleFlash.Play();

        // Play sound
        shootingFiringAudioSet.PlayRandom();

        // Reset cooldown
        shootingCooldownLeft = shootingCooldown;
    }

    /// <summary>
    /// Moves the player. Must be called once and only once per frame to apply gravity.
    /// </summary>
    /// <param name="direction">The direction of movement on the XZ plane.</param>
    /// <param name="jump">Should it try to jump if it's on ground?</param>
    public void Move(Vector2 direction, bool jump = false, bool relativeToWorld = false)
    {
        if (!isAlive)
            return;

        if (characterController.isGrounded)
        {
            // Cancel out any Y speed
            ySpeed = 0;

            // Apply jump
            if (jump)
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
            direction.y * speed.y * (relativeToWorld ? Vector3.forward : transform.forward) * Time.deltaTime +
            direction.x * speed.x * (relativeToWorld ? Vector3.right : transform.right) * Time.deltaTime +
            ySpeed * Vector3.up * Time.deltaTime);
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
            ignoreObject.layer = 2; // Ignore raycast
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
                    //Debug.Log($"Shooting recursive, #{currentRecursion+1}");
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
                hit.collider.SendMessageUpwards(
                    "OnDamage",
                    new OnDamageOptions(shootingDamage, hit.point, hit.normal, direction, this, true),
                    SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Message")]
    private void OnDamage(OnDamageOptions options)
    {
        // Blood effect
        if (options.drawBlood)
            bloodEffectPoolManager.ActivatePooledObject(options.point, Quaternion.LookRotation(options.normal));

        // Take damage if not friendly fire
        if (GameManager.instance.friendlyFire || options.inflictingPlayer.team != team || options.inflictingPlayer == this)
        {
            health -= options.damage;
            if (isAlive && health <= 0)
            {
                // Die
                isAlive = false;
                respawnTimeLeft = respawnTime;
                SetRagdoll(true);
                ragdollRigidbodies[0].AddForceAtPosition(options.damageDirection * ragdollDeathImpulse, options.point, ForceMode.Impulse);
            }
        }
    }

    private void SetRagdoll(bool ragdoll)
    {
        foreach (Rigidbody rigidbody in ragdollRigidbodies) rigidbody.isKinematic = !ragdoll;
        foreach (Collider collider in ragdollColliders) collider.enabled = ragdoll;
        foreach (GameObject disable in ragdollDisables) disable.SetActive(!ragdoll);
        foreach (GameObject layerChange in ragdollLayerChanges) layerChange.layer = ragdoll ? ragdollRagdollLayer : ragdollOriginalLayer;

        characterController.enabled = !ragdoll;
    }
}
