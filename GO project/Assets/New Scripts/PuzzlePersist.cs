using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzlePersist : Singleton<PuzzlePersist>
{
    private const int UnresolvedPuzzleResult = -1;

    public TextAsset savedPuzzleData; // This will hold the saved puzzle data as a TextAsset
    public List<TextAsset> savedPuzzlePools = new();
    public string activePoolId;
    public string activePoolLabel;
    [Range(0f,1f)] public float requiredSolveRateToUnlockNext = 1f;
    public string nextPoolIdToUnlock;
    public bool activePoolOrderInitialized;
    public int activePoolCurrentPuzzleIndex;
    public int activePoolSolvedCount;
    public int activePoolFailedCount;
    public GoLessonData activeLessonData;
    public int activeLessonSlideIndex;
    public int activeLessonWrongAnswerCount;

    [SerializeField] private List<int> activePoolFirstResultStates = new();

    public bool HasSelectedPuzzlePool => savedPuzzlePools != null && savedPuzzlePools.Count > 0;
    public bool IsLessonModeActive => activeLessonData != null;

    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        base.Awake();
        DontDestroyOnLoad(this.gameObject);
    }

    public void BeginPuzzlePoolSession(
        string poolId,
        string poolLabel,
        List<TextAsset> puzzlePool,
        float unlockThreshold,
        string nextPoolId)
    {
        ClearLessonSession();

        activePoolId = string.IsNullOrWhiteSpace(poolId) ? poolLabel : poolId;
        activePoolLabel = string.IsNullOrWhiteSpace(poolLabel) ? activePoolId : poolLabel;
        requiredSolveRateToUnlockNext = Mathf.Clamp01(unlockThreshold);
        nextPoolIdToUnlock = nextPoolId;

        savedPuzzlePools = GetRandomPuzzles(puzzlePool, 10);
        savedPuzzleData = savedPuzzlePools.Count > 0 ? savedPuzzlePools[0] : null;

        activePoolOrderInitialized = false;
        activePoolCurrentPuzzleIndex = 0;
        activePoolSolvedCount = 0;
        activePoolFailedCount = 0;
        ResetFirstResultStates();
    }

    private List<TextAsset> GetRandomPuzzles(List<TextAsset> sourcePool, int count)
    {
        if (sourcePool == null || sourcePool.Count == 0)
            return new List<TextAsset>();

        List<TextAsset> tempPool = new List<TextAsset>(sourcePool);
        List<TextAsset> result = new List<TextAsset>();

        int takeCount = Mathf.Min(count, tempPool.Count);

        for (int i = 0; i < takeCount; i++)
        {
            int randomIndex = Random.Range(0, tempPool.Count);
            result.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex);
        }

        return result;
    }

    public void BeginLessonSession(GoLessonData lessonData)
    {
        ClearSelectedPuzzlePool();

        activeLessonData = lessonData;
        activeLessonSlideIndex = 0;
        activeLessonWrongAnswerCount = 0;
        RefreshLessonSlidePuzzleData();
    }

    public void SetLessonSlideIndex(int slideIndex)
    {
        if (activeLessonData == null)
        {
            activeLessonSlideIndex = 0;
            savedPuzzleData = null;
            return;
        }

        if (activeLessonData.slides == null || activeLessonData.slides.Count == 0)
        {
            activeLessonSlideIndex = 0;
            savedPuzzleData = null;
            return;
        }

        activeLessonSlideIndex = Mathf.Clamp(slideIndex,0,activeLessonData.slides.Count - 1);
        RefreshLessonSlidePuzzleData();
    }

    public void RecordLessonWrongAnswer()
    {
        activeLessonWrongAnswerCount = Mathf.Max(0,activeLessonWrongAnswerCount + 1);
    }

    public void ClearLessonSession()
    {
        activeLessonData = null;
        activeLessonSlideIndex = 0;
        activeLessonWrongAnswerCount = 0;

        if (!HasSelectedPuzzlePool)
            savedPuzzleData = null;
    }

    public void SetCurrentPuzzle(TextAsset puzzleData)
    {
        savedPuzzleData = puzzleData;
    }

    public void SetPuzzlePoolOrder(List<TextAsset> puzzlePool)
    {
        savedPuzzlePools = puzzlePool != null ? new List<TextAsset>(puzzlePool) : new List<TextAsset>();
        activePoolOrderInitialized = true;
        EnsureRuntimePoolState();
        savedPuzzleData = HasSelectedPuzzlePool ? savedPuzzlePools[activePoolCurrentPuzzleIndex] : null;
    }

    public void SetCurrentPuzzleIndex(int puzzleIndex)
    {
        EnsureRuntimePoolState();
        if (!HasSelectedPuzzlePool)
        {
            activePoolCurrentPuzzleIndex = 0;
            savedPuzzleData = null;
            return;
        }

        activePoolCurrentPuzzleIndex = Mathf.Clamp(puzzleIndex,0,savedPuzzlePools.Count - 1);
        savedPuzzleData = savedPuzzlePools[activePoolCurrentPuzzleIndex];
    }

    public void UpdateActivePoolStats(int solvedCount,int failedCount)
    {
        activePoolSolvedCount = Mathf.Max(0,solvedCount);
        activePoolFailedCount = Mathf.Max(0,failedCount);
    }

    public void ResetActivePoolProgress()
    {
        EnsureRuntimePoolState();

        activePoolCurrentPuzzleIndex = 0;
        activePoolSolvedCount = 0;
        activePoolFailedCount = 0;
        activePoolOrderInitialized = false;
        ResetFirstResultStates();
        savedPuzzleData = HasSelectedPuzzlePool ? savedPuzzlePools[0] : null;
    }

    public bool TryRecordPuzzleFirstResult(int puzzleIndex,bool wasSolved)
    {
        EnsureRuntimePoolState();

        if (!HasSelectedPuzzlePool || puzzleIndex < 0 || puzzleIndex >= activePoolFirstResultStates.Count)
            return false;

        if (activePoolFirstResultStates[puzzleIndex] != UnresolvedPuzzleResult)
            return false;

        activePoolFirstResultStates[puzzleIndex] = wasSolved ? 1 : 0;
        if (wasSolved)
            activePoolSolvedCount++;
        else
            activePoolFailedCount++;

        return true;
    }

    public bool HasRecordedPuzzleFirstResult(int puzzleIndex)
    {
        EnsureRuntimePoolState();

        return puzzleIndex >= 0 &&
            puzzleIndex < activePoolFirstResultStates.Count &&
            activePoolFirstResultStates[puzzleIndex] != UnresolvedPuzzleResult;
    }

    public void EnsureRuntimePoolState()
    {
        if (savedPuzzlePools == null)
            savedPuzzlePools = new List<TextAsset>();

        if (activePoolFirstResultStates == null)
            activePoolFirstResultStates = new List<int>();

        int targetCount = savedPuzzlePools.Count;
        while (activePoolFirstResultStates.Count < targetCount)
            activePoolFirstResultStates.Add(UnresolvedPuzzleResult);

        while (activePoolFirstResultStates.Count > targetCount)
            activePoolFirstResultStates.RemoveAt(activePoolFirstResultStates.Count - 1);

        if (!HasSelectedPuzzlePool)
        {
            activePoolCurrentPuzzleIndex = 0;
            savedPuzzleData = null;
            return;
        }

        activePoolCurrentPuzzleIndex = Mathf.Clamp(activePoolCurrentPuzzleIndex,0,savedPuzzlePools.Count - 1);
        if (savedPuzzleData == null)
            savedPuzzleData = savedPuzzlePools[activePoolCurrentPuzzleIndex];
    }

    public void ClearSelectedPuzzlePool()
    {
        savedPuzzleData = null;
        savedPuzzlePools.Clear();
        activePoolId = string.Empty;
        activePoolLabel = string.Empty;
        requiredSolveRateToUnlockNext = 1f;
        nextPoolIdToUnlock = string.Empty;
        activePoolOrderInitialized = false;
        activePoolCurrentPuzzleIndex = 0;
        activePoolSolvedCount = 0;
        activePoolFailedCount = 0;
        activePoolFirstResultStates.Clear();
    }

    private void RefreshLessonSlidePuzzleData()
    {
        if (activeLessonData == null || activeLessonData.slides == null || activeLessonData.slides.Count == 0)
        {
            savedPuzzleData = null;
            return;
        }

        GoLessonSlideData currentSlide = activeLessonData.slides[Mathf.Clamp(activeLessonSlideIndex,0,activeLessonData.slides.Count - 1)];
        savedPuzzleData = currentSlide != null ? currentSlide.GetBoardTextAsset() : null;
    }

    private void ResetFirstResultStates()
    {
        activePoolFirstResultStates.Clear();

        for (int i = 0; i < savedPuzzlePools.Count; i++)
            activePoolFirstResultStates.Add(UnresolvedPuzzleResult);
    }
}
