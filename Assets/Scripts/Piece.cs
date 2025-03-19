using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Piece : NetworkBehaviour
{
    public PlayerStatus playerStatus;
    public List<Vector2Int> shape = new List<Vector2Int>();

    public bool isPlaced;
    public int points;
    public bool _isDragging;
    private Board board;

    private Vector3 _startDragPosition;
    private Quaternion _startDragRotation;
    private List<Vector2Int> _startDragShape;
    private Vector3 _offset;

    public GameObject tileMarkerPrefab;
    private List<GameObject> tileMarkers = new List<GameObject>();

    void Start()
    {
        isPlaced = false;
        _isDragging = false;
        SetColor(GameManager.Instance.GetPlayerByPlayerStatus(playerStatus).color);
        board = GameManager.Instance.board;
        InstantiateTileMarkers();
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

        Tile nearestTile = board.GetClosestTile(transform.position);
        if (nearestTile != null)
        {
            List<Tile> targetTiles = GetTargetTilesForPiece(nearestTile);
            if (targetTiles != null && board.AreTilesValidForPlacement(targetTiles, playerStatus))
            {
                ActivateTileMarkers(targetTiles);
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
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        _offset = transform.position - new Vector3(mousePos.x, mousePos.y, 0);
        _isDragging = true;
        _startDragPosition = transform.position;
        _startDragRotation = transform.rotation;
        _startDragShape = shape;
    }

    private void OnMouseDown()
    {
        if (GameManager.Instance.GetCurrentPlayerStatus() == playerStatus && !isPlaced)
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
        
        Tile nearestTile = board.GetClosestTile(transform.position);
        List<Tile> targetTiles = nearestTile != null ? GetTargetTilesForPiece(nearestTile) : null;

        if (targetTiles == null || !board.AreTilesValidForPlacement(targetTiles, playerStatus)) // Hier wird später eine Anfrage zum prüfen an den Server geschickt (CmdRequestCheckValidPosition(...))
        {
            DragResetPiecePosition();
            return;
        }
        
        /*
        if (!PlacePieceRpc(GetTilePositions(targetTiles))) // Hier wird später eine Anfrage zum placen an den Server geschickt (CmdRequestPlacePiece(...))
        {
            // Should not happen
            DragResetPiecePosition();
        }
        */
        PlacePieceRpc(new Vector2IntList { Values = GetTilePositions(targetTiles) });
        DeactivateTileMarkers();
    }

    private void MovePiece(Vector3 newPosition)
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


    public List<Vector2Int> GetTilePositions(List<Tile> tiles)
    {
        List<Vector2Int> tilePositions = new List<Vector2Int>();
        foreach (Tile tile in tiles)
        {
            tilePositions.Add(new Vector2Int(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y)));
        }
        return tilePositions;
    }


    [Rpc(SendTo.Server)]
    public void PlacePieceRpc(Vector2IntList targetTilePositions)  //Wird im Multiplayer später zu CmdRequestPlacePiece(...)
    {
        /*
        if (targetTilePositions == null || targetTilePositions.Count < shape.Count)
        {
            return false;
        } 
        */

        transform.position = new Vector3(targetTilePositions.Values[0].x, targetTilePositions.Values[0].y, -2);
        board.SetTilesOccupied(targetTilePositions.Values, playerStatus);
        GameManager.Instance.GetPlayerByPlayerStatus(playerStatus).firstPiecePlaced = true;
        isPlaced = true;

        GameManager.Instance.EndTurn(this);
        //return true;
    }

    public List<Tile> GetTargetTilesForPiece(Tile nearestTile)
    {
        List<Tile> targetTiles = new List<Tile>();
        foreach (var gridPos in shape)
        {
            Vector2Int targetPos = new Vector2Int((int)nearestTile.transform.position.x, (int)nearestTile.transform.position.y) + gridPos;
            Tile targetTile = board.GetTileAtPosition(targetPos);

            if (targetTile == null)
                return new List<Tile>(); // Return empty list if any tile is invalid
            
            targetTiles.Add(targetTile);
        }
        return targetTiles;
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

    private void ActivateTileMarkers(List<Tile> targetTiles)
    {
        for (int i = 0; i < tileMarkers.Count; i++)
        {
            var marker = tileMarkers[i];
            marker.SetActive(true);
            marker.transform.position = targetTiles[i].transform.position + new Vector3(0, 0, -0.5f);
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
}
