using UnityEngine;
using System.Linq;
using Oculus.Interaction;

[RequireComponent(typeof(Rigidbody))]
public class PorteSysteme : MonoBehaviour
{
    public enum EtatFerme { AngleMinimum, AngleMaximum }
    public enum AxeRotation { Y, Z }

    [Header("Configuration")]
    public AxeRotation axeRotation = AxeRotation.Y;
    public bool inverserSens = false;
    public float minAngle = 0f;
    public float maxAngle = 90f;
    public bool porteVerrouillee = true;

    [Header("Logique d'Ouverture")]
    public EtatFerme etatFerme = EtatFerme.AngleMaximum;

    [Header("Physique & Sensation")]
    public float inertiaDamping = 2f;
    public float throwMultiplier = 1.5f;

    [Header("Audio")]
    public AudioSource audioSourceSerrure;
    public AudioClip sonFermeturePorte;
    public AudioClip sonOuverturePorte;
    public float delaiSonOuverture = 0.2f;

    [Header("Audio Dynamique Fermeture")]
    public float minVelocityForLoudSound = 50f;
    public float maxVelocityForLoudSound = 500f;
    [Range(0f, 1f)]
    public float minCloseVolume = 0.3f;
    [Range(0f, 1f)]
    public float maxCloseVolume = 1.0f;

    [Header("Gravité (pour portes horizontales)")]
    public bool utiliserGravite = false;
    public float vitesseGravite = 50f;

    [Header("Aimant de Fermeture")]
    public bool utiliserAimant = false;
    [Range(0.01f, 1f)]
    public float pourcentageAimantation = 0.15f;
    public float vitesseAimantation = 150f;
    public float distanceResistance = 0.05f;

    [Header("Références")]
    public GrabInteractable poigneeInteractable;
    public PoigneeRotation poigneeScript;

