using System.Collections;
using UnityEngine;
using Oculus.Platform; // (le namespace peut varier selon votre version de l’Integration)

public class RecenterWithOVRPlugin : MonoBehaviour
{
    [Tooltip("Délai avant recentrage")]
    [SerializeField] private float delayBeforeRecenter = 0.5f;

    [Tooltip("Flags de recentrage (Default, Controllers, IgnoreAll...)")]
    [SerializeField] private OVRPlugin.RecenterFlags recenterFlags = OVRPlugin.RecenterFlags.Default;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(delayBeforeRecenter);
        bool success = OVRPlugin.RecenterTrackingOrigin(recenterFlags);
        Debug.Log($"[RecenterWithOVRPlugin] RecenterTrackingOrigin => {success}");
    }
}
