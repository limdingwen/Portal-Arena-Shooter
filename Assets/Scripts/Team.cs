using UnityEngine;
using System.Collections;

[System.Serializable]
public struct Team
{
    [Tooltip("Name of the team")]
    public string name;
    [Tooltip("Color of the team")]
    public Color color;
    [Tooltip("Unity tag used to find the team's spawns")]
    public string spawnTag;

    [System.NonSerialized]
    public GameObject[] spawns;
}
