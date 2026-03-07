using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Runtime run-currency manager.
/// Current MVP scope:
/// - Glass (main)
/// - Fragments (secondary, passively generated from lifetime Glass)
/// - Lumen (augment/prestige currency, passively generated from lifetime Fragments)
///
/// Notes:
/// - All currencies are run-domain in this iteration (cleared on fresh reset).
/// - Uses double internally for incremental-game scale.
/// - Saves doubles as strings (round-trip format) to avoid float precision loss.
/// </summary>
public class RunCurrencyManager : MonoBehaviour
{
    public static RunCurrencyManager Instance { get; private set; }

    [Header("Debug Start Values")]
    [Tooltip("Optional starting Glass for quick test sessions.")]
    [SerializeField] private double startingGlass = 0d;

    [Tooltip("Optional starting Fragments for quick test sessions.")]
    [SerializeField] private double startingFragments = 0d;

    [Tooltip("Optional starting Lumen for quick test sessions.")]
    [SerializeField] private double startingLumen = 0d;

    [Tooltip("If true, logs currency changes for debugging.")]
    [SerializeField] private bool logCurrencyChanges = false;

    [Header("Passive Generation")]
    [Tooltip("If true, passive conversion ticks run in Update.")]
    [SerializeField] private bool passiveGenerationEnabled = true;

    [Tooltip("Tick interval in seconds for passive generation.")]
    [Min(0.05f)] [SerializeField] private float passiveTickSeconds = 1f;

    [Tooltip("Fragments gained per second from lifetime Glass. Formula: lifetimeGlass * rate * multiplier.")]
    [Min(0f)] [SerializeField] private double fragmentsFromLifetimeGlassRate = 0.0001d;

    [Tooltip("Runtime multiplier for fragments conversion (upgrade hook).")]
    [Min(0f)] [SerializeField] private double fragmentsConversionMultiplier = 1f;

    [Tooltip("Lumen gained per second from lifetime Fragments. Formula: lifetimeFragments * rate * multiplier.")]
    [Min(0f)] [SerializeField] private double lumenFromLifetimeFragmentsRate = 0.00005d;
    [Tooltip("Runtime multiplier for lumen conversion (upgrade hook).")]
    [Min(0f)] [SerializeField] private double lumenConversionMultiplier = 1f;

    [Header("Persistence (Run Domain)")]
    [Tooltip("If true, run currency state is persisted in PlayerPrefs.")]
    [SerializeField] private bool persistRunCurrencyState = true;

    [Tooltip("Base save key prefix for run currency.")]
    [SerializeField] private string runCurrencySaveKeyPrefix = "run.currency";

    [Tooltip("If true, saves when currency changes (can be frequent).")]
    [SerializeField] private bool autoSaveOnCurrencyChange = false;

    [Tooltip("If true, manual save/clear operations emit save feedback events.")]
    [SerializeField] private bool emitSaveFeedbackEvents = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private double currentGlass;
    [SerializeField] private double currentFragments;
    [SerializeField] private double currentLumen;

    [SerializeField] private double lifetimeGlassEarned;
    [SerializeField] private double lifetimeFragmentsEarned;
    [SerializeField] private double lifetimeLumenEarned;

    [Tooltip("Set true once Fragments are earned this run. Used for HUD root visibility.")]
    [SerializeField] private bool hasSeenFragmentsThisRun;

    [Tooltip("Set true once Lumen is earned this run. Used for HUD root visibility.")]
    [SerializeField] private bool hasSeenLumenThisRun;

    // Passive tick accumulator.
    private double passiveTickAccumulator;

    /// <summary>
    /// Legacy event kept for compatibility with existing Glass-only HUD logic.
    /// Args: currentGlass, lifetimeGlassEarned
    /// </summary>
    public static event Action<double, double> GlassChanged;

    /// <summary>
    /// Unified event for any currency/state change.
    /// New HUD/debug systems should prefer this.
    /// </summary>
    public static event Action CurrencyStateChanged;

