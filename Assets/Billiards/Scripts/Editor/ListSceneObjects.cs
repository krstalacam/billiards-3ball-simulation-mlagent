using UnityEditor;
using UnityEngine;
using System.Text;

public class ListSceneObjects
{
    [MenuItem("Tools/List All Scene Objects")]
    static void ListObjects()
    {
        // Yeni Unity API:
        // includeInactive → UNITY bu parametreyi FindObjectsByType içine taşımadı
        // Bu yüzden filter = FindObjectsInactive → hepsini alır
        var allObjects = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("---- Scene Objects ----");

        foreach (var obj in allObjects)
        {
            sb.AppendLine(obj.name);
        }

        Debug.Log(sb.ToString());
    }
}
