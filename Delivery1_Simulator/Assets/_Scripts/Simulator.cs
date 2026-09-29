using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Simulator : MonoBehaviour
{

    public static Action<string,string,int,float,DateTime> OnNewPlayer; //Name, Country, age, gender and date
    public static Action<DateTime,uint> OnNewSession;
    public static Action<DateTime, uint> OnEndSession;
    public static Action<int, DateTime, uint> OnBuyItem; //Item id and date

    private DateTime _currentDate;

    public int MaxPlayers=100;
    public float ReplayChance => _currentDate.Month < 6 ? 0.7f: 0.95f;
    public float BuyProbability = 0.1f;


    private int _nPlayers;

    private List<string> Countries;

    [SerializeField]
    private Lexic.NameGenerator namegen;

    #region Subscribe
    private void OnEnable()
    {
        CallbackEvents.OnAddPlayerCallback += OnPlayerAdded;
        CallbackEvents.OnNewSessionCallback += OnNewSessionAdded;
        CallbackEvents.OnEndSessionCallback += OnEndSessionAdded;
        CallbackEvents.OnItemBuyCallback += OnItemBought;
    }

    private void OnDisable()
    {
        CallbackEvents.OnAddPlayerCallback -= OnPlayerAdded;
        CallbackEvents.OnNewSessionCallback -= OnNewSessionAdded;
        CallbackEvents.OnEndSessionCallback -= OnEndSessionAdded;
    }

    #endregion

    void Start()
    {
        Countries = new List<string>();
        int nCountries = Random.Range(1, 10);

        string[] coutryNames = System.Enum.GetNames(typeof(AllCountries));
        for (int i = 0; i < nCountries; i++)
        {
            int rdm = Random.Range(0, coutryNames.Length);
            Countries.Add(((AllCountries)rdm).ToString());
        }
        MakeOnePlayer();
       
     
    }

    void MakeOnePlayer()
    {
        _nPlayers++;
        if (_nPlayers > MaxPlayers)
        {
            Debug.Log("Finished");
            return;
        }
            

        _currentDate = GetNewPlayerDate();
        AddNewPlayer(_currentDate);
    }

    
   

    void AddNewPlayer(DateTime dateTime)
    {
        string name = namegen.GetNextRandomName();
        int rdm = Random.Range(0, Countries.Count);
        string country = Countries[rdm];
        float gender = Random.value;
        int age = Random.Range(12, 99);

        OnNewPlayer?.Invoke(name, country,age, gender, dateTime);
    }

    void AddNewSession(uint playerID)
    {
        DateTime dateTime = _currentDate;
        OnNewSession?.Invoke(dateTime, playerID);
    }

    void EndSession(uint sessionId)
    {
        _currentDate = _currentDate.Add(GetSessionLength());
        DateTime dateTime = _currentDate;
        OnEndSession?.Invoke(dateTime, sessionId);
    }

    void TryBuy(uint sessionId)
    {
        _currentDate = _currentDate.Add(GetSessionLength());
        if (UserBuys())
            OnBuyItem?.Invoke(GetItem(),_currentDate, sessionId);
        else
            EndSession(sessionId);
    }

    private bool UserBuys()
    {
        return Random.value < BuyProbability;
    }

    private int GetItem()
    {
        float rdm = Random.value;
        if (rdm < 0.5f)
            return 1;
        if (rdm < 0.75f)
            return 2;
        if (rdm < 0.9f)
            return 3;
        if (rdm < 0.91f)
            return 4;

        return 5;

    }



    #region Probabilistic values
    DateTime GetNewPlayerDate()
    {
        int year = 2022;
        int month = Random.Range(1, 13);
        int numOfDays = DateTime.DaysInMonth(year, month);
        int day = Random.Range(1, numOfDays + 1);

        int hour = Random.Range(0, 24);
        int minut = Random.Range(0, 60);
        int second = Random.Range(0, 60);

        return new DateTime(year, month, day, hour, minut, second);

    }


    private TimeSpan GetSessionLength()
    {
        float lengthInSeconds = Random.Range(30, 500);
        return TimeSpan.FromSeconds(lengthInSeconds);
    }

    private TimeSpan GetBuyTime()
    {
        float lengthInSeconds = Random.Range(10, 120);
        return TimeSpan.FromSeconds(lengthInSeconds);
    }

    private TimeSpan TimeTillNextSession()
    {
        float lengthInSeconds = Random.Range(3000, 30000);
        return TimeSpan.FromSeconds(lengthInSeconds);
    }
    #endregion

    #region callback subscribers
    private void OnPlayerAdded(uint playerId)
    {
        AddNewSession(playerId);
    }
    private void OnNewSessionAdded(uint sessionId)
    {
        TryBuy(sessionId);
    }

    private void OnItemBought(uint sessionId)
    {
        EndSession(sessionId);
    }

    private void OnEndSessionAdded(uint playerId)
    {
        
        if(Random.value > ReplayChance)
        {
            MakeOnePlayer();
            return;
        }
        TimeSpan timeSpan = TimeTillNextSession();
       
        _currentDate = _currentDate.Add(timeSpan);
       // Debug.Log(_currentDate.ToLongDateString());

        if (_currentDate.Year == 2022)
            AddNewSession(playerId);
        else
            MakeOnePlayer();
    }

   
    #endregion
}

public class CallbackEvents
{
    public static Action<uint> OnEndSessionCallback;
    public static Action<uint> OnNewSessionCallback;
    public static Action<uint> OnAddPlayerCallback;
    public static Action<uint> OnItemBuyCallback;
}
