using UnityEngine;
using UnityEditor;

public class PortalSanityChecker : ScriptableObject
{
    const float GlobalScaleTolerance = 0.01f;

    [MenuItem("Tools/Portal Sanity Checker")]
    static void DoIt()
    {
        string buffer = "";

        Portal[] portals = FindObjectsOfType<Portal>();
        foreach (Portal portal in portals)
        {
            // Check if portal does not link up to any portal
            if (portal.target == null)
            {
                buffer += $"{portal.gameObject.name} does not have a target.\n";
                continue; // Prevent null errors
            }
            // Check if portal's target links back here, to the same portal
            // I.e. Is it correctly bidirectionally linked?
            else if (portal.target.target != portal)
            {
                buffer += $"{portal.gameObject.name}'s target, {portal.target.gameObject.name}, does not link back.\n";
            }

            // Check scale difference
            if (
                Mathf.Abs(portal.transform.lossyScale.x - portal.target.transform.lossyScale.x) > GlobalScaleTolerance ||
                Mathf.Abs(portal.transform.lossyScale.y - portal.target.transform.lossyScale.y) > GlobalScaleTolerance ||
                Mathf.Abs(portal.transform.lossyScale.z - portal.target.transform.lossyScale.z) > GlobalScaleTolerance
                )
            {
                buffer += $"{portal.gameObject.name} and {portal.target.gameObject.name} has differing global scales.\n";
            }
        }

        if (buffer == "")
            buffer = "No errors found!";

        EditorUtility.DisplayDialog("Portal Sanity Checker", buffer, "OK", "");
    }
}