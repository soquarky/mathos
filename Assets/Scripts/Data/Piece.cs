using System;
using UnityEngine;

namespace StevensMathOS.Data
{
    /// <summary>
    /// Piece type enumeration.
    /// </summary>
    public enum PieceType
    {
        Rock = 0,
        Paper = 1,
        Scissors = 2,
        Empty = 3
    }

    /// <summary>
    /// Lightweight value-type representing a grid piece.
    /// </summary>
    public struct Piece
    {
        public Vector2Int position;
        public PieceType type;
        public int owner;
        public float health;

        /// <summary>
        /// Copy constructor for cloning.
        /// </summary>
        public Piece(Piece other)
        {
            position = other.position;
            type = other.type;
            owner = other.owner;
            health = other.health;
        }

        /// <summary>
        /// Human readable type string.
        /// </summary>
        public string GetTypeString()
        {
            switch (type)
            {
                case PieceType.Rock: return "Rock";
                case PieceType.Paper: return "Paper";
                case PieceType.Scissors: return "Scissors";
                default: return "Empty";
            }
        }

        /// <summary>
        /// ToString override for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"Piece({GetTypeString()}, owner={owner}, health={health:F1})";
        }
    }

    /*
     EXAMPLE:
     // Piece p = new Piece { position = new Vector2Int(0,0), type = PieceType.Rock, owner = 1, health = 10f };
     // Piece clone = new Piece(p);
     // Debug.Log(clone.GetTypeString());
     */
}
