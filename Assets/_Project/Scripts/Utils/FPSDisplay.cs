using UnityEngine;
using TMPro;

public class FPSDisplay : MonoBehaviour
{
    public TMP_Text fpsText;

    private float deltaTime = 0.0f;
    public bool ShouldShow = true;

    void Start()
    {
        // #if !DEVELOPMENT_BUILD
        //     gameObject.SetActive(false);
        // #endif
    }

    void Update()
    {
        if (!ShouldShow)
        {
            fpsText.text = "";
            return;
        }
        
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        fpsText.text = Mathf.Ceil(fps).ToString() + " FPS";
    }
}
