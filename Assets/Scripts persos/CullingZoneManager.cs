using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CullingZoneManager : MonoBehaviour
{
    [Tooltip("Liste des objets à désactiver quand le Player entre dans cette zone")]
    public List<GameObject> objectsToToggle = new List<GameObject>();

    // **static** : partagé par toutes les zones
    private static Dictionary<GameObject, int> disableCounts = new Dictionary<GameObject, int>();

    void Awake()
    {
        // S'assurer que ce collider est un trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[CullingZone] Enter zone «{name}»");
        foreach (var go in objectsToToggle)
            SetDisabled(go, true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[CullingZone] Exit zone «{name}»");
        foreach (var go in objectsToToggle)
            SetDisabled(go, false);
    }

    static void SetDisabled(GameObject go, bool disable)
    {
        disableCounts.TryGetValue(go, out int count);

        if (disable)
        {
            // Entrée de zone
            disableCounts[go] = count + 1;
            if (go.activeSelf)
                go.SetActive(false);
        }
        else if (count > 0)
        {
            // Sortie de zone
            count--;
            if (count == 0)
            {
                // plus aucune zone ne demande la désactivation
                disableCounts.Remove(go);
                go.SetActive(true);
            }
            else
            {
                // reste au moins une zone active
                disableCounts[go] = count;
            }
        }
    }
}
