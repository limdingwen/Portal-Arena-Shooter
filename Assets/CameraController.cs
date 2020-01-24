using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// The camera controller.
/// </summary>
[RequireComponent(typeof(Camera), typeof(AudioListener))]
public class CameraController : MonoBehaviourPun
{
    private new Camera camera;
    private AudioListener audioListener;

    private void Awake()
    {
        camera = GetComponent<Camera>();
        audioListener = GetComponent<AudioListener>();
    }

    private void Start()
    {
        // Disable camera + audio listener if this player instance is not ours
        camera.enabled = photonView.IsMine;
        audioListener.enabled = photonView.IsMine;

        // Set as main camera if it is ours
        if (photonView.IsMine)
        {
            GameManager.instance.localMainCamera = camera;
        }
    }
}
