using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using System.Drawing;
using Unity.Collections;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private bool debugMode = false; // Set this in the Inspector

    public const int COUNT_PIECES_PER_PLAYER = 3;

    public static GameManager Instance { get; private set; }
    public Board board;

    public Button playerFinishedButton;
    private PlayerStatus localPlayerStatus;
    //private string localPlayerName;
    public NetworkList<LocalPlayerData> playerDataList;
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

        boardOccupied = new NetworkList<int>(new List<int>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        playerDataList = new NetworkList<LocalPlayerData>(new List<LocalPlayerData>(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        //currentPlayerStatus = playerOrder[0];


        if (playerFinishedButton != null)
        {
            playerFinishedButton.onClick.AddListener(() => OnPlayerFinishedButtonPressed());
            playerFinishedButton.gameObject.SetActive(false);
        }
    }

    private void Start() {
        if (debugMode) {
#if UNITY_EDITOR
                Debug.Log("Starting in Debug Mode as Host...");
                NetworkManager.Singleton.StartHost();
#else                
                Debug.Log("Starting in Debug Mode as Client...");
                NetworkManager.Singleton.StartClient();
#endif
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!debugMode) {
            localPlayerStatus = LobbyManager.Instance.GetCurrentPlayerStatus();
            Debug.Log("Set local player Status: " + localPlayerStatus);
        }
        else {
            if (IsHost) {
                localPlayerStatus = PlayerStatus.PlayerRed;
            } else if (IsClient) {
                localPlayerStatus = PlayerStatus.PlayerGreen;
            } else {
                localPlayerStatus = PlayerStatus.None;
            }
        }

        // Initialize boardOccupied
        if (IsServer)
        {
           InitializeBoardOccupied();
        }

        AddPlayerServerRpc(NetworkManager.Singleton.LocalClientId.ToString(), localPlayerStatus);

        if (IsServer) {
            NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        }
        
        // bisher nicht benutzt aber wird später für die anzeige wer dran ist benötigt
        currentPlayerStatus.OnValueChanged += (PlayerStatus oldPlayerStatus, PlayerStatus newPlayerStatus) =>
        {
            OnCurrentPlayerStatusChanged?.Invoke(this, EventArgs.Empty);
            Debug.Log("Current Player Status changed from " + oldPlayerStatus + " to " + newPlayerStatus);
        };
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log("OnClientConnectedCallback: clientId =" + clientId);
        
        // print every player from players
        foreach (var player in playerDataList)
        {
            Debug.Log("Player: " + player.playerStatus + " clientId: " + player.clientId);
        }


        // if all players are connected, start the game
        if (NetworkManager.Singleton.ConnectedClientsList.Count == 4) //TODO change hard coded 2
        {
            // Start game
            currentPlayerStatus.Value = PlayerStatus.PlayerRed;
            TriggerOnGameStartedServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddPlayerServerRpc(string networkId, PlayerStatus playerStatus)
    {
        if (!IsServer) return;

        playerDataList.Add(new LocalPlayerData {
            playerStatus = playerStatus,
            clientId = networkId,
            points = 0,
            availablePiecesCount = COUNT_PIECES_PER_PLAYER,
            isFinished = false,
            pressedPlayerFinishButton = false,
        });
    }

    // bisher nicht benutzt
    [ServerRpc]
    private void TriggerOnGameStartedServerRpc() {
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

    // wird nur vom Server aufgerufen
    public void SetTilesOccupied(Vector2IntList tileGridPositions, PlayerStatus playerStatus)
    {
            foreach (var tileGridPos in tileGridPositions.Values)
            {
                SetTileOccupiedRpc(tileGridPos, playerStatus);
            }
    }

    // wird somit auch nur vom Server aufgerufen
    public void SetTileOccupiedRpc(Vector2Int tilePos, PlayerStatus playerStatus)
    {
        if (!IsServer) return;
        boardOccupied[tilePos.x + tilePos.y * Board.boardXSize] = (int)playerStatus;
    }


    // wird nur vom Server aufgerufen
    public void EndTurn(Piece piece)
    {
        if (!IsServer) return;

        // Finde den aktuellen Spieler
        int currentPlayerIndex = GetCurrentPlayerIndex();
        var currentPlayer = playerDataList[currentPlayerIndex];

        // Aktualisiere Punkte und verfügbare Pieces
        if (piece != null)
        {
            currentPlayer.points += piece.points;
            currentPlayer.availablePiecesCount--;
        }

        // Überprüfe, ob der Spieler fertig ist
        if (currentPlayer.availablePiecesCount == 0 || currentPlayer.pressedPlayerFinishButton)
        {
            currentPlayer.isFinished = true;
            Debug.Log(currentPlayer.playerStatus + " is finished.");
        }

        // Aktualisiere die Daten des Spielers
        playerDataList[currentPlayerIndex] = currentPlayer;

        // Überprüfe, ob alle Spieler fertig sind
        bool allFinished = true;
        foreach (var player in playerDataList)
        {
            if (!player.isFinished)
            {
                allFinished = false;
                break;
            }
        }

        if (allFinished)
        {
            Debug.Log("All players are finished.");
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
            // Wechsle zum nächsten Spieler in der Reihenfolge
            int currentIndex = playerOrder.IndexOf(currentPlayerStatus.Value);
            currentPlayerStatus.Value = playerOrder[(currentIndex + 1) % playerOrder.Count];

            // Überprüfe, ob der nächste Spieler fertig ist
            bool isFinished = false;
            foreach (var player in playerDataList)
            {
                if (player.playerStatus == currentPlayerStatus.Value)
                {
                    isFinished = player.isFinished;
                    break;
                }
            }

            // Wiederhole, falls der Spieler fertig ist
            if (!isFinished) break;

        } while (true);

        movesPlayed++;
        UpdatePlayerFinishedButtonClientRpc();
    }

    public void GameOver()
    {
        if (!IsServer) return;

        Debug.Log("Server Game Over");

        // Find player with max points
        int maxPoints = int.MinValue;
        List<LocalPlayerData> topPlayers = new List<LocalPlayerData>();

        foreach (var player in playerDataList)
        {
            if (player.points > maxPoints)
            {
                maxPoints = player.points;
                topPlayers.Clear();
                topPlayers.Add(player);
            }
            else if (player.points == maxPoints)
            {
                topPlayers.Add(player);
            }
        }

        // Erstelle eine Liste mit den Punkten aller Spieler
        List<PlayerStatus> allPlayerStatuses = new List<PlayerStatus>();
        List<int> allPlayerPoints = new List<int>();
        foreach (var player in playerDataList)
        {
            allPlayerStatuses.Add(player.playerStatus);
            allPlayerPoints.Add(player.points);
        }

        // Prüfe, ob es ein Unentschieden gibt
        if (topPlayers.Count > 1)
        {
            Debug.Log("Server: It's a draw between: " + string.Join(", ", topPlayers.Select(p => p.playerStatus)));
            GameOverClientRpc(true, topPlayers.Select(p => p.playerStatus).ToArray(), maxPoints, allPlayerStatuses.ToArray(), allPlayerPoints.ToArray());
        }
        else
        {
            Debug.Log("Server: " + topPlayers[0].playerStatus + " wins!");
            GameOverClientRpc(false, new PlayerStatus[] { topPlayers[0].playerStatus }, maxPoints, allPlayerStatuses.ToArray(), allPlayerPoints.ToArray());
        }
    }

    [ClientRpc]
    private void GameOverClientRpc(bool isDraw, PlayerStatus[] winners, int points, PlayerStatus[] allPlayerStatuses, int[] allPlayerPoints)
    {
        if (isDraw)
        {
            Debug.Log("Game Over! It's a draw between: " + string.Join(", ", winners) + " with " + points + " points.");
        }
        else
        {
            Debug.Log("Game Over! Winner: " + winners[0] + " with " + points + " points.");
        }

        Debug.Log("Points for all players:");
        for (int i = 0; i < allPlayerStatuses.Length; i++)
        {
            Debug.Log(allPlayerStatuses[i] + ": " + allPlayerPoints[i] + " points");
        }
    }

    public void OnPlayerFinishedButtonPressed()
    {
        OnPlayerFinishedButtonPressedServerRpc(localPlayerStatus);
    }

    [ServerRpc(RequireOwnership = false)]
    public void OnPlayerFinishedButtonPressedServerRpc(PlayerStatus playerStatus)
    {
        if (!IsServer) return;

        SetPlayerFinishedButtonPressed(playerStatus, true);
        Debug.Log(playerStatus + " pressed the finish button.");

        EndTurn(null);
    }

    [ClientRpc]
    private void UpdatePlayerFinishedButtonClientRpc()
    {
        var localPlayerData = GetLocalPlayerData();

        if (localPlayerData.isFinished || localPlayerData.pressedPlayerFinishButton || movesPlayed <= 8)
        {
            playerFinishedButton.gameObject.SetActive(false);
        }
        else
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

    public int GetCurrentPlayerIndex()
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].playerStatus == currentPlayerStatus.Value)
            {
                return i;
            }
        }
        return -1; // Spieler nicht gefunden
    }

    public LocalPlayerData GetLocalPlayerData()
    {
        string localClientId = NetworkManager.Singleton.LocalClientId.ToString();

        foreach (var player in playerDataList)
        {
            if (player.clientId.ToString() == localClientId)
            {
                return player;
            }
        }

        // Rückgabe eines Standardwerts, falls kein Spieler gefunden wird
        return default;
    }

    public LocalPlayerData GetPlayerDataByStatus(PlayerStatus playerStatus)
    {
        foreach (var player in playerDataList)
        {
            if (player.playerStatus == playerStatus)
            {
                return player;
            }
        }

        // Rückgabe eines Standardwerts, falls kein Spieler gefunden wird
        return default;
    }

    // wird nur vom Server aufgerufen
    public void SetFirstPiecePlacedForPlayerStatus(PlayerStatus playerStatus, bool value)
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].playerStatus == playerStatus)
            {
                var playerData = playerDataList[i];
                playerData.firstPiecePlaced = value;
                playerDataList[i] = playerData;
                break;
            }
        }
    }

    // wird nur vom Server aufgerufen
    public void SetPlayerFinishedButtonPressed(PlayerStatus playerStatus, bool value)
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].playerStatus == playerStatus)
            {
                var playerData = playerDataList[i];
                playerData.pressedPlayerFinishButton = value;
                playerDataList[i] = playerData;
                break;
            }
        }
    }


    public UnityEngine.Color GetColorForPlayerStatus(PlayerStatus playerStatus)
    {
        switch (playerStatus)
        {
            case PlayerStatus.PlayerRed:
                return UnityEngine.Color.red;
            case PlayerStatus.PlayerGreen:
                return UnityEngine.Color.green;
            case PlayerStatus.PlayerBlue:
                return UnityEngine.Color.blue;
            case PlayerStatus.PlayerYellow:
                return UnityEngine.Color.yellow;
            default:
                return UnityEngine.Color.white;
        }
    }

    // override onDestroy to dispose boardOccupied
    public override void OnDestroy()
    {
        boardOccupied.Dispose();
        playerDataList.Dispose();
    }

}