    public double CurrentGlass => currentGlass;
    public double CurrentFragments => currentFragments;
    public double CurrentLumen => currentLumen;

    public double LifetimeGlassEarned => lifetimeGlassEarned;
    public double LifetimeFragmentsEarned => lifetimeFragmentsEarned;
    public double LifetimeLumenEarned => lifetimeLumenEarned;

    public bool HasSeenFragmentsThisRun => hasSeenFragmentsThisRun;
    public bool HasSeenLumenThisRun => hasSeenLumenThisRun;

    // Stable key tokens.
    private const string GlassToken = "glass";
    private const string FragmentsToken = "fragments";
    private const string LumenToken = "lumen";

    private const string LifetimeGlassToken = "lifetime_glass";
    private const string LifetimeFragmentsToken = "lifetime_fragments";
    private const string LifetimeLumenToken = "lifetime_lumen";

    private const string SeenFragmentsToken = "ui_seen_fragments";
    private const string SeenLumenToken = "ui_seen_lumen";

    private const string PendingFragmentsToken = "pending_fragments";
    private const string PendingLumenToken = "pending_lumen";


    // Legacy migration token from previous build.
    private const string LegacyCurrentGlassToken = "current_glass";

    // Debug key accessors for panel visibility.
    public bool DebugPersistRunCurrencyState => persistRunCurrencyState;
    public string DebugGlassSaveKey => BuildRunCurrencySaveKey(GlassToken);
    public string DebugFragmentsSaveKey => BuildRunCurrencySaveKey(FragmentsToken);
    public string DebugLumenSaveKey => BuildRunCurrencySaveKey(LumenToken);
    public string DebugLifetimeGlassSaveKey => BuildRunCurrencySaveKey(LifetimeGlassToken);
    public string DebugLifetimeFragmentsSaveKey => BuildRunCurrencySaveKey(LifetimeFragmentsToken);
    public string DebugLifetimeLumenSaveKey => BuildRunCurrencySaveKey(LifetimeLumenToken);
    public string DebugSeenFragmentsSaveKey => BuildRunCurrencySaveKey(SeenFragmentsToken);
    public string DebugSeenLumenSaveKey => BuildRunCurrencySaveKey(SeenLumenToken);
    

    /// <summary>
    /// Backward-compatible alias used by RuntimeDebugPanel.
    /// </summary>
    public string DebugCurrentGlassSaveKey => DebugGlassSaveKey;


    /// <summary>
    /// True when passive conversion is currently enabled.
    /// </summary>
    public bool PassiveGenerationEnabled => passiveGenerationEnabled;

    /// <summary>
    /// Runtime tick duration after safety clamp.
    /// </summary>
    public float PassiveTickSeconds => Mathf.Max(0.05f, passiveTickSeconds);


    [Tooltip("Sub-unit Fragments not yet converted into whole spendable units.")]
    [SerializeField] private double pendingFragments;

    [Tooltip("Sub-unit Lumen not yet converted into whole spendable units.")]
    [SerializeField] private double pendingLumen;

    /// <summary>
    /// 0..1 progress toward next whole spendable Fragment.
    /// Uses pending bank, so spending current Fragments does not reset this bar.
    /// </summary>
    public float FragmentsProgressToNextWhole01 => GetPendingProgress01(pendingFragments);

    /// <summary>
    /// 0..1 progress toward next whole spendable Lumen.
    /// Uses pending bank, so spending current Lumen does not reset this bar.
    /// </summary>
    public float LumenProgressToNextWhole01 => GetPendingProgress01(pendingLumen);

    /// <summary>
    /// Raw pending sub-unit Fragments (0..1 in normal state).
    /// Useful for tuning and debug panel visibility.
    /// </summary>
    public double PendingFragments => pendingFragments;

