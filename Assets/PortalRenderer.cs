using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to a camera that renders portals.
/// This component takes into account portal occlusion volumes and then manually calls each directly visible portal to render recursively.
/// </summary>
public class PortalRenderer : MonoBehaviour
{
    public static PortalRenderer instance;

    [Tooltip("When not in a portal occlusion volume, render all portals?")]
    public bool defaultRenderAllPortals = false;

    public int debugRenderPortalCount = 0;

    private Portal[] allPortals;
    private PortalOcclusionVolume[] portalOcclusionVolumes;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        allPortals = FindObjectsOfType<Portal>();
        portalOcclusionVolumes = FindObjectsOfType<PortalOcclusionVolume>();
    }

    private void OnPreRender()
    {
        PortalOcclusionVolume currentVolume = null;
        foreach (PortalOcclusionVolume portalOcclusionVolume in portalOcclusionVolumes)
        {
            if (portalOcclusionVolume.collider.bounds.Contains(transform.position))
            {
                currentVolume = portalOcclusionVolume;
                break;
            }
        }

        Portal[] portalsToRender = currentVolume ? currentVolume.visiblePortals : defaultRenderAllPortals ? allPortals : null;
        debugRenderPortalCount = 0;
        if (portalsToRender != null)
        {
            foreach (Portal portal in portalsToRender)
            {
                // Frustum cull; do not render portal view through texture if not visible
                if (portal.viewThroughToRenderer.isVisible)
                {
                    Portal.RenderViewThroughRecursive(
                        portal,
                        GameManager.instance.mainCamera.transform.position,
                        GameManager.instance.mainCamera.transform.rotation,
                        out PortalRenderTexturePoolItem _,
                        out Texture _);
                    debugRenderPortalCount++;
                }
            }
        }
    }

    private void OnPostRender()
    {
        GameManager.instance.ReleaseAllPortalRenderTextures();
    }
}
