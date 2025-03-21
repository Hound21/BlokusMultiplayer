using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game/PlayerData")]
public class PlayerDataSO : ScriptableObject
{
    public Dictionary<string, PlayerStatus> playerStati = new Dictionary<string, PlayerStatus>(); // PlayerID -> PlayerStatus
    public Dictionary<string, string> playerNames = new Dictionary<string, string>(); // PlayerID -> PlayerName
}
