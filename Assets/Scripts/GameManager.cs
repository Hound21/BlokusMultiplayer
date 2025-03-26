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

    public Button playerFinishedButton;
    private PlayerStatus localPlayerStatus;
    private string localPlayerName;
    public Dictionary<PlayerStatus, LocalPlayer> players = new Dictionary<PlayerStatus, LocalPlayer>();
    public NetworkVariable<PlayerStatus> currentPlayerStatus = new NetworkVariable<PlayerStatus>(PlayerStatus.None);
    public NetworkList<int> boardOccupied;

    private List<PlayerStatus> playerOrder = new List<PlayerStatus>{
        PlayerStatus.PlayerRed, PlayerStatus.PlayerGreen, PlayerStatus.PlayerBlue, PlayerStatus.PlayerYellow
    };
    private int movesPlayed = 0;

    public event EventHandler OnCurrentPlayerStatusChanged;
    public event EventHandler OnGameStarted;

    private void Awake()
    {
        if (Instance == null) {
            Instance = this;
        }
        else {
            Destroy(gameObject);
        }

        boardOccupied = new NetworkList<int>();

        // Initialize boardOccupied
        if (IsServer)
        {
           InitializeBoardOccupied();
        }

        /*
        players[PlayerStatus.PlayerRed] = new LocalPlayer(PlayerStatus.PlayerRed, Color.red);
        players[PlayerStatus.PlayerGreen] = new LocalPlayer(PlayerStatus.PlayerGreen, Color.green);
        players[PlayerStatus.PlayerBlue] = new LocalPlayer(PlayerStatus.PlayerBlue, Color.blue);
        players[PlayerStatus.PlayerYellow] = new LocalPlayer(PlayerStatus.PlayerYellow, Color.yellow);
        

        // add pieces to players
        foreach (Piece piece in FindObjectsOfType<Piece>())
        {
            players[piece.playerStatus].AddPiece(piece);
        }            
        //currentPlayerStatus = playerOrder[0];
        */

        if (playerFinishedButton != null)
        {
            playerFinishedButton.onClick.AddListener(OnPlayerFinishedButtonPressed);
            playerFinishedButton.gameObject.SetActive(false);
        }
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("OnNetworkSpawn: " + NetworkManager.Singleton.LocalClientId);

        LobbyManager.Instance.UpdatePlayerNetworkId();
        localPlayerStatus = LobbyManager.Instance.GetCurrentPlayerStatus();
        Debug.Log("Local Player Status: " + localPlayerStatus + "mit NetworkID-PlayerStatus: " + LobbyManager.Instance.GetPlayerStatusByNetworkId(NetworkManager.Singleton.LocalClientId.ToString()));

        // server muss wisser wer connected ist
        if (IsServer) {
            NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        }
        
        // bisher nicht benutzt aber wird später für die anzeige wer dran ist benötigt
        currentPlayerStatus.OnValueChanged += (PlayerStatus oldPlayerStatus, PlayerStatus newPlayerStatus) =>
        {
            OnCurrentPlayerStatusChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log("OnClientConnectedCallback: clientId =" + clientId);

        PlayerStatus clientPlayerStatus = LobbyManager.Instance.GetPlayerStatusByNetworkId(clientId.ToString());
        if (clientPlayerStatus == PlayerStatus.None) {
            Debug.LogError("Client has no player status");
            return;
        }
        if (players.ContainsKey(clientPlayerStatus)) {
            Debug.LogError("Player status already exists");
            return;
        }
        players[clientPlayerStatus] = new LocalPlayer(clientPlayerStatus, GetColorForPlayerStatus(clientPlayerStatus));
        Debug.Log("Player " + clientPlayerStatus + " added");


        // if all players are connected, start the game
        if (NetworkManager.Singleton.ConnectedClientsList.Count == 4) //TODO change hard coded 2
        {
            // Start game
            currentPlayerStatus.Value = PlayerStatus.PlayerRed;
            TriggerOnGameStartedRpc();
        }
    }

    // bisher nicht benutzt
    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnGameStartedRpc() {
        OnGameStarted?.Invoke(this, EventArgs.Empty);
    }

    // wird nur vom Server aufgerufen
    private void InitializeBoardOccupied()
    {
        for (int i = 0; i < Board.boardYSize; i++)
            {
                for (int j = 0; j < Board.boardXSize; j++)
                {
                    boardOccupied.Add((int)PlayerStatus.None);
                }
            }
    }

    [Rpc(SendTo.Server)]
    public void SetTileOccupiedRpc(Vector2Int tilePos, PlayerStatus playerStatus)
    {
        boardOccupied[tilePos.x + tilePos.y * Board.boardXSize] = (int)playerStatus;
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

    public Color GetColorForPlayerStatus(PlayerStatus playerStatus)
    {
        switch (playerStatus)
        {
            case PlayerStatus.PlayerRed:
                return Color.red;
            case PlayerStatus.PlayerGreen:
                return Color.green;
            case PlayerStatus.PlayerBlue:
                return Color.blue;
            case PlayerStatus.PlayerYellow:
                return Color.yellow;
            default:
                return Color.white;
        }
    }

    // override onDestroy to dispose boardOccupied
    public override void OnDestroy()
    {
        boardOccupied.Dispose();
    }

}
