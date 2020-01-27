using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A single portal. Can link up to other portals.
/// </summary>
public class Portal : MonoBehaviour
{
    private struct VisiblePortalResources
    {
        public Portal visiblePortal;
        public PortalRenderTexturePoolItem portalRenderTexturePoolItem;
        public Texture originalTexture;

        public VisiblePortalResources(Portal visiblePortal, PortalRenderTexturePoolItem temporaryRenderTexture, Texture originalTexture)
        {
            this.visiblePortal = visiblePortal;
            this.portalRenderTexturePoolItem = temporaryRenderTexture;
            this.originalTexture = originalTexture;
        }
    }

    /// <summary>
    /// Transforms a position from a sender portal to the target portal.
    /// For example, if the position is 1 unit behind the sender portal,
    /// the returned position will be 1 unit behind the target portal,
    /// regardless of orientation.
    /// Used for seamless transitions from one portal's space to another.
    /// </summary>
    /// <param name="sender">Sender portal</param>
    /// <param name="target">Target portal</param>
    /// <param name="position">Original position</param>
    /// <returns></returns>
    public static Vector3 TransformPositionBetweenPortals(Portal sender, Portal target, Vector3 position)
    {
        return
            target.portalNormal.TransformPoint(
                sender.transform.InverseTransformPoint(position));
    }

    /// <summary>
    /// Transforms a direction from a sender portal to the target portal.
    /// For example, if the direction is straight-on through the sender portal,
    /// the returned direction will also be straight-on through the target portal,
    /// regardless of orientation.
    /// Used for seamless transitions from one portal's space to another.
    /// </summary>
    /// <param name="sender">Sender portal</param>
    /// <param name="target">Target portal</param>
    /// <param name="position">Original position</param>
    /// <returns></returns>
    public static Vector3 TransformDirectionBetweenPortals(Portal sender, Portal target, Vector3 direction)
    {
        return
            target.portalNormal.TransformDirection(
                sender.transform.InverseTransformDirection(direction));
    }

    /// <summary>
    /// Transforms a rotation from a sender portal to the target portal.
    /// For example, if the rotation is straight-on through the sender portal,
    /// the returned rotation will also be straight-on through the target portal,
    /// regardless of orientation.
    /// Used for seamless transitions from one portal's space to another.
    /// </summary>
    /// <param name="sender">Sender portal</param>
    /// <param name="target">Target portal</param>
    /// <param name="rotation">Original rotation</param>
    /// <returns></returns>
    public static Quaternion TransformRotationBetweenPortals(Portal sender, Portal target, Quaternion rotation)
    {
        return
            target.portalNormal.rotation *
            Quaternion.Inverse(sender.transform.rotation) *
            rotation;
    }

    /// <summary>
    /// Renders a portal, with recursions.
    /// The portal will follow the manually-defined portal visibility graph up to a certain number of recursions, depth-first.
    /// It will then render the innermost portals first, followed by outer ones, all the way until the original one is rendered.
    /// GameManager.instance.mainCamera MUST be defined before calling this function.
    /// </summary>
    /// <param name="portalToRender">Which portal are we rendering?</param>
    /// <param name="refPosition">The reference camera's position</param>
    /// <param name="refRotation">The reference camera's rotation</param>
    /// <param name="temporaryRenderTexturePoolItem">Handle to the temporay render texture pool item used to render the portal. CALLEE MUST RELEASE AFTER USE!!!</param>
    /// <param name="originalTexture">Handle to the original texture on the portal. Reset material back to this after use</param>
    /// <param name="currentRecursion">How deep are we?</param>
    /// <param name="overrideMaxRecursion">Override the global GameManager maxRecursion value</param>
    public static void RenderViewThroughRecursive(
        Portal portalToRender,
        Vector3 refPosition,
        Quaternion refRotation,
        out PortalRenderTexturePoolItem temporaryRenderTexturePoolItem,
        out Texture originalTexture,
        int currentRecursion = 0,
        int? overrideMaxRecursion = null)
    {
        // =======
        // RECURSE
        // =======

        // Override max recursions
        int maxRecursions = overrideMaxRecursion.HasValue ? overrideMaxRecursion.Value : GameManager.instance.portalMaxRecursion;

        // Calculate target view through camera position
        Vector3 targetRefPosition = TransformPositionBetweenPortals(portalToRender, portalToRender.target, refPosition);
        Quaternion targetRefRotation = TransformRotationBetweenPortals(portalToRender, portalToRender.target, refRotation);

        // Store visible portal resources to release and reset (see function description for details)
        List<VisiblePortalResources> visiblePortalResourcesList = new List<VisiblePortalResources>();

        // Recurse if not at limit
        if (currentRecursion < maxRecursions)
        {
            foreach (Portal visiblePortal in portalToRender.target.viewThroughFromVisiblePortals)
            {
                RenderViewThroughRecursive(
                    visiblePortal,
                    targetRefPosition,
                    targetRefRotation,
                    out PortalRenderTexturePoolItem visiblePortalTemporaryRenderTexturePoolItem,
                    out Texture visiblePortalOriginalTexture,
                    currentRecursion + 1,
                    overrideMaxRecursion);

                visiblePortalResourcesList.Add(
                    new VisiblePortalResources(visiblePortal, visiblePortalTemporaryRenderTexturePoolItem, visiblePortalOriginalTexture));
            }
        }

        // ======
        // RENDER
        // ======

        // Get new temporary render texture and set to portal's material
        // Will be released by CALLEE, not by this function. This is so that the callee can use the render texture
        // for their own purposes, such as a Render() or a main camera render, before releasing it.
        temporaryRenderTexturePoolItem = GameManager.instance.GetPortalRenderTexture();
        originalTexture = portalToRender.viewThroughToMaterial.GetTexture("_MainTex");
        portalToRender.viewThroughToMaterial.SetTexture("_MainTex", temporaryRenderTexturePoolItem.renderTexture);

        // Use portal camera
        Camera portalCamera = GameManager.instance.portalCamera;
        portalCamera.targetTexture = temporaryRenderTexturePoolItem.renderTexture;

        // Set target camera transform
        portalCamera.transform.SetPositionAndRotation(targetRefPosition, targetRefRotation);

        // Convert target portal's plane to camera space (relative to target camera)
        // Explanation: https://danielilett.com/2019-12-18-tut4-3-matrix-matching/
        Vector4 targetViewThroughPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(portalCamera.worldToCameraMatrix))
            * portalToRender.target.viewThroughFromPlane;

        // Set target camera projection matrix to clip walls between target portal and target camera
        // Portal camera will inherit FOV and near/clip values from main camera.
        portalCamera.projectionMatrix =
            GameManager.instance.mainCamera.CalculateObliqueMatrix(targetViewThroughPlaneCameraSpace);

        // Render portal camera to target texture
        portalCamera.Render();

        // =================
        // RELEASE AND RESET
        // =================

        foreach (VisiblePortalResources resources in visiblePortalResourcesList)
        {
            // Reset to original texture
            // So that it will remain correct if the visible portal is still expecting to be rendered
            // on another camera but has already rendered its texture. Originally the texture may be overriden by other renders.
            resources.visiblePortal.viewThroughToMaterial.SetTexture("_MainTex", resources.originalTexture);
            // Release temp render texture
            GameManager.instance.ReleasePortalRenderTexture(resources.portalRenderTexturePoolItem);
        }
    }

    [Tooltip("The target portal.")]
    public Portal target;
    [Tooltip("This transform represents the normal of the portal's visible surface.")]
    public Transform portalNormal;
    [Tooltip("This transform represents where the NavMeshLink will be generated.")]
    public Transform navMeshLinkGuide;

    [Header("View through (to others)")]
    [Tooltip(
        "The renderer that will render the view through using a runtime generated texture." +
        "Requires the custom portal material.")]
    public MeshRenderer viewThroughToRenderer;
    [Tooltip("The portal surface shader. Used to generate the material at runtime.")]
    public Shader viewThroughToShader;
    private Material viewThroughToMaterial;
    //private RenderTexture viewThroughToTexture;

    [Header("View through (from others)")]
    [System.NonSerialized]
    public Vector4 viewThroughFromPlane; // Plane math object generated from portal position for clipping use
    [Tooltip("Portal visibility graph for recursion. What portals will be visible, if other portals were to view through to this one?")]
    public Portal[] viewThroughFromVisiblePortals;

    [Header("Teleportation")]
    [Tooltip("Disable teleportation for gameplay purposes or for performance reasons.")]
    public bool enableTeleportation = true;

    // Teleportation
    private List<PortalableObject> objectsInPortal = new List<PortalableObject>();

    private void Awake()
    {
        // Assign render texture to portal view
        viewThroughToMaterial = new Material(viewThroughToShader);
        viewThroughToRenderer.material = viewThroughToMaterial;

        // Generate plane of this portal for view through purposes
        Plane tempPlane = new Plane(portalNormal.forward, transform.position);
        viewThroughFromPlane = new Vector4(tempPlane.normal.x, tempPlane.normal.y, tempPlane.normal.z, tempPlane.distance);
    }

    private void Start()
    {
        // Generate nav mesh links
        OffMeshLink offMeshLink = gameObject.AddComponent<OffMeshLink>();
        Vector3 temp = navMeshLinkGuide.localPosition;
        temp.Scale(transform.localScale);
        offMeshLink.startTransform = navMeshLinkGuide;
        offMeshLink.endTransform = target.navMeshLinkGuide;
        offMeshLink.biDirectional = false;
        //offMeshLink.costOverride = 0;
    }

    private void LateUpdate()
    {
        UpdateTeleport();
    }

    private void UpdateTeleport()
    {
        if (!enableTeleportation)
            return;

        for (int i = 0; i < objectsInPortal.Count; i++)
        {
            // Check if portalable object is behind the portal
            // If so, we can assume they have crossed through the portal.
            // Implying from this, you should not be able to touch a portal from behind.
            // This can be changed later to allow portals to be touched from behind, but no support for now.
            Vector3 objPosRelativeToPortalNormal = portalNormal.transform.InverseTransformPoint(objectsInPortal[i].transform.position);
            if (objPosRelativeToPortalNormal.z < 0)
            {
                //Debug.Log("Object warped!");

                // NavMeshAgent support part 1
                NavMeshAgent navMeshAgent = objectsInPortal[i].GetComponent<NavMeshAgent>();
                if (navMeshAgent)
                {
                    navMeshAgent.enabled = false;
                }

                // Warp object
                objectsInPortal[i].transform.SetPositionAndRotation(
                    TransformPositionBetweenPortals(this, target, objectsInPortal[i].transform.position),
                    TransformRotationBetweenPortals(this, target, objectsInPortal[i].transform.rotation));

                // NavMeshAgent support part 2
                if (navMeshAgent)
                {
                    navMeshAgent.enabled = true;
                }

                // Update physics transforms after warp
                Physics.SyncTransforms();

                // Object is no longer in this side of the portal
                objectsInPortal.RemoveAt(i);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enableTeleportation)
            return;

        // Track object going through the portal
        // So we can teleport it instantly when it reaches the other side
        PortalableObject portalableObject = other.GetComponent<PortalableObject>();
        if (portalableObject)
        {
            //Debug.Log("Object added to portal");
            objectsInPortal.Add(portalableObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enableTeleportation)
            return;

        // Untrack object no longer going through the portal
        PortalableObject portalableObject = other.GetComponent<PortalableObject>();
        if (portalableObject)
        {
            objectsInPortal.Remove(portalableObject);
        }
    }

    private void OnDrawGizmos()
    {
        // Draw editor line to target portal to visualize linkage
        if (target)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, target.transform.position);
        }

        // Draw editor line to visible portals
        Gizmos.color = Color.blue;
        foreach (Portal visiblePortal in viewThroughFromVisiblePortals)
        {
            Gizmos.DrawLine(transform.position, visiblePortal.transform.position);
        }
    }

    private void OnDestroy()
    {
        Destroy(viewThroughToMaterial);
        //Destroy(viewThroughToTexture);
    }
}
