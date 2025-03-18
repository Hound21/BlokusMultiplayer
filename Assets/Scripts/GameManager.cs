using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public Board board;
    public Button playerFinishedButton;
    public Dictionary<PlayerStatus, LocalPlayer> players;
    public PlayerStatus currentPlayerStatus;
    private List<PlayerStatus> playerOrder = new List<PlayerStatus>{
        PlayerStatus.PlayerRed, PlayerStatus.PlayerGreen, PlayerStatus.PlayerBlue, PlayerStatus.PlayerYellow
    };
    private int movesPlayed = 0;

    private void Awake()
    {
        Instance = this;

        players = new Dictionary<PlayerStatus, LocalPlayer>();

        players[PlayerStatus.PlayerRed] = new LocalPlayer(PlayerStatus.PlayerRed, Color.red);
        players[PlayerStatus.PlayerGreen] = new LocalPlayer(PlayerStatus.PlayerGreen, Color.green);
        players[PlayerStatus.PlayerBlue] = new LocalPlayer(PlayerStatus.PlayerBlue, Color.blue);
        players[PlayerStatus.PlayerYellow] = new LocalPlayer(PlayerStatus.PlayerYellow, Color.yellow);
    }

    void Start()
    {
        if (board != null)
        {
            board.InitializeBoard();
        }
        // add pieces to players
        foreach (Piece piece in FindObjectsOfType<Piece>())
        {
            players[piece.playerStatus].AddPiece(piece);
        }            
        currentPlayerStatus = playerOrder[0];

        if (playerFinishedButton != null)
        {
            playerFinishedButton.onClick.AddListener(OnPlayerFinishedButtonPressed);
            playerFinishedButton.gameObject.SetActive(false);
        }
    }

    public void EndTurn(Piece piece)
    {
        LocalPlayer currentPlayer = players[currentPlayerStatus];

        if (piece != null)
        {
            currentPlayer.RemovePiece(piece);
            currentPlayer.AddPoints(piece.points);
        }

        if (currentPlayer.availablePieces.Count == 0 || currentPlayer.pressedPlayerFinishButton)
        {
            currentPlayer.isFinished = true;
            Debug.Log(currentPlayer.playerStatus + " is finshed.");
        }

        if (players.Values.All(player => player.isFinished))
        {
            GameOver();
            return;
        }

        ChangePlayerTurn();
        //board.PrintGrid();
    }

    public void ChangePlayerTurn()
    {
        do
        {
            currentPlayerStatus = playerOrder[(playerOrder.IndexOf(currentPlayerStatus) + 1) % playerOrder.Count];
        } while (players[currentPlayerStatus].isFinished);

        movesPlayed++;
        UpdatePlayerFinishedButton();
    }

    public void GameOver()
    {
        Debug.Log("Game Over");

        // Find player with max points
        int maxPoints = int.MinValue;
        List<LocalPlayer> topPlayers = new List<LocalPlayer>();

        foreach (var player in players.Values)
        {
            if (player.Points > maxPoints)
            {
                maxPoints = player.Points;
                topPlayers.Clear();
                topPlayers.Add(player);
            }
            else if (player.Points == maxPoints)
            {
                topPlayers.Add(player);
            }
        }

        // Prüfe, ob es ein Unentschieden gibt
        if (topPlayers.Count > 1)
        {
            Debug.Log("It's a draw between: " + string.Join(", ", topPlayers.Select(p => p.playerStatus)));
        }
        else
        {
            Debug.Log(topPlayers[0].playerStatus + " wins!");
        }

        Debug.Log("Points:");
        foreach (var player in players.Values)
        {
            Debug.Log(player.playerStatus + ": " + player.Points);
        }
    }

    public void OnPlayerFinishedButtonPressed()
    {
        players[currentPlayerStatus].pressedPlayerFinishButton = true;
        EndTurn(null);
    }

    private void UpdatePlayerFinishedButton() //TODO
    {
        if (playerFinishedButton != null && movesPlayed >= 4)
        {
            playerFinishedButton.gameObject.SetActive(true);
        }
    }


    public PlayerStatus GetCurrentPlayerStatus()
    {
        return currentPlayerStatus;
    }


    public LocalPlayer GetPlayerByPlayerStatus(PlayerStatus playerStatus)
    {
        return players[playerStatus];
    }
}
