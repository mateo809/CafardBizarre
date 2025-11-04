using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance;

    public int dayNumber;

    public float durationOfDay;

    public float maxDurationOfDay;

    public bool isInGameState;

    public int weekNumber;

    public int groupCurrentMonney;

    public int quota;

    public int totalDebtRemaining;

    public List<RoachController> allPlayers;

    private ItemSpawner _itemSpawner;

    



    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _itemSpawner = GetComponent<ItemSpawner>();
        StartNewWeek();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateState();
    }


    void UpdateState()
    {
        if (isInGameState)
        {
            durationOfDay += Time.deltaTime;
            if (IsDayFinished())
            {
                FinishDay();
            }
        }
        else
        {
            if (AllInventoryAreEmpty())
            {
                EndSellingState();
            }
        }
    }

    void StartNewDay()
    {

        _itemSpawner.SpawnItems();
        dayNumber += 1;
        isInGameState = true;
        durationOfDay = 0;
    }

    void FinishDay()
    {
        isInGameState = false;
    }

    bool IsDayFinished()
    {
        if (durationOfDay >= maxDurationOfDay)
        {
            return true;
        }
        return false;
    }
    bool AllInventoryAreEmpty()
    {
        return false;
    }


    void EndSellingState()
    {
        //Animation d'une ou deux secondes / compte a rebours

        if (dayNumber == 3)
        {
            FinishWeek();
        }

    }

    void StartNewWeek()
    {
        weekNumber += 1;
        dayNumber = 0;
        double multiplier = 0.1 * weekNumber;
        quota = (int) (totalDebtRemaining * multiplier);
        StartNewDay();
    }
    void FinishWeek()
    {

        if (HasQuota())
        {
            totalDebtRemaining -= groupCurrentMonney;
            groupCurrentMonney = 0;
        } else {
            Loose();
            return;
        }

        if (totalDebtRemaining <= 0)
        {
            Win();
            return;
        }
    }

    bool HasQuota()
    {
        if (groupCurrentMonney >= quota)
        {
            return true;
        }
        return false;
    }

    void Win()
    {

    }

    void Loose()
    {

    }


}
