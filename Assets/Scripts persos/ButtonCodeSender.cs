using UnityEngine;

public class ButtonCodeSender : MonoBehaviour
{
    [SerializeField] private string chiffre;
    [SerializeField] private ClavierCoffreManager clavier;
    [SerializeField] private float cooldown = 0.5f;

    private bool canSend = true;

    private void OnTriggerEnter(Collider other)
    {
        string name = other.name.ToLower();

        if (canSend && (name.Contains("hand") || name.Contains("controller")) && clavier != null)
        {
            canSend = false;
            clavier.AjouterChiffre(chiffre);
            Invoke(nameof(ResetCooldown), cooldown);
        }
    }

    private void ResetCooldown()
    {
        canSend = true;
    }
}
