using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    public Board board;
    public PlayerDataSO playerData;
    public Button playerFinishedButton;
    private PlayerStatus localPlayerStatus;
    private string localPlayerName;
    public Dictionary<PlayerStatus, LocalPlayer> players;
    public NetworkVariable<PlayerStatus> currentPlayerStatus = new NetworkVariable<PlayerStatus>();
    private List<PlayerStatus> playerOrder = new List<PlayerStatus>{
        PlayerStatus.PlayerRed, PlayerStatus.PlayerGreen, PlayerStatus.PlayerBlue, PlayerStatus.PlayerYellow
    };
    private int movesPlayed = 0;

    public event EventHandler OnCurrentPlayerStatusChanged;
    public event EventHandler OnGameStarted;

    private void Awake()
    {
        Instance = this;

        players = new Dictionary<PlayerStatus, LocalPlayer>();

        players[PlayerStatus.PlayerRed] = new LocalPlayer(PlayerStatus.PlayerRed, Color.red);
        players[PlayerStatus.PlayerGreen] = new LocalPlayer(PlayerStatus.PlayerGreen, Color.green);
        players[PlayerStatus.PlayerBlue] = new LocalPlayer(PlayerStatus.PlayerBlue, Color.blue);
        players[PlayerStatus.PlayerYellow] = new LocalPlayer(PlayerStatus.PlayerYellow, Color.yellow);

        if (board != null)
        {
            board.InitializeBoard();
        }
        // add pieces to players
        foreach (Piece piece in FindObjectsOfType<Piece>())
        {
            players[piece.playerStatus].AddPiece(piece);
        }            
        //currentPlayerStatus = playerOrder[0];

        if (playerFinishedButton != null)
        {
            playerFinishedButton.onClick.AddListener(OnPlayerFinishedButtonPressed);
            playerFinishedButton.gameObject.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn: " + NetworkManager.Singleton.LocalClientId);

        /* get color from lobby player and set it to player
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(NetworkManager.Singleton.LocalClientId, out var networkedClient))
        {
            localPlayerStatus = networkedClient.PlayerObject.Data[LobbyManager.KEY_PLAYER_STATUS].Value;
            Debug.Log("Local player status: " + localPlayerStatus);
        }
        */

        if (IsServer) {
            NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        }

        currentPlayerStatus.OnValueChanged += (PlayerStatus oldPlayerStatus, PlayerStatus newPlayerStatus) =>
        {
            OnCurrentPlayerStatusChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private void NetworkManager_OnClientConnectedCallback(ulong obj)
    {
        if (NetworkManager.Singleton.ConnectedClientsList.Count == 2) //TODO change hard coded 2
        {
            // Start game
            currentPlayerStatus.Value = PlayerStatus.PlayerRed;
            TriggerOnGameStartedRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameStartedRpc() {
        OnGameStarted?.Invoke(this, EventArgs.Empty);
    }

    public void EndTurn(Piece piece)
    {
        LocalPlayer currentPlayer = players[currentPlayerStatus.Value];

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
            currentPlayerStatus.Value = playerOrder[(playerOrder.IndexOf(currentPlayerStatus.Value) + 1) % playerOrder.Count];
        } while (players[currentPlayerStatus.Value].isFinished);

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
        players[currentPlayerStatus.Value].pressedPlayerFinishButton = true;
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
        return currentPlayerStatus.Value;
    }

    public PlayerStatus GetLocalPlayerStatus()
    {
        return localPlayerStatus;
    }


    public LocalPlayer GetPlayerByPlayerStatus(PlayerStatus playerStatus)
    {
        return players[playerStatus];
    }
}
