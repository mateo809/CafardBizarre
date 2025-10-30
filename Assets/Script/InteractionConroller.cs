using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionConroller : MonoBehaviour
{

    [SerializeField] Camera _camera;

    [SerializeField] TextMeshProUGUI _interactionText;

    [SerializeField] float _interactionDistance = 5f;

    ItemToPickup _itemToPickup;

    private PlayerInventory _playerInventory;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _playerInventory = GetComponent<PlayerInventory>();
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

        // Dessine le raycast en jaune dans la scène
        Debug.DrawRay(ray.origin, ray.direction * _interactionDistance, Color.yellow, 10f);

        if (Physics.Raycast(transform.position, transform.forward, out var hit, _interactionDistance))
        {
            _itemToPickup = hit.collider?.GetComponent<ItemToPickup>();
        }
        else
        {
            _itemToPickup = null;
        }
    }

    void UpdateInteractionText()
    {
        if (_itemToPickup == null)
        {
            _interactionText.enabled = false;
            return;
        }

        _interactionText.enabled = true;
        _interactionText.text = _itemToPickup._interactMessage;
    }

    void CheckForInteractionInput()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame && _itemToPickup != null)
        {
            ItemData dataToStore = new ItemData();
            dataToStore = _itemToPickup._itemData;
            if (_playerInventory.PutInInventory(dataToStore))
            {
                _playerInventory.UpdateInventoryRender();
                Destroy(_itemToPickup.gameObject);
            } else
            {
                Debug.LogWarning("Inventory is full");
            }
            

            
        }
    }

    public void DoAll(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        UpdateCurrentInteractable();
        UpdateInteractionText();
        CheckForInteractionInput();
    }
}
