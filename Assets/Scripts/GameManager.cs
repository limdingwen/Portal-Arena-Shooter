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
    [Tooltip("Number of players in a team")]
    public int teamPlayerCount = 10;
    [Tooltip("The human player prefab")]
    public GameObject humanPlayerPrefab;
    [Tooltip("The AI player prefab")]
    public GameObject aiPlayerPrefab;
    [Tooltip("Tag for deathmatch spawns")]
    public string deathmatchSpawnTag = "Deathmatch Spawn";
    private GameObject[] deathmatchSpawns;
    [Tooltip("Should players, in deathmatch, respawn in blue/red spawns as well?")]
    public bool deathmatchSpawnIncludesInitials = true;

    // Team definitions
    [Tooltip("Blue team definition")]
    public Team blueTeam;
    [Tooltip("Red team definition")]
    public Team redTeam;

    // Global instances
    [System.NonSerialized]
    public Camera mainCamera;
    [System.NonSerialized]
    public PlayerController humanPlayer;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Get player spawns
        blueTeam.spawns = GameObject.FindGameObjectsWithTag(blueTeam.spawnTag);
        redTeam.spawns = GameObject.FindGameObjectsWithTag(redTeam.spawnTag);
        deathmatchSpawns = GameObject.FindGameObjectsWithTag(deathmatchSpawnTag);

        // Initially spawn at red and blue spawns
        InitialSpawn(blueTeam, false);
        InitialSpawn(redTeam, true);
    }

    private void InitialSpawn(Team team, bool includeHuman)
    {
        List<GameObject> spawnsList = new List<GameObject>(team.spawns);

        // Spawn human player
        if (includeHuman)
        {
            humanPlayer = Spawn(spawnsList, humanPlayerPrefab, team).GetComponent<PlayerController>();
        }

        // Spawn AI players
        for (int i = 0; i < teamPlayerCount - (includeHuman ? 1 : 0); i++)
        {
            Spawn(spawnsList, aiPlayerPrefab, team);
        }
    }

    private GameObject Spawn(List<GameObject> spawnsList, GameObject playerPrefab, Team team)
    {
        // Ran out of spawns?
        if (spawnsList.Count == 0)
        {
            Debug.LogWarning($"Not enough spawns! Cannot spawn {teamPlayerCount} players in team {team.name}.");
            return null;
        }

        GameObject spawn = spawnsList[Random.Range(0, spawnsList.Count)];
        spawnsList.Remove(spawn);

        GameObject newPlayer = Instantiate(playerPrefab, spawn.transform.position, spawn.transform.rotation);
        newPlayer.GetComponent<PlayerController>().team = team;

        return newPlayer;
    }

    public Transform GetRandomDeathmatchSpawn()
    {
        List<GameObject> spawns = new List<GameObject>(deathmatchSpawns);
        if (deathmatchSpawnIncludesInitials)
        {
            spawns.AddRange(redTeam.spawns);
            spawns.AddRange(blueTeam.spawns);
        }

        return spawns[Random.Range(0, spawns.Count)].transform;
    }

    //private void Update()
    //{
    //    // Release mouse on tab
    //    if (Input.GetKeyDown("tab"))
    //    {
    //        Debug.Log("Toggling mouse");
    //        if (Cursor.visible)
    //        {
    //            Cursor.visible = false;
    //            Cursor.lockState = CursorLockMode.Locked;
    //        }
    //        else
    //        {
    //            Cursor.visible = true;
    //            Cursor.lockState = CursorLockMode.None;
    //        }
    //    }
    //}
}
