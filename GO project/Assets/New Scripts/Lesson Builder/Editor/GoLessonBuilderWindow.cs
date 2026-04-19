using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GoLessonBuilderWindow : EditorWindow
{
    private const string LessonInfoProperty = "lessonTitle";
    private const string LessonIdProperty = "lessonId";
    private const string SlidesProperty = "slides";
    private const string SlideNameProperty = "slideName";
    private const string SlideTypeProperty = "slideType";
    private const string SlideBodyTextProperty = "bodyText";
    private const string SlideBoardSourceProperty = "boardSource";
    private const string SlideBoardJsonProperty = "boardJsonFile";
    private const string SlideInlineBoardSizeProperty = "inlineBoardSize";
    private const string SlideInlineCurrentPlayerProperty = "inlineCurrentPlayer";
    private const string SlideInlineBoardFlatProperty = "inlineBoardFlat";
    private const string SlideInlineAnnotationTypeFlatProperty = "inlineAnnotationTypeFlat";
    private const string SlideInlineAnnotationNumberFlatProperty = "inlineAnnotationNumberFlat";
    private const string SlideCorrectYesProperty = "correctYesAnswer";
    private const string SlideCorrectNumberProperty = "correctNumberAnswer";

    private const float BoardLabelWidth = 48f;
    private const float BoardCellSize = 30f;

    private readonly Dictionary<string,bool> slideFoldouts = new();
    private readonly Dictionary<string,bool> inlineGridVisibility = new();

    private GoLessonData selectedLesson;
    private SerializedObject serializedLesson;
    private Vector2 scrollPosition;
    private GoLessonInlineAnnotationType inlineAnnotationPaintTool = GoLessonInlineAnnotationType.Triangle;
    private int inlineAnnotationPaintNumber = 1;

    [MenuItem("Tools/Go Lesson Builder")]
    public static void OpenWindow()
    {
        GetWindow<GoLessonBuilderWindow>("Go Lesson Builder");
    }

    public static void OpenWindow(GoLessonData lessonData)
    {
        GoLessonBuilderWindow window = GetWindow<GoLessonBuilderWindow>("Go Lesson Builder");
        window.SetSelectedLesson(lessonData);
    }

    private void OnEnable()
    {
        if (selectedLesson == null && Selection.activeObject is GoLessonData lessonData)
            SetSelectedLesson(lessonData);
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is GoLessonData lessonData)
        {
            SetSelectedLesson(lessonData);
            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawLessonSelection();

        if (selectedLesson == null)
        {
            EditorGUILayout.HelpBox("Select an existing lesson asset or create a new one to start building slides.",MessageType.Info);
            return;
        }

        EnsureSerializedLesson();
        serializedLesson.Update();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawLessonMetadata();
        GUILayout.Space(8f);
        DrawSlides();
        EditorGUILayout.EndScrollView();

        serializedLesson.ApplyModifiedProperties();
        if (GUI.changed)
            EditorUtility.SetDirty(selectedLesson);
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Go Lesson Builder",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Author lesson slides, then launch them in PuzzleLeveling through LessonSceneLauncher.",EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(8f);
    }

    private void DrawLessonSelection()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GoLessonData lessonSelection = (GoLessonData)EditorGUILayout.ObjectField("Lesson Asset",selectedLesson,typeof(GoLessonData),false);
        if (lessonSelection != selectedLesson)
            SetSelectedLesson(lessonSelection);

        if (GUILayout.Button("New Lesson",GUILayout.Width(100f)))
            CreateLessonAsset();

        using (new EditorGUI.DisabledScope(selectedLesson == null))
        {
            if (GUILayout.Button("Ping",GUILayout.Width(60f)))
                EditorGUIUtility.PingObject(selectedLesson);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLessonMetadata()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Lesson",EditorStyles.boldLabel);

        SerializedProperty lessonTitle = serializedLesson.FindProperty(LessonInfoProperty);
        SerializedProperty lessonId = serializedLesson.FindProperty(LessonIdProperty);

        EditorGUILayout.PropertyField(lessonTitle,new GUIContent("Lesson Name"));
        EditorGUILayout.PropertyField(lessonId,new GUIContent("Lesson Id"));
        DrawAssetFileNameField();
        EditorGUILayout.EndVertical();
    }

    private void DrawAssetFileNameField()
    {
        if (selectedLesson == null)
            return;

        string assetPath = AssetDatabase.GetAssetPath(selectedLesson);
        string assetFileName = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : Path.GetFileNameWithoutExtension(assetPath);
        string newAssetFileName = EditorGUILayout.DelayedTextField("Asset File Name",assetFileName);

        if (string.IsNullOrWhiteSpace(newAssetFileName) || newAssetFileName == assetFileName)
            return;

        string renameResult = AssetDatabase.RenameAsset(assetPath,newAssetFileName);
        if (!string.IsNullOrWhiteSpace(renameResult))
            Debug.LogWarning($"Could not rename lesson asset: {renameResult}");
    }

    private void DrawSlides()
    {
        SerializedProperty slides = serializedLesson.FindProperty(SlidesProperty);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Slides ({slides.arraySize})",EditorStyles.boldLabel);

        if (GUILayout.Button("+ New Slide",GUILayout.Width(110f)))
            AddSlide(slides);

        EditorGUILayout.EndHorizontal();

        if (slides.arraySize == 0)
        {
            EditorGUILayout.HelpBox("This lesson has no slides yet.",MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < slides.arraySize; i++)
        {
            SerializedProperty slideProperty = slides.GetArrayElementAtIndex(i);
            DrawSlide(slides,slideProperty,i);
            GUILayout.Space(6f);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSlide(SerializedProperty slides,SerializedProperty slideProperty,int slideIndex)
    {
        SerializedProperty slideName = slideProperty.FindPropertyRelative(SlideNameProperty);
        SerializedProperty slideType = slideProperty.FindPropertyRelative(SlideTypeProperty);
        SerializedProperty bodyText = slideProperty.FindPropertyRelative(SlideBodyTextProperty);
        SerializedProperty boardSource = slideProperty.FindPropertyRelative(SlideBoardSourceProperty);
        SerializedProperty correctYesAnswer = slideProperty.FindPropertyRelative(SlideCorrectYesProperty);
        SerializedProperty correctNumberAnswer = slideProperty.FindPropertyRelative(SlideCorrectNumberProperty);

        string slideTitle = string.IsNullOrWhiteSpace(slideName.stringValue) ? $"Slide {slideIndex + 1}" : slideName.stringValue;
        string slideTypeLabel = ((GoLessonSlideType)slideType.enumValueIndex).ToString();
        string foldoutKey = slideProperty.propertyPath;
        bool isExpanded = GetSlideFoldoutState(foldoutKey);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        bool nextExpanded = EditorGUILayout.Foldout(isExpanded,$"{slideIndex + 1}. {slideTitle} [{slideTypeLabel}]",true);
        if (nextExpanded != isExpanded)
            slideFoldouts[foldoutKey] = nextExpanded;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Duplicate",GUILayout.Width(78f)))
        {
            DuplicateSlide(slides,slideIndex);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        using (new EditorGUI.DisabledScope(slideIndex <= 0))
        {
            if (GUILayout.Button("Up",GUILayout.Width(44f)))
            {
                slides.MoveArrayElement(slideIndex,slideIndex - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }

        using (new EditorGUI.DisabledScope(slideIndex >= slides.arraySize - 1))
        {
            if (GUILayout.Button("Down",GUILayout.Width(52f)))
            {
                slides.MoveArrayElement(slideIndex,slideIndex + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }

        if (GUILayout.Button("Delete",GUILayout.Width(60f)))
        {
            slides.DeleteArrayElementAtIndex(slideIndex);
            slideFoldouts.Remove(foldoutKey);
            inlineGridVisibility.Remove(GetInlineGridVisibilityKey(foldoutKey));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (!nextExpanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.PropertyField(slideName,new GUIContent("Slide Name"));
        EditorGUILayout.PropertyField(slideType,new GUIContent("Slide Type"));
        EditorGUILayout.PropertyField(boardSource,new GUIContent("Board Source"));

        GoLessonSlideType selectedType = (GoLessonSlideType)slideType.enumValueIndex;
        GoLessonSlideBoardSource selectedBoardSource = (GoLessonSlideBoardSource)boardSource.enumValueIndex;

        if (selectedBoardSource == GoLessonSlideBoardSource.JsonFile)
            DrawJsonBoardSection(slideProperty,selectedType);
        else
            DrawInlineBoardSection(slideProperty,selectedType);

        EditorGUILayout.PropertyField(bodyText,new GUIContent("Enter Text"));

        if (selectedType == GoLessonSlideType.YesNo)
            EditorGUILayout.PropertyField(correctYesAnswer,new GUIContent("Correct Answer"));
        else if (selectedType == GoLessonSlideType.Number)
            EditorGUILayout.PropertyField(correctNumberAnswer,new GUIContent("Correct Number"));

        EditorGUILayout.EndVertical();
    }

    private void DrawJsonBoardSection(SerializedProperty slideProperty,GoLessonSlideType slideType)
    {
        SerializedProperty boardJsonFile = slideProperty.FindPropertyRelative(SlideBoardJsonProperty);
        EditorGUILayout.PropertyField(boardJsonFile,new GUIContent("Json File"));

        TextAsset boardAsset = boardJsonFile.objectReferenceValue as TextAsset;
        if (boardAsset == null)
        {
            EditorGUILayout.HelpBox("Assign a JSON file, or switch Board Source to Inline Grid to build the board here instead.",MessageType.Info);
            return;
        }

        if (!GoLessonBoardJsonUtility.TryParseTextAsset(boardAsset,out GoLessonPuzzleJsonData parsedBoard,out string errorMessage))
        {
            EditorGUILayout.HelpBox(errorMessage,MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Copy Board + Annotations To Inline Grid",GUILayout.Width(260f)))
            CopyJsonBoardToInlineGrid(slideProperty,parsedBoard);
        EditorGUILayout.EndHorizontal();

        if (slideType == GoLessonSlideType.Puzzle && !parsedBoard.HasMoves)
            EditorGUILayout.HelpBox("This JSON only contains board state. Puzzle slides still need preset move data to behave like a puzzle.",MessageType.Warning);
        else if (parsedBoard.HasMoves)
            EditorGUILayout.HelpBox($"This JSON includes {parsedBoard.moves.Count} move group(s). File mode preserves that puzzle data.",MessageType.Info);
        if (parsedBoard.HasAnnotations)
            EditorGUILayout.HelpBox($"This JSON includes {parsedBoard.annotations.Count} annotation(s).",MessageType.Info);

        EditorGUILayout.LabelField($"Board Preview ({parsedBoard.boardSize}x{parsedBoard.boardSize})",EditorStyles.miniBoldLabel);
        DrawReadOnlyBoardGrid(parsedBoard.boardSize,parsedBoard.boardFlat);
    }

    private void DrawInlineBoardSection(SerializedProperty slideProperty,GoLessonSlideType slideType)
    {
        SerializedProperty inlineBoardSize = slideProperty.FindPropertyRelative(SlideInlineBoardSizeProperty);
        SerializedProperty inlineCurrentPlayer = slideProperty.FindPropertyRelative(SlideInlineCurrentPlayerProperty);
        SerializedProperty inlineBoardFlat = slideProperty.FindPropertyRelative(SlideInlineBoardFlatProperty);
        SerializedProperty inlineAnnotationTypeFlat = slideProperty.FindPropertyRelative(SlideInlineAnnotationTypeFlatProperty);
        SerializedProperty inlineAnnotationNumberFlat = slideProperty.FindPropertyRelative(SlideInlineAnnotationNumberFlatProperty);
        string visibilityKey = GetInlineGridVisibilityKey(slideProperty.propertyPath);

        EnsureInlineBoardData(inlineBoardSize,inlineCurrentPlayer,inlineBoardFlat,inlineAnnotationTypeFlat,inlineAnnotationNumberFlat);

        bool isGridVisible = GetInlineGridVisibilityState(visibilityKey);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Inline Board ({inlineBoardSize.intValue}x{inlineBoardSize.intValue})",EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(isGridVisible ? "Hide Grid" : "Show Grid",GUILayout.Width(90f)))
        {
            isGridVisible = !isGridVisible;
            inlineGridVisibility[visibilityKey] = isGridVisible;
        }
        EditorGUILayout.EndHorizontal();

        if (!isGridVisible)
        {
            EditorGUILayout.HelpBox(
                $"Board Size: {inlineBoardSize.intValue}x{inlineBoardSize.intValue}\nCurrent Player: {(inlineCurrentPlayer.intValue == 1 ? "Black" : "White")}\nAnnotations: {GetInlineAnnotationCount(inlineAnnotationTypeFlat)}",
                MessageType.None);
            if (slideType == GoLessonSlideType.Puzzle)
                EditorGUILayout.HelpBox("Inline Grid stores board + annotations but not preset puzzle move trees. Switch this slide back to Json File if it needs move data.",MessageType.Warning);
            return;
        }

        int currentBoardSize = inlineBoardSize.intValue;
        int newBoardSize = EditorGUILayout.IntSlider("Board Size",currentBoardSize,GoLessonBoardJsonUtility.MinBoardSize,GoLessonBoardJsonUtility.MaxBoardSize);
        if (newBoardSize != currentBoardSize)
            ResizeInlineBoard(inlineBoardSize,inlineBoardFlat,inlineAnnotationTypeFlat,inlineAnnotationNumberFlat,newBoardSize);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Switch Player",GUILayout.Width(110f)))
            inlineCurrentPlayer.intValue = inlineCurrentPlayer.intValue == 1 ? 2 : 1;
        EditorGUILayout.LabelField($"Current Player: {(inlineCurrentPlayer.intValue == 1 ? "Black (1)" : "White (2)")}",EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Storage matches runtime: boardFlat[0] = bottom row, left column. Bottom-left is (1,1).",MessageType.Info);
        if (slideType == GoLessonSlideType.Puzzle)
            EditorGUILayout.HelpBox("Inline Grid stores board + annotations but not preset puzzle move trees. Switch this slide back to Json File if it needs move data.",MessageType.Warning);

        DrawEditableBoardGrid(inlineBoardSize.intValue,inlineBoardFlat,inlineCurrentPlayer.intValue);
        GUILayout.Space(8f);
        DrawEditableAnnotationGrid(inlineBoardSize.intValue,inlineAnnotationTypeFlat,inlineAnnotationNumberFlat);
    }

    private void DrawEditableBoardGrid(int boardSize,SerializedProperty boardFlat,int currentPlayer)
    {
        DrawBoardColumnLabels(boardSize);

        for (int y = boardSize - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((y + 1).ToString(),GUILayout.Width(BoardLabelWidth),GUILayout.Height(BoardCellSize));

            for (int x = 0; x < boardSize; x++)
            {
                int index = y * boardSize + x;
                SerializedProperty boardCell = boardFlat.GetArrayElementAtIndex(index);
                int currentValue = Mathf.Clamp(boardCell.intValue,0,2);
                GUIContent cellContent = new(GetStoneLabel(currentValue),$"(row,col)=({y + 1},{x + 1})");

                if (GUILayout.Button(cellContent,GUILayout.Width(BoardCellSize),GUILayout.Height(BoardCellSize)))
                    boardCell.intValue = currentValue == 0 ? currentPlayer : 0;
            }

            EditorGUILayout.EndHorizontal();
        }

        DrawBoardColumnLabels(boardSize);
    }

    private void DrawEditableAnnotationGrid(int boardSize,SerializedProperty annotationTypeFlat,SerializedProperty annotationNumberFlat)
    {
        EditorGUILayout.LabelField("Inline Annotations",EditorStyles.miniBoldLabel);
        inlineAnnotationPaintTool = (GoLessonInlineAnnotationType)EditorGUILayout.EnumPopup("Annotation Tool",inlineAnnotationPaintTool);
        if (inlineAnnotationPaintTool == GoLessonInlineAnnotationType.Number)
            inlineAnnotationPaintNumber = Mathf.Max(0,EditorGUILayout.IntField("Number Value",inlineAnnotationPaintNumber));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All Annotations",GUILayout.Width(150f)))
            ClearAllAnnotations(annotationTypeFlat,annotationNumberFlat);
        EditorGUILayout.LabelField("Click a cell to paint the selected annotation. Use `None` to erase.",EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        DrawBoardColumnLabels(boardSize);

        for (int y = boardSize - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((y + 1).ToString(),GUILayout.Width(BoardLabelWidth),GUILayout.Height(BoardCellSize));

            for (int x = 0; x < boardSize; x++)
            {
                int index = y * boardSize + x;
                SerializedProperty typeCell = annotationTypeFlat.GetArrayElementAtIndex(index);
                SerializedProperty numberCell = annotationNumberFlat.GetArrayElementAtIndex(index);
                int currentType = GoLessonBoardJsonUtility.ClampAnnotationType(typeCell.intValue);
                int currentNumber = Mathf.Max(0,numberCell.intValue);

                GUIContent cellContent = new(GetAnnotationLabel(currentType,currentNumber),$"(row,col)=({y + 1},{x + 1})");
                if (GUILayout.Button(cellContent,GUILayout.Width(BoardCellSize),GUILayout.Height(BoardCellSize)))
                    ApplyAnnotationBrushToCell(typeCell,numberCell,currentType,currentNumber);
            }

            EditorGUILayout.EndHorizontal();
        }

        DrawBoardColumnLabels(boardSize);
    }

    private void DrawReadOnlyBoardGrid(int boardSize,int[] boardFlat)
    {
        DrawBoardColumnLabels(boardSize);

        for (int y = boardSize - 1; y >= 0; y--)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((y + 1).ToString(),GUILayout.Width(BoardLabelWidth),GUILayout.Height(BoardCellSize));

            for (int x = 0; x < boardSize; x++)
            {
                int index = y * boardSize + x;
                int currentValue = index >= 0 && index < boardFlat.Length ? Mathf.Clamp(boardFlat[index],0,2) : 0;
                GUIContent cellContent = new(GetStoneLabel(currentValue),$"(row,col)=({y + 1},{x + 1})");
                GUILayout.Box(cellContent,GUILayout.Width(BoardCellSize),GUILayout.Height(BoardCellSize));
            }

            EditorGUILayout.EndHorizontal();
        }

        DrawBoardColumnLabels(boardSize);
    }

    private void DrawBoardColumnLabels(int boardSize)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(BoardLabelWidth);

        for (int x = 1; x <= boardSize; x++)
            GUILayout.Label(x.ToString(),GUILayout.Width(BoardCellSize),GUILayout.Height(22f));

        EditorGUILayout.EndHorizontal();
    }

    private void CopyJsonBoardToInlineGrid(SerializedProperty slideProperty,GoLessonPuzzleJsonData parsedBoard)
    {
        SerializedProperty boardSource = slideProperty.FindPropertyRelative(SlideBoardSourceProperty);
        SerializedProperty inlineBoardSize = slideProperty.FindPropertyRelative(SlideInlineBoardSizeProperty);
        SerializedProperty inlineCurrentPlayer = slideProperty.FindPropertyRelative(SlideInlineCurrentPlayerProperty);
        SerializedProperty inlineBoardFlat = slideProperty.FindPropertyRelative(SlideInlineBoardFlatProperty);
        SerializedProperty inlineAnnotationTypeFlat = slideProperty.FindPropertyRelative(SlideInlineAnnotationTypeFlatProperty);
        SerializedProperty inlineAnnotationNumberFlat = slideProperty.FindPropertyRelative(SlideInlineAnnotationNumberFlatProperty);

        int normalizedBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(parsedBoard.boardSize);
        inlineBoardSize.intValue = normalizedBoardSize;
        inlineCurrentPlayer.intValue = 1;
        ResizeBoardArray(inlineBoardFlat,GuessBoardSizeFromArraySize(inlineBoardFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
        ResizeAnnotationTypeArray(inlineAnnotationTypeFlat,GuessBoardSizeFromArraySize(inlineAnnotationTypeFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
        ResizeAnnotationNumberArray(inlineAnnotationNumberFlat,GuessBoardSizeFromArraySize(inlineAnnotationNumberFlat.arraySize,normalizedBoardSize),normalizedBoardSize);

        for (int i = 0; i < inlineBoardFlat.arraySize; i++)
        {
            int value = i < parsedBoard.boardFlat.Length ? parsedBoard.boardFlat[i] : 0;
            inlineBoardFlat.GetArrayElementAtIndex(i).intValue = Mathf.Clamp(value,0,2);
            inlineAnnotationTypeFlat.GetArrayElementAtIndex(i).intValue = 0;
            inlineAnnotationNumberFlat.GetArrayElementAtIndex(i).intValue = 0;
        }

        if (parsedBoard.annotations != null)
        {
            for (int i = 0; i < parsedBoard.annotations.Count; i++)
            {
                GoLessonPuzzleJsonAnnotation annotation = parsedBoard.annotations[i];
                if (annotation == null)
                    continue;

                int row = annotation.row - 1;
                int col = annotation.col - 1;
                if (row < 0 || col < 0 || row >= normalizedBoardSize || col >= normalizedBoardSize)
                    continue;

                int index = row * normalizedBoardSize + col;
                int annotationType = GoLessonBoardJsonUtility.ClampAnnotationType(annotation.annotationType);
                inlineAnnotationTypeFlat.GetArrayElementAtIndex(index).intValue = annotationType;
                inlineAnnotationNumberFlat.GetArrayElementAtIndex(index).intValue = annotationType == (int)GoLessonInlineAnnotationType.Number
                    ? Mathf.Max(0,annotation.numberValue)
                    : 0;
            }
        }

        boardSource.enumValueIndex = (int)GoLessonSlideBoardSource.InlineGrid;
    }

    private void EnsureInlineBoardData(
        SerializedProperty inlineBoardSize,
        SerializedProperty inlineCurrentPlayer,
        SerializedProperty inlineBoardFlat,
        SerializedProperty inlineAnnotationTypeFlat,
        SerializedProperty inlineAnnotationNumberFlat)
    {
        int normalizedBoardSize = GuessBoardSizeFromArraySize(inlineBoardFlat.arraySize,inlineBoardSize.intValue);
        normalizedBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(normalizedBoardSize);
        int expectedArraySize = normalizedBoardSize * normalizedBoardSize;

        if (inlineBoardSize.intValue != normalizedBoardSize || inlineBoardFlat.arraySize != expectedArraySize)
        {
            ResizeBoardArray(inlineBoardFlat,GuessBoardSizeFromArraySize(inlineBoardFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
            ResizeAnnotationTypeArray(inlineAnnotationTypeFlat,GuessBoardSizeFromArraySize(inlineAnnotationTypeFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
            ResizeAnnotationNumberArray(inlineAnnotationNumberFlat,GuessBoardSizeFromArraySize(inlineAnnotationNumberFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
            inlineBoardSize.intValue = normalizedBoardSize;
        }
        else
        {
            if (inlineAnnotationTypeFlat.arraySize != expectedArraySize)
                ResizeAnnotationTypeArray(inlineAnnotationTypeFlat,GuessBoardSizeFromArraySize(inlineAnnotationTypeFlat.arraySize,normalizedBoardSize),normalizedBoardSize);

            if (inlineAnnotationNumberFlat.arraySize != expectedArraySize)
                ResizeAnnotationNumberArray(inlineAnnotationNumberFlat,GuessBoardSizeFromArraySize(inlineAnnotationNumberFlat.arraySize,normalizedBoardSize),normalizedBoardSize);
        }

        inlineCurrentPlayer.intValue = inlineCurrentPlayer.intValue == 2 ? 2 : 1;
        ClampBoardArrayValues(inlineBoardFlat);
        ClampAnnotationArrayValues(inlineAnnotationTypeFlat,inlineAnnotationNumberFlat);
    }

    private void ResizeInlineBoard(
        SerializedProperty inlineBoardSize,
        SerializedProperty inlineBoardFlat,
        SerializedProperty inlineAnnotationTypeFlat,
        SerializedProperty inlineAnnotationNumberFlat,
        int newBoardSize)
    {
        int oldBoardSize = GuessBoardSizeFromArraySize(inlineBoardFlat.arraySize,inlineBoardSize.intValue);
        int normalizedBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(newBoardSize);
        ResizeBoardArray(inlineBoardFlat,oldBoardSize,normalizedBoardSize);
        ResizeAnnotationTypeArray(inlineAnnotationTypeFlat,oldBoardSize,normalizedBoardSize);
        ResizeAnnotationNumberArray(inlineAnnotationNumberFlat,oldBoardSize,normalizedBoardSize);
        inlineBoardSize.intValue = normalizedBoardSize;
    }

    private void ResizeBoardArray(SerializedProperty boardFlat,int oldBoardSize,int newBoardSize)
    {
        int[] cachedValues = new int[boardFlat.arraySize];
        for (int i = 0; i < boardFlat.arraySize; i++)
            cachedValues[i] = Mathf.Clamp(boardFlat.GetArrayElementAtIndex(i).intValue,0,2);

        int normalizedOldBoardSize = oldBoardSize > 0 ? GoLessonBoardJsonUtility.ClampBoardSize(oldBoardSize) : 0;
        int normalizedNewBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(newBoardSize);
        int copySize = normalizedOldBoardSize > 0 ? Mathf.Min(normalizedOldBoardSize,normalizedNewBoardSize) : 0;

        boardFlat.arraySize = normalizedNewBoardSize * normalizedNewBoardSize;
        for (int i = 0; i < boardFlat.arraySize; i++)
            boardFlat.GetArrayElementAtIndex(i).intValue = 0;

        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedOldBoardSize + x;
                if (sourceIndex < 0 || sourceIndex >= cachedValues.Length)
                    continue;

                int destinationIndex = y * normalizedNewBoardSize + x;
                boardFlat.GetArrayElementAtIndex(destinationIndex).intValue = cachedValues[sourceIndex];
            }
        }
    }

    private void ClampBoardArrayValues(SerializedProperty boardFlat)
    {
        for (int i = 0; i < boardFlat.arraySize; i++)
        {
            SerializedProperty boardCell = boardFlat.GetArrayElementAtIndex(i);
            boardCell.intValue = Mathf.Clamp(boardCell.intValue,0,2);
        }
    }

    private void ResizeAnnotationTypeArray(SerializedProperty annotationTypeFlat,int oldBoardSize,int newBoardSize)
    {
        int[] cachedValues = new int[annotationTypeFlat.arraySize];
        for (int i = 0; i < annotationTypeFlat.arraySize; i++)
            cachedValues[i] = GoLessonBoardJsonUtility.ClampAnnotationType(annotationTypeFlat.GetArrayElementAtIndex(i).intValue);

        int normalizedOldBoardSize = oldBoardSize > 0 ? GoLessonBoardJsonUtility.ClampBoardSize(oldBoardSize) : 0;
        int normalizedNewBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(newBoardSize);
        int copySize = normalizedOldBoardSize > 0 ? Mathf.Min(normalizedOldBoardSize,normalizedNewBoardSize) : 0;

        annotationTypeFlat.arraySize = normalizedNewBoardSize * normalizedNewBoardSize;
        for (int i = 0; i < annotationTypeFlat.arraySize; i++)
            annotationTypeFlat.GetArrayElementAtIndex(i).intValue = 0;

        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedOldBoardSize + x;
                if (sourceIndex < 0 || sourceIndex >= cachedValues.Length)
                    continue;

                int destinationIndex = y * normalizedNewBoardSize + x;
                annotationTypeFlat.GetArrayElementAtIndex(destinationIndex).intValue = cachedValues[sourceIndex];
            }
        }
    }

    private void ResizeAnnotationNumberArray(SerializedProperty annotationNumberFlat,int oldBoardSize,int newBoardSize)
    {
        int[] cachedValues = new int[annotationNumberFlat.arraySize];
        for (int i = 0; i < annotationNumberFlat.arraySize; i++)
            cachedValues[i] = Mathf.Max(0,annotationNumberFlat.GetArrayElementAtIndex(i).intValue);

        int normalizedOldBoardSize = oldBoardSize > 0 ? GoLessonBoardJsonUtility.ClampBoardSize(oldBoardSize) : 0;
        int normalizedNewBoardSize = GoLessonBoardJsonUtility.ClampBoardSize(newBoardSize);
        int copySize = normalizedOldBoardSize > 0 ? Mathf.Min(normalizedOldBoardSize,normalizedNewBoardSize) : 0;

        annotationNumberFlat.arraySize = normalizedNewBoardSize * normalizedNewBoardSize;
        for (int i = 0; i < annotationNumberFlat.arraySize; i++)
            annotationNumberFlat.GetArrayElementAtIndex(i).intValue = 0;

        for (int y = 0; y < copySize; y++)
        {
            for (int x = 0; x < copySize; x++)
            {
                int sourceIndex = y * normalizedOldBoardSize + x;
                if (sourceIndex < 0 || sourceIndex >= cachedValues.Length)
                    continue;

                int destinationIndex = y * normalizedNewBoardSize + x;
                annotationNumberFlat.GetArrayElementAtIndex(destinationIndex).intValue = cachedValues[sourceIndex];
            }
        }
    }

    private void ClampAnnotationArrayValues(SerializedProperty annotationTypeFlat,SerializedProperty annotationNumberFlat)
    {
        int count = Mathf.Min(annotationTypeFlat.arraySize,annotationNumberFlat.arraySize);
        for (int i = 0; i < count; i++)
        {
            SerializedProperty typeCell = annotationTypeFlat.GetArrayElementAtIndex(i);
            SerializedProperty numberCell = annotationNumberFlat.GetArrayElementAtIndex(i);
            int clampedType = GoLessonBoardJsonUtility.ClampAnnotationType(typeCell.intValue);
            typeCell.intValue = clampedType;
            numberCell.intValue = clampedType == (int)GoLessonInlineAnnotationType.Number
                ? Mathf.Max(0,numberCell.intValue)
                : 0;
        }
    }

    private int GuessBoardSizeFromArraySize(int arraySize,int fallbackSize)
    {
        int roundedBoardSize = Mathf.RoundToInt(Mathf.Sqrt(arraySize));
        if (roundedBoardSize > 0 && roundedBoardSize * roundedBoardSize == arraySize)
            return roundedBoardSize;

        return fallbackSize <= 0 ? GoLessonBoardJsonUtility.DefaultBoardSize : fallbackSize;
    }

    private string GetStoneLabel(int boardValue)
    {
        return boardValue switch
        {
            1 => "●",
            2 => "○",
            _ => "·"
        };
    }

    private string GetAnnotationLabel(int annotationType,int annotationNumber)
    {
        return annotationType switch
        {
            (int)GoLessonInlineAnnotationType.Number => annotationNumber.ToString(),
            (int)GoLessonInlineAnnotationType.Triangle => "▲",
            (int)GoLessonInlineAnnotationType.Square => "■",
            _ => "·"
        };
    }

    private void ApplyAnnotationBrushToCell(SerializedProperty typeCell,SerializedProperty numberCell,int currentType,int currentNumber)
    {
        int brushType = (int)inlineAnnotationPaintTool;
        if (brushType == (int)GoLessonInlineAnnotationType.None)
        {
            typeCell.intValue = 0;
            numberCell.intValue = 0;
            return;
        }

        if (brushType == (int)GoLessonInlineAnnotationType.Number)
        {
            int paintValue = Mathf.Max(0,inlineAnnotationPaintNumber);
            bool shouldClear = currentType == brushType && currentNumber == paintValue;
            typeCell.intValue = shouldClear ? 0 : brushType;
            numberCell.intValue = shouldClear ? 0 : paintValue;
            return;
        }

        bool toggleOff = currentType == brushType;
        typeCell.intValue = toggleOff ? 0 : brushType;
        numberCell.intValue = 0;
    }

    private void ClearAllAnnotations(SerializedProperty annotationTypeFlat,SerializedProperty annotationNumberFlat)
    {
        int count = Mathf.Min(annotationTypeFlat.arraySize,annotationNumberFlat.arraySize);
        for (int i = 0; i < count; i++)
        {
            annotationTypeFlat.GetArrayElementAtIndex(i).intValue = 0;
            annotationNumberFlat.GetArrayElementAtIndex(i).intValue = 0;
        }
    }

    private int GetInlineAnnotationCount(SerializedProperty annotationTypeFlat)
    {
        int count = 0;
        for (int i = 0; i < annotationTypeFlat.arraySize; i++)
        {
            int annotationType = GoLessonBoardJsonUtility.ClampAnnotationType(annotationTypeFlat.GetArrayElementAtIndex(i).intValue);
            if (annotationType != (int)GoLessonInlineAnnotationType.None)
                count++;
        }

        return count;
    }

    private bool GetSlideFoldoutState(string foldoutKey)
    {
        if (!slideFoldouts.TryGetValue(foldoutKey,out bool isExpanded))
        {
            isExpanded = true;
            slideFoldouts[foldoutKey] = true;
        }

        return isExpanded;
    }

    private string GetInlineGridVisibilityKey(string slidePropertyPath)
    {
        return $"{slidePropertyPath}.InlineGridVisible";
    }

    private bool GetInlineGridVisibilityState(string visibilityKey)
    {
        if (!inlineGridVisibility.TryGetValue(visibilityKey,out bool isVisible))
        {
            isVisible = false;
            inlineGridVisibility[visibilityKey] = false;
        }

        return isVisible;
    }

    private void CreateLessonAsset()
    {
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Create Go Lesson",
            "NewGoLesson",
            "asset",
            "Choose where to save the new lesson asset.");

        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        GoLessonData lessonData = CreateInstance<GoLessonData>();
        lessonData.lessonTitle = Path.GetFileNameWithoutExtension(assetPath);
        lessonData.lessonId = SanitizeId(lessonData.lessonTitle);
        AssetDatabase.CreateAsset(lessonData,assetPath);
        AssetDatabase.SaveAssets();
        SetSelectedLesson(lessonData);
        Selection.activeObject = lessonData;
    }

    private void AddSlide(SerializedProperty slides)
    {
        int newIndex = slides.arraySize;
        slides.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newSlide = slides.GetArrayElementAtIndex(newIndex);
        ResetSlideValues(newSlide,newIndex);
    }

    private void DuplicateSlide(SerializedProperty slides,int sourceIndex)
    {
        slides.InsertArrayElementAtIndex(sourceIndex);
        slides.MoveArrayElement(sourceIndex,sourceIndex + 1);
    }

    private void ResetSlideValues(SerializedProperty slideProperty,int slideIndex)
    {
        slideProperty.FindPropertyRelative(SlideNameProperty).stringValue = $"Slide {slideIndex + 1}";
        slideProperty.FindPropertyRelative(SlideTypeProperty).enumValueIndex = (int)GoLessonSlideType.Content;
        slideProperty.FindPropertyRelative(SlideBodyTextProperty).stringValue = string.Empty;
        slideProperty.FindPropertyRelative(SlideBoardSourceProperty).enumValueIndex = (int)GoLessonSlideBoardSource.InlineGrid;
        slideProperty.FindPropertyRelative(SlideBoardJsonProperty).objectReferenceValue = null;
        slideProperty.FindPropertyRelative(SlideInlineBoardSizeProperty).intValue = GoLessonBoardJsonUtility.DefaultBoardSize;
        slideProperty.FindPropertyRelative(SlideInlineCurrentPlayerProperty).intValue = 1;
        ResizeBoardArray(slideProperty.FindPropertyRelative(SlideInlineBoardFlatProperty),0,GoLessonBoardJsonUtility.DefaultBoardSize);
        ResizeAnnotationTypeArray(slideProperty.FindPropertyRelative(SlideInlineAnnotationTypeFlatProperty),0,GoLessonBoardJsonUtility.DefaultBoardSize);
        ResizeAnnotationNumberArray(slideProperty.FindPropertyRelative(SlideInlineAnnotationNumberFlatProperty),0,GoLessonBoardJsonUtility.DefaultBoardSize);
        slideProperty.FindPropertyRelative(SlideCorrectYesProperty).boolValue = true;
        slideProperty.FindPropertyRelative(SlideCorrectNumberProperty).intValue = 0;
    }

    private void EnsureSerializedLesson()
    {
        if (selectedLesson == null)
        {
            serializedLesson = null;
            return;
        }

        if (serializedLesson == null || serializedLesson.targetObject != selectedLesson)
            serializedLesson = new SerializedObject(selectedLesson);
    }

    private void SetSelectedLesson(GoLessonData lessonData)
    {
        selectedLesson = lessonData;
        serializedLesson = lessonData != null ? new SerializedObject(lessonData) : null;
        slideFoldouts.Clear();
        inlineGridVisibility.Clear();
    }

    private string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "new_lesson";

        string trimmed = value.Trim().ToLowerInvariant();
        return trimmed.Replace(" ","_");
    }
}