    /// <summary>
    /// Raw pending sub-unit Lumen (0..1 in normal state).
    /// Useful for tuning and debug panel visibility.
    /// </summary>
    public double PendingLumen => pendingLumen;

    public string DebugPendingFragmentsSaveKey => BuildRunCurrencySaveKey(PendingFragmentsToken);
    public string DebugPendingLumenSaveKey => BuildRunCurrencySaveKey(PendingLumenToken);







    /// <summary>
    /// 0..1 progress toward next passive generation tick.
    /// HUD can bind sliders to this for "next gain" visualization.
    /// </summary>
    public float PassiveTickProgress01
    {
        get
        {
            if (!passiveGenerationEnabled)
            {
                return 0f;
            }

            double tick = Math.Max(0.05d, passiveTickSeconds);
            double normalized = tick > 0d ? (passiveTickAccumulator / tick) : 0d;
            return Mathf.Clamp01((float)normalized);
        }
    }

    




    private void Awake()
    {
        // Singleton guard for runtime manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistRunCurrencyState)
        {
            LoadRunCurrencyState();
        }
        else
        {
            InitializeFromStartingValues();
            NotifyCurrencyChanged();
        }
    }

    private void Update()
    {
        TickPassiveGeneration(Time.deltaTime);
    }

    private void OnEnable()
    {
        WorldBlock.Destroyed += OnWorldBlockDestroyed;
    }

    private void OnDisable()
    {
        WorldBlock.Destroyed -= OnWorldBlockDestroyed;
    }

    private void OnWorldBlockDestroyed(WorldBlock block)
    {
        if (block == null)
        {
            return;
        }

        AddGlass(block.GlassReward);
    }

    /// <summary>
    /// Adds Glass and increments lifetime glass earned.
    /// </summary>
    public void AddGlass(double amount)
    {
        if (amount <= 0d)
        {
            return;
        }

        currentGlass += amount;
        lifetimeGlassEarned += amount;

        if (logCurrencyChanges)
        {
            Debug.Log($"RunCurrencyManager: +{amount} Glass -> Current={currentGlass} Lifetime={lifetimeGlassEarned}");
        }

        NotifyCurrencyChanged();

        if (autoSaveOnCurrencyChange)
        {
            SaveRunCurrencyState(withFeedback: false);
        }
    }

    /// <summary>
    /// Adds Fragments using pending-bank conversion:
    /// - amount contributes to lifetime + pending
    /// - whole units are transferred from pending -> current
    /// - "seen this run" flips only when at least 1 whole unit is credited
    /// </summary>
    public void AddFragments(double amount)
    {
        if (amount <= 0d)
        {
            return;
        }

        AddWithPendingBank(
            amount: amount,
            ref currentFragments,
            ref lifetimeFragmentsEarned,
            ref pendingFragments,
            ref hasSeenFragmentsThisRun,
            currencyNameForLog: "Fragments");

        NotifyCurrencyChanged();

        if (autoSaveOnCurrencyChange)
        {
            SaveRunCurrencyState(withFeedback: false);
        }
    }


    /// <summary>
    /// Adds Lumen using pending-bank conversion:
    /// - amount contributes to lifetime + pending
    /// - whole units are transferred from pending -> current
    /// - "seen this run" flips only when at least 1 whole unit is credited
    /// </summary>
    public void AddLumen(double amount)
    {
        if (amount <= 0d)
        {
            return;
        }

        AddWithPendingBank(
            amount: amount,
            ref currentLumen,
            ref lifetimeLumenEarned,
            ref pendingLumen,
            ref hasSeenLumenThisRun,
            currencyNameForLog: "Lumen");

        NotifyCurrencyChanged();

        if (autoSaveOnCurrencyChange)
        {
            SaveRunCurrencyState(withFeedback: false);
        }
}


    /// <summary>
    /// Legacy helper kept for compatibility.
    /// Resets only current Glass (does not touch Fragments/Lumen).
    /// </summary>
    public void ResetRunGlass()
    {
        currentGlass = 0d;
        NotifyCurrencyChanged();

        if (autoSaveOnCurrencyChange)
        {
            SaveRunCurrencyState(withFeedback: false);
        }
    }

