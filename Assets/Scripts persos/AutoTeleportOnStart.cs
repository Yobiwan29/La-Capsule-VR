using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class AutoTeleportOnStart : MonoBehaviour
{
    [Header("R�f�rences")]
    [Tooltip("R�f�rence au TeleportationProvider (sous LocomotionSystem)")]
    [SerializeField] private TeleportationProvider teleportationProvider;

    [Tooltip("R�f�rence au TeleportationAnchor (SpawnHotspot) o� le joueur doit appara�tre")]
    [SerializeField] private TeleportationAnchor spawnHotspot;

    [Header("R�glages")]
    [Tooltip("D�lai (en secondes) avant d'appeler la t�l�portation, " +
             "pour laisser le temps � l�OpenXR/OVR/XRIT de s'initialiser")]
    [SerializeField] private float delayBeforeTeleport = 0.5f;

    private void Start()
    {
        StartCoroutine(TeleportRoutine());
    }

    private IEnumerator TeleportRoutine()
    {
        // 1. Attendre un court d�lai pour s'assurer que tout est initialis�
        yield return new WaitForSeconds(delayBeforeTeleport);

        // 2. Construire la TeleportRequest en copiant la rotation (Up+Forward) du hotspot
        TeleportRequest request = new TeleportRequest
        {
            destinationPosition = spawnHotspot.transform.position,
            destinationRotation = spawnHotspot.transform.rotation,
            matchOrientation = MatchOrientation.TargetUpAndForward,
            requestTime = Time.time
        };

        // 3. Envoyer la requ�te au TeleportationProvider
        bool success = teleportationProvider.QueueTeleportRequest(request);

        if (success)
        {
            Debug.Log($"[AutoTeleportOnStart] T�l�portation vers {spawnHotspot.transform.position} r�ussie.");

            // 4. D�truire le SpawnHotspot maintenant que la requ�te est envoy�e
            Destroy(spawnHotspot.gameObject);
        }
        else
        {
            Debug.LogWarning("[AutoTeleportOnStart] �chec de mise en file de la t�l�portation.");
        }
    }
}
