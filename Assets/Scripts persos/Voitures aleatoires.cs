using System.Collections;
using UnityEngine;

public class LecteurAudioAleatoire : MonoBehaviour
{
    // ... (gardez toutes vos variables d'avant : clipsAudio, delaiMinimum, etc.)
    [Header("Clips Audio")]
    public AudioClip[] clipsAudio;

    [Header("Réglages du Délai")]
    public float delaiMinimum = 5.0f;
    public float delaiMaximum = 15.0f;

    private AudioSource audioSource;
    private int dernierIndexJoue = -1;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        // On s'assure que "Play On Awake" est bien désactivé par sécurité
        audioSource.playOnAwake = false;

        StartCoroutine(BoucleDeLecture());
    }

    IEnumerator BoucleDeLecture()
    {
        // Boucle infinie
        while (true)
        {
            // === MODIFICATION ===
            // 1. ON ATTEND D'ABORD
            float delai = Random.Range(delaiMinimum, delaiMaximum);
            Debug.Log($"Prochain son dans {delai:F1} secondes.");
            yield return new WaitForSeconds(delai);

            // Vérification de sécurité
            if (clipsAudio.Length == 0)
            {
                Debug.LogWarning("Aucun clip audio n'est assigné au script !");
                yield break;
            }

            // 2. ENSUITE ON JOUE LE SON
            int indexAleatoire;
            do
            {
                indexAleatoire = Random.Range(0, clipsAudio.Length);
            }
            while (clipsAudio.Length > 1 && indexAleatoire == dernierIndexJoue);

            dernierIndexJoue = indexAleatoire;

            AudioClip clipAJouer = clipsAudio[indexAleatoire];

            Debug.Log("Lecture de : " + clipAJouer.name);
            audioSource.clip = clipAJouer;
            audioSource.Play();

            // 3. On attend la fin du son avant de recommencer la boucle
            yield return new WaitForSeconds(clipAJouer.length);
        }
    }
}