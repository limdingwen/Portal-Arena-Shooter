using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game manager.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // Player instantiation
    [Tooltip("The player prefab.")]
    public GameObject playerPrefab;
    private GameObject[] playerSpawns;
    //private int playerSpawnIndex = 0;

    // Local instances
    [System.NonSerialized]
    public Camera mainCamera;
    [System.NonSerialized]
    public GameObject player;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Get player spawns
        playerSpawns = GameObject.FindGameObjectsWithTag("Spawn");

        // Get current spawn in a round-robin way
        Transform playerSpawn = playerSpawns[0].transform;
        //playerSpawnIndex++;
        //if (playerSpawnIndex >= playerSpawns.Length)
        //    playerSpawnIndex = 0;

        // Spawn player
        player = Instantiate(playerPrefab, playerSpawn.position, playerSpawn.rotation);
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
