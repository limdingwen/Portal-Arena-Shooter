using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to a camera that renders portals.
/// This component takes into account portal occlusion volumes and then manually calls each directly visible portal to render recursively.
/// Required due to needing to release temporary render textures after rendering.
/// </summary>
public class PortalRenderer : MonoBehaviour
{
    public static PortalRenderer instance;

    [Tooltip("When not in a portal occlusion volume, render all portals?")]
    public bool defaultRenderAllPortals = false;

    //[System.NonSerialized]
    //public 

    private Portal[] allPortals;
    private List<PortalRenderTexturePoolItem> renderTexturesToReleaseThisRender = new List<PortalRenderTexturePoolItem>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        allPortals = FindObjectsOfType<Portal>();
    }

    private void OnPreRender()
    {
        renderTexturesToReleaseThisRender.Clear();
        foreach (Portal portal in allPortals)
        {
            Portal.RenderViewThroughRecursive(
                portal,
                GameManager.instance.mainCamera.transform.position,
                GameManager.instance.mainCamera.transform.rotation,
                out PortalRenderTexturePoolItem portalRenderTexturePoolItem,
                out Texture _);
            renderTexturesToReleaseThisRender.Add(portalRenderTexturePoolItem);
        }
    }

    private void OnPostRender()
    {
        foreach (PortalRenderTexturePoolItem portalRenderTexturePoolItem in renderTexturesToReleaseThisRender)
        {
            GameManager.instance.ReleasePortalRenderTexture(portalRenderTexturePoolItem);
        }
    }
}