    // --- Variables privées ---
    private Rigidbody rb;
    private Transform hand;
    private Vector3 previousHandPos;
    private float inertialVelocity = 0f;
    private bool wasDoorClosed = true;
    private bool wasControllingLastFrame = false;
    private bool wasGrabbingLastFrame = false;
    private Vector3 grabStartPosition;
    private float lastKnownVelocity = 0f;
    private float dernierTempsSonOuverture = -1f;
    private float currentAngle; // NOUVEAU : Notre propre variable pour suivre l'angle

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        // On lit l'angle initial UNE SEULE FOIS au démarrage
        currentAngle = GetInitialAngle();
    }

    void Update()
    {
        if (poigneeInteractable == null) return;

        bool isHandleGrabbed = poigneeInteractable.SelectingInteractors.Any();
        float angleFerme = (etatFerme == EtatFerme.AngleMaximum) ? maxAngle : minAngle;
        // On n'utilise plus GetCurrentLocalAngle(), on utilise notre variable interne 'currentAngle'
        bool isDoorClosed = Mathf.Abs(currentAngle - angleFerme) < 1.5f;

        bool magnetIsBroken = true;
        if (utiliserAimant && isDoorClosed)
        {
            if (isHandleGrabbed)
            {
                hand = poigneeInteractable.SelectingInteractors.FirstOrDefault()?.transform;
                if (hand != null)
                {
                    if (!wasGrabbingLastFrame) grabStartPosition = hand.position;
                    if (Vector3.Distance(hand.position, grabStartPosition) <= distanceResistance) magnetIsBroken = false;
                }
                else { magnetIsBroken = false; }
            }
            else { magnetIsBroken = false; }
        }

        bool canBeControlledByPlayer;
        if (poigneeScript != null)
        {
            bool isHandleDown = poigneeScript.IsPoigneeAbaissee();
            canBeControlledByPlayer = !porteVerrouillee && isHandleGrabbed && (isHandleDown || !isDoorClosed) && magnetIsBroken;
        }
        else
        {
            canBeControlledByPlayer = !porteVerrouillee && isHandleGrabbed && magnetIsBroken;
        }

        if (canBeControlledByPlayer)
        {
            hand = poigneeInteractable.SelectingInteractors.FirstOrDefault()?.transform;
            if (hand == null) return;

            if (!wasControllingLastFrame)
            {
                previousHandPos = hand.position;
                inertialVelocity = 0f;
            }

            Vector3 handMovement = hand.position - previousHandPos;
            Vector3 lever = previousHandPos - transform.position;
            float radius = lever.magnitude;

            if (radius > 0.01f)
            {
                Vector3 axis = (axeRotation == AxeRotation.Y) ? transform.up : transform.forward;
                Vector3 arcDirection = Vector3.Cross(axis, lever).normalized;
                float projectedDistance = Vector3.Dot(handMovement, arcDirection);

                float deltaAngle = (projectedDistance / radius) * Mathf.Rad2Deg;
                if (inverserSens) deltaAngle = -deltaAngle;

                float newAngle = Mathf.Clamp(currentAngle + deltaAngle, minAngle, maxAngle);
                ApplyRotation(newAngle);

                if (Time.deltaTime > 0)
                    inertialVelocity = (deltaAngle / Time.deltaTime) * throwMultiplier;

                lastKnownVelocity = inertialVelocity;
            }

            previousHandPos = hand.position;
        }
        else
        {
            if (wasControllingLastFrame)
            {
                inertialVelocity = lastKnownVelocity;
            }

            if ((currentAngle <= minAngle && inertialVelocity < 0) || (currentAngle >= maxAngle && inertialVelocity > 0))
            {
                inertialVelocity = 0;
            }

            if (utiliserGravite && !isDoorClosed && currentAngle > minAngle)
            {
                float directionGravite = Mathf.Sign(minAngle - currentAngle);
                inertialVelocity += directionGravite * vitesseGravite * Time.deltaTime;
            }

            float plageAngulaire = Mathf.Abs(maxAngle - minAngle);
            float distanceDeFermeture = Mathf.Abs(currentAngle - angleFerme);
            bool inSnappingZone = utiliserAimant && !isDoorClosed && distanceDeFermeture < plageAngulaire * pourcentageAimantation;

            if (inSnappingZone)
            {
                float targetVelocity = Mathf.Sign(angleFerme - currentAngle) * vitesseAimantation;
                inertialVelocity = Mathf.Lerp(inertialVelocity, targetVelocity, Time.deltaTime * 10f);
            }
            else
            {
                inertialVelocity = Mathf.Lerp(inertialVelocity, 0f, Time.deltaTime * inertiaDamping);
            }

            if (Mathf.Abs(inertialVelocity) > 0.01f)
            {
                float angleDelta = inertialVelocity * Time.deltaTime;
                float newAngle = Mathf.Clamp(currentAngle + angleDelta, minAngle, maxAngle);
                ApplyRotation(newAngle);
            }
        }

        // --- GESTION DES SONS ---
        if (!isDoorClosed && wasDoorClosed)
        {
            if (audioSourceSerrure != null && sonOuverturePorte != null)
            {
                audioSourceSerrure.PlayOneShot(sonOuverturePorte);
            }
        }

        if (isDoorClosed && !wasDoorClosed)
        {
            if (audioSourceSerrure != null && sonFermeturePorte != null)
            {
                float velocityForSound = wasControllingLastFrame ? lastKnownVelocity : inertialVelocity;

                float velocityMagnitude = Mathf.Abs(velocityForSound);
                float volumeFactor = Mathf.InverseLerp(minVelocityForLoudSound, maxVelocityForLoudSound, velocityMagnitude);
                float finalVolume = Mathf.Lerp(minCloseVolume, maxCloseVolume, volumeFactor);
                finalVolume = Mathf.Clamp01(finalVolume);

                audioSourceSerrure.clip = sonFermeturePorte;
                audioSourceSerrure.volume = finalVolume;
                audioSourceSerrure.Play();
            }
        }

        wasDoorClosed = isDoorClosed;
        wasControllingLastFrame = canBeControlledByPlayer;
        wasGrabbingLastFrame = isHandleGrabbed;
    }

    private void ApplyRotation(float angle)
    {
        Quaternion targetRotation = (axeRotation == AxeRotation.Y) ? Quaternion.Euler(0, angle, 0) : Quaternion.Euler(0, 0, angle);
        transform.localRotation = targetRotation;
        // On met à jour notre angle interne en même temps que la rotation visuelle
        currentAngle = angle;
    }

    private float GetInitialAngle()
    {
        Vector3 angles = transform.localEulerAngles;
        float angle = (axeRotation == AxeRotation.Y) ? angles.y : angles.z;
        return (angle > 180f) ? angle - 360f : angle;
    }
}