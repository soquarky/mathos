using System;
using UnityEngine;
using StevensMathOS.Data;

namespace StevensMathOS.GameModes.DoltPvP.Physics
{
    /// <summary>
    /// Result of a duel resolution.
    /// </summary>
    public struct DuelResult
    {
        public int winnerId;
        public float damageDealt;
        public Vector2Int victimPosition;
        public bool isCritical;
        public string description;
    }

    /// <summary>
    /// Static resolver that computes duel outcomes between two pieces.
    /// </summary>
    public static class DuelResolver
    {
        /// <summary>
        /// Resolves a duel between attacker and defender using the non-transitive payoff matrix.
        /// </summary>
        /// <param name="attacker">Attacking piece.</param>
        /// <param name="defender">Defending piece.</param>
        /// <param name="eventBus">Optional event bus (no events fired here per spec).</param>
        /// <returns>DuelResult describing the outcome.</returns>
        public static DuelResult ResolveDuel(Piece attacker, Piece defender, GameEventBus eventBus = null)
        {
            var result = new DuelResult
            {
                winnerId = -1,
                damageDealt = 0f,
                victimPosition = defender.position,
                isCritical = false,
                description = "Invalid duel"
            };

            // Payoff matrix (explicit values)
            float multiplier = GetMultiplier(attacker.type, defender.type);

            if (multiplier <= 0f)
            {
                result.description = $"{attacker.type} draws with {defender.type}: no damage.";
                return result;
            }

            float baseDamage = UnityEngine.Random.Range(4f, 10f);
            float damage = baseDamage * multiplier;
            result.damageDealt = damage;
            result.isCritical = damage > 8.0f;

            // Determine winner: multiplier > 1 -> attacker advantage, < 1 -> defender advantage
            if (multiplier > 1f)
            {
                result.winnerId = attacker.owner;
                result.description = $"{attacker.type} beats {defender.type} for {damage:F1} damage";
            }
            else if (multiplier < 1f)
            {
                result.winnerId = defender.owner;
                result.description = $"{attacker.type} loses to {defender.type}; defender takes {damage:F1} (counter)";
            }
            else
            {
                // multiplier == 1 : neutral, treat as small damage to defender
                result.winnerId = attacker.owner;
                result.description = $"{attacker.type} ambles against {defender.type} for {damage:F1} damage";
            }

            return result;
        }

        private static float GetMultiplier(PieceType attacker, PieceType defender)
        {
            // Explicit non-transitive matrix
            if (attacker == PieceType.Rock && defender == PieceType.Scissors) return 1.2f;
            if (attacker == PieceType.Rock && defender == PieceType.Paper) return 0.8f;

            if (attacker == PieceType.Paper && defender == PieceType.Rock) return 1.1f;
            if (attacker == PieceType.Paper && defender == PieceType.Scissors) return 0.9f;

            if (attacker == PieceType.Scissors && defender == PieceType.Paper) return 1.15f;
            if (attacker == PieceType.Scissors && defender == PieceType.Rock) return 0.85f;

            if (attacker == defender) return 0f;

            return 0f; // default safe
        }
    }

    /*
     EXAMPLE:
     // Piece rock = new Piece { position = new Vector2Int(0,0), type = PieceType.Rock, owner = 1, health = 10 };
     // Piece scissors = new Piece { position = new Vector2Int(0,1), type = PieceType.Scissors, owner = 2, health = 10 };
     // DuelResult r = DuelResolver.ResolveDuel(rock, scissors);
     // // r.damageDealt uses 1.2x multiplier for Rock vs Scissors
     */
}
