using System.Collections;
using TMPro;
using UnityEngine;

public class ClickManager : MonoBehaviour
{
    [SerializeField] private CubeGrid cubeGrid;
    [SerializeField] private ConditionManager conditionManager; // Reference to ConditionManager
    [SerializeField] private TextMeshProUGUI errorMessage;

    private static bool globalInputEnabled = true;

    void Start ()
    {
        // Find references if not assigned in the inspector
        if (cubeGrid == null)
        {
            cubeGrid = FindObjectOfType<CubeGrid>();
        }
        if (conditionManager == null)
        {
            conditionManager = FindObjectOfType<ConditionManager>();
        }

        if (cubeGrid == null)
        {
            Debug.LogError("Error: CubeGrid not found in the scene.");
        }
        if (conditionManager == null)
        {
            Debug.LogError("Error: ConditionManager not found in the scene.");
        }

        if (errorMessage == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("ErrorMessage");
            if (obj != null)
            {
                errorMessage = obj.GetComponent<TextMeshProUGUI>();
                errorMessage.text = "";
            }
            else
            {
                Debug.LogWarning("ErrorMessage GameObject with tag 'errorMessage' not found.");
            }
        }

    }

    void Update ()
    {
        if (!globalInputEnabled || GoBoardAnnotationController.IsAnnotationInputActive || cubeGrid == null || !cubeGrid.IsPlayerTurn())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray,out RaycastHit hit))
            {
                if (!cubeGrid.TryResolveGridTile(hit.collider.gameObject,out GameObject clickedCube))
                    return;

                if (cubeGrid.CubeObjects.Contains(clickedCube))
                {
                    // Check if strict pathing is enabled in the ConditionManager
                    bool isStrict = conditionManager != null && conditionManager.strictPathwayCheck;

                    // If pathing is strict OR the move is in the allowed list, attempt to place the tile.
                    if (isStrict || cubeGrid.AllowedCubes.Contains(clickedCube.name))
                    {
                        bool success = cubeGrid.PlacePlayerTile(clickedCube);
                        if (!success)
                        {
                            ShowErrorMessage("Failed to place tile. Try again.");
                        }
                    }
                    else
                    {
                        // This block now only runs if pathing is NOT strict AND the move is invalid.
                        ShowErrorMessage("Move invalid, try again");
                    }
                }
            }
        }
    }

    private void ShowErrorMessage (string message)
    {
        if (errorMessage != null)
        {
            errorMessage.text = message;
            CancelInvoke("HideErrorMessage");
            Invoke("HideErrorMessage",1.75f);
        }
    }

    private void HideErrorMessage ()
    {
        if (errorMessage != null)
        {
            errorMessage.text = "";
        }
    }

    public static void SetGlobalInputEnabled (bool isEnabled)
    {
        globalInputEnabled = isEnabled;
    }
}
