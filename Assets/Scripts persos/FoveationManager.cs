using UnityEngine;

public class FoveationManager : MonoBehaviour
{
    // ## VARIABLE PUBLIQUE ##
    // Cette variable apparaîtra comme un menu déroulant dans l'inspecteur Unity.
    // Nous lui donnons une valeur par défaut de "High".
    [Tooltip("Définit le niveau de rendu fovéal. High est un bon point de départ.")]
    public OVRManager.FoveatedRenderingLevel foveationLevel = OVRManager.FoveatedRenderingLevel.High;

    [Tooltip("Permet au système d'ajuster dynamiquement le niveau de fovéation en fonction de la charge GPU.")]
    public bool useDynamicFoveation = true;

    void Start()
    {
        // On applique le niveau de fovéation choisi dans l'inspecteur.
        OVRManager.foveatedRenderingLevel = foveationLevel;

        // On applique le choix pour la fovéation dynamique.
        OVRManager.useDynamicFoveatedRendering = useDynamicFoveation;

        Debug.Log("Meta Quest Foveated Rendering activé au niveau : " + foveationLevel);
        Debug.Log("Fovéation dynamique : " + (useDynamicFoveation ? "Activée" : "Désactivée"));
    }
}