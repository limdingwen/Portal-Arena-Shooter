using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game manager.
/// </summary>
public partial class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Player instantiation")]
    [Tooltip("Number of players in a team")]
    public int teamPlayerCount = 10;
    [Tooltip("The human player prefab")]
    public GameObject humanPlayerPrefab;
    [Tooltip("The AI player prefab")]
    public GameObject aiPlayerPrefab;
    [Tooltip("To prevent players from clipping through the spawn")]
    public Vector3 globalSpawnOffset;

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

    [Header("Portals")]
    [Tooltip("Portal camera, reused by all portals for recursive rendering")]
    public Camera portalCamera;
    [Tooltip("The global max recursion for portal rendering")]
    public int portalMaxRecursion = 1;
    [Tooltip("How many render textures to allocate initially for portal rendering? " +
        "Formula is n*m+q, where n is number of recursions and m is the max or average amount of visible portals per portal," +
        "while q is the max or average amount of directly (not through other portals) visible portals at a given time," +
        "or the max or average amount of visible portals in a portal occlusion volume. " +
        "This can usually be set to 0 and just use resizable to dynamically size the pool when needed, but you can make it" +
        "statically allocated only to reduce chance of hitches during gameplay")]
    public int portalRenderTexturesPoolInitialSize = 0;
    [Tooltip("Max amount of render textures allocated, if resizable, to prevent filling up entire memory")]
    public int portalRenderTexturesPoolMaxSize = 100;
    [Tooltip("Allow the GameManager to allocate new render textures when needed? Only grows, does not shrink")]
    public bool portalRenderTexturesPoolResizable = true;
    private List<PortalRenderTexturePoolItem> portalRenderTexturesPool = new List<PortalRenderTexturePoolItem>();

    // Global instances
    [System.NonSerialized]
    public Camera mainCamera;
    [System.NonSerialized]
    public PlayerController humanPlayer;

    private void Awake()
    {
        instance = this;

        // Allocate initial render textures
        for (int i = 0; i < portalRenderTexturesPoolInitialSize; i++)
        {
            AllocatePortalRenderTexture();
        }
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

    /// <summary>
    /// Gives the callee a pooled render texture. Use ReleasePortalRenderTexture once done with use.
    /// </summary>
    /// <returns>An unused render texture, or null if pool is full.</returns>
    public PortalRenderTexturePoolItem GetPortalRenderTexture()
    {
        foreach (PortalRenderTexturePoolItem item in portalRenderTexturesPool)
        {
            if (!item.used)
            {
                // Return unused render texture
                item.used = true;
                return item;
            }
        }

        // No unused render textures
        if (portalRenderTexturesPoolResizable)
        {
            if (portalRenderTexturesPool.Count < portalRenderTexturesPoolMaxSize)
            {
                Debug.Log($"PortalRenderTexturePool full. Allocating new RT. New size is {portalRenderTexturesPool.Count + 1}.");
                return AllocatePortalRenderTexture(true);
            }
            else {
                Debug.LogWarning($"PortalRenderTexturePool full." +
                    $"Resizing allowed but max size ({portalRenderTexturesPoolMaxSize}) is reached. Returning null.");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("PortalRenderTexturePool full. Resizing not allowed. Returning null.");
            return null;
        }
    }

    /// <summary>
    /// Releases the PortalRenderTexturePoolItem.
    /// </summary>
    /// <param name="item">The PortalRenderTexturePoolItem.</param>
    public void ReleasePortalRenderTexture(PortalRenderTexturePoolItem item)
    {
        item.used = false;
    }

    /// <summary>
    /// Releases all PortalRenderTextures.
    /// </summary>
    public void ReleaseAllPortalRenderTextures()
    {
        foreach (PortalRenderTexturePoolItem item in portalRenderTexturesPool)
        {
            ReleasePortalRenderTexture(item);
        }
    }

    private PortalRenderTexturePoolItem AllocatePortalRenderTexture(bool used = false)
    {
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.DefaultHDR);
        renderTexture.Create();

        PortalRenderTexturePoolItem item = new PortalRenderTexturePoolItem(renderTexture, used);
        portalRenderTexturesPool.Add(item);

        return item;
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

        PlayerController newPlayer = Instantiate(playerPrefab, spawn.transform.position + globalSpawnOffset, spawn.transform.rotation).GetComponent<PlayerController>();
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

            // Only check if not already true
            if (!personNearSpawn)
            {
                foreach (PlayerController playerController in redTeam.players)
                {
                    if ((playerController.transform.position - spawn.transform.position).magnitude < deathmatchSpawnNearbyPlayerRadius)
                    {
                        personNearSpawn = true;
                        break;
                    }
                }
            }

            // If no person near spawn at all,
            // Add to favored spawn
            if (!personNearSpawn)
            {
                favoredSpawns.Add(spawn);
            }
        }

        if (favoredSpawns.Count == 0)
            Debug.Log("No favored spawns!");
        
        // Get random spawn, prioritizing favored spawns
        List<GameObject> spawnListUsed = favoredSpawns.Count != 0 ? favoredSpawns : spawns;
        return spawnListUsed[Random.Range(0, spawnListUsed.Count)].transform;
    }

    public Team GetEnemyTeam(Team yourTeam)
    {
        if (yourTeam == redTeam) return blueTeam;
        else return redTeam;
    }

    private void OnDestroy()
    {
        // Release render texture pool
        foreach (PortalRenderTexturePoolItem item in portalRenderTexturesPool)
        {
            item.renderTexture.Release();
        }
    }
}
