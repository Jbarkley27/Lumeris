using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [Header("General")]
    public static ResourceManager Instance;

    public enum ResourceType { REPUTATION, RANK, CREDITS };


    [Header("Player Resources")]
    public int Reputation;
    public int Rank;
    public int Credits;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetResources();
    }

    public void ResetResources()
    {
        Reputation = 0;
        Rank = 0;
        Credits = 0;
    }

    public void AddResource(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.REPUTATION:
                Reputation += amount;
                break;
            case ResourceType.RANK:
                Rank += amount;
                break;
            case ResourceType.CREDITS:
                Credits += amount;
                break;
        }

        ResourceUIManager.Instance.UpdateResourceUI(type, Reputation);
    }

    
}