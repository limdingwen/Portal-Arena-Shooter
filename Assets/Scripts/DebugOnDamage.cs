using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prints out a debug message on damage.
/// </summary>
public class DebugOnDamage : MonoBehaviour
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Message")]
    private void OnDamage(OnDamageOptions options)
    {
        Debug.Log($"Ow! {options.damage} damage.");
    }
}
