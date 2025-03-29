using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class Board : NetworkBehaviour
{
    public Tile tilePrefab;
    public Tile[,] boardTiles;
    public const int boardXSize = 20;
    public const int boardYSize = 20;

    //public event EventHandler OnBoardOccupiedChanged;

    void Start()
    {
        boardTiles = new Tile[boardXSize, boardYSize];
        InitializeBoard();
    }


    /*
    public void PrintGrid()
    {
        for (int i = 0; i < 20; i++)
        {
            //print row
            string row = "";
            for (int j = 0; j < 20; j++)
            {
                row += boardOccupied[i, j] + " ";
            }
            Debug.Log(row);
        }
    }
    */

    public void InitializeBoard()
    {
        for (int i = 0; i < boardYSize; i++)
        {
            for (int j = 0; j < boardXSize; j++)
            {
                Tile tile = Instantiate(tilePrefab, new Vector3(i, j, 0), Quaternion.identity);
                bool isOffset = (i % 2 == 0 && j % 2 != 0) || (i % 2 != 0 && j % 2 == 0);
                tile.Init(isOffset);
                boardTiles[i, j] = tile;
                //boardOccupied[i, j] = PlayerStatus.None;
                tile.gridPosition = new Vector2Int(i, j);
            }
        }
    }


    public Vector2Int GetClosestTileGridPosition(Vector3 position)
    {
        float closestDistance = Mathf.Infinity;
        Tile closestTile = null;

        for (int x = 0; x < boardXSize; x++)
        {
            for (int y = 0; y < boardYSize; y++)
            {
                Tile tile = boardTiles[x, y];
                if (tile == null) continue;

                float distance = Vector3.Distance(tile.transform.position, position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tile;
                }
            }
        }
        return closestDistance <= 2.25f ? closestTile.gridPosition : new Vector2Int(-1, -1);
    }

    /*
    public Tile GetClosestTile(Vector3 position)
    {
        float closestDistance = Mathf.Infinity;
        Tile closestTile = null;

        for (int x = 0; x < boardXSize; x++)
        {
            for (int y = 0; y < boardYSize; y++)
            {
                Tile tile = boardTiles[x, y];
                if (tile == null) continue;

                float distance = Vector3.Distance(tile.transform.position, position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tile;
                }
            }
        }
        return closestDistance <= 2.25f ? closestTile : null;
    }
    */


    public bool AreTilesValidForPlacement(Vector2IntList tileGridPositions, PlayerStatus playerStatus)
    {
        if (tileGridPositions.Values.Count == 0)
            return false;

        // Check if all tiles are on the board
        foreach (Vector2Int pos in tileGridPositions.Values)
        {
            if (!IsPositionOnBoard(pos))
            {
                return false;
            }
        }

        // Check if any tile is already occupied
        foreach (Vector2Int pos in tileGridPositions.Values)
        {
            if (GetGridPositionOccupant(pos) != PlayerStatus.None)
            {
                //Debug.Log("Tile is already occupied");
                return false;
            }
        }

        LocalPlayer player = GameManager.Instance.players[playerStatus];

        // Check starting corner for first piece
        if (!player.firstPiecePlaced)
        {
            Vector2Int startGridPos = GetStartingGridPositionForPlayer(playerStatus);
            foreach (Vector2Int gridPos in tileGridPositions.Values)
            {
                if (gridPos == startGridPos)
                {
                    return true;
                }
                return false;
            }
        }

        // Check adjacent rules
        bool hasCornerAdjacent = false;
        foreach (Vector2Int gridPos in tileGridPositions.Values)
        {
            if (CheckIfFriendlyPieceAtCorner(gridPos, playerStatus))
            {
                hasCornerAdjacent = true;
            }

            if (CheckIfFriendlyPieceAtSide(gridPos, playerStatus))
            {
                //Debug.Log("Tile has friendly piece at side");
                return false;
            }
        }
        //Debug.Log("Tile has friendly piece at corner: " + hasCornerAdjacent);
        return hasCornerAdjacent;
    }

    private Vector2Int GetStartingGridPositionForPlayer(PlayerStatus playerStatus)
    {
        Vector2Int startPos;

        switch (playerStatus)
        {
            case PlayerStatus.PlayerRed:
                startPos = new Vector2Int(boardXSize - 1, 0); // Rechts unten
                break;
            case PlayerStatus.PlayerGreen:
                startPos = new Vector2Int(0, 0); // Links unten
                break;
            case PlayerStatus.PlayerBlue:
                startPos = new Vector2Int(0, boardYSize - 1); // Links oben
                break;
            case PlayerStatus.PlayerYellow:
                startPos = new Vector2Int(boardXSize - 1, boardYSize - 1); // Rechts oben
                break;
            default:
                return new Vector2Int(-1, -1);
        }

        return startPos;
    }

    private bool CheckIfFriendlyPieceAtCorner(Vector2Int gridPos, PlayerStatus playerStatus)
    {
        Vector2Int[] cornerOffsets = {
            new Vector2Int(-1, -1), new Vector2Int(1, 1),
            new Vector2Int(-1, 1), new Vector2Int(1, -1)
        };

        return CheckIfFriendlyPiece(gridPos, playerStatus, cornerOffsets);
    }

    private bool CheckIfFriendlyPieceAtSide(Vector2Int gridPos, PlayerStatus playerStatus)
    {
        Vector2Int[] sideOffsets = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(0, 1)
        };

        return CheckIfFriendlyPiece(gridPos, playerStatus, sideOffsets);
    }

    private bool CheckIfFriendlyPiece(Vector2Int position, PlayerStatus playerStatus, Vector2Int[] offsets)
    {
        foreach (var offset in offsets)
        {
            Vector2Int targetPos = position + offset;
            if (!IsPositionOnBoard(targetPos))
            {
                continue;
            }    
            PlayerStatus occupyingPlayer = GetGridPositionOccupant(targetPos);

            if (occupyingPlayer == playerStatus)
            {
                return true;
            }
        }
        return false;
    }


    public void SetTilesOccupied(Vector2IntList tileGridPositions, PlayerStatus playerStatus)
    {
            foreach (var tileGridPos in tileGridPositions.Values)
            {
                GameManager.Instance.SetTileOccupiedRpc(tileGridPos, playerStatus);
            }
    }

    private PlayerStatus GetGridPositionOccupant(Vector2Int pos) //OVERLOADED
    {
        Debug.Log("Debug");
        return (PlayerStatus)GameManager.Instance.boardOccupied[pos.x + pos.y * boardXSize];
    }

    /* nichtmehr benutzt
    public PlayerStatus GetGridPositionOccupant(Tile tile) //OVERLOADED
    {
        return boardOccupied[(int)tile.transform.position.x, (int)tile.transform.position.y];
    }
    */

    private bool IsPositionOnBoard(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= boardXSize || pos.y < 0 || pos.y >= boardYSize)
        {
            return false;
        }
        return true;
    }


    /* nichtmehr benutzt
    public Tile GetTileAtPosition(Vector2Int pos)
    {
        int x = pos.x;
        int y = pos.y;

        if (x < 0 || x >= boardXSize || y < 0 || y >= boardYSize)
        {
            return null;
        }
        return boardTiles[x, y];
    }
    */

    /*
   private void OnDestroy() 
    {
        boardOccupied.Clear();
        boardOccupied = null;        
    }
    */
}


public enum PlayerStatus
{
    None = -1,
    PlayerRed = 0,
    PlayerGreen = 1,
    PlayerBlue = 2,
    PlayerYellow = 3
}