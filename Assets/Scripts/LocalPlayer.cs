using UnityEngine;
using System.Collections.Generic;

public class LocalPlayer
{
    public PlayerStatus playerStatus;
    public Color color;
    public List<Piece> availablePieces;
    public int Points { get; set; }
    public bool isFinished { get; set; } = false;
    public bool firstPiecePlaced;
    public bool pressedPlayerFinishButton;


    public LocalPlayer(PlayerStatus playerStatus, Color color)
    {
        this.playerStatus = playerStatus;
        this.color = color;
        this.firstPiecePlaced = false;
        this.pressedPlayerFinishButton = false;
        this.availablePieces = new List<Piece>();
        Points = 0;
    }

    public void AddPiece(Piece piece)
    {
        availablePieces.Add(piece);
    }

    public void RemovePiece(Piece piece)
    {
        availablePieces.Remove(piece);
    }

    public void AddPoints(int points)
    {
        Points += points;
    }

    public bool HasValidMove()
    {
        //TODO
        return true;
    }
}
