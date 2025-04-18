using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Piece : NetworkBehaviour
{
    [SerializeField] public GameObject tileMarkerPrefab;
    [SerializeField] public List<Vector2Int> shape = new List<Vector2Int>();
    [SerializeField] public int points;

    public NetworkVariable<PlayerStatus> playerStatus = new NetworkVariable<PlayerStatus>(PlayerStatus.None);
    private NetworkVariable<bool> isPlaced = new NetworkVariable<bool>(false);
    private NetworkVariable<Color> pieceColor = new NetworkVariable<Color>(Color.white);
    private Board board;
    public bool _isDragging = false;
    private Vector3 _startDragPosition;
    private Quaternion _startDragRotation;
    private List<Vector2Int> _startDragShape;
    private Vector3 _offset;
    private List<GameObject> tileMarkers = new List<GameObject>();

    void Start()
    {
        board = GameManager.Instance.board;
        InstantiateTileMarkers();

        pieceColor.OnValueChanged += (oldColor, newColor) =>
        {
            SetColor(newColor);
        };
        SetColor(pieceColor.Value);
    }

    private void Update()
    {
        if (_isDragging)
        {
            HandleDragging();
            if (Input.GetMouseButtonDown(1))
            {
                RotatePiece();
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                FlipPiece();
            }
        }
    }


    // DRAGGING MECHANICS
    private void HandleDragging() // 
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3 newPosition = new Vector3(mousePos.x + _offset.x, mousePos.y + _offset.y, -2);

        MovePiece(newPosition);

        Vector2Int closestTileGridPos = board.GetClosestTileGridPosition(transform.position);
        if (closestTileGridPos != new Vector2Int(-1, -1))
        {
            Vector2IntList targetTilesGridPositions = GetTargetTilesGridPositionsForPiece(closestTileGridPos);
            if (board.AreTilesValidForPlacement(targetTilesGridPositions, playerStatus.Value))
            {
                ActivateTileMarkers(targetTilesGridPositions);
            }
            else
            {
                DeactivateTileMarkers();
            }
        }
        else
        {
            DeactivateTileMarkers();
        } 
    }

    private void BeginDragging()
    {
        Debug.Log("BeginDragging called for player: " + playerStatus.Value);
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _offset = transform.position - new Vector3(mousePos.x, mousePos.y, 0);
        _isDragging = true;
        _startDragPosition = transform.position;
        _startDragRotation = transform.rotation;
        _startDragShape = shape;
    }

    private void OnMouseDown()
    {
        if (playerStatus.Value == PlayerStatus.PlayerGreen)
        {
            Debug.Log("LocalPlayerStatus: " + GameManager.Instance.GetLocalPlayerStatus() + ", CurrentPlayerStatus: " + GameManager.Instance.GetCurrentPlayerStatus() + "IsPlaced: " + isPlaced.Value);
        }


        PlayerStatus currentPlayerStatus = GameManager.Instance.GetCurrentPlayerStatus();
        if (GameManager.Instance.GetLocalPlayerStatus() == currentPlayerStatus && currentPlayerStatus == playerStatus.Value && !isPlaced.Value)
        {
            BeginDragging();
        }
    }

    private void OnMouseUp() //Test
    {
        if (!_isDragging)
        {
            return;
        }
        _isDragging = false;
        
        Vector2Int nearestTileGridPosition = board.GetClosestTileGridPosition(transform.position);

        if (nearestTileGridPosition != new Vector2Int(-1, -1))
        {
            Vector2IntList targetTilesGridPositions = GetTargetTilesGridPositionsForPiece(nearestTileGridPosition);

            if (board.AreTilesValidForPlacement(targetTilesGridPositions, playerStatus.Value))
            {
                PlacePieceServerRpc(targetTilesGridPositions);
                DeactivateTileMarkers();
                return;
            }
        }

        DragResetPiecePosition();
    }

    private void MovePiece(Vector3 newPosition)
    {
        MovePieceServerRpc(newPosition);
    }

    [ServerRpc(RequireOwnership = false)]
    public void MovePieceServerRpc(Vector3 newPosition, ServerRpcParams rpcParams = default)
    {
        transform.position = newPosition;
        MovePieceClientRpc(newPosition);
    }

    [ClientRpc]
    public void MovePieceClientRpc(Vector3 newPosition, ClientRpcParams rpcParams = default)
    {
        transform.position = newPosition;
    }

    private void DragResetPiecePosition()
    {
        transform.position = _startDragPosition;
        transform.rotation = _startDragRotation;
        shape = _startDragShape;
        _isDragging = false;
    }

    [ClientRpc]
    private void ResetPiecePositionClientRpc(ulong clientId, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        Debug.Log("ResetPiecePositionClientRpc called for clientId: " + clientId);
        DragResetPiecePosition();
    }

    private void RotatePiece()
    {
        transform.RotateAround(transform.position, Vector3.forward, 90);
        for (int i = 0; i < shape.Count; i++)
        {
            shape[i] = new Vector2Int(-shape[i].y, shape[i].x);
        }
    }

    private void FlipPiece()
    {
        transform.RotateAround(transform.position, Vector3.up, 180);
        for (int i = 0; i < shape.Count; i++)
        {
            shape[i] = new Vector2Int(-shape[i].x, shape[i].y);
        }   
    }
    // END DRAGGING


    /* Nicht mehr benötigt
    public List<Vector2Int> GetTilePositions(List<Tile> tiles)
    {
        List<Vector2Int> tilePositions = new List<Vector2Int>();
        foreach (Tile tile in tiles)
        {
            tilePositions.Add(new Vector2Int(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y)));
        }
        return tilePositions;
    }
    */


    [ServerRpc(RequireOwnership = false)]
    public void PlacePieceServerRpc(Vector2IntList targetTilePositions, ServerRpcParams rpcParams = default)  //Wird im Multiplayer später zu CmdRequestPlacePiece(...)
    {
        Debug.Log("PlacePieceServerRpc called for player: " + playerStatus.Value);

        // check if request is valid
        if (!board.AreTilesValidForPlacement(targetTilePositions, playerStatus.Value)) 
        {
            Debug.Log("Invalid placement request for player: " + playerStatus.Value);
            ResetPiecePositionClientRpc(rpcParams.Receive.SenderClientId);
            return;
        }
        
        GameManager.Instance.SetTilesOccupied(targetTilePositions, playerStatus.Value);
        GameManager.Instance.SetFirstPiecePlacedForPlayerStatus(playerStatus.Value, true);

        Vector3 targetPosGrid = new Vector3(targetTilePositions.Values[0].x, targetTilePositions.Values[0].y, -2);
        SnapPieceToGridClientRpc(targetPosGrid, rpcParams.Receive.SenderClientId);
        isPlaced.Value = true;

        GameManager.Instance.EndTurn(this);

        Debug.Log("PlacePieceServerRpc completed successfully.");
    }

    [ClientRpc]
    public void SnapPieceToGridClientRpc(Vector3 targetPosGrid, ulong clientId, ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;

        Debug.Log("SnapPieceToGridClientRpc called for clientId: " + clientId);
        transform.position = targetPosGrid;
    }

    // Die Vector2IntList kann außerhalb des Grids liegen
    public Vector2IntList GetTargetTilesGridPositionsForPiece(Vector2Int nearestTileGridPosition)
    {
        Vector2IntList targetTilesGridPositions = new Vector2IntList { Values = new List<Vector2Int>() };
        foreach (var gridPos in shape)
        {
            Vector2Int targetPos = nearestTileGridPosition + gridPos;    
            targetTilesGridPositions.Values.Add(targetPos);
        }
        return targetTilesGridPositions;
    }

    private void InstantiateTileMarkers()
    {
        for (int i = 0; i < shape.Count; i++)
        {
            GameObject marker = Instantiate(tileMarkerPrefab);
            marker.SetActive(false);
            tileMarkers.Add(marker);
        }
    }

    private void ActivateTileMarkers(Vector2IntList targetTilesGridPositions)
    {
        for (int i = 0; i < tileMarkers.Count; i++)
        {
            var marker = tileMarkers[i];
            marker.SetActive(true);
            Vector3 targetPos = new Vector3(targetTilesGridPositions.Values[i].x, targetTilesGridPositions.Values[i].y, -0.5f);
            marker.transform.position =targetPos;
        }
    }

    private void DeactivateTileMarkers()
    {
        foreach (var marker in tileMarkers)
        {
            marker.SetActive(false);
        }
    }

    public void SetColor(Color newColor)
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer spriteRend in spriteRenderers)
        {
            spriteRend.color = newColor; // Farbe direkt setzen
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPieceColorServerRpc(Color color, ServerRpcParams rpcParams = default)
    {
        pieceColor.Value = color;
    }
}
