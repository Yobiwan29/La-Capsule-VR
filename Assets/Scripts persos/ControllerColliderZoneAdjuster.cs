using System.Collections.Generic;
using UnityEngine;

public class ControllerColliderZoneAdjuster : MonoBehaviour
{
    [Header("Références aux colliders des mains")]
    public SphereCollider leftHandCollider;
    public SphereCollider rightHandCollider;

    [Header("Rayons")]
    [Tooltip("Rayon appliqué quand au moins UNE main est dans la zone")]
    public float smallRadius = 0.002f;
    [Tooltip("Rayon appliqué quand AUCUNE main n'est dans la zone")]
    public float normalRadius = 0.04f;

    [Header("Porte / verrou")]
    [Tooltip("Script de rotation de la porte (ex: PorteRotation) placé sur le Pivot de la porte")]
    public MonoBehaviour scriptPorteRotation;
    [Tooltip("Désactiver totalement la zone une fois le coffre déverrouillé ?")]
    public bool disableZoneOnUnlock = true;

    [Header("Détection")]
    [Tooltip("Optionnel : layer des colliders de mains (si laissé à -1, on utilise la détection par nom)")]
    public int handsLayer = -1;

    // État interne
    private bool coffreDeverrouille = false;
    private readonly HashSet<Collider> _handsInside = new HashSet<Collider>();
    private Collider _zoneCollider;

    // Sauvegarde des rayons initiaux si tu préfères restaurer exactement
    private float _leftDefaultRadius = -1f;
    private float _rightDefaultRadius = -1f;

    private void Reset()
    {
        _zoneCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        if (_zoneCollider == null) _zoneCollider = GetComponent<Collider>();

        if (leftHandCollider != null) _leftDefaultRadius = leftHandCollider.radius;
        if (rightHandCollider != null) _rightDefaultRadius = rightHandCollider.radius;

        // On part en état "aucune main"
        ApplyNormalRadius();
        SetDoorRotationEnabled(true);
    }

    private void OnEnable()
    {
        // Sécurité : si la zone est réactivée, on repart propre
        _handsInside.Clear();
        ApplyNormalRadius();
        SetDoorRotationEnabled(true);
    }

    private void OnDisable()
    {
        // En cas de désactivation de la zone, on remet tout normal
        _handsInside.Clear();
        ApplyNormalRadius();
        SetDoorRotationEnabled(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHand(other)) return;

        // Ajoute cette main à l'ensemble
        if (_handsInside.Add(other))
        {
            if (_handsInside.Count == 1)
            {
                // La première main vient d'entrer → on réduit les rayons
                ApplySmallRadius();

                // Tant que le coffre est verrouillé, on empêche la rotation
                if (!coffreDeverrouille)
                    SetDoorRotationEnabled(false);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsHand(other)) return;

        if (_handsInside.Remove(other))
        {
            if (_handsInside.Count == 0)
            {
                // Plus aucune main → on remet les rayons normaux
                ApplyNormalRadius();

                // On rend la porte manipulable (ou elle l'était déjà si déverrouillé)
                SetDoorRotationEnabled(true);
            }
        }
    }

    private bool IsHand(Collider other)
    {
        if (handsLayer >= 0 && other.gameObject.layer == handsLayer)
            return true;

        // Fallback par nom si tu ne veux/peux pas gérer les layers
        string n = other.name.ToLower();
        return n.Contains("controller") || n.Contains("hand") || n.Contains("interactor");
    }

    private void ApplySmallRadius()
    {
        if (leftHandCollider != null) leftHandCollider.radius = smallRadius;
        if (rightHandCollider != null) rightHandCollider.radius = smallRadius;
    }

    private void ApplyNormalRadius()
    {
        // Si tu préfères restaurer EXACTEMENT les valeurs d'origine, dé-commente les lignes avec _leftDefaultRadius/_rightDefaultRadius
        if (leftHandCollider != null) leftHandCollider.radius = (_leftDefaultRadius > 0f ? _leftDefaultRadius : normalRadius);
        if (rightHandCollider != null) rightHandCollider.radius = (_rightDefaultRadius > 0f ? _rightDefaultRadius : normalRadius);
    }

    private void SetDoorRotationEnabled(bool enabled)
    {
        if (scriptPorteRotation == null) return;

        // Si le coffre est déverrouillé, on ne re-bloque plus la porte
        if (coffreDeverrouille && !enabled)
            return;

        scriptPorteRotation.enabled = enabled;
    }

    // Appelée par ton ClavierCoffreManager quand le code est correct
    public void DeverrouillerCoffre()
    {
        coffreDeverrouille = true;

        // On remet les rayons à la normale et on autorise la porte
        ApplyNormalRadius();
        SetDoorRotationEnabled(true);

        if (disableZoneOnUnlock && _zoneCollider != null)
            _zoneCollider.enabled = false; // coupe totalement l'impact de la zone après ouverture
    }
}
