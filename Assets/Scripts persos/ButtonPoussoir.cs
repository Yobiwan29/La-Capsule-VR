using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ButtonPoussoir : MonoBehaviour
{
    private Vector3 initialLocalPosition;
    private bool isPressed = false;
    private bool isInCooldown = false;

    [Header("Paramètres d'animation")]
    [SerializeField] private float pressDepth = 0.02f;
    [SerializeField] private float returnSpeed = 7f;
    [SerializeField] private float cooldownDelay = 0.5f;

    private AudioSource audioSource;

    private void Start()
    {
        initialLocalPosition = transform.localPosition;

        // Récupère l'AudioSource attaché
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        string name = other.name.ToLower();

        if (!isPressed && !isInCooldown && (name.Contains("hand") || name.Contains("controller")))
        {
            isPressed = true;
            isInCooldown = true;

            StopAllCoroutines();
            transform.localPosition = initialLocalPosition - transform.right * pressDepth;

            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Play();
            }

            Invoke(nameof(ReleaseButton), 0.1f);
            Invoke(nameof(ResetCooldown), cooldownDelay);
        }
    }

    private void ReleaseButton()
    {
        isPressed = false;
        StartCoroutine(ReturnToInitial());
    }

    private void ResetCooldown()
    {
        isInCooldown = false;
    }

    private System.Collections.IEnumerator ReturnToInitial()
    {
        while (Vector3.Distance(transform.localPosition, initialLocalPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialLocalPosition, Time.deltaTime * returnSpeed);
            yield return null;
        }

        transform.localPosition = initialLocalPosition;
    }
}
