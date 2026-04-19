using System;
using System.Collections.Generic;
using UnityEngine;

public enum GoLessonSlideBoardSource
{
    JsonFile = 0,
    InlineGrid = 1
}

public enum GoLessonInlineAnnotationType
{
    None = 0,
    Number = 1,
    Triangle = 2,
    Square = 3
}

[Serializable]
public class GoLessonPuzzleJsonData
{
    public int boardSize;
    public int[] boardFlat;
    public List<GoLessonPuzzleJsonMove> moves = new();
    public List<GoLessonPuzzleJsonAnnotation> annotations = new();

    public bool HasValidBoard => boardSize > 0 && boardFlat != null && boardFlat.Length == boardSize * boardSize;
    public bool HasMoves => moves != null && moves.Count > 0;
    public bool HasAnnotations => annotations != null && annotations.Count > 0;
}

[Serializable]
public class GoLessonPuzzleJsonMove
{
    public string playerMove;
    public string aiMove;
    public bool isKoMove;
    public List<GoLessonPuzzleJsonSubMove> correctMoves = new();
    public List<GoLessonPuzzleJsonSubMove> wrongMoves = new();
}

[Serializable]
public class GoLessonPuzzleJsonSubMove
{
    public string playerMove;
    public string aiMove;
    public bool isKoMove;
}

[Serializable]
public class GoLessonPuzzleJsonAnnotation
{
    public int row;
    public int col;
    public int annotationType;
    public int numberValue;
}

public static class GoLessonBoardJsonUtility
{
    public const int MinBoardSize = 5;
    public const int MaxBoardSize = 19;
    public const int DefaultBoardSize = 9;

    public static int ClampBoardSize(int boardSize)
    {
        return Mathf.Clamp(boardSize,MinBoardSize,MaxBoardSize);
    }

    public static int[] CreateEmptyBoardFlat(int boardSize)
    {
        int clampedSize = ClampBoardSize(boardSize);
        return new int[clampedSize * clampedSize];
    }

