using UnityEngine;
using Oculus.Interaction;
using System.Collections;
using System.Linq; // N'oublie pas d'ajouter ce using

public class ClefInteraction : MonoBehaviour
{
    [Header("Références")]
    // ✅ On a seulement besoin de la référence au script de la porte maintenant
    public PorteSysteme scriptPorteSysteme;

    [Header("Réglages")]
    public float rotationDuration = 0.5f;

    // --- Variables privées ---
    private Rigidbody rb;
    private GrabInteractable grabInteractable;
    private AudioSource audioSource;
    private bool sequenceEnCours = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<GrabInteractable>();
        audioSource = GetComponent<AudioSource>();

        if (scriptPorteSysteme != null)
        {
            scriptPorteSysteme.porteVerrouillee = true;
            Debug.Log("<color=orange>[Clé]</color> Porte initialisée comme VERROUILLÉE.");
        }
        else
        {
            Debug.LogError("<color=red>[Clé]</color> ATTENTION: La référence au script de la porte est manquante !");
        }
    }

    // ✅ C'est la nouvelle fonction publique appelée par la Serrure
    public void LancerSequenceInsertion(Transform pointInsertion)
    {
        if (sequenceEnCours) return;
        sequenceEnCours = true;

        Debug.Log("<color=orange>[Clé]</color> Séquence d'insertion lancée.");
        StartCoroutine(SequenceComplete(pointInsertion));
    }

    private IEnumerator SequenceComplete(Transform pointInsertion)
    {
        // --- 1. Forcer la main à lâcher la clé ---
        Debug.Log("<color=orange>[Clé]</color> Étape 1: Forcer le relâchement.");
        if (grabInteractable.SelectingInteractors.Any())
        {
            var interactor = grabInteractable.SelectingInteractors.First();
            interactor.ForceRelease();
        }
        // Petite attente pour que le SDK traite le relâchement
        yield return new WaitForSeconds(0.1f);

        // --- 2. Neutraliser la physique et placer la clé ---
        Debug.Log("<color=orange>[Clé]</color> Étape 2: Neutralisation de la physique et placement.");
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetParent(pointInsertion, true);
        transform.position = pointInsertion.position;
        transform.rotation = pointInsertion.rotation;

        // --- 3. Animation de la clé ---
        Debug.Log("<color=orange>[Clé]</color> Étape 3: Animation de rotation.");
        if (audioSource != null) audioSource.Play();

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, 90, 0);
        float t = 0;
        while (t < rotationDuration)
        {
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t / rotationDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localRotation = endRot;

        // --- 4. Déverrouillage final ---
        Debug.Log("<color=orange>[Clé]</color> Étape 4: Déverrouillage de la porte.");
        if (scriptPorteSysteme != null)
        {
            scriptPorteSysteme.porteVerrouillee = false;
            // ✅ LE TEST CRUCIAL !
            Debug.Log("<color=green>[Clé]</color> DÉVERROUILLAGE EFFECTUÉ. Nouvel état: " + scriptPorteSysteme.porteVerrouillee);
        }
        else
        {
            Debug.LogError("<color=red>[Clé]</color> IMPOSSIBLE DE DÉVERROUILLER: La référence au script de la porte est NULLE !");
        }
        grabInteractable.enabled = false;
    }
}