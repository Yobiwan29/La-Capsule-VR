using UnityEngine;
using TMPro;
using System.Collections;

public class ClavierCoffreManager : MonoBehaviour
{
    [Header("Code correct à entrer")]
    public string codeCorrect = "1234";

    [Header("Objet à activer une fois le code valide")]
    public GameObject coffreASOuvrir;

    [Header("Affichage du code saisi")]
    public TextMeshProUGUI affichageCode;

    [Header("Effet sonore")]
    public AudioSource audioValidation;

    private string codeActuel = "";

    public void AjouterChiffre(string chiffre)
    {
        if (codeActuel.Length >= codeCorrect.Length)
            return;

        codeActuel += chiffre;
        Debug.Log($"Code actuel : '{codeActuel}'");

        if (affichageCode != null)
        {
            affichageCode.text = codeActuel;
        }

        if (codeActuel.Length == codeCorrect.Length)
        {
            Debug.Log($"Comparaison : '{codeActuel}' == '{codeCorrect}'");

            if (codeActuel == codeCorrect)
            {
                Debug.Log("✅ Code correct !");
                StartCoroutine(JouerSonEtOuvrir());
            }
            else
            {
                Debug.Log("❌ Code incorrect.");
                // On ne réinitialise que si le code est faux.
                Invoke(nameof(ResetCode), 1.0f);
            }
        }
    }

    private IEnumerator JouerSonEtOuvrir()
    {
        yield return new WaitForSeconds(0.5f);

        if (audioValidation != null)
        {
            audioValidation.Play();
        }

        OuvrirCoffre();
    }

    private void OuvrirCoffre()
    {
        if (coffreASOuvrir != null)
        {
            var script = coffreASOuvrir.GetComponent<PorteSysteme>();
            if (script != null)
            {
                script.porteVerrouillee = false; // ✅ Déverrouillage direct
                Debug.Log("🚪 Porte déverrouillée via le pavé numérique !");
            }
        }

        // Neutralise définitivement le blocage dans ZoneBoutonsVR
        var zone = FindFirstObjectByType<ControllerColliderZoneAdjuster>();
        if (zone != null)
        {
            zone.DeverrouillerCoffre();
        }
    }


    private void ResetCode()
    {
        codeActuel = "";

        if (affichageCode != null)
        {
            affichageCode.text = "";
        }
    }
}