    public static int[] ResizeBoardFlat(int[] existingBoardFlat,int existingBoardSize,int newBoardSize)
    {
        int normalizedExistingSize = ClampBoardSize(existingBoardSize <= 0 ? DefaultBoardSize : existingBoardSize);
        int normalizedNewSize = ClampBoardSize(newBoardSize);

        int[] resizedBoard = new int[normalizedNewSize * normalizedNewSize];
        if (existingBoardFlat == null || existingBoardFlat.Length == 0)
            return resizedBoard;

        int copySize = Mathf.Min(normalizedExistingSize,normalizedNewSize);
        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedExistingSize + x;
                if (sourceIndex < 0 || sourceIndex >= existingBoardFlat.Length)
                    continue;

                resizedBoard[y * normalizedNewSize + x] = Mathf.Clamp(existingBoardFlat[sourceIndex],0,2);
            }
        }

        return resizedBoard;
    }

    public static int ClampAnnotationType(int annotationType)
    {
        return Mathf.Clamp(annotationType,(int)GoLessonInlineAnnotationType.None,(int)GoLessonInlineAnnotationType.Square);
    }

    public static int[] ResizeAnnotationTypeFlat(int[] existingAnnotationTypeFlat,int existingBoardSize,int newBoardSize)
    {
        int normalizedExistingSize = ClampBoardSize(existingBoardSize <= 0 ? DefaultBoardSize : existingBoardSize);
        int normalizedNewSize = ClampBoardSize(newBoardSize);

        int[] resizedAnnotations = new int[normalizedNewSize * normalizedNewSize];
        if (existingAnnotationTypeFlat == null || existingAnnotationTypeFlat.Length == 0)
            return resizedAnnotations;

        int copySize = Mathf.Min(normalizedExistingSize,normalizedNewSize);
        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedExistingSize + x;
                if (sourceIndex < 0 || sourceIndex >= existingAnnotationTypeFlat.Length)
                    continue;

                resizedAnnotations[y * normalizedNewSize + x] = ClampAnnotationType(existingAnnotationTypeFlat[sourceIndex]);
            }
        }

        return resizedAnnotations;
    }

    public static int[] ResizeAnnotationNumberFlat(int[] existingAnnotationNumberFlat,int existingBoardSize,int newBoardSize)
    {
        int normalizedExistingSize = ClampBoardSize(existingBoardSize <= 0 ? DefaultBoardSize : existingBoardSize);
        int normalizedNewSize = ClampBoardSize(newBoardSize);

        int[] resizedNumbers = new int[normalizedNewSize * normalizedNewSize];
        if (existingAnnotationNumberFlat == null || existingAnnotationNumberFlat.Length == 0)
            return resizedNumbers;

        int copySize = Mathf.Min(normalizedExistingSize,normalizedNewSize);
        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedExistingSize + x;
                if (sourceIndex < 0 || sourceIndex >= existingAnnotationNumberFlat.Length)
                    continue;

                resizedNumbers[y * normalizedNewSize + x] = Mathf.Max(0,existingAnnotationNumberFlat[sourceIndex]);
            }
        }

        return resizedNumbers;
    }

    public static bool TryParseTextAsset(TextAsset boardJsonFile,out GoLessonPuzzleJsonData parsedData,out string errorMessage)
    {
        if (boardJsonFile == null)
        {
            parsedData = null;
            errorMessage = "Assign a JSON file to preview its board.";
            return false;
        }

        return TryParseJson(boardJsonFile.text,out parsedData,out errorMessage);
    }

    public static bool TryParseJson(string json,out GoLessonPuzzleJsonData parsedData,out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            parsedData = null;
            errorMessage = "The JSON file is empty.";
            return false;
        }

        try
        {
            parsedData = JsonUtility.FromJson<GoLessonPuzzleJsonData>(json);
        }
        catch (Exception exception)
        {
            parsedData = null;
            errorMessage = $"Could not parse the JSON file.\n{exception.Message}";
            return false;
        }

        if (parsedData == null)
        {
            errorMessage = "Could not parse the JSON file.";
            return false;
        }

        if (parsedData.boardSize <= 0)
        {
            errorMessage = "The JSON file does not contain a valid board size.";
            return false;
        }

        if (parsedData.boardFlat == null || parsedData.boardFlat.Length != parsedData.boardSize * parsedData.boardSize)
        {
            errorMessage = "The JSON file does not contain a valid board grid.";
            return false;
        }

        parsedData.moves ??= new List<GoLessonPuzzleJsonMove>();
        parsedData.annotations ??= new List<GoLessonPuzzleJsonAnnotation>();

        for (int i = 0; i < parsedData.annotations.Count; i++)
        {
            GoLessonPuzzleJsonAnnotation annotation = parsedData.annotations[i];
            if (annotation == null)
                continue;

            annotation.annotationType = ClampAnnotationType(annotation.annotationType);
            annotation.numberValue = Mathf.Max(0,annotation.numberValue);
        }

        errorMessage = string.Empty;
        return true;
    }

    public static string BuildJson(int boardSize,int[] boardFlat)
    {
        return BuildJson(boardSize,boardFlat,null,null);
    }

    public static string BuildJson(int boardSize,int[] boardFlat,int[] annotationTypeFlat,int[] annotationNumberFlat)
    {
        int normalizedBoardSize = ClampBoardSize(boardSize);
        int[] normalizedBoardFlat = ResizeBoardFlat(boardFlat,normalizedBoardSize,normalizedBoardSize);
        int[] normalizedAnnotationTypes = ResizeAnnotationTypeFlat(annotationTypeFlat,normalizedBoardSize,normalizedBoardSize);
        int[] normalizedAnnotationNumbers = ResizeAnnotationNumberFlat(annotationNumberFlat,normalizedBoardSize,normalizedBoardSize);

        List<GoLessonPuzzleJsonAnnotation> annotations = new();
        for (int y = 0; y < normalizedBoardSize; y++)
        {
            for (int x = 0; x < normalizedBoardSize; x++)
            {
                int index = y * normalizedBoardSize + x;
                int annotationType = ClampAnnotationType(normalizedAnnotationTypes[index]);
                if (annotationType == (int)GoLessonInlineAnnotationType.None)
                    continue;

                GoLessonPuzzleJsonAnnotation annotation = new()
                {
                    row = y + 1,
                    col = x + 1,
                    annotationType = annotationType,
                    numberValue = annotationType == (int)GoLessonInlineAnnotationType.Number
                        ? Mathf.Max(0,normalizedAnnotationNumbers[index])
                        : 0
                };
                annotations.Add(annotation);
            }
        }

        GoLessonPuzzleJsonData jsonData = new()
        {
            boardSize = normalizedBoardSize,
            boardFlat = normalizedBoardFlat,
            moves = new List<GoLessonPuzzleJsonMove>(),
            annotations = annotations
        };

        return JsonUtility.ToJson(jsonData);
    }
}
