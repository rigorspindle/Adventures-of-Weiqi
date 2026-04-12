using TMPro;
using UnityEngine;

public class GoBoardAnnotationMarker : MonoBehaviour
{
    public enum MarkerType
    {
        None = 0,
        Number = 1,
        Triangle = 2,
        Square = 3
    }

    [SerializeField] private TMP_Text annotationText;
    [SerializeField] private string triangleSymbol = "\u25B3";
    [SerializeField] private string squareSymbol = "\u25A1";

    public MarkerType CurrentMarkerType { get; private set; }
    public string CurrentLabel { get; private set; } = string.Empty;

    public void SetNumber(string label)
    {
        CurrentMarkerType = MarkerType.Number;
        CurrentLabel = label ?? string.Empty;
        ApplyText(CurrentLabel);
    }

    public void SetTriangle()
    {
        CurrentMarkerType = MarkerType.Triangle;
        CurrentLabel = triangleSymbol;
        ApplyText(CurrentLabel);
    }

    public void SetSquare()
    {
        CurrentMarkerType = MarkerType.Square;
        CurrentLabel = squareSymbol;
        ApplyText(CurrentLabel);
    }

    public void ClearMarker()
    {
        CurrentMarkerType = MarkerType.None;
        CurrentLabel = string.Empty;
        ApplyText(string.Empty);
    }

    private void Awake()
    {
        if (annotationText == null)
            annotationText = GetComponentInChildren<TMP_Text>(true);

        ClearMarker();
    }

    private void ApplyText(string value)
    {
        if (annotationText == null)
            return;

        annotationText.text = value ?? string.Empty;
        annotationText.gameObject.SetActive(!string.IsNullOrEmpty(annotationText.text));
    }
}
