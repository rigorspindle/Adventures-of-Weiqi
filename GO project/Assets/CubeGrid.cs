using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using System.Linq;
using EditorAttributes;

[System.Serializable]
public class BoardVisuals
{
    public int gridSize;
    public Sprite gridSprite;
}

public class CubeGrid : MonoBehaviour
{
    public GameObject playerTile;
    public GameObject computerTile;
    public GameObject gridTilePrefab;
    public int gridSize = 5;
    

    [Line]
    [SerializeField] bool useReferenceSpriteBounds = true;
    [SerializeField] bool fitReferenceSpriteToBoard = true;
    [SerializeField] SpriteRenderer gridReferenceSprite;
    [SerializeField] Transform boardModelRoot;
    [Min(0f)] [SerializeField] Vector2 fitPadding = Vector2.zero;
    
    [SerializeField] List<BoardVisuals> boardVisuals = new();

    [Line]
    [ReadOnly] [SerializeField] private Vector2 boardProjectedSize;
    [ReadOnly] [SerializeField] private Vector2 spriteProjectedSize;
    [ReadOnly] [SerializeField] private Vector3 fittedBottomLeft;
    [ReadOnly] [SerializeField] private Vector3 fittedTopRight;

    private List<GameObject> cubeObjects = new List<GameObject>();
    private List<string> allowedCubes = new List<string>();
    private Dictionary<string,string> presetMoves = new Dictionary<string,string>();
    private List<string> playerMoveHistory = new List<string>(); // Tracking of new player moves
    public TextAsset puzzleJsonFile;

    private PresetMovesData currentPuzzleData;
    public PresetMovesData CurrentPuzzleData => currentPuzzleData;

    public event Action OnGameOver;
    public event Action OnGridInitialized;

    private bool playerTurn = true;
    private bool gameEnded = false;
    private bool aiProcessing = false;
    private int[,] boardState;

    [SerializeField] private CaptureManager captureManager; // Reference to CaptureManager
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ConditionManager conditionManager; // Reference to ConditionManager

    public List<string> illegalMoves = new List<string>(); // Public list of illegal moves as strings

    public List<GameObject> CubeObjects => cubeObjects;
    public List<string> AllowedCubes => allowedCubes;

    public List<GameObject> GetCubeObjects () => cubeObjects;

    private int initialGridSize;

    private void Awake ()
    {
        initialGridSize = gridSize;
    }

    void Start ()
    {
        if (PuzzlePersist.Instance != null && PuzzlePersist.Instance.savedPuzzleData != null)
            puzzleJsonFile = PuzzlePersist.Instance.savedPuzzleData;

        if (puzzleJsonFile != null)
        {
            LoadAllowedMovesFromTextAsset();
            if (currentPuzzleData != null && currentPuzzleData.boardSize > 0)
            {
                gridSize = currentPuzzleData.boardSize;
            }
        }
        else
        {
            Debug.LogWarning($"CubeGrid: puzzleJsonFile not assigned. Using default grid size: {gridSize}x{gridSize}");
        }

        boardState = new int[gridSize,gridSize];
        InitializeGrid();

        gameManager = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();
        captureManager = FindObjectOfType<CaptureManager>();
        conditionManager = FindObjectOfType<ConditionManager>();

        captureManager?.RefreshFromCubeGrid();

        if (captureManager == null)
            Debug.LogError("CubeGrid: CaptureManager not found!");
        if (conditionManager == null)
            Debug.LogError("CubeGrid: ConditionManager not found!");

        StartCoroutine(MonitorIllegalMoves());
    }

    public void LoadPuzzleIntoCurrentScene (TextAsset puzzleData)
    {
        StopAllCoroutines();

        puzzleJsonFile = puzzleData;
        currentPuzzleData = null;
        presetMoves.Clear();
        allowedCubes.Clear();
        playerMoveHistory.Clear();

        playerTurn = true;
        gameEnded = false;
        aiProcessing = false;

        if (puzzleJsonFile != null)
        {
            LoadAllowedMovesFromTextAsset();
            gridSize = currentPuzzleData != null && currentPuzzleData.boardSize > 0 ? currentPuzzleData.boardSize : initialGridSize;
        }
        else
        {
            gridSize = initialGridSize;
        }

        boardState = new int[gridSize,gridSize];

        if (captureManager == null)
            captureManager = FindObjectOfType<CaptureManager>();
        if (gameManager == null)
            gameManager = GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();
        if (conditionManager == null)
            conditionManager = FindObjectOfType<ConditionManager>();

        captureManager?.RefreshFromCubeGrid();
        InitializeGrid();
        captureManager?.RefreshFromCubeGrid();
        StartCoroutine(MonitorIllegalMoves());
    }

