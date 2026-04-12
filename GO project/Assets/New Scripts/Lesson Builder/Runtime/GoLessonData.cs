using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GoLesson",menuName = "Go Lessons/Lesson Data")]
public class GoLessonData : ScriptableObject
{
    public string lessonTitle = "New Lesson";
    public string lessonId = "new_lesson";

    public List<GoLessonSlideData> slides = new();

    public int SlideCount => slides != null ? slides.Count : 0;

    public GoLessonSlideData GetSlide(int slideIndex)
    {
        if (slides == null || slides.Count == 0)
            return null;

        int clampedIndex = Mathf.Clamp(slideIndex,0,slides.Count - 1);
        return slides[clampedIndex];
    }

    public int ClampSlideIndex(int slideIndex)
    {
        if (slides == null || slides.Count == 0)
            return 0;

        return Mathf.Clamp(slideIndex,0,slides.Count - 1);
    }

    public string GetDisplayTitle()
    {
        if (!string.IsNullOrWhiteSpace(lessonTitle))
            return lessonTitle;

        return string.IsNullOrWhiteSpace(name) ? "Lesson" : name;
    }
}

[Serializable]
public class GoLessonSlideData
{
    public string slideName = "New Slide";
    public GoLessonSlideType slideType = GoLessonSlideType.Content;

    [TextArea(3,10)]
    public string bodyText = string.Empty;
    public GoLessonSlideBoardSource boardSource = GoLessonSlideBoardSource.JsonFile;
    public TextAsset boardJsonFile;
    public int inlineBoardSize = GoLessonBoardJsonUtility.DefaultBoardSize;
    public int inlineCurrentPlayer = 1;
    public int[] inlineBoardFlat = new int[GoLessonBoardJsonUtility.DefaultBoardSize * GoLessonBoardJsonUtility.DefaultBoardSize];

    public bool correctYesAnswer = true;
    public int correctNumberAnswer = 0;

    public bool RequiresPuzzleCompletion => slideType == GoLessonSlideType.Puzzle;
    public bool UsesYesNoAnswer => slideType == GoLessonSlideType.YesNo;
    public bool UsesNumberAnswer => slideType == GoLessonSlideType.Number;
    public bool UsesInlineGrid => boardSource == GoLessonSlideBoardSource.InlineGrid;
    public bool HasBoardReference => UsesInlineGrid || boardJsonFile != null;

    public string GetDisplayName(int slideIndex)
    {
        if (!string.IsNullOrWhiteSpace(slideName))
            return slideName;

        return $"Slide {slideIndex + 1}";
    }

    public void EnsureInlineBoardData()
    {
        int normalizedBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(inlineBoardSize);
        inlineBoardFlat = GoLessonBoardJsonUtility.ResizeBoardFlat(inlineBoardFlat,inlineBoardSize,normalizedBoardSize);
        inlineBoardSize = normalizedBoardSize;
        inlineCurrentPlayer = inlineCurrentPlayer == 2 ? 2 : 1;
    }

    public void ResizeInlineBoard(int newBoardSize)
    {
        EnsureInlineBoardData();
        int normalizedBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(newBoardSize);
        inlineBoardFlat = GoLessonBoardJsonUtility.ResizeBoardFlat(inlineBoardFlat,inlineBoardSize,normalizedBoardSize);
        inlineBoardSize = normalizedBoardSize;
    }

    public TextAsset GetBoardTextAsset()
    {
        if (!UsesInlineGrid)
            return boardJsonFile;

        EnsureInlineBoardData();

        TextAsset runtimeBoardTextAsset = new TextAsset(GoLessonBoardJsonUtility.BuildJson(inlineBoardSize,inlineBoardFlat));
        runtimeBoardTextAsset.name = string.IsNullOrWhiteSpace(slideName) ? "LessonInlineBoard" : $"{slideName}_InlineBoard";

        return runtimeBoardTextAsset;
    }
}

public enum GoLessonSlideType
{
    Content = 0,
    Puzzle = 1,
    YesNo = 2,
    Number = 3
}
