using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionConroller : MonoBehaviour
{

    [SerializeField] Camera _camera;

    [SerializeField] TextMeshProUGUI _interactionText;

    [SerializeField] float _interactionDistance = 10f;

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
       // UpdateCurrentInteractable();

        //UpdateInteractionText("Press 'E' to pick up");


    }

    public void UpdateCurrentInteractable()
    {
        var ray = _camera.ViewportPointToRay(new Vector2(0.5f, 0.5f));

        // Dessine le raycast en jaune dans la scène
        

        if (Physics.Raycast(transform.position, transform.forward, out var hit, _interactionDistance))
        {
            _itemToPickup = hit.collider?.GetComponent<ItemToPickup>();
            if (_itemToPickup != null)
            {

            Debug.LogWarning("item found ");
            }
        }
        else
        {
            _itemToPickup = null;
        }
    }

    void UpdateInteractionText(string text)
    {
        if (_itemToPickup == null)
        {
            _interactionText.enabled = false;
            return;
        }

        _interactionText.enabled = true;
        _interactionText.text = text;
    }


}