using NUnit.Framework;
using PurrNet;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class ItemSpawner : MonoBehaviour
{

    private  List<GameObject> _allItems = new List<GameObject>();

    [SerializeField] private GameObject _itemSpawnersParent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {

        GetAllItemData();
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void GetAllItemData()
    {
        

        // Charge tous les prefabs du dossier Resources/ItemsToPickup
        GameObject[] loadedItems = Resources.LoadAll<GameObject>("ItemsToPickup");

        foreach (var item in loadedItems)
        {
            _allItems.Add(item);
        }

        Debug.Log($"{_allItems.Count} items chargés depuis Resources/ItemsToPickup");
    }

    public void SpawnItems()
    {
        List<Transform> allSpawners = new List<Transform>();
        for (int i = 0; i < _itemSpawnersParent.transform.childCount; i++)
        {
            allSpawners.Add(_itemSpawnersParent.transform.GetChild(i).transform);
        }




        int totalItemSpawned = 0;

        //nombre et value des items a voir plus tard en fonction du nbSemaine et nbJour

        foreach (Transform spawn in allSpawners) {

            //faire du random et ajouter un system de rareté de spawn aux différents items
            Instantiate(_allItems[0], spawn);
            totalItemSpawned++;


        }

    }

    
}
