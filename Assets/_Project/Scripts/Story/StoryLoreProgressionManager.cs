using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores unlocked story/lore progress as an index into an ordered dialogue array.
/// Meta progression persists across runs.
/// </summary>
public class StoryLoreProgressionManager : MonoBehaviour
{
    [Serializable]
    public struct StoryDialoguePiece
    {
        [Tooltip("Short title shown in lore list.")]
        public string title;

        [TextArea(2, 8)]
        [Tooltip("Full lore/body text.")]
        public string description;

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description);
    }

    public static StoryLoreProgressionManager Instance { get; private set; }

    /// <summary>
    /// Fired when a new piece is unlocked.
    /// Args: unlockedIndex, unlockedPiece
    /// </summary>
    public static event Action<int, StoryDialoguePiece> StoryPieceUnlocked;

    [Header("Data")]
    [Tooltip("Ordered list of story pieces. Unlocks advance from 0 -> N-1.")]
    [SerializeField] private StoryDialoguePiece[] storyDialoguePieces = Array.Empty<StoryDialoguePiece>();

    [Header("Persistence")]
    [SerializeField] private bool persistState = true;
    [SerializeField] private string unlockedCountSaveKey = "meta.story.unlocked_count";
    [SerializeField] private string grantedBlockIdsSaveKey = "meta.story.granted_block_ids";

    [Header("Debug")]
    [SerializeField] private bool logUnlocks = true;

    [Header("Runtime (Read Only)")]
    [SerializeField] private int unlockedCount = 0;
    [SerializeField] private int grantedBlockIdsCount = 0;

    // Meta guard: each unique story block ID can only grant once.
    private readonly HashSet<string> grantedBlockIds = new HashSet<string>(StringComparer.Ordinal);

    public int UnlockedCount => unlockedCount;
    public int TotalStoryPieces => storyDialoguePieces != null ? storyDialoguePieces.Length : 0;
    public bool HasMoreToUnlock => unlockedCount < TotalStoryPieces;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadState();
    }

    /// <summary>
    /// Unlocks the next story piece directly (no block-id guard).
    /// Useful for debug/cheat/admin flows.
    /// </summary>
    public bool TryUnlockNextStoryPiece(out int unlockedIndex, out StoryDialoguePiece unlockedPiece)
    {
        return TryUnlockNextStoryPieceInternal(
            out unlockedIndex,
            out unlockedPiece,
            saveAfterUnlock: true,
            logContext: "direct");
    }

    /// <summary>
    /// Unlocks next story piece only if this blockId has never granted before.
    /// </summary>
    public bool TryUnlockNextStoryPieceFromBlockId(string blockId, out int unlockedIndex, out StoryDialoguePiece unlockedPiece)
    {
        unlockedIndex = -1;
        unlockedPiece = default;

        if (string.IsNullOrWhiteSpace(blockId))
        {
            Debug.LogWarning("StoryLoreProgressionManager: Cannot unlock from empty blockId.");
            return false;
        }

        if (grantedBlockIds.Contains(blockId))
        {
            return false;
        }

        if (!TryUnlockNextStoryPieceInternal(
                out unlockedIndex,
                out unlockedPiece,
                saveAfterUnlock: false,
                logContext: $"block '{blockId}'"))
        {
            return false;
        }

        grantedBlockIds.Add(blockId);
        grantedBlockIdsCount = grantedBlockIds.Count;
        SaveState();

        return true;
    }

    public bool HasBlockAlreadyGranted(string blockId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            return false;
        }

        return grantedBlockIds.Contains(blockId);
    }

    public bool IsStoryPieceUnlocked(int index)
    {
        return index >= 0 && index < unlockedCount;
    }

    public bool TryGetStoryPiece(int index, out StoryDialoguePiece piece)
    {
        piece = default;

        if (storyDialoguePieces == null || index < 0 || index >= storyDialoguePieces.Length)
        {
            return false;
        }

        piece = storyDialoguePieces[index];
        return true;
    }

    [ContextMenu("Debug/Unlock Next Story Piece")]
    private void DebugUnlockNext()
    {
        TryUnlockNextStoryPiece(out _, out _);
    }

    [ContextMenu("Debug/Reset Story Unlocks")]
    private void DebugResetUnlocks()
    {
        unlockedCount = 0;
        grantedBlockIds.Clear();
        grantedBlockIdsCount = 0;
        SaveState();

        if (logUnlocks)
        {
            Debug.Log("StoryLoreProgressionManager: Story unlocks + granted block IDs reset.");
        }
    }

    private bool TryUnlockNextStoryPieceInternal(
        out int unlockedIndex,
        out StoryDialoguePiece unlockedPiece,
        bool saveAfterUnlock,
        string logContext)
    {
        unlockedIndex = -1;
        unlockedPiece = default;

        int total = TotalStoryPieces;
        if (total <= 0)
        {
            return false;
        }

        unlockedCount = Mathf.Clamp(unlockedCount, 0, total);

        if (unlockedCount >= total)
        {
            return false;
        }

        unlockedIndex = unlockedCount;
        unlockedPiece = storyDialoguePieces[unlockedIndex];
        unlockedCount++;

        if (saveAfterUnlock)
        {
            SaveState();
        }

        StoryPieceUnlocked?.Invoke(unlockedIndex, unlockedPiece);

        if (logUnlocks)
        {
            string safeTitle = string.IsNullOrWhiteSpace(unlockedPiece.title) ? "(untitled)" : unlockedPiece.title;
            Debug.Log(
                $"StoryLoreProgressionManager: Unlocked story piece {unlockedIndex + 1}/{total} '{safeTitle}' ({logContext}).");
        }

        return true;
    }

    private void LoadState()
    {
        grantedBlockIds.Clear();

        int total = TotalStoryPieces;

        if (!persistState)
        {
            unlockedCount = Mathf.Clamp(unlockedCount, 0, total);
            grantedBlockIdsCount = grantedBlockIds.Count;
            return;
        }

        unlockedCount = Mathf.Clamp(PlayerPrefs.GetInt(unlockedCountSaveKey, 0), 0, total);

        string serializedIds = PlayerPrefs.GetString(grantedBlockIdsSaveKey, string.Empty);
        if (!string.IsNullOrEmpty(serializedIds))
        {
            string[] ids = serializedIds.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i].Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    grantedBlockIds.Add(id);
                }
            }
        }

        grantedBlockIdsCount = grantedBlockIds.Count;
    }

    private void SaveState()
    {
        if (!persistState)
        {
            return;
        }

        PlayerPrefs.SetInt(unlockedCountSaveKey, Mathf.Max(0, unlockedCount));
        PlayerPrefs.SetString(grantedBlockIdsSaveKey, SerializeGrantedBlockIds());
        PlayerPrefs.Save();

        grantedBlockIdsCount = grantedBlockIds.Count;
    }

    private string SerializeGrantedBlockIds()
    {
        if (grantedBlockIds.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n", grantedBlockIds);
    }
}
