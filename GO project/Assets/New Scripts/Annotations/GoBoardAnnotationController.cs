using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GoBoardAnnotationController : MonoBehaviour
{
    public enum AnnotationTool
    {
        None = 0,
        Number = 1,
        Triangle = 2,
        Square = 3
    }

    [Header("Board")]
    [SerializeField] private CubeGrid cubeGrid;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform annotationRoot;
    [SerializeField] private GoBoardAnnotationMarker annotationPrefab;

    [Header("Buttons")]
    [SerializeField] private Button numberButton;
    [SerializeField] private Button triangleButton;
    [SerializeField] private Button squareButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button clearButton;
    [SerializeField] private Color inactiveToolButtonColor = Color.white;
    [SerializeField] private Color activeToolButtonColor = new Color(0.55f,0.85f,1f,1f);

    [Header("Placement")]
    [Min(0f)] [SerializeField] private float emptyPointHeight = 0.18f;
    [Min(0f)] [SerializeField] private float occupiedPointHeight = 0.62f;

    [Header("Numbers")]
    [SerializeField] private int startingNumber = 1;
    [SerializeField] private int nextNumber = 1;
    [SerializeField] private bool autoIncrementNumbers = true;
    [SerializeField] private bool resetNumberSequenceOnClear = true;

    private readonly Dictionary<string,GoBoardAnnotationMarker> annotationsByTileName = new();

    public static bool IsAnnotationInputActive { get; private set; }
    public AnnotationTool CurrentTool { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        EnsureAnnotationRoot();
        ResetNumberSequence();
        SetCurrentTool(AnnotationTool.None);
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureAnnotationRoot();
        BindButtons();

        if (cubeGrid != null)
            cubeGrid.OnGridInitialized += HandleGridInitialized;

        UpdateInputCaptureState();
        UpdateToolButtonVisuals();
    }

    private void OnDisable()
    {
        UnbindButtons();

        if (cubeGrid != null)
            cubeGrid.OnGridInitialized -= HandleGridInitialized;

        SetCurrentTool(AnnotationTool.None);
    }

    private void Update()
    {
        if (CurrentTool == AnnotationTool.None || cubeGrid == null)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!TryGetClickedGridTile(out GameObject gridTile))
            return;

        ApplyCurrentAnnotation(gridTile);
    }

    public void ActivateNumberTool()
    {
        SetCurrentTool(AnnotationTool.Number);
    }

    public void ActivateTriangleTool()
    {
        SetCurrentTool(AnnotationTool.Triangle);
    }

    public void ActivateSquareTool()
    {
        SetCurrentTool(AnnotationTool.Square);
    }

    public void CancelAnnotationTool()
    {
        SetCurrentTool(AnnotationTool.None);
    }

    public void ClearAnnotations()
    {
        foreach (GoBoardAnnotationMarker marker in annotationsByTileName.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }

        annotationsByTileName.Clear();

        if (resetNumberSequenceOnClear)
            ResetNumberSequence();
    }

    public void ApplySerializedAnnotations(List<CubeGrid.AnnotationData> serializedAnnotations)
    {
        ClearAnnotations();

        if (serializedAnnotations == null || serializedAnnotations.Count == 0)
        {
            SetCurrentTool(AnnotationTool.None);
            return;
        }

        ResolveReferences();
        EnsureAnnotationRoot();

        for (int i = 0; i < serializedAnnotations.Count; i++)
        {
            CubeGrid.AnnotationData annotation = serializedAnnotations[i];
            if (annotation == null)
                continue;

            if (!TryGetGridTileByBoardCoordinate(annotation.row,annotation.col,out GameObject gridTile))
                continue;

            ApplySerializedAnnotationToTile(gridTile,annotation.annotationType,annotation.numberValue);
        }

        SetCurrentTool(AnnotationTool.None);
    }

    public void SetNextNumber(int value)
    {
        nextNumber = Mathf.Max(0,value);
    }

    public void ResetNumberSequence()
    {
        nextNumber = Mathf.Max(0,startingNumber);
    }

    private void ResolveReferences()
    {
        if (cubeGrid == null)
            cubeGrid = FindObjectOfType<CubeGrid>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void EnsureAnnotationRoot()
    {
        if (annotationRoot != null)
            return;

        GameObject rootObject = new GameObject("BoardAnnotations");
        rootObject.transform.SetParent(transform,false);
        annotationRoot = rootObject.transform;
    }

    private void BindButtons()
    {
        BindButton(numberButton,ActivateNumberTool);
        BindButton(triangleButton,ActivateTriangleTool);
        BindButton(squareButton,ActivateSquareTool);
        BindButton(cancelButton,CancelAnnotationTool);
        BindButton(clearButton,ClearAnnotations);
    }

    private void UnbindButtons()
    {
        UnbindButton(numberButton,ActivateNumberTool);
        UnbindButton(triangleButton,ActivateTriangleTool);
        UnbindButton(squareButton,ActivateSquareTool);
        UnbindButton(cancelButton,CancelAnnotationTool);
        UnbindButton(clearButton,ClearAnnotations);
    }

    private void BindButton(Button button,UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void UnbindButton(Button button,UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
    }

    private void UpdateInputCaptureState()
    {
        IsAnnotationInputActive = CurrentTool != AnnotationTool.None;
    }

    private void SetCurrentTool(AnnotationTool tool)
    {
        CurrentTool = tool;
        UpdateInputCaptureState();
        UpdateToolButtonVisuals();
    }

    private void UpdateToolButtonVisuals()
    {
        ApplyToolButtonVisual(numberButton,CurrentTool == AnnotationTool.Number);
        ApplyToolButtonVisual(triangleButton,CurrentTool == AnnotationTool.Triangle);
        ApplyToolButtonVisual(squareButton,CurrentTool == AnnotationTool.Square);
    }

    private void ApplyToolButtonVisual(Button button,bool isActive)
    {
        if (button == null || button.targetGraphic == null)
            return;

        button.targetGraphic.color = isActive ? activeToolButtonColor : inactiveToolButtonColor;
    }

    private void HandleGridInitialized()
    {
        ClearAnnotations();
        CancelAnnotationTool();
    }

    private bool TryGetGridTileByBoardCoordinate(int row,int col,out GameObject gridTile)
    {
        gridTile = null;

        if (cubeGrid == null || row < 1 || col < 1)
            return false;

        List<GameObject> tiles = cubeGrid.CubeObjects;
        if (tiles == null || tiles.Count == 0)
            return false;

        string tileName = $"({row},{col})";
        for (int i = 0; i < tiles.Count; i++)
        {
            GameObject tile = tiles[i];
            if (tile != null && tile.name == tileName)
            {
                gridTile = tile;
                return true;
            }
        }

        return false;
    }

    private bool TryGetClickedGridTile(out GameObject gridTile)
    {
        gridTile = null;

        Camera activeCamera = targetCamera != null ? targetCamera : Camera.main;
        if (activeCamera == null)
            return false;

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits,(a,b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (cubeGrid.TryResolveGridTile(hitCollider.gameObject,out gridTile))
                return true;
        }

        return false;
    }

    private void ApplyCurrentAnnotation(GameObject gridTile)
    {
        if (annotationPrefab == null)
        {
            Debug.LogWarning("GoBoardAnnotationController: No annotation prefab assigned.");
            return;
        }

        string tileName = gridTile.name;
        Vector3 markerPosition = GetMarkerPosition(gridTile,tileName);
        GoBoardAnnotationMarker marker = GetOrCreateMarker(tileName,markerPosition);

        switch (CurrentTool)
        {
            case AnnotationTool.Number:
                marker.SetNumber(nextNumber.ToString());
                if (autoIncrementNumbers)
                    nextNumber++;
                break;

            case AnnotationTool.Triangle:
                marker.SetTriangle();
                break;

            case AnnotationTool.Square:
                marker.SetSquare();
                break;
        }
    }

    private void ApplySerializedAnnotationToTile(GameObject gridTile,int annotationTypeValue,int numberValue)
    {
        if (annotationPrefab == null)
        {
            Debug.LogWarning("GoBoardAnnotationController: No annotation prefab assigned.");
            return;
        }

        AnnotationTool annotationTool = AnnotationTool.None;
        switch (annotationTypeValue)
        {
            case 1:
                annotationTool = AnnotationTool.Number;
                break;
            case 2:
                annotationTool = AnnotationTool.Triangle;
                break;
            case 3:
                annotationTool = AnnotationTool.Square;
                break;
        }

        if (annotationTool == AnnotationTool.None)
            return;

        string tileName = gridTile.name;
        Vector3 markerPosition = GetMarkerPosition(gridTile,tileName);
        GoBoardAnnotationMarker marker = GetOrCreateMarker(tileName,markerPosition);

        switch (annotationTool)
        {
            case AnnotationTool.Number:
                marker.SetNumber(Mathf.Max(0,numberValue).ToString());
                break;

            case AnnotationTool.Triangle:
                marker.SetTriangle();
                break;

            case AnnotationTool.Square:
                marker.SetSquare();
                break;
        }
    }

    private GoBoardAnnotationMarker GetOrCreateMarker(string tileName,Vector3 markerPosition)
    {
        if (annotationsByTileName.TryGetValue(tileName,out GoBoardAnnotationMarker existingMarker) && existingMarker != null)
        {
            existingMarker.transform.position = markerPosition;
            return existingMarker;
        }

        GoBoardAnnotationMarker createdMarker = Instantiate(annotationPrefab,markerPosition,Quaternion.identity,annotationRoot);
        createdMarker.name = $"Annotation {tileName}";
        annotationsByTileName[tileName] = createdMarker;
        return createdMarker;
    }

    private Vector3 GetMarkerPosition(GameObject gridTile,string tileName)
    {
        (int x, int y) = ParseTileName(tileName);
        int[,] boardState = cubeGrid.GetBoardState();
        bool hasStone = boardState != null &&
                        y >= 0 && y < boardState.GetLength(0) &&
                        x >= 0 && x < boardState.GetLength(1) &&
                        boardState[y,x] != 0;

        float yOffset = hasStone ? occupiedPointHeight : emptyPointHeight;
        return gridTile.transform.position + Vector3.up * yOffset;
    }

    private (int x,int y) ParseTileName(string tileName)
    {
        string[] parts = tileName.Trim('(',')').Split(',');
        if (parts.Length != 2)
            return (0,0);

        int row = int.Parse(parts[0]) - 1;
        int column = int.Parse(parts[1]) - 1;
        return (column,row);
    }
}
