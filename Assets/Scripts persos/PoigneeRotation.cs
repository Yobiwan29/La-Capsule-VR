using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Grab;

public class PoigneeRotation : MonoBehaviour
{
    [Header("Configuration de la Poignée")]
    public float minAngle = -45f;
    public float maxAngle = 0f;
    public float rotationSensitivity = 300f;
    public Vector3 rotationAxis = Vector3.forward;
    public float retourVitesse = 15f;

    [Tooltip("Pourcentage d'abaissement requis (0.8 = 80%) pour que la poignée déverrouille la porte.")]
    [Range(0.1f, 1f)]
    public float pourcentageAbaissementRequis = 0.8f;

    [Header("Animation du Pêne")]
    [Tooltip("Fais glisser ici l'objet 3D du pêne à animer.")]
    public Transform pene;
    [Tooltip("Le déplacement du pêne sur ses axes locaux quand la poignée est abaissée à fond.")]
    public Vector3 deplacementPene = new Vector3(-0.05f, 0, 0);

    [Header("Audio")]
    public AudioSource poigneeSound;
    // ✅ 1. NOUVELLE VARIABLE PUBLIQUE POUR LE SEUIL DU SON
    [Tooltip("Pourcentage d'abaissement à partir duquel le son de la poignée se déclenche (0.5 = 50%).")]
    [Range(0.01f, 1f)]
    public float pourcentageSonRequis = 0.5f;
    private bool aJoueLeSon = false;

    // --- Le reste des variables ne change pas ---
    private Quaternion initialRotation;
    private GrabInteractable grabInteractable;
    private bool isGrabbing = false;
    private Transform grabberHand;
    private Vector3 lastHandPosition;
    public float currentAngle = 0f;
    private Vector3 positionInitialePene;

    public bool IsGrabbed() { return isGrabbing; }

    public bool IsPoigneeAbaissee()
    {
        float plageAngulaire = maxAngle - minAngle;
        float angleSeuil = maxAngle - (plageAngulaire * pourcentageAbaissementRequis);
        return currentAngle <= angleSeuil;
    }

    // --- Awake et OnDestroy ne changent pas ---
    void Awake()
    {
        initialRotation = transform.localRotation;
        if (pene != null)
        {
            positionInitialePene = pene.localPosition;
        }
        grabInteractable = GetComponent<GrabInteractable>();
        if (grabInteractable != null)
        {
            grabInteractable.WhenSelectingInteractorAdded.Action += HandleSelect;
            grabInteractable.WhenSelectingInteractorRemoved.Action += HandleUnselect;
        }
        if (poigneeSound == null) poigneeSound = GetComponent<AudioSource>();
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.WhenSelectingInteractorAdded.Action -= HandleSelect;
            grabInteractable.WhenSelectingInteractorRemoved.Action -= HandleUnselect;
        }
    }

    // --- HandleSelect et HandleUnselect ne changent pas ---
    private void HandleSelect(GrabInteractor interactor)
    {
        isGrabbing = true;
        grabberHand = interactor.transform;
        lastHandPosition = grabberHand.position;
    }

    private void HandleUnselect(GrabInteractor interactor)
    {
        isGrabbing = false;
        grabberHand = null;
    }

    void Update()
    {
        // --- Logique de la poignée et du pêne (ne change pas) ---
        if (isGrabbing && grabberHand != null)
        {
            float deltaY = grabberHand.position.y - lastHandPosition.y;
            float angleDelta = deltaY * rotationSensitivity;
            currentAngle = Mathf.Clamp(currentAngle + angleDelta, minAngle, maxAngle);
            lastHandPosition = grabberHand.position;
        }
        else
        {
            currentAngle = Mathf.Lerp(currentAngle, 0f, retourVitesse * Time.deltaTime);
        }

        transform.localRotation = initialRotation * Quaternion.AngleAxis(currentAngle, rotationAxis);

        if (pene != null)
        {
            float pourcentage = Mathf.InverseLerp(maxAngle, minAngle, currentAngle);
            Vector3 positionRentree = positionInitialePene + deplacementPene;
            pene.localPosition = Vector3.Lerp(positionInitialePene, positionRentree, pourcentage);
        }

        // --- ✅ 2. LOGIQUE DU SON MISE À JOUR ---
        // On calcule le pourcentage de rotation actuel de la poignée (entre 0.0 et 1.0)
        float pourcentageActuel = Mathf.InverseLerp(maxAngle, minAngle, currentAngle);

        // Si on a atteint le seuil requis ET que le son n'a pas encore été joué...
        if (pourcentageActuel >= pourcentageSonRequis && !aJoueLeSon)
        {
            if (poigneeSound != null) poigneeSound.Play();
            aJoueLeSon = true; // On met le verrou
        }
        // Si on repasse en dessous du seuil, on réarme le son
        else if (pourcentageActuel < pourcentageSonRequis && aJoueLeSon)
        {
            aJoueLeSon = false;
        }
    }
}