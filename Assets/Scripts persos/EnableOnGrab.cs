using System;
using System.Collections; // <-- AJOUTÉ pour les Coroutines
using UnityEngine;
using Oculus.Interaction;

[DisallowMultipleComponent]
[RequireComponent(typeof(GrabInteractable))]
public class EnableOnGrab : MonoBehaviour
{
    [Tooltip("Le script activé pendant le grab (auto-assigné au démarrage). Laisser vide.")]
    [SerializeField] private MonoBehaviour target;

    [Tooltip("Désactiver le script dès qu'on quitte l'état Select.")]
    public bool disableOnRelease = true;

    // NOUVEAU : Variable pour le délai
    [Tooltip("Délai en secondes avant de désactiver le script cible après un relâchement.")]
    public float disableDelay = 3.1f; // Un peu plus que les 3s du WallBlocker par sécurité

    private GrabInteractable grab;

    void OnValidate()
    {
        if (target == null)
            target = FindWallBlockerOnSelf();
    }

    void Reset()
    {
        grab = GetComponent<GrabInteractable>();
        if (target == null)
            target = FindWallBlockerOnSelf();
    }

    void Awake()
    {
        if (grab == null) grab = GetComponent<GrabInteractable>();
        if (target == null) target = FindWallBlockerOnSelf();

        if (Application.isPlaying && target != null)
            target.enabled = false;
    }

    void OnEnable()
    {
        if (grab != null) grab.WhenStateChanged += OnStateChanged;
    }

    void OnDisable()
    {
        if (grab != null) grab.WhenStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(InteractableStateChangeArgs args)
    {
        if (target == null) return;

        // On arrête toute coroutine de désactivation précédente au cas où on attrape à nouveau l'objet rapidement
        StopAllCoroutines();

        if (args.NewState == InteractableState.Select)
        {
            target.enabled = true;
        }
        else if (disableOnRelease &&
                 (args.NewState == InteractableState.Hover || args.NewState == InteractableState.Normal))
        {
            // On lance la coroutine pour désactiver la cible après un délai
            StartCoroutine(DisableTargetAfterDelay());
        }
    }

    // NOUVEAU : Coroutine pour la désactivation retardée
    private IEnumerator DisableTargetAfterDelay()
    {
        // On attend la durée spécifiée
        yield return new WaitForSeconds(disableDelay);

        // Petite sécurité : on vérifie que l'objet n'a pas été attrapé à nouveau pendant le délai
        if (grab != null && grab.Interactors.Count == 0)
        {
            if (target != null) target.enabled = false;
        }
    }

    private MonoBehaviour FindWallBlockerOnSelf()
    {
        var exact = GetComponent<WallBlockerRotationFixed>();
        if (exact != null) return exact;

        MonoBehaviour firstEnabled = null;
        var monos = GetComponents<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m == this || m == null) continue;
            string typeName = m.GetType().Name;
            if (typeName.IndexOf("WallBlocker", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!m.enabled) return m;
                firstEnabled ??= m;
            }
        }
        return firstEnabled;
    }
}