[Serializable]
public struct LocalPlayerData : INetworkSerializable, IEquatable<LocalPlayerData>
{
    public PlayerStatus playerStatus;
    public FixedString128Bytes clientId;
    public int availablePiecesCount;
    public int points;
    public bool isFinished;
    public bool firstPiecePlaced;
    public bool pressedPlayerFinishButton;

    // Implementierung von INetworkSerializable
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref playerStatus);
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref availablePiecesCount);
        serializer.SerializeValue(ref points);
        serializer.SerializeValue(ref isFinished);
        serializer.SerializeValue(ref firstPiecePlaced);
        serializer.SerializeValue(ref pressedPlayerFinishButton);
    }

    // Implementierung von IEquatable<LocalPlayerData>
    public bool Equals(LocalPlayerData other)
    {
        return playerStatus == other.playerStatus &&
               clientId.Equals(other.clientId) &&
               availablePiecesCount == other.availablePiecesCount &&
               points == other.points &&
               isFinished == other.isFinished &&
               firstPiecePlaced == other.firstPiecePlaced &&
               pressedPlayerFinishButton == other.pressedPlayerFinishButton;
    }

    // Überschreibe GetHashCode (optional, aber empfohlen)
    public override int GetHashCode()
    {
        return HashCode.Combine(playerStatus, clientId, availablePiecesCount, points, isFinished, firstPiecePlaced, pressedPlayerFinishButton);
    }

    // Überschreibe Equals für object (optional, aber empfohlen)
    public override bool Equals(object obj)
    {
        return obj is LocalPlayerData other && Equals(other);
    }
}