    /// <summary>
    /// Run reset helper.
    /// - Always resets current run balances to zero.
    /// - If clearLifetime is true: also clears lifetime totals and "seen this run" flags.
    ///   (fresh run/reset behavior)
    /// - If clearLifetime is false: keeps lifetime + seen flags for future prestige-style flows.
    /// </summary>
    public void ResetRunCurrency(bool clearLifetime, bool saveAfterReset)
    {
        currentGlass = 0d;
        currentFragments = 0d;
        currentLumen = 0d;

        pendingFragments = 0d;
        pendingLumen = 0d;


        if (clearLifetime)
        {
            lifetimeGlassEarned = 0d;
            lifetimeFragmentsEarned = 0d;
            lifetimeLumenEarned = 0d;

            hasSeenFragmentsThisRun = false;
            hasSeenLumenThisRun = false;
        }

        passiveTickAccumulator = 0d;

        NotifyCurrencyChanged();

        if (saveAfterReset)
        {
            SaveRunCurrencyState(withFeedback: emitSaveFeedbackEvents);
        }
    }

    /// <summary>
    /// Loads all run currency state.
    /// Includes migration fallback from old key: "current_glass".
    /// </summary>
    public void LoadRunCurrencyState()
    {
        if (!persistRunCurrencyState)
        {
            InitializeFromStartingValues();
            NotifyCurrencyChanged();
            return;
        }

        // Glass load: prefer new key, fallback to old key for migration.
        bool hasGlass = TryReadDoubleByToken(
            GlassToken,
            out double loadedGlass,
            Math.Max(0d, startingGlass),
            LegacyCurrentGlassToken);

        bool hasFragments = TryReadDoubleByToken(
            FragmentsToken,
            out double loadedFragments,
            Math.Max(0d, startingFragments));

        bool hasLumen = TryReadDoubleByToken(
            LumenToken,
            out double loadedLumen,
            Math.Max(0d, startingLumen));

        bool hasLifetimeGlass = TryReadDoubleByToken(
            LifetimeGlassToken,
            out double loadedLifetimeGlass,
            0d);

        bool hasLifetimeFragments = TryReadDoubleByToken(
            LifetimeFragmentsToken,
            out double loadedLifetimeFragments,
            0d);

        bool hasLifetimeLumen = TryReadDoubleByToken(
            LifetimeLumenToken,
            out double loadedLifetimeLumen,
            0d);

        currentGlass = hasGlass ? loadedGlass : Math.Max(0d, startingGlass);
        currentFragments = hasFragments ? loadedFragments : Math.Max(0d, startingFragments);
        currentLumen = hasLumen ? loadedLumen : Math.Max(0d, startingLumen);

        lifetimeGlassEarned = hasLifetimeGlass ? loadedLifetimeGlass : 0d;
        lifetimeFragmentsEarned = hasLifetimeFragments ? loadedLifetimeFragments : 0d;
        lifetimeLumenEarned = hasLifetimeLumen ? loadedLifetimeLumen : 0d;

        // Clamp negatives from corrupted/edited saves.
        currentGlass = Math.Max(0d, currentGlass);
        currentFragments = Math.Max(0d, currentFragments);
        currentLumen = Math.Max(0d, currentLumen);

        lifetimeGlassEarned = Math.Max(0d, lifetimeGlassEarned);
        lifetimeFragmentsEarned = Math.Max(0d, lifetimeFragmentsEarned);
        lifetimeLumenEarned = Math.Max(0d, lifetimeLumenEarned);

        TryReadDoubleByToken(PendingFragmentsToken, out pendingFragments, 0d);
        TryReadDoubleByToken(PendingLumenToken, out pendingLumen, 0d);

        // Clamp pending to sane range.
        pendingFragments = System.Math.Max(0d, pendingFragments);
        pendingLumen = System.Math.Max(0d, pendingLumen);

        // Normalize in case old/edited saves exceed 1.
        if (pendingFragments >= 1d) pendingFragments -= System.Math.Floor(pendingFragments);
        if (pendingLumen >= 1d) pendingLumen -= System.Math.Floor(pendingLumen);


        // "Seen" flags are persisted, but Continue should also show if loaded amount > 0.
        hasSeenFragmentsThisRun = PlayerPrefs.GetInt(BuildRunCurrencySaveKey(SeenFragmentsToken), 0) == 1 || currentFragments > 0d;
        hasSeenLumenThisRun = PlayerPrefs.GetInt(BuildRunCurrencySaveKey(SeenLumenToken), 0) == 1 || currentLumen > 0d;

        passiveTickAccumulator = 0d;

        NotifyCurrencyChanged();
    }

