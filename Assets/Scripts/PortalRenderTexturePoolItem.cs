using UnityEngine;

public class PortalRenderTexturePoolItem
{
    public RenderTexture renderTexture;
    public bool used;

    public PortalRenderTexturePoolItem(RenderTexture renderTexture, bool used)
    {
        this.renderTexture = renderTexture;
        this.used = used;
    }
}