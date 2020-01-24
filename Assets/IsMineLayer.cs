using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Changes the layer of the object based on the isMine property.
/// Requires photonView.
/// </summary>
public class IsMineLayer : MonoBehaviourPun
{
    public int layerMine;
    public int layerNotMine;

    private void Start()
    {
        gameObject.layer = photonView.IsMine ? layerMine : layerNotMine;
    }
}