    public void InitializeGrid ()
    {
        ApplyBoardVisualForCurrentGridSize();

        foreach (Transform child in transform)
            Destroy(child.gameObject);
        cubeObjects.Clear();
        Array.Clear(boardState,0,boardState.Length);

        FitReferenceSpriteToBoardIfEnabled();

        Vector3 gridOrigin = transform.position;
        bool useSpriteBounds = TryGetGridCorners(out Vector3 bottomLeft,out Vector3 topRight);

        for (int i = 1; i <= gridSize; i++)
        {
            for (int j = 1; j <= gridSize; j++)
            {
                Vector3 position;
                if (useSpriteBounds)
                {
                    if (gridSize <= 1)
                    {
                        position = (bottomLeft + topRight) * 0.5f;
                    }
                    else
                    {
                        float xT = (j - 1) / (float)(gridSize - 1);
                        float zT = (i - 1) / (float)(gridSize - 1);
                        position = new Vector3(
                            Mathf.Lerp(bottomLeft.x,topRight.x,xT),
                            gridOrigin.y,
                            Mathf.Lerp(bottomLeft.z,topRight.z,zT));
                    }
                }
                else
                {
                    Vector3 localOffset = new Vector3(j - 1,0,i - 1);
                    position = gridOrigin + localOffset;
                }

                GameObject gridTile = Instantiate(gridTilePrefab,position,Quaternion.identity);
                gridTile.name = $"({i},{j})";  // 1-based naming: (row,column)
                gridTile.transform.parent = this.transform;

                string coords = $"{i},{j}";
                if (illegalMoves.Contains(coords))
                {
                    gridTile.GetComponent<Collider>().enabled = false;
                    gridTile.GetComponent<Renderer>().material.color = Color.red;
                }

                cubeObjects.Add(gridTile);
            }
        }

        if (CurrentPuzzleData != null && CurrentPuzzleData.boardFlat != null)
        {
            int[,] boardArray = CurrentPuzzleData.ToArray();
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    int player = boardArray[y,x];
                    if (player != 0)
                    {
                        PlaceStoneAt(x,y,player);
                    }
                }
            }
            Debug.Log("Board state restored from puzzle data.");
        }
        if (useSpriteBounds)
            Debug.Log($"Grid initialized from sprite corners BL:{bottomLeft} TR:{topRight} size:{gridSize}x{gridSize}");
        else
            Debug.Log($"Grid initialized at {gridOrigin} with size: {gridSize}x{gridSize}");

        OnGridInitialized?.Invoke();
    }

    private void ApplyBoardVisualForCurrentGridSize ()
    {
        if (gridReferenceSprite == null)
            return;

        if (!TryGetBoardVisualSprite(gridSize,out Sprite boardSprite))
            return;

        if (gridReferenceSprite.sprite == boardSprite)
            return;

        gridReferenceSprite.sprite = boardSprite;
    }

    private bool TryGetBoardVisualSprite (int size,out Sprite sprite)
    {
        sprite = null;

        if (boardVisuals == null || boardVisuals.Count == 0)
            return false;

        BoardVisuals exactMatch = null;
        BoardVisuals closestMatch = null;
        int closestDistance = int.MaxValue;

        foreach (BoardVisuals visual in boardVisuals)
        {
            if (visual == null || visual.gridSprite == null || visual.gridSize <= 0)
                continue;

            if (visual.gridSize == size)
            {
                exactMatch = visual;
                break;
            }

            int distance = Mathf.Abs(visual.gridSize - size);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestMatch = visual;
            }
        }

        BoardVisuals chosenVisual = exactMatch ?? closestMatch;
        if (chosenVisual == null)
            return false;

        sprite = chosenVisual.gridSprite;
        return sprite != null;
    }

    [ContextMenu("Fit Reference Sprite To Board")]
    private void FitReferenceSpriteToBoardContext ()
    {
        FitReferenceSpriteToBoard(force: true);
    }

    private void FitReferenceSpriteToBoardIfEnabled ()
    {
        FitReferenceSpriteToBoard(force: false);
    }

    private bool FitReferenceSpriteToBoard (bool force)
    {
        if (!force && !fitReferenceSpriteToBoard)
            return false;

        if (gridReferenceSprite == null)
            return false;

        if (!TryGetBoardProjectedSize(out float boardSizeX,out float boardSizeY,out Vector3 boardCenter))
            return false;

        if (!TryGetReferenceSpriteProjectedState(out float spriteSizeX,out float spriteSizeY,out _))
            return false;

        float targetSizeX = Mathf.Max(0.001f,boardSizeX - (fitPadding.x * 2f));
        float targetSizeY = Mathf.Max(0.001f,boardSizeY - (fitPadding.y * 2f));

        Transform spriteTransform = gridReferenceSprite.transform;
        Vector3 localScale = spriteTransform.localScale;

        if (spriteSizeX > Mathf.Epsilon)
            localScale.x *= targetSizeX / spriteSizeX;
        if (spriteSizeY > Mathf.Epsilon)
            localScale.y *= targetSizeY / spriteSizeY;
        spriteTransform.localScale = localScale;

        Vector3 axisX = spriteTransform.right.normalized;
        Vector3 axisY = spriteTransform.up.normalized;

        // Recompute center after scaling (pivot may not be at geometric center).
        if (TryGetReferenceSpriteProjectedState(out float scaledSizeX,out float scaledSizeY,out Vector3 scaledSpriteCenter))
        {
            float centerDeltaX = Vector3.Dot(boardCenter - scaledSpriteCenter,axisX);
            float centerDeltaY = Vector3.Dot(boardCenter - scaledSpriteCenter,axisY);
            spriteTransform.position += axisX * centerDeltaX + axisY * centerDeltaY;
            spriteProjectedSize = new Vector2(scaledSizeX,scaledSizeY);
        }

        boardProjectedSize = new Vector2(boardSizeX,boardSizeY);
        if (spriteProjectedSize == Vector2.zero)
            spriteProjectedSize = new Vector2(targetSizeX,targetSizeY);
        return true;
    }

    private bool TryGetGridCorners (out Vector3 bottomLeft,out Vector3 topRight)
    {
        bottomLeft = default;
        topRight = default;

        if (!useReferenceSpriteBounds || gridReferenceSprite == null)
            return false;

        Bounds bounds = gridReferenceSprite.bounds;
        if (bounds.size.x <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
        {
            Debug.LogWarning("CubeGrid: reference sprite must have non-zero X and Z world bounds. Falling back to transform origin.");
            return false;
        }

        float y = transform.position.y;
        bottomLeft = new Vector3(bounds.min.x,y,bounds.min.z);
        topRight = new Vector3(bounds.max.x,y,bounds.max.z);
        fittedBottomLeft = bottomLeft;
        fittedTopRight = topRight;
        return true;
    }

    private bool TryGetBoardProjectedSize (out float sizeX,out float sizeY,out Vector3 boardCenter)
    {
        sizeX = 0f;
        sizeY = 0f;
        boardCenter = default;

        if (gridReferenceSprite == null)
            return false;

        Transform root = boardModelRoot != null ? boardModelRoot : gridReferenceSprite.transform.parent;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        Vector3 axisX = gridReferenceSprite.transform.right.normalized;
        Vector3 axisY = gridReferenceSprite.transform.up.normalized;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        bool hasBounds = false;
        Bounds combinedBounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer == gridReferenceSprite)
                continue;

            Bounds b = renderer.bounds;
            if (!hasBounds)
            {
                combinedBounds = b;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(b);
            }

            Vector3 bMin = b.min;
            Vector3 bMax = b.max;
            UpdateProjectedMinMax(new Vector3(bMin.x,bMin.y,bMin.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMin.x,bMin.y,bMax.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMin.x,bMax.y,bMin.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMin.x,bMax.y,bMax.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMax.x,bMin.y,bMin.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMax.x,bMin.y,bMax.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMax.x,bMax.y,bMin.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
            UpdateProjectedMinMax(new Vector3(bMax.x,bMax.y,bMax.z),axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);
        }

        if (!hasBounds || !IsFiniteRange(minX,maxX) || !IsFiniteRange(minY,maxY))
            return false;

        sizeX = Mathf.Max(0f,maxX - minX);
        sizeY = Mathf.Max(0f,maxY - minY);
        boardCenter = combinedBounds.center;
        return sizeX > Mathf.Epsilon && sizeY > Mathf.Epsilon;
    }

    private bool TryGetReferenceSpriteProjectedState (out float sizeX,out float sizeY,out Vector3 center)
    {
        sizeX = 0f;
        sizeY = 0f;
        center = default;

        if (gridReferenceSprite == null || gridReferenceSprite.sprite == null)
            return false;

        Transform spriteTransform = gridReferenceSprite.transform;
        Vector3 axisX = spriteTransform.right.normalized;
        Vector3 axisY = spriteTransform.up.normalized;

        Bounds spriteLocalBounds = gridReferenceSprite.sprite.bounds;
        Vector3 c = spriteLocalBounds.center;
        Vector3 e = spriteLocalBounds.extents;

        Vector3[] corners =
        {
            spriteTransform.TransformPoint(new Vector3(c.x - e.x,c.y - e.y,0f)),
            spriteTransform.TransformPoint(new Vector3(c.x - e.x,c.y + e.y,0f)),
            spriteTransform.TransformPoint(new Vector3(c.x + e.x,c.y - e.y,0f)),
            spriteTransform.TransformPoint(new Vector3(c.x + e.x,c.y + e.y,0f))
        };

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
            UpdateProjectedMinMax(corners[i],axisX,axisY,ref minX,ref maxX,ref minY,ref maxY);

        if (!IsFiniteRange(minX,maxX) || !IsFiniteRange(minY,maxY))
            return false;

        sizeX = Mathf.Max(0f,maxX - minX);
        sizeY = Mathf.Max(0f,maxY - minY);
        center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        return sizeX > Mathf.Epsilon && sizeY > Mathf.Epsilon;
    }

    private static void UpdateProjectedMinMax (Vector3 point,Vector3 axisX,Vector3 axisY,ref float minX,ref float maxX,ref float minY,ref float maxY)
    {
        float projectedX = Vector3.Dot(point,axisX);
        float projectedY = Vector3.Dot(point,axisY);

        if (projectedX < minX)
            minX = projectedX;
        if (projectedX > maxX)
            maxX = projectedX;
        if (projectedY < minY)
            minY = projectedY;
        if (projectedY > maxY)
            maxY = projectedY;
    }

    private static bool IsFiniteRange (float min,float max)
    {
        return !float.IsNaN(min) && !float.IsInfinity(min) &&
               !float.IsNaN(max) && !float.IsInfinity(max);
    }

    private void LoadAllowedMovesFromTextAsset ()
    {
        string json = puzzleJsonFile.text;
        currentPuzzleData = JsonUtility.FromJson<PresetMovesData>(json);

        presetMoves.Clear();
        allowedCubes.Clear();

        if (currentPuzzleData != null && currentPuzzleData.moves != null)
        {
            foreach (var move in currentPuzzleData.moves)
            {
                if (move.wrongMoves != null)
                {
                    foreach (var subMove in move.wrongMoves)
                    {
                        if (!string.IsNullOrEmpty(subMove.playerMove) && !string.IsNullOrEmpty(subMove.aiMove))
                        {
                            if (!presetMoves.ContainsKey(subMove.playerMove))
                                presetMoves.Add(subMove.playerMove,subMove.aiMove);
                            if (!allowedCubes.Contains(subMove.playerMove))
                                allowedCubes.Add(subMove.playerMove);
                        }
                    }
                }

                if (move.correctMoves != null)
                {
                    foreach (var subMove in move.correctMoves)
                    {
                        if (!string.IsNullOrEmpty(subMove.playerMove) && !string.IsNullOrEmpty(subMove.aiMove))
                        {
                            if (!presetMoves.ContainsKey(subMove.playerMove))
                                presetMoves.Add(subMove.playerMove,subMove.aiMove);
                            if (!allowedCubes.Contains(subMove.playerMove))
                                allowedCubes.Add(subMove.playerMove);
                        }
                    }
                }
            }
        }
        Debug.Log("Loaded and parsed preset moves from puzzle file.");
    }

    private bool ResolveKoForCurrentPlayerStep (string lastTileName)
    {
        if (currentPuzzleData == null || currentPuzzleData.moves == null)
            return false;

        foreach (var moveGroup in currentPuzzleData.moves)
        {
            // correct path
            if (moveGroup.correctMoves != null && moveGroup.correctMoves.Count > 0)
            {
                var path = moveGroup.correctMoves.Select(m => m.playerMove).ToList();
                if (playerMoveHistory.Count <= path.Count &&
                    playerMoveHistory.SequenceEqual(path.Take(playerMoveHistory.Count)))
                {
                    int nextIdx = playerMoveHistory.Count;
                    if (nextIdx >= 0 && nextIdx < moveGroup.correctMoves.Count &&
                        moveGroup.correctMoves[nextIdx].playerMove == lastTileName)
                    {
                        return moveGroup.correctMoves[nextIdx].isKoMove;
                    }
                }
            }

            // wrong path
            if (moveGroup.wrongMoves != null && moveGroup.wrongMoves.Count > 0)
            {
                var path = moveGroup.wrongMoves.Select(m => m.playerMove).ToList();
                if (playerMoveHistory.Count <= path.Count &&
                    playerMoveHistory.SequenceEqual(path.Take(playerMoveHistory.Count)))
                {
                    int nextIdx = playerMoveHistory.Count;
                    if (nextIdx >= 0 && nextIdx < moveGroup.wrongMoves.Count &&
                        moveGroup.wrongMoves[nextIdx].playerMove == lastTileName)
                    {
                        return moveGroup.wrongMoves[nextIdx].isKoMove;
                    }
                }
            }
        }

        return false;
    }

    public void SaveBoardStateToJson (string filePath)
    {
        BoardData boardData = new BoardData
        {
            boardFlat = new int[gridSize * gridSize],
            boardSize = gridSize
        };

        int index = 0;
        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                boardData.boardFlat[index++] = boardState[y,x];

        string json = JsonUtility.ToJson(boardData,true);
        File.WriteAllText(filePath,json);
        Debug.Log($"Board state saved to {filePath}");
    }

    public void LoadBoardStateFromJson (string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        BoardData boardData = JsonUtility.FromJson<BoardData>(json);

        if (boardData.boardSize != gridSize)
        {
            Debug.LogError("Mismatch between board sizes. JSON file might be corrupted.");
            return;
        }

        int index = 0;
        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                boardState[y,x] = boardData.boardFlat[index++];

        Debug.Log($"Board state loaded from {filePath}");
        RefreshBoardVisuals();
    }

    public bool IsPlayerTurn () => playerTurn && !gameEnded && !aiProcessing;

    public int CountAIPieces ()
    {
        int count = 0;
        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                if (boardState[y,x] == 2)
                    count++;
        return count;
    }

    public int CountPlayerPieces ()
    {
        int count = 0;
        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                if (boardState[y,x] == 1)
                    count++;
        return count;
    }

    public bool PlacePlayerTile (GameObject gridTile)
    {
        if (!playerTurn || aiProcessing || gameEnded)
            return false;

        string tileName = gridTile.name;
        (int x, int y) = ParseTileName(tileName);

        // Determine ko flag for this specific step BEFORE any ko block check
        bool isKoThisStep = ResolveKoForCurrentPlayerStep(tileName);

        // KO: immediate recapture at KoPoint by the banned player is illegal
        // Allow override if puzzle step is flagged as ko-override (isKoMove == true)
        if (captureManager != null && captureManager.IsMoveKoBlocked(x,y,1) && !isKoThisStep)
        {
            Debug.LogWarning($"Illegal (ko) recapture at ({y + 1}, {x + 1}) for player 1.");
            return false;
        }

        if (!IsMoveLegal(x,y))
        {
            Debug.LogWarning($"Illegal move attempt at ({x + 1}, {y + 1})");
            return false;
        }

        Vector3 tilePosition = gridTile.transform.position + Vector3.up * 0.5f;
        GameObject stone = Instantiate(playerTile,tilePosition,Quaternion.identity);
        stone.transform.parent = gridTile.transform;

        boardState[y,x] = 1;
        playerMoveHistory.Add(tileName);

        if (captureManager != null)
        {
            // Pass the ko-override flag so the just-placed group can be treated alive for this pass if needed
            captureManager.CheckForCaptures(new Vector2Int(x,y),1,isKoThisStep);
        }

        // If player 1 was the banned side from a previous ko, their move (anywhere) expires that ko.
        captureManager?.ExpireKoIfBannedPlayerMoved(1,new Vector2Int(x,y));

        if (gameManager != null)
        {
            gameManager.turnCount++;
            gameManager.UpdateTurnCounter();
        }

        conditionManager?.CheckMovesPath(playerMoveHistory,allowWin: false);

        if (gameManager != null && gameManager.gameIsOver)
        {
            gameEnded = true;
        }

        playerTurn = false;
        aiProcessing = true;

        string aiMove = GetAIResponse();
        if (aiMove != null)
            StartCoroutine(AIDelayedMove(aiMove));
        else
        {
            Debug.LogWarning("No AI response found for this move sequence.");
            playerTurn = true;
            aiProcessing = false;
        }

        return true;
    }

    public bool TryResolveGridTile(GameObject clickedObject,out GameObject gridTile)
    {
        gridTile = null;
        if (clickedObject == null)
            return false;

        if (cubeObjects.Contains(clickedObject))
        {
            gridTile = clickedObject;
            return true;
        }

        Transform current = clickedObject.transform.parent;
        while (current != null)
        {
            GameObject currentObject = current.gameObject;
            if (cubeObjects.Contains(currentObject))
            {
                gridTile = currentObject;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private string GetAIResponse ()
    {
        string bestMatchAiMove = null;
        int longestMatchLength = 0;

        foreach (var moveGroup in currentPuzzleData.moves.Where(mg => mg.wrongMoves != null))
        {
            var path = moveGroup.wrongMoves;
            var playerPathInGroup = path.Select(p => p.playerMove).ToList();

            if (playerMoveHistory.Count <= playerPathInGroup.Count &&
                playerMoveHistory.SequenceEqual(playerPathInGroup.Take(playerMoveHistory.Count)))
            {
                if (playerMoveHistory.Count > longestMatchLength)
                {
                    longestMatchLength = playerMoveHistory.Count;
                    bestMatchAiMove = path[longestMatchLength - 1].aiMove;
                }
            }
        }

        foreach (var moveGroup in currentPuzzleData.moves.Where(mg => mg.correctMoves != null))
        {
            var path = moveGroup.correctMoves;
            var playerPathInGroup = path.Select(p => p.playerMove).ToList();

            if (playerMoveHistory.Count <= playerPathInGroup.Count &&
                playerMoveHistory.SequenceEqual(playerPathInGroup.Take(playerMoveHistory.Count)))
            {
                if (playerMoveHistory.Count > longestMatchLength)
                {
                    longestMatchLength = playerMoveHistory.Count;
                    bestMatchAiMove = path[longestMatchLength - 1].aiMove;
                }
            }
        }

        if (bestMatchAiMove != null)
            return bestMatchAiMove;
        if (playerMoveHistory.Count > 0 && presetMoves.TryGetValue(playerMoveHistory.Last(),out string response))
            return response;
        return null;
    }

    public void PlaceStoneAt (int x,int y,int player)
    {
        if (x < 0 || y < 0 || x >= gridSize || y >= gridSize)
        {
            Debug.LogError($"Invalid coordinates for PlaceStoneAt: ({x}, {y})");
            return;
        }

        string tileName = $"({y + 1},{x + 1})";
        GameObject gridTile = cubeObjects.Find(tile => tile.name == tileName);

        if (gridTile != null)
        {
            foreach (Transform child in gridTile.transform)
                Destroy(child.gameObject);

            if (player != 0)
            {
                GameObject stoneToPlace = (player == 1) ? playerTile : computerTile;
                Vector3 tilePosition = gridTile.transform.position + Vector3.up * 0.5f;
                Instantiate(stoneToPlace,tilePosition,Quaternion.identity,gridTile.transform);
            }

            boardState[y,x] = player;
            captureManager?.CheckForCaptures();
        }
        else
        {
            Debug.LogError($"Grid tile '{tileName}' not found for coordinates: ({x}, {y})");
        }
    }

    private IEnumerator AIDelayedMove (string intendedMove)
    {
        yield return new WaitForSeconds(1.0f);

        if (gameEnded && intendedMove != "(0,0)")
        {
            Debug.Log("Game has ended, AI will not place a stone.");
            aiProcessing = false;
            yield break;
        }

        if (intendedMove == "(0,0)")
        {
            Debug.Log("AI skips turn as per puzzle logic.");
            aiProcessing = false;
            if (!gameEnded)
                playerTurn = true;
            conditionManager?.CheckMovesPath(playerMoveHistory,allowWin: true);
            yield break;
        }

        string aiMove = FindValidAIMove(intendedMove);
        if (aiMove == null)
        {
            Debug.Log("AI has no valid moves left.");
            if (gameManager != null && !gameManager.gameIsOver)
                gameManager.EndGame();
            yield break;
        }

        (int x, int y) = ParseTileName(aiMove);

        // KO: AI cannot immediately recapture at KoPoint if AI is the banned player
        if (captureManager != null && captureManager.IsMoveKoBlocked(x,y,2))
        {
            Debug.Log("AI move is ko-blocked at this point; skipping AI move.");
            aiProcessing = false;
            playerTurn = true;
            conditionManager?.CheckMovesPath(playerMoveHistory,allowWin: true);
            yield break;
        }

        if (!IsMoveLegal(x,y))
        {
            Debug.LogError($"AI attempted an illegal move at ({x + 1}, {y + 1})");
            playerTurn = true;
            aiProcessing = false;
            yield break;
        }

        // Tentatively place AI stone
        boardState[y,x] = 2;

        // Capture BEFORE suicide; protect the just-placed AI group from self-capture this pass
        bool suicidalBefore = !HasLiberties(x,y,2);
        captureManager?.CheckForCaptures(new Vector2Int(x,y),2,true);

        // Re-evaluate AFTER potential captures
        bool hasLibAfter = HasLiberties(x,y,2);
        bool capturedAny = (captureManager != null && captureManager.LastRemovedCount > 0);

        // Strict Go: illegal only if still no liberties AND captured nothing
        if (!hasLibAfter && !capturedAny)
        {
            Debug.LogWarning($"AI attempted a suicidal move at ({x + 1}, {y + 1}). Reverting.");
            boardState[y,x] = 0;
            playerTurn = true;
            aiProcessing = false;
            yield break;
        }

        GameObject aiTile = cubeObjects.Find(cube => cube.name == aiMove);
        if (aiTile != null)
        {
            Vector3 tilePosition = aiTile.transform.position + Vector3.up * 0.5f;
            Instantiate(computerTile,tilePosition,Quaternion.identity,aiTile.transform);
            Debug.Log($"AI placed a stone at ({x + 1}, {y + 1}).");

            // Expire ko if AI was the banned side
            captureManager?.ExpireKoIfBannedPlayerMoved(2,new Vector2Int(x,y));
            conditionManager?.CheckMovesPath(playerMoveHistory,allowWin: true);
        }

        if (!gameEnded)
        {
            playerTurn = true;
            aiProcessing = false;
        }
    }

    private bool HasLiberties (int x,int y,int player)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        List<Vector2Int> group = new List<Vector2Int>();
        return captureManager.FloodFill(x,y,player,visited,group,null,false);
    }

    private bool HasValidMoves (int player)
    {
        IEnumerable<string> movesToCheck = (player == 1) ? (IEnumerable<string>)presetMoves.Keys : presetMoves.Values;
        foreach (var move in movesToCheck)
        {
            (int x, int y) = ParseTileName(move);
            if (IsMoveLegal(x,y))
                return true;
        }
        return false;
    }

    public bool HasPlayerValidMoves () => HasValidMoves(1);
    public bool HasAIValidMoves () => HasValidMoves(2);

    private bool IsMoveLegal (int x,int y)
    {
        if (x < 0 || y < 0 || x >= gridSize || y >= gridSize)
            return false;
        if (boardState[y,x] != 0)
            return false;
        string coords = $"{y + 1},{x + 1}";
        if (illegalMoves.Contains(coords))
            return false;
        return true;
    }

    private string FindValidAIMove (string intendedMove)
    {
        (int x, int y) = ParseTileName(intendedMove);
        if (IsMoveLegal(x,y))
            return intendedMove;
        foreach (var move in presetMoves.Values)
        {
            (x, y) = ParseTileName(move);
            if (IsMoveLegal(x,y))
                return move;
        }
        return null;
    }

    private (int, int) ParseTileName (string name)
    {
        string[] parts = name.Trim('(',')').Split(',');
        return (int.Parse(parts[1]) - 1, int.Parse(parts[0]) - 1);
    }

    public int[,] GetBoardState () => boardState;

    public void SetGameEnded () { gameEnded = true; }

    public void UpdateBoardState (int[,] newBoardState)
    {
        if (newBoardState == null || newBoardState.GetLength(0) != gridSize || newBoardState.GetLength(1) != gridSize)
        {
            Debug.LogError("UpdateBoardState: Invalid board state provided.");
            return;
        }
        boardState = newBoardState;
        Debug.Log("Board state updated from editor.");
        RefreshBoardVisuals();
    }

    private void RefreshBoardVisuals ()
    {
        foreach (GameObject go in cubeObjects)
            foreach (Transform child in go.transform)
                Destroy(child.gameObject);

        for (int y = 0; y < gridSize; y++)
            for (int x = 0; x < gridSize; x++)
                if (boardState[y,x] != 0)
                    PlaceStoneAt(x,y,boardState[y,x]);

        Debug.Log("Board visuals refreshed.");
    }

    private void UpdateIllegalTiles ()
    {
        foreach (GameObject tile in cubeObjects)
        {
            string[] parts = tile.name.Trim('(',')').Split(',');
            int row = int.Parse(parts[0]);
            int column = int.Parse(parts[1]);
            string coords = $"{row},{column}";
            bool isIllegal = illegalMoves.Contains(coords);
            tile.SetActive(!isIllegal);
        }
    }

    private IEnumerator MonitorIllegalMoves ()
    {
        List<string> previousIllegalMoves = new List<string>(illegalMoves);
        while (true)
        {
            if (!AreListsEqual(illegalMoves,previousIllegalMoves))
            {
                UpdateIllegalTiles();
                previousIllegalMoves = new List<string>(illegalMoves);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    private bool AreListsEqual (List<string> list1,List<string> list2)
    {
        return list1.Count == list2.Count && new HashSet<string>(list1).SetEquals(list2);
    }

    [System.Serializable]
    public class BoardData
    {
        public int[] boardFlat;
        public int boardSize;
    }

    [System.Serializable]
    public class Move
    {
        public string playerMove;
        public string aiMove;
        public List<SubMove> correctMoves;
        public List<SubMove> wrongMoves;
        public List<SubMove> correctPath;
        public List<SubMove> wrongPath;
    }

    [System.Serializable]
    public class PresetMovesData
    {
        public int boardSize;
        public int[] boardFlat;
        public List<Move> moves;

        public int[,] ToArray ()
        {
            int[,] array = new int[boardSize,boardSize];
            for (int y = 0; y < boardSize; y++)
                for (int x = 0; x < boardSize; x++)
                    array[y,x] = boardFlat[y * boardSize + x];
            return array;
        }
    }

    [System.Serializable]
    public class SubMove
    {
        public string playerMove;
        public string aiMove;
        public bool isKoMove;
    }
}
