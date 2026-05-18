using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    public Transform door;
    public float openSpeed = 3f;
    public float closeSpeed = 3f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool playerInRange;

    void Start()
    {
        // Sécurité : vérifier que door est assigné
        if (door == null)
        {
            Debug.LogError("[DoorTrigger] 'door' n'est pas assigné dans l'Inspector !", this);
            enabled = false; // Désactive le script pour éviter le crash
            return;
        }

        // Sécurité : vérifier que le layer "Player" existe
        if (LayerMask.NameToLayer("Player") == -1)
        {
            Debug.LogError("[DoorTrigger] Le layer 'Player' n'existe pas ! Crée-le dans Edit > Project Settings > Tags and Layers", this);
            enabled = false;
            return;
        }

        closedRotation = Quaternion.Euler(0f, 0f, 0f);
        openRotation = Quaternion.Euler(0f, 60f, 0f);
    }

    void Update()
    {
        Quaternion targetRotation = playerInRange ? openRotation : closedRotation;
        float speed = playerInRange ? openSpeed : closeSpeed;

        door.rotation = Quaternion.Slerp(door.rotation, targetRotation, Time.deltaTime * speed);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") || other.gameObject.layer == LayerMask.NameToLayer("Vehicule"))
            playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player") || other.gameObject.layer == LayerMask.NameToLayer("Vehicule"))
            playerInRange = false;
    }
}