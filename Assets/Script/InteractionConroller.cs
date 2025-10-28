using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionConroller : MonoBehaviour
{

    [SerializeField] Camera _camera;

    [SerializeField] TextMeshProUGUI _interactionText;

    [SerializeField] float _interactionDistance = 5f;

    ItemToPickup _itemToPickup;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCurrentInteractable();

        UpdateInteractionText();

        CheckForInteractionInput();
    }

    void UpdateCurrentInteractable()
    {
        var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));

        Physics.Raycast(ray, out var hit, _interactionDistance);

        _itemToPickup = hit.collider?.GetComponent<ItemToPickup>();
    }

    void UpdateInteractionText()
    {
        if (_itemToPickup == null)
        {
            _interactionText.text = string.Empty;
        }

        _interactionText.text = _itemToPickup._interactMessage;
    }

    void CheckForInteractionInput()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && _itemToPickup != null)
        {
            _itemToPickup.PickUpItem();
        }
    }

    public void DoAll()
    {
        UpdateCurrentInteractable();

        UpdateInteractionText();

        CheckForInteractionInput();
    }
}
