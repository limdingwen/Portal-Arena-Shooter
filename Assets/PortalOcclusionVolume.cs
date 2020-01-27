using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a portal occlusion volume, which specifies which portals are visible in a given bounds.
/// </summary>
public class PortalOcclusionVolume : MonoBehaviour
{
    [Tooltip("The collider whose bounds to use")]
    public new Collider collider;
    [Tooltip("What portals to render in these bounds?")]
    public Portal[] visiblePortals;
}
