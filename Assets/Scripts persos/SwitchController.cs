using UnityEngine;

/// <summary>
/// Contrôle un interrupteur mural pour allumer/éteindre une lumière
/// avec une animation de rotation douce.
/// </summary>
public class SwitchController : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("La lumière à contrôler (glissez l'objet Light ici).")]
    public Light targetLight;

    [Tooltip("La partie mobile de l'interrupteur qui doit tourner.")]
    public Transform switchLever;

    [Header("Paramètres d'Animation")]
    [Tooltip("La rotation du levier quand la lumière est allumée.")]
    public Vector3 onRotation = new Vector3(-30, 0, 0);

    [Tooltip("La rotation du levier quand la lumière est éteinte.")]
    public Vector3 offRotation = new Vector3(30, 0, 0);

    [Tooltip("Vitesse de l'animation de rotation.")]
    [Range(1f, 20f)]
    public float animationSpeed = 10f;

    [Header("Audio (Optionnel)")]
    [Tooltip("Le son à jouer lors de l'activation/désactivation.")]
    public AudioClip switchSound;
    private AudioSource audioSource;

    // État interne pour savoir si la lumière est allumée
    private bool isLightOn = false;

    void Start()
    {
        // S'assurer que la lumière et l'interrupteur sont dans l'état initial (éteint)
        if (targetLight != null)
        {
            targetLight.enabled = isLightOn;
        }

        if (switchLever != null)
        {
            // Positionne le levier instantanément au démarrage
            switchLever.localEulerAngles = offRotation;
        }

        // Prépare le composant AudioSource pour jouer le son
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Update()
    {
        // Si le levier de l'interrupteur est défini, on l'anime en continu
        if (switchLever != null)
        {
            // Détermine la rotation cible en fonction de l'état de la lumière
            Vector3 targetRot = isLightOn ? onRotation : offRotation;

            // Fait une rotation douce (interpolation) du levier vers sa position cible
            switchLever.localRotation = Quaternion.Slerp(
                switchLever.localRotation,
                Quaternion.Euler(targetRot),
                Time.deltaTime * animationSpeed
            );
        }
    }

    /// <summary>
    /// Méthode publique à appeler pour activer/désactiver l'interrupteur.
    /// </summary>
    public void Interact()
    {
        // Inverse l'état actuel (si c'est allumé, ça éteint, et vice-versa)
        isLightOn = !isLightOn;

        // Met à jour l'état de la lumière
        if (targetLight != null)
        {
            targetLight.enabled = isLightOn;
        }

        // Joue le son de l'interrupteur s'il est défini
        if (switchSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(switchSound);
        }

        Debug.Log("L'interrupteur a été actionné. Lumière : " + (isLightOn ? "ON" : "OFF"));
    }
}