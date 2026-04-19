using TMPro;
using UnityEngine;

public class GoBoardAnnotationMarker : MonoBehaviour
{
    private const string SolidTriangleSymbol = "\u25B2";
    private const string OutlineSquareSymbol = "\u25A1";
    private const string SolidSquareSymbol = "\u25A0";
    private const string OutlineTriangleSymbol = "\u25B3";
    private const string TriangleAsciiFallback = "^";

    public enum MarkerType
    {
        None = 0,
        Number = 1,
        Triangle = 2,
        Square = 3
    }

    [SerializeField] private TMP_Text annotationText;
    [Header("Optional Shape Visuals")]
    [SerializeField] private GameObject triangleVisual;
    [SerializeField] private GameObject squareVisual;

    [Header("Text Fallbacks")]
    [SerializeField] private string triangleSymbol = TriangleAsciiFallback;
    [SerializeField] private string squareSymbol = OutlineSquareSymbol;

    public MarkerType CurrentMarkerType { get; private set; }
    public string CurrentLabel { get; private set; } = string.Empty;

    public void SetNumber(string label)
    {
        CurrentMarkerType = MarkerType.Number;
        CurrentLabel = label ?? string.Empty;
        SetShapeVisualState(false,false);
        ApplyText(CurrentLabel);
    }

    public void SetTriangle()
    {
        CurrentMarkerType = MarkerType.Triangle;
        SetShapeVisualState(true,false);

        if (triangleVisual != null)
        {
            CurrentLabel = string.Empty;
            ApplyText(string.Empty);
            return;
        }

        CurrentLabel = triangleSymbol ?? string.Empty;
        ApplyText(CurrentLabel);
    }

    public void SetSquare()
    {
        CurrentMarkerType = MarkerType.Square;
        SetShapeVisualState(false,true);

        if (squareVisual != null)
        {
            CurrentLabel = string.Empty;
            ApplyText(string.Empty);
            return;
        }

        CurrentLabel = squareSymbol ?? string.Empty;
        ApplyText(CurrentLabel);
    }

    public void ClearMarker()
    {
        CurrentMarkerType = MarkerType.None;
        CurrentLabel = string.Empty;
        SetShapeVisualState(false,false);
        ApplyText(string.Empty);
    }

    private void Awake()
    {
        if (annotationText == null)
            annotationText = GetComponentInChildren<TMP_Text>(true);

        ClearMarker();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(triangleSymbol) ||
            triangleSymbol == SolidTriangleSymbol ||
            triangleSymbol == OutlineTriangleSymbol)
        {
            triangleSymbol = TriangleAsciiFallback;
        }

        if (string.IsNullOrWhiteSpace(squareSymbol) || squareSymbol == SolidSquareSymbol)
            squareSymbol = OutlineSquareSymbol;
    }

    private void SetShapeVisualState(bool showTriangle,bool showSquare)
    {
        if (triangleVisual != null)
            triangleVisual.SetActive(showTriangle);

        if (squareVisual != null)
            squareVisual.SetActive(showSquare);
    }

    private void ApplyText(string value)
    {
        if (annotationText == null)
            return;

        annotationText.text = value ?? string.Empty;
        annotationText.gameObject.SetActive(!string.IsNullOrEmpty(annotationText.text));
    }
}
