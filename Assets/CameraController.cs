using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviourPun
{
    private new Camera camera;

    private void Awake()
    {
        camera = GetComponent<Camera>();
    }

    private void Start()
    {
        // Disable camera if this player instance is not ours
        camera.enabled = photonView.IsMine;

        // Set as main camera if it is ours
        if (photonView.IsMine)
        {
            GameManager.instance.localMainCamera = camera;
        }
    }
}
