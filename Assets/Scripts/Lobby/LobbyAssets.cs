using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LobbyAssets : MonoBehaviour {



    public static LobbyAssets Instance { get; private set; }


    [SerializeField] private Sprite redCharacterSprite;
    [SerializeField] private Sprite greenCharacterSprite;
    [SerializeField] private Sprite blueCharacterSprite;
    [SerializeField] private Sprite yellowCharacterSprite;


    private void Awake() {
        Instance = this;
    }

    public Sprite GetSprite(PlayerStatus playerStatus) {
        switch (playerStatus) {
            default:
            case PlayerStatus.PlayerRed:   return redCharacterSprite;
            case PlayerStatus.PlayerGreen:    return greenCharacterSprite;
            case PlayerStatus.PlayerBlue:   return blueCharacterSprite;
            case PlayerStatus.PlayerYellow: return yellowCharacterSprite;
        }
    }

}