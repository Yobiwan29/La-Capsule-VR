using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class AutoTeleportOnStart : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence au TeleportationProvider (sous LocomotionSystem)")]
    [SerializeField] private TeleportationProvider teleportationProvider;

    [Tooltip("Référence au TeleportationAnchor (SpawnHotspot) où le joueur doit apparaître")]
    [SerializeField] private TeleportationAnchor spawnHotspot;

    [Header("Réglages")]
    [Tooltip("Délai (en secondes) avant d'appeler la téléportation, " +
             "pour laisser le temps à l’OpenXR/OVR/XRIT de s'initialiser")]
    [SerializeField] private float delayBeforeTeleport = 0.5f;

    private void Start()
    {
        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        // 1. Attendre un court délai pour s'assurer que tout est initialisé
        yield return new WaitForSeconds(delayBeforeTeleport);

        // 2. Construire la TeleportRequest en copiant la rotation (Up+Forward) du hotspot
        TeleportRequest request = new TeleportRequest
        {
            destinationPosition = spawnHotspot.transform.position,
            destinationRotation = spawnHotspot.transform.rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward,
            requestTime = Time.time
        };

        // 3. Envoyer la requête au TeleportationProvider
        bool success = teleportationProvider.QueueTeleportRequest(request);

        if (success)
        {
            Debug.Log($"[AutoTeleportOnStart] Téléportation vers {spawnHotspot.transform.position} réussie.");

            // 4. Détruire le SpawnHotspot maintenant que la requête est envoyée
            Destroy(spawnHotspot.gameObject);
        }
        else
        {
            Debug.LogWarning("[AutoTeleportOnStart] Échec de mise en file de la téléportation.");
        }
    }
}
