using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game manager.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Player instantiation")]
    [Tooltip("Number of players in a team")]
    public int teamPlayerCount = 10;
    [Tooltip("The human player prefab")]
    public GameObject humanPlayerPrefab;
    [Tooltip("The AI player prefab")]
    public GameObject aiPlayerPrefab;

    [Header("Deathmatch Spawns")]
    [Tooltip("Tag for deathmatch spawns")]
    public string deathmatchSpawnTag = "Deathmatch Spawn";
    private GameObject[] deathmatchSpawns;
    [Tooltip("Should players, in deathmatch, respawn in blue/red spawns as well?")]
    public bool deathmatchSpawnIncludesInitials = true;
    [Tooltip("If a player is occupying the space within X meters of a spawn, do not favor it.")]
    public float deathmatchSpawnNearbyPlayerRadius = 1;

    [Header("Teams")]
    [Tooltip("Blue team data")]
    public Team blueTeam;
    [Tooltip("Red team data")]
    public Team redTeam;

    [Header("Game Settings")]
    [Tooltip("Is friendly fire on?")]
    public bool friendlyFire = false;
    [Tooltip("Automatically suicide if below this Y, to bring player back to playfield if he has glitched out of world")]
    public float killY = -10;

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
            humanPlayer = Spawn(spawnsList, humanPlayerPrefab, team);
            if (humanPlayer)
                team.players.Add(humanPlayer);
        }

        // Spawn AI players
        for (int i = 0; i < teamPlayerCount - (includeHuman ? 1 : 0); i++)
        {
            PlayerController aiPlayer = Spawn(spawnsList, aiPlayerPrefab, team);
            if (aiPlayer)
                team.players.Add(aiPlayer);
        }
    }

    private PlayerController Spawn(List<GameObject> spawnsList, GameObject playerPrefab, Team team)
    {
        // Ran out of spawns?
        if (spawnsList.Count == 0)
        {
            Debug.LogWarning($"Not enough spawns! Cannot spawn {teamPlayerCount} players in team {team.name}.");
            return null;
        }

        GameObject spawn = spawnsList[Random.Range(0, spawnsList.Count)];
        spawnsList.Remove(spawn);

        PlayerController newPlayer = Instantiate(playerPrefab, spawn.transform.position, spawn.transform.rotation).GetComponent<PlayerController>();
        newPlayer.team = team;

        return newPlayer;
    }

    public Transform GetRandomDeathmatchSpawn()
    {
        // Compile list of spawns
        List<GameObject> spawns = new List<GameObject>(deathmatchSpawns);
        if (deathmatchSpawnIncludesInitials)
        {
            spawns.AddRange(redTeam.spawns);
            spawns.AddRange(blueTeam.spawns);
        }

        // Get favored spawns
        List<GameObject> favoredSpawns = new List<GameObject>();
        foreach (GameObject spawn in spawns)
        {
            // Check if person is near spawn
            bool personNearSpawn = false;
            foreach (PlayerController playerController in blueTeam.players)
            {
                if ((playerController.transform.position - spawn.transform.position).magnitude < deathmatchSpawnNearbyPlayerRadius)
                {
                    personNearSpawn = true;
                    break;
                }
            }
            if (personNearSpawn) break;
            foreach (PlayerController playerController in redTeam.players)
            {
                if ((playerController.transform.position - spawn.transform.position).magnitude < deathmatchSpawnNearbyPlayerRadius)
                {
                    personNearSpawn = true;
                    break;
                }
            }

            // Add to favored spawn
            if (!personNearSpawn)
            {
                favoredSpawns.Add(spawn);
            }
        }
        
        // Get random spawn, prioritizing favored spawns
        List<GameObject> spawnListUsed = favoredSpawns.Count != 0 ? favoredSpawns : spawns;
        return spawnListUsed[Random.Range(0, spawnListUsed.Count)].transform;
    }

    public Team GetEnemyTeam(Team yourTeam)
    {
        if (yourTeam == redTeam) return blueTeam;
        else return redTeam;
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
