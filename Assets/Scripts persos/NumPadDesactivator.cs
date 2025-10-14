using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(GrabInteractable))]
public class LockScript : MonoBehaviour
{
    [Header("Référence au clavier")]
    [Tooltip("Glissez ici l'objet parent qui contient tous les boutons du clavier.")]
    public GameObject clavierParent;

    private GrabInteractable grabInteractable;
    private Collider[] collidersDuClavier;

    void Awake()
    {
        grabInteractable = GetComponent<GrabInteractable>();

        if (clavierParent != null)
        {
            collidersDuClavier = clavierParent.GetComponentsInChildren<Collider>();
        }
        else
        {
            Debug.LogError("[FinalLockScript] La référence 'clavierParent' n'est pas assignée !");
        }
    }

    void OnEnable()
    {
        grabInteractable.WhenStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        grabInteractable.WhenStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(InteractableStateChangeArgs args)
    {
        // CORRIGÉ ICI : On compare l'état de l'objet (args.NewState) avec une valeur de même type (InteractableState).
        if (args.NewState == InteractableState.Select)
        {
            DesactiverClavier();
        }
        // CORRIGÉ ICI : Même chose pour l'état précédent.
        else if (args.PreviousState == InteractableState.Select)
        {
            ReactiverClavier();
        }
    }

    private void DesactiverClavier()
    {
        if (collidersDuClavier == null) return;
        foreach (var col in collidersDuClavier)
        {
            col.enabled = false;
        }
        Debug.Log("🔒 Clavier désactivé.");
    }

    private void ReactiverClavier()
    {
        if (collidersDuClavier == null) return;
        foreach (var col in collidersDuClavier)
        {
            col.enabled = true;
        }
        Debug.Log("🔓 Clavier réactivé.");
    }
}