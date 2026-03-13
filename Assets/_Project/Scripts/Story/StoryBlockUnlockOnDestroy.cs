using UnityEngine;

/// <summary>
/// Put this on the Story Block prefab.
/// When this specific block is destroyed, unlocks the next story piece.
/// Uses WorldBlock.BlockId as a persistent "already granted" guard.
/// </summary>
[RequireComponent(typeof(WorldBlock))]
public class StoryBlockUnlockOnDestroy : MonoBehaviour
{
    [SerializeField] private bool unlockOnlyOncePerInstance = true;
    [SerializeField] private bool logIfManagerMissing = true;

    private WorldBlock cachedBlock;
    private bool hasUnlocked;

    private void Awake()
    {
        cachedBlock = GetComponent<WorldBlock>();
    }

    private void OnEnable()
    {
        WorldBlock.Destroyed += OnAnyBlockDestroyed;
    }

    private void OnDisable()
    {
        WorldBlock.Destroyed -= OnAnyBlockDestroyed;
    }

    private void OnAnyBlockDestroyed(WorldBlock destroyedBlock)
    {
        if (destroyedBlock == null || destroyedBlock != cachedBlock)
        {
            return;
        }

        if (unlockOnlyOncePerInstance && hasUnlocked)
        {
            return;
        }

        hasUnlocked = true;

        if (StoryLoreProgressionManager.Instance == null)
        {
            if (logIfManagerMissing)
            {
                Debug.LogWarning("StoryBlockUnlockOnDestroy: Missing StoryLoreProgressionManager in scene.");
            }
            return;
        }

        StoryLoreProgressionManager.Instance.TryUnlockNextStoryPieceFromBlockId(
            destroyedBlock.BlockId,
            out _,
            out _);
    }
}
