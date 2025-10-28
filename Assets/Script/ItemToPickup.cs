using UnityEngine;

public class ItemToPickup : MonoBehaviour
{
    
    public string _interactMessage;


    public float weight;

    private void Start()
    {
        Debug.Log(_interactMessage);
    }

    public void PickUpItem()
    {
        Debug.LogWarning("CACA");
    }
     
}