    /// <summary>
    /// Saves all run currency values.
    /// Doubles are stored as round-trip invariant strings.
    /// </summary>
    public void SaveRunCurrencyState(bool withFeedback)
    {
        if (!persistRunCurrencyState)
        {
            return;
        }

        void Write()
        {
            WriteDoubleAsString(BuildRunCurrencySaveKey(GlassToken), currentGlass);
            WriteDoubleAsString(BuildRunCurrencySaveKey(FragmentsToken), currentFragments);
            WriteDoubleAsString(BuildRunCurrencySaveKey(LumenToken), currentLumen);

            WriteDoubleAsString(BuildRunCurrencySaveKey(LifetimeGlassToken), lifetimeGlassEarned);
            WriteDoubleAsString(BuildRunCurrencySaveKey(LifetimeFragmentsToken), lifetimeFragmentsEarned);
            WriteDoubleAsString(BuildRunCurrencySaveKey(LifetimeLumenToken), lifetimeLumenEarned);

            PlayerPrefs.SetInt(BuildRunCurrencySaveKey(SeenFragmentsToken), hasSeenFragmentsThisRun ? 1 : 0);
            PlayerPrefs.SetInt(BuildRunCurrencySaveKey(SeenLumenToken), hasSeenLumenThisRun ? 1 : 0);

            WriteDoubleAsString(BuildRunCurrencySaveKey(PendingFragmentsToken), pendingFragments);
            WriteDoubleAsString(BuildRunCurrencySaveKey(PendingLumenToken), pendingLumen);


            // Optional migration cleanup: remove old key once new save is written.
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LegacyCurrentGlassToken));

            PlayerPrefs.Save();
        }

        if (withFeedback)
        {
            SaveFeedbackEvents.RunWithFeedback(Write);
        }
        else
        {
            Write();
        }
    }

    /// <summary>
    /// Clears all run-currency keys from PlayerPrefs.
    /// </summary>
    public void ClearRunCurrencyState(bool withFeedback)
    {
        void Clear()
        {
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(GlassToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(FragmentsToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LumenToken));

            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LifetimeGlassToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LifetimeFragmentsToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LifetimeLumenToken));

            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(SeenFragmentsToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(SeenLumenToken));


            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(PendingFragmentsToken));
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(PendingLumenToken));


            // Legacy cleanup.
            PlayerPrefs.DeleteKey(BuildRunCurrencySaveKey(LegacyCurrentGlassToken));

            PlayerPrefs.Save();
        }

        if (withFeedback)
        {
            SaveFeedbackEvents.RunWithFeedback(Clear);
        }
        else
        {
            Clear();
        }
    }

    /// <summary>
    /// Debug helper: read saved Glass without mutating runtime state.
    /// </summary>
    public bool TryReadSavedCurrentGlass(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(GlassToken), out value, 0d);
    }

    /// <summary>
    /// Debug helper: read saved Fragments without mutating runtime state.
    /// </summary>
    public bool TryReadSavedCurrentFragments(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(FragmentsToken), out value, 0d);
    }

    /// <summary>
    /// Debug helper: read saved Lumen without mutating runtime state.
    /// </summary>
    public bool TryReadSavedCurrentLumen(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(LumenToken), out value, 0d);
    }

    /// <summary>
    /// Debug helper preserved for existing panel/hud flow.
    /// </summary>
    public bool TryReadSavedLifetimeGlass(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(LifetimeGlassToken), out value, 0d);
    }

    private void TickPassiveGeneration(float deltaTime)
    {
        if (!passiveGenerationEnabled)
        {
            return;
        }

        double tick = Math.Max(0.05d, passiveTickSeconds);
        passiveTickAccumulator += Math.Max(0f, deltaTime);

        // Use while-loop so slow frames still process all missed ticks deterministically.
        while (passiveTickAccumulator >= tick)
        {
            passiveTickAccumulator -= tick;
            RunPassiveGenerationTick(tick);
        }
    }

    /// <summary>
    /// One passive generation tick.
    /// - Fragments from lifetime Glass
    /// - Lumen from lifetime Fragments
    /// IMPORTANT:
    /// Uses pending-bank conversion so tiny gains accumulate until whole units are credited.
    /// This keeps spendable values whole-step friendly and keeps slider progress meaningful.
    /// </summary>
    private void RunPassiveGenerationTick(double tickSeconds)
    {
        // Use baselines from start of tick so formulas are deterministic for this tick.
        double baselineLifetimeGlass = Math.Max(0d, lifetimeGlassEarned);
        double baselineLifetimeFragments = Math.Max(0d, lifetimeFragmentsEarned);

        double fragmentGain =
            baselineLifetimeGlass *
            Math.Max(0d, fragmentsFromLifetimeGlassRate) *
            Math.Max(0d, fragmentsConversionMultiplier) *
            tickSeconds;

        double lumenGain =
            baselineLifetimeFragments *
            Math.Max(0d, lumenFromLifetimeFragmentsRate) *
            Math.Max(0d, lumenConversionMultiplier) *
            tickSeconds;

        bool changed = false;

        if (fragmentGain > 0d)
        {
            AddWithPendingBank(
                amount: fragmentGain,
                ref currentFragments,
                ref lifetimeFragmentsEarned,
                ref pendingFragments,
                ref hasSeenFragmentsThisRun,
                currencyNameForLog: "Fragments");

            changed = true;
        }

        if (lumenGain > 0d)
        {
            AddWithPendingBank(
                amount: lumenGain,
                ref currentLumen,
                ref lifetimeLumenEarned,
                ref pendingLumen,
                ref hasSeenLumenThisRun,
                currencyNameForLog: "Lumen");

            changed = true;
        }

        if (!changed)
        {
            return;
        }

        NotifyCurrencyChanged();

        if (autoSaveOnCurrencyChange)
        {
            SaveRunCurrencyState(withFeedback: false);
        }
    }


    private void InitializeFromStartingValues()
    {
        currentGlass = Math.Max(0d, startingGlass);
        currentFragments = Math.Max(0d, startingFragments);
        currentLumen = Math.Max(0d, startingLumen);

        lifetimeGlassEarned = 0d;
        lifetimeFragmentsEarned = 0d;
        lifetimeLumenEarned = 0d;

        hasSeenFragmentsThisRun = currentFragments > 0d;
        hasSeenLumenThisRun = currentLumen > 0d;

        passiveTickAccumulator = 0d;

        pendingFragments = 0d;
        pendingLumen = 0d;

    }

    private void NotifyCurrencyChanged()
    {
        // Keep old Glass event alive for existing listeners.
        GlassChanged?.Invoke(currentGlass, lifetimeGlassEarned);

        // New generalized event for future HUD/debug listeners.
        CurrencyStateChanged?.Invoke();
    }

    private bool TryReadDoubleByToken(string primaryToken, out double value, double fallbackIfMissing, params string[] legacyTokens)
    {
        if (TryReadSavedDouble(BuildRunCurrencySaveKey(primaryToken), out value, fallbackIfMissing))
        {
            return true;
        }

        if (legacyTokens != null)
        {
            for (int i = 0; i < legacyTokens.Length; i++)
            {
                if (TryReadSavedDouble(BuildRunCurrencySaveKey(legacyTokens[i]), out value, fallbackIfMissing))
                {
                    return true;
                }
            }
        }

        value = fallbackIfMissing;
        return false;
    }

    /// <summary>
    /// Debug helper: read saved pending Fragments without mutating runtime state.
    /// </summary>
    public bool TryReadSavedPendingFragments(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(PendingFragmentsToken), out value, 0d);
    }

    /// <summary>
    /// Debug helper: read saved pending Lumen without mutating runtime state.
    /// </summary>
    public bool TryReadSavedPendingLumen(out double value)
    {
        return TryReadSavedDouble(BuildRunCurrencySaveKey(PendingLumenToken), out value, 0d);
    }


    private static void WriteDoubleAsString(string key, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = 0d;
        }

        PlayerPrefs.SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Reads double from PlayerPrefs:
    /// 1) preferred string value (new format)
    /// 2) float fallback (legacy data)
    /// Returns false only when key does not exist.
    /// </summary>
    private static bool TryReadSavedDouble(string key, out double value, double fallbackIfParseFails)
    {
        value = fallbackIfParseFails;

        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        string raw = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrWhiteSpace(raw) &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            value = parsed;
            return true;
        }

        float legacy = PlayerPrefs.GetFloat(key, float.NaN);
        if (!float.IsNaN(legacy))
        {
            value = legacy;
        }

        return true;
    }

    private string BuildRunCurrencySaveKey(string token)
    {
        return $"{runCurrencySaveKeyPrefix}.{token}";
    }

    /// <summary>
    /// Autosave on app background to reduce progress loss on mobile/alt-tab cases.
    /// </summary>
    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            return;
        }

        SaveRunCurrencyState(withFeedback: false);
    }

    /// <summary>
    /// Autosave on app quit for desktop/editor stop cases.
    /// </summary>
    private void OnApplicationQuit()
    {
        SaveRunCurrencyState(withFeedback: false);
    }


    /// <summary>
    /// Core pending-bank accumulator.
    /// 1) Add amount into lifetime + pending.
    /// 2) Convert whole units from pending into spendable current.
    /// 3) Keep pending in [0,1) after carry.
    /// </summary>
    private void AddWithPendingBank(
        double amount,
        ref double current,
        ref double lifetime,
        ref double pending,
        ref bool hasSeenThisRun,
        string currencyNameForLog)
    {
        lifetime += amount;
        pending += amount;

        // Small epsilon protects against floating-point edge cases near exact integers.
        double wholeUnits = System.Math.Floor(pending + 1e-9d);

        if (wholeUnits > 0d)
        {
            current += wholeUnits;
            pending -= wholeUnits;
            hasSeenThisRun = true;
        }

        // Clamp safe range after conversion.
        if (pending < 0d)
        {
            pending = 0d;
        }

        // Hard safety: if numeric drift ever pushes >=1 again, keep only fractional remainder.
        if (pending >= 1d)
        {
            pending = pending - System.Math.Floor(pending);
        }

        if (logCurrencyChanges)
        {
            Debug.Log(
                $"RunCurrencyManager: +{amount} {currencyNameForLog} -> Current={current}, Lifetime={lifetime}, Pending={pending:0.######}");
        }
    }

    /// <summary>
    /// Converts pending bank value into a normalized 0..1 slider value.
    /// </summary>
    private static float GetPendingProgress01(double pending)
    {
        if (pending <= 0d)
        {
            return 0f;
        }

        double fractional = pending - System.Math.Floor(pending);
        return Mathf.Clamp01((float)fractional);
    }


}
