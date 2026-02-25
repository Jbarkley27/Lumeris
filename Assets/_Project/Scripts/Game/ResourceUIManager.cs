using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ResourceUIManager : MonoBehaviour
{
    [Header("General")]
    public static ResourceUIManager Instance;

    [Header("UI Elements")]
    public TMP_Text reputationText;
    public Image reputationIcon;
    public TMP_Text rankText;
    public Image rankIcon;
    public TMP_Text creditsText;
    public Image creditsIcon;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void UpdateResourceUI(ResourceManager.ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceManager.ResourceType.REPUTATION:
                reputationText.text = amount.ToString();
                AnimationLibrary.Instance.PunchScale(reputationIcon.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                AnimationLibrary.Instance.PunchScale(reputationText.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                break;
            case ResourceManager.ResourceType.RANK:
                rankText.text = amount.ToString();
                AnimationLibrary.Instance.PunchScale(rankIcon.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                AnimationLibrary.Instance.PunchScale(rankText.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                break;
            case ResourceManager.ResourceType.CREDITS:
                creditsText.text = amount.ToString();
                AnimationLibrary.Instance.PunchScale(creditsIcon.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                AnimationLibrary.Instance.PunchScale(creditsText.transform, .1f, 0.3f, Ease.OutElastic, Vector3.one);
                break;
        }
    }

}