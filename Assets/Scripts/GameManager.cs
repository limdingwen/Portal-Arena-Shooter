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
    /// <summary>
    /// The local main camera. Possible to be null if no camera claims it.
    /// Set this variable directly to claim main camera.
    /// </summary>
    [HideInInspector] public Camera localMainCamera;
    /// <summary>
    /// The local player. Possible to be null if no player is spawned.
    /// </summary>
    [HideInInspector] public GameObject localPlayer;

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
}
