using System;
using UnityEngine;

/// <summary>
/// Global save feedback event hub.
/// Save systems call RunWithFeedback or RaiseSaveStarted/RaiseSaveFinished.
/// UI systems (like blinking save icon) subscribe here.
/// </summary>
public static class SaveFeedbackEvents
{
    /// <summary>
    /// Fired when a save operation starts.
    /// </summary>
    public static event Action SaveStarted;

    /// <summary>
    /// Fired when a save operation ends (success or failure).
    /// </summary>
    public static event Action SaveFinished;


    // Supports nested save wrappers safely.
    // IsSaveInProgress remains true until outermost save finishes.
    private static int activeSaveDepth = 0;

    public static bool IsSaveInProgress => activeSaveDepth > 0;
    public static int TotalSaveOperations { get; private set; }


    public static void RaiseSaveStarted()
    {
        SaveStarted?.Invoke();
    }

    public static void RaiseSaveFinished()
    {
        SaveFinished?.Invoke();
    }

    /// <summary>
    /// Wraps any save action with start/finish feedback.
    /// Uses finally so UI always exits saving state even if exceptions happen.
    /// </summary>
    public static void RunWithFeedback(Action saveAction)
    {
        bool isOutermostSave = activeSaveDepth == 0;
        activeSaveDepth++;

        if (isOutermostSave)
        {
            RaiseSaveStarted();
        }

        try
        {
            saveAction?.Invoke();
            TotalSaveOperations++;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SaveFeedbackEvents: Save action threw exception: {ex}");
            throw;
        }
        finally
        {
            activeSaveDepth = Mathf.Max(0, activeSaveDepth - 1);

            if (activeSaveDepth == 0)
            {
                RaiseSaveFinished();
            }
        }
    }

}
