using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    public Tile tilePrefab;
    public Tile[,] boardTiles = new Tile[20, 20];
    public PlayerStatus[,] boardOccupied = new PlayerStatus[20, 20];
    public int boardXSize;
    public int boardYSize;

    void Start()
    {
        if (boardTiles[0, 0] == null)
        {
            InitializeBoard();
        }
        boardXSize = boardTiles.GetLength(0);
        boardYSize = boardTiles.GetLength(1);
    }

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

    public void InitializeBoard()
    {
        for (int i = 0; i < 20; i++)
        {
            for (int j = 0; j < 20; j++)
            {
                Tile tile = Instantiate(tilePrefab, new Vector3(i, j, 0), Quaternion.identity);
                bool isOffset = (i % 2 == 0 && j % 2 != 0) || (i % 2 != 0 && j % 2 == 0);
                tile.Init(isOffset);
                boardTiles[i, j] = tile;
                boardOccupied[i, j] = PlayerStatus.None;
            }
        }
    }

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




    public bool AreTilesValidForPlacement(List<Tile> tiles, PlayerStatus playerStatus)
    {
        if (tiles == null || tiles.Count == 0)
            return false;

        // Check if any tile is already occupied
        foreach (Tile tile in tiles)
        {
            if (IsTileOccupied(new Vector2Int(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y))) != PlayerStatus.None)
            {
                //Debug.Log("Tile is already occupied");
                return false;
            }
        }

        LocalPlayer player = GameManager.Instance.players[playerStatus];

        // Check starting corner for first piece
        if (!player.firstPiecePlaced)
        {
            Tile startTile = GetStartingTileForPlayer(playerStatus);
            foreach (Tile tile in tiles)
            {
                if (tile == startTile)
                    return true;
            }
            return false;
        }

        // Check adjacent rules
        bool hasCornerAdjacent = false;
        foreach (Tile tile in tiles)
        {
            if (CheckIfFriendlyPieceAtCorner(tile, playerStatus))
            {
                hasCornerAdjacent = true;
            }

            if (CheckIfFriendlyPieceAtSide(tile, playerStatus))
            {
                //Debug.Log("Tile has friendly piece at side");
                return false;
            }
        }
        //Debug.Log("Tile has friendly piece at corner: " + hasCornerAdjacent);
        return hasCornerAdjacent;
    }

    private Tile GetStartingTileForPlayer(PlayerStatus playerStatus)
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
                return null;
        }

        return GetTileAtPosition(startPos);
    }

    private bool CheckIfFriendlyPieceAtCorner(Tile tile, PlayerStatus playerStatus)
    {
        Vector2Int[] cornerOffsets = {
            new Vector2Int(-1, -1), new Vector2Int(1, 1),
            new Vector2Int(-1, 1), new Vector2Int(1, -1)
        };

        return CheckIfFriendlyPiece(new Vector2Int(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y)), playerStatus, cornerOffsets);
    }

    private bool CheckIfFriendlyPieceAtSide(Tile tile, PlayerStatus playerStatus)
    {
        Vector2Int[] sideOffsets = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(0, 1)
        };

        return CheckIfFriendlyPiece(new Vector2Int(Mathf.RoundToInt(tile.transform.position.x), Mathf.RoundToInt(tile.transform.position.y)), playerStatus, sideOffsets);
    }

    private bool CheckIfFriendlyPiece(Vector2Int position, PlayerStatus playerStatus, Vector2Int[] offsets)
    {
        foreach (var offset in offsets)
        {
            Vector2Int targetPos = position + offset;
            if (targetPos.x < 0 || targetPos.x >= boardXSize || targetPos.y < 0 || targetPos.y >= boardYSize)
            {
                continue;
            }    
            PlayerStatus occupyingPlayer = IsTileOccupied(targetPos);

            if (occupyingPlayer == playerStatus)
            {
                return true;
            }
        }
        return false;
    }


    public void SetTilesOccupied(List<Tile> tiles, PlayerStatus playerStatus)
    {
        foreach (var tile in tiles)
        {
            SetTileOccupied(tile, playerStatus);
        }
    }

    public void SetTileOccupied(Tile tile, PlayerStatus playerStatus)
    {
        boardOccupied[(int)tile.transform.position.x, (int)tile.transform.position.y] = playerStatus;
    }

    public PlayerStatus IsTileOccupied(Vector2Int pos) //OVERLOADED
    {
        return boardOccupied[pos.x, pos.y];
    }

    public PlayerStatus IsTileOccupied(Tile tile) //OVERLOADED
    {
        return boardOccupied[(int)tile.transform.position.x, (int)tile.transform.position.y];
    }

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
}


public enum PlayerStatus
{
    None = -1,
    PlayerRed = 0,
    PlayerGreen = 1,
    PlayerBlue = 2,
    PlayerYellow = 3
}