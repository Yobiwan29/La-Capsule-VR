using UnityEngine;
using Oculus.Interaction;
using System.Linq;

public class TiroirSuiviMain : MonoBehaviour
{
    [Header("Réglages tiroir")]
    public Transform poignee;
    public float minZ = -0.4f;      // Tiroir complètement ouvert
    public float maxZ = 0f;         // Tiroir fermé
    public float mouvementMultiplicateur = 1.5f;
    public float inertieDamping = 4f;

    [Header("Audio glissement")]
    public float vitesseSeuil = 0.005f;
    public float volumeMax = 1f;
    public float fadeInTime = 0.15f;
    public float tempsToléranceSon = 0.1f; // ✅ Nouveau : délai avant coupure
    public AudioClip glissementClip;

    private float tempsDepuisVitesseFaible = 0f;

    [Header("Audio clacs")]
    public AudioClip clacFermetureClip;   // Son du "clac" à la fermeture
    public AudioClip clacOuvertureClip;   // Son du "clac" à l'ouverture
    public float clacSeuilVitesse = 0.02f;
    public float clacVolume = 1f;

    private GrabInteractable grabInteractable;
    private Transform grabberHand;

    private AudioSource glissAudioSource;
    private AudioSource clacAudioSource;

    private bool isGrabbing = false;
    private Vector3 mainInitiale;
    private float tiroirZInitial;
    private float previousZ;
    private float vitesseTiroir = 0f;

    private float fadeTimer = 0f;
    private bool isFadingIn = false;
    private bool clacFermetureJoue = false;
    private bool clacOuvertureJoue = false;

    void Start()
    {
        if (poignee != null)
            grabInteractable = poignee.GetComponent<GrabInteractable>();

        // --- Audio glissement ---
        glissAudioSource = gameObject.AddComponent<AudioSource>();
        glissAudioSource.playOnAwake = false;
        glissAudioSource.loop = true;
        glissAudioSource.clip = glissementClip;
        glissAudioSource.volume = 0f;
        glissAudioSource.spatialBlend = 1f;

        // --- Audio clac ---
        clacAudioSource = gameObject.AddComponent<AudioSource>();
        clacAudioSource.playOnAwake = false;
        clacAudioSource.loop = false;
        clacAudioSource.volume = clacVolume;
        clacAudioSource.spatialBlend = 1f;
    }

    void Update()
    {
        if (grabInteractable == null || glissementClip == null) return;

        var interactors = grabInteractable.Interactors.ToList();
        bool grabEnCours = interactors.Any(i => i.SelectedInteractable == grabInteractable);

        if (grabEnCours)
        {
            if (!isGrabbing)
            {
                isGrabbing = true;
                grabberHand = interactors.First().transform;
                mainInitiale = grabberHand.position;
                tiroirZInitial = transform.localPosition.z;
                previousZ = tiroirZInitial;
                vitesseTiroir = 0f;
                clacFermetureJoue = false;
                clacOuvertureJoue = false;
                return;
            }

            Vector3 mainActuelle = grabberHand.position;
            float deltaZMain = mainActuelle.z - mainInitiale.z;
            float nouveauZ = tiroirZInitial - deltaZMain * mouvementMultiplicateur;
            float clampedZ = Mathf.Clamp(nouveauZ, minZ, maxZ);

            // Réinitialiser les clacs si on quitte les butées
            if (clampedZ < maxZ - 0.001f) clacFermetureJoue = false;
            if (clampedZ > minZ + 0.001f) clacOuvertureJoue = false;

            vitesseTiroir = (clampedZ - previousZ) / Time.deltaTime;
            previousZ = clampedZ;

            transform.localPosition = new Vector3(
                transform.localPosition.x,
                transform.localPosition.y,
                clampedZ
            );

            // ✅ CLAC fermeture en grab
            if (!clacFermetureJoue && clampedZ == maxZ && Mathf.Abs(vitesseTiroir) > clacSeuilVitesse && clacFermetureClip != null)
            {
                clacAudioSource.PlayOneShot(clacFermetureClip);
                clacFermetureJoue = true;
            }

            // ✅ CLAC ouverture en grab
            if (!clacOuvertureJoue && clampedZ == minZ && Mathf.Abs(vitesseTiroir) > clacSeuilVitesse && clacOuvertureClip != null)
            {
                clacAudioSource.PlayOneShot(clacOuvertureClip);
                clacOuvertureJoue = true;
            }
        }
        else
        {
            if (isGrabbing)
            {
                isGrabbing = false;
                grabberHand = null;
            }

            if (Mathf.Abs(vitesseTiroir) > 0.0001f)
            {
                float nextZ = transform.localPosition.z + vitesseTiroir * Time.deltaTime;
                nextZ = Mathf.Clamp(nextZ, minZ, maxZ);

                if (nextZ == minZ || nextZ == maxZ)
                {
                    if (!clacFermetureJoue && nextZ == maxZ && Mathf.Abs(vitesseTiroir) > clacSeuilVitesse && clacFermetureClip != null)
                    {
                        clacAudioSource.PlayOneShot(clacFermetureClip);
                        clacFermetureJoue = true;
                    }

                    if (!clacOuvertureJoue && nextZ == minZ && Mathf.Abs(vitesseTiroir) > clacSeuilVitesse && clacOuvertureClip != null)
                    {
                        clacAudioSource.PlayOneShot(clacOuvertureClip);
                        clacOuvertureJoue = true;
                    }

                    vitesseTiroir = 0f;
                }
                else
                {
                    vitesseTiroir = Mathf.Lerp(vitesseTiroir, 0f, Time.deltaTime * inertieDamping);
                }

                transform.localPosition = new Vector3(
                    transform.localPosition.x,
                    transform.localPosition.y,
                    nextZ
                );
            }
        }

        // --- AUDIO GLISSEMENT ---
        float vitesseAbs = Mathf.Abs(vitesseTiroir);

        if (vitesseAbs > vitesseSeuil)
        {
            tempsDepuisVitesseFaible = 0f; // Reset timer dès qu'on bouge assez vite

            if (!glissAudioSource.isPlaying)
            {
                glissAudioSource.volume = 0f;
                glissAudioSource.Play();
                fadeTimer = 0f;
                isFadingIn = true;
            }

            if (isFadingIn)
            {
                fadeTimer += Time.deltaTime;
                float fadeProgress = Mathf.Clamp01(fadeTimer / fadeInTime);
                glissAudioSource.volume = fadeProgress * Mathf.Clamp01(vitesseAbs / 0.2f) * volumeMax;

                if (fadeProgress >= 1f)
                    isFadingIn = false;
            }
            else
            {
                glissAudioSource.volume = Mathf.Clamp01(vitesseAbs / 0.2f) * volumeMax;
            }
        }
        else
        {
            // ✅ Attendre avant de couper le son (tolérance pour inversion rapide)
            tempsDepuisVitesseFaible += Time.deltaTime;
            if (tempsDepuisVitesseFaible >= tempsToléranceSon && glissAudioSource.isPlaying)
            {
                glissAudioSource.Stop();
                isFadingIn = false;
            }
        }
    }
}
