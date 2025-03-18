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

    public Sprite GetSprite(LobbyManager.PlayerCharacter playerCharacter) {
        switch (playerCharacter) {
            default:
            case LobbyManager.PlayerCharacter.RedCharacter:   return redCharacterSprite;
            case LobbyManager.PlayerCharacter.GreenCharacter:    return greenCharacterSprite;
            case LobbyManager.PlayerCharacter.BlueCharacter:   return blueCharacterSprite;
            case LobbyManager.PlayerCharacter.YellowCharacter: return yellowCharacterSprite;
        }
    }

}