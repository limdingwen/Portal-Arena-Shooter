using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A single portal. Can link up to other portals.
/// </summary>
public class Portal : MonoBehaviour
{
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
    private RenderTexture viewThroughToTexture;

    [Header("View through (from others)")]
    [Tooltip("The camera that will be rendered by other portals.")]
    public Camera viewThroughFromCamera;
    [System.NonSerialized]
    public Vector4 viewThroughFromPlane;

    // Teleportation
    private List<PortalableObject> objectsInPortal = new List<PortalableObject>();

    private void Awake()
    {
        // Create render texture for portal view
        viewThroughToTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.DefaultHDR);

        // Assign render texture to portal view
        viewThroughToMaterial = new Material(viewThroughToShader);
        viewThroughToMaterial.SetTexture("_MainTex", viewThroughToTexture);
        viewThroughToRenderer.material = viewThroughToMaterial;

        // Generate plane of this portal for view through purposes
        Plane tempPlane = new Plane(portalNormal.forward, transform.position);
        viewThroughFromPlane = new Vector4(tempPlane.normal.x, tempPlane.normal.y, tempPlane.normal.z, tempPlane.distance);
    }

    private void Start()
    {
        // Make target camera render onto our render texture
        target.viewThroughFromCamera.targetTexture = viewThroughToTexture;

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
        UpdateViewThrough();
        UpdateTeleport();
    }

    private void UpdateViewThrough()
    {
        // Frustum cull portals
        // Don't update and render target camera when this portal is not visible
        if (!viewThroughToRenderer.isVisible)
        {
            target.viewThroughFromCamera.enabled = false;
            return;
        }
        else
        {
            target.viewThroughFromCamera.enabled = true;
        }

        // Don't view through if no main camera (in game will appear as not updating)
        // (shouldn't appear as anything anyway if there isn't a camera...)
        if (!GameManager.instance.mainCamera)
            return;

        // Set target camera transform
        target.viewThroughFromCamera.transform.SetPositionAndRotation(
            TransformPositionBetweenPortals(this, target, GameManager.instance.mainCamera.transform.position),
            TransformRotationBetweenPortals(this, target, GameManager.instance.mainCamera.transform.rotation));

        // Convert target portal's plane to camera space (relative to target camera)
        // Explanation: https://danielilett.com/2019-12-18-tut4-3-matrix-matching/
        Vector4 targetViewThroughPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(target.viewThroughFromCamera.worldToCameraMatrix))
            * target.viewThroughFromPlane;

        // Set target camera projection matrix to clip walls between target portal and target camera
        // Portal camera will inherit FOV and near/clip values from main camera.
        target.viewThroughFromCamera.projectionMatrix = 
            GameManager.instance.mainCamera.CalculateObliqueMatrix(targetViewThroughPlaneCameraSpace);
    }

    private void UpdateTeleport()
    {
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
    }

    private void OnDestroy()
    {
        Destroy(viewThroughToMaterial);
        Destroy(viewThroughToTexture);
    }
}
