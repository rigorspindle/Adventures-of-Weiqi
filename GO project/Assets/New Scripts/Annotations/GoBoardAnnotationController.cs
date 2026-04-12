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
        UpdateInputCaptureState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureAnnotationRoot();
        BindButtons();

        if (cubeGrid != null)
            cubeGrid.OnGridInitialized += HandleGridInitialized;

        UpdateInputCaptureState();
    }

    private void OnDisable()
    {
        UnbindButtons();

        if (cubeGrid != null)
            cubeGrid.OnGridInitialized -= HandleGridInitialized;

        CurrentTool = AnnotationTool.None;
        UpdateInputCaptureState();
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
        CurrentTool = AnnotationTool.Number;
        UpdateInputCaptureState();
    }

    public void ActivateTriangleTool()
    {
        CurrentTool = AnnotationTool.Triangle;
        UpdateInputCaptureState();
    }

    public void ActivateSquareTool()
    {
        CurrentTool = AnnotationTool.Square;
        UpdateInputCaptureState();
    }

    public void CancelAnnotationTool()
    {
        CurrentTool = AnnotationTool.None;
        UpdateInputCaptureState();
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

    private void HandleGridInitialized()
    {
        ClearAnnotations();
        CancelAnnotationTool();
    }

    private bool TryGetClickedGridTile(out GameObject gridTile)
    {
        gridTile = null;

        Camera activeCamera = targetCamera != null ? targetCamera : Camera.main;
        if (activeCamera == null)
            return false;

        Ray ray = activeCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray,out RaycastHit hit))
            return false;

        return cubeGrid.TryResolveGridTile(hit.collider.gameObject,out gridTile);
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
