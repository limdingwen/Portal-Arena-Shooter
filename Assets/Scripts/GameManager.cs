using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game manager.
/// </summary>
public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager instance;

    // Player instantiation
    [Tooltip("The player prefab.")]
    public GameObject playerPrefab;
    [Tooltip("The possible player spawns. Used in a round-robin manner to minimize spawning into each other.")]
    public Transform[] playerSpawns;
    private int playerSpawnIndex = 0;

    // Local instances
    [System.NonSerialized]
    public Camera localMainCamera;
    [System.NonSerialized]
    public GameObject localPlayer;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            StartNetworked();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connecting to test room...");
        PhotonNetwork.JoinOrCreateRoom("Test Room", new RoomOptions(), TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        StartNetworked();
    }

    private void StartNetworked()
    {
        // Get current spawn in a round-robin way
        Transform playerSpawn = playerSpawns[playerSpawnIndex];
        playerSpawnIndex++;
        if (playerSpawnIndex >= playerSpawns.Length)
            playerSpawnIndex = 0;

        // Spawn player
        localPlayer = PhotonNetwork.Instantiate(playerPrefab.name, playerSpawn.position, playerSpawn.rotation);
    }

    private void Update()
    {
        // Release mouse on tab
        if (Input.GetKeyDown("tab"))
        {
            Debug.Log("Toggling mouse");
            if (Cursor.visible)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
