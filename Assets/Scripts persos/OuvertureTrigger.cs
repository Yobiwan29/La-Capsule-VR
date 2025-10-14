using UnityEngine;

public class OuvertureTrigger : MonoBehaviour
{
    public CouvercleRotationAutoShut couvercle;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            couvercle.SetPlayerInZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            couvercle.SetPlayerInZone(false);
    }
}
