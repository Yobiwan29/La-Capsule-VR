using UnityEngine;

public class Serrure : MonoBehaviour
{
    // Fais glisser ici la clé correspondante dans l'inspecteur
    public ClefInteraction clefCorrespondante;

    // Variable pour s'assurer que la serrure n'est utilisée qu'une seule fois
    private bool estDeverrouillee = false;

    // Cette fonction est appelée automatiquement par Unity quand un autre collider entre dans le trigger
    private void OnTriggerEnter(Collider other)
    {
        // Si déjà déverrouillée, ou si l'objet entrant n'est pas la bonne clé, on ne fait rien
        if (estDeverrouillee || clefCorrespondante == null) return;

        // On vérifie si l'objet qui est entré est bien la clé que l'on attend
        ClefInteraction clefEntrante = other.GetComponent<ClefInteraction>();
        if (clefEntrante == clefCorrespondante)
        {
            Debug.Log("<color=cyan>[Serrure]</color> La bonne clé est entrée dans le trigger ! Lancement de la séquence.");
            estDeverrouillee = true; // On verrouille la serrure pour éviter les doubles appels

            // On demande à la clé de commencer sa séquence d'insertion
            clefCorrespondante.LancerSequenceInsertion(transform);
        }
    }
}