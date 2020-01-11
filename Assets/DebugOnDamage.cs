using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prints out a debug message on damage.
/// </summary>
public class DebugOnDamage : MonoBehaviour
{
    private void OnDamage()
    {
        Debug.Log("Ow! " + Time.time);
    }
}
