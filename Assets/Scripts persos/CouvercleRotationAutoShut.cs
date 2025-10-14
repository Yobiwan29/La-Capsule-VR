using UnityEngine;
using Oculus.Interaction;
using System.Linq;

public class CouvercleRotationAutoShut : MonoBehaviour
{
    [Header("Réglages")]
    public float minAngle = 0f;
    public float maxAngle = 80f;
    public float retourSpeed = 150f;
    public Transform poignee;
    public AudioSource audioSource; // ⇦ glisse ici l'audio source contenant ton .wav

    private GrabInteractable grabInteractable;
    private Transform grabberHand;
    private Vector3 previousDirection;
    private float currentAngle = 0f;
    private bool isGrabbing = false;
    private bool hasPlayedCloseSound = false;

    // ➤ Ajout : détection présence joueur
    private bool playerInZone = false;

    void Start()
    {
        if (poignee != null)
            grabInteractable = poignee.GetComponent<GrabInteractable>();

        currentAngle = GetCurrentLocalAngle();
    }

    void Update()
    {
        if (grabInteractable == null) return;

        // ➤ Gestion ouverture auto si joueur dans zone
        if (playerInZone && !isGrabbing)
        {
            hasPlayedCloseSound = false;
            currentAngle = Mathf.MoveTowards(currentAngle, maxAngle, retourSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            return;
        }

        var interactors = grabInteractable.Interactors.ToList();
        var interactor = interactors.FirstOrDefault(i => i.SelectedInteractable == grabInteractable);

        // ➤ Si aucun interactor actif → fermeture automatique
        if (interactor == null)
        {
            isGrabbing = false;
            grabberHand = null;

            float previousAngle = currentAngle;

            currentAngle = Mathf.MoveTowards(currentAngle, minAngle, retourSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);

            // ➤ Joue le son si le couvercle atteint minAngle à cette frame
            if (!hasPlayedCloseSound && Mathf.Approximately(currentAngle, minAngle) && previousAngle > minAngle + 0.1f)
            {
                if (audioSource != null)
                {
                    audioSource.Play();
                    hasPlayedCloseSound = true;
                }
            }

            return;
        }

        // ➤ Grab actif
        if (!isGrabbing)
        {
            isGrabbing = true;
            grabberHand = interactor.transform;
            previousDirection = grabberHand.position - transform.position;
            hasPlayedCloseSound = false; // reset si réouverture
            return;
        }

        Vector3 currentDirection = grabberHand.position - transform.position;
        float deltaAngle = Vector3.SignedAngle(previousDirection, currentDirection, Vector3.right);
        currentAngle = Mathf.Clamp(currentAngle + deltaAngle, minAngle, maxAngle);
        transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        previousDirection = currentDirection;
    }

    private float GetCurrentLocalAngle()
    {
        float angle = transform.localEulerAngles.z;
        return (angle > 180f) ? angle - 360f : angle;
    }

    public void SetPlayerInZone(bool inZone)
    {
        playerInZone = inZone;
    }

    // ➤ Ajout : détection trigger zone
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger détecté avec : " + other.name);
        if (other.CompareTag("Player")) // Assure-toi que ton PlayerController a bien ce tag
        {
            playerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
        }
    }
}
