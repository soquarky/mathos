using System;
using System.Collections.Generic;
using UnityEngine;
using StevensMathOS.GameModes.DoltPvP.Core;
using StevensMathOS.Data;

namespace StevensMathOS.GameModes.DoltPvP
{
    /// <summary>
    /// Main game controller for Dolt PvP mode.
    /// </summary>
    public class DoltPvPManager
    {
        private readonly GridManager _gridManager;
        private readonly PlayerProgressionData _progressionData;
        private readonly GameEventBus _eventBus;

        private readonly Dictionary<(PieceType, PieceType), float> _payoffMatrix = new Dictionary<(PieceType, PieceType), float>(9);

        public event Action<int, float> OnDuelResolvedLocal;
        public event Action<List<Vector2Int>> OnChainReactionTriggeredLocal;
        public event Action<int> OnTalentUnlockedLocal;

        /// <summary>
        /// Constructor (dependency injection).
        /// </summary>
        public DoltPvPManager(GridManager gridManager, PlayerProgressionData progressionData, GameEventBus eventBus = null)
        {
            _gridManager = gridManager ?? throw new ArgumentNullException(nameof(gridManager));
            _progressionData = progressionData ?? throw new ArgumentNullException(nameof(progressionData));
            _eventBus = eventBus; // can be null

            InitializePayoffMatrix();
        }

        /// <summary>
        /// Initialization -- clears grid and syncs progression.
        /// </summary>
        public void Initialize()
        {
            try { _gridManager.ClearGrid(); } catch (Exception ex) { Debug.LogError($"Initialize: {ex}"); }
            SyncProgression();
        }

        /// <summary>
        /// PlacePiece entry point for the game loop.
        /// </summary>
        public bool PlacePiece(int x, int y, PieceType type, int owner)
        {
            var pos = new Vector2Int(x, y);
            if (!_gridManager.IsValidPosition(pos))
            {
                _eventBus?.Publish("OnInvalidPlacementAttempted", new { pos, reason = "InvalidPosition" });
                return false;
            }

            if (_gridManager.IsOccupied(pos))
            {
                _eventBus?.Publish("OnInvalidPlacementAttempted", new { pos, reason = "Occupied" });
                return false;
            }

            var piece = new Piece { position = pos, type = type, owner = owner, health = 10f };
            var placed = _gridManager.PlacePieceAt(pos, piece);
            if (!placed) return false;

            // Resolve adjacent duels
            var adj = _gridManager.GetAdjacentCells(pos);
            foreach (var p in adj)
            {
                var other = _gridManager.GetPieceAt(p);
                if (other != null && other.Value.owner != owner)
                {
                    try { ResolveAdjacentDuel(pos, p); } catch (Exception ex) { Debug.LogError(ex.ToString()); }
                }
            }

            // Chain reaction
            TriggerChainReaction(pos);

            SyncProgression();
            return true;
        }

        /// <summary>
        /// Resolves a duel between two adjacent positions.
        /// </summary>
        public void ResolveAdjacentDuel(Vector2Int pos1, Vector2Int pos2)
        {
            if (!_gridManager.IsValidPosition(pos1) || !_gridManager.IsValidPosition(pos2)) return;

            var a = _gridManager.GetPieceAt(pos1);
            var b = _gridManager.GetPieceAt(pos2);
            if (a == null || b == null) return;

            try
            {
                var attacker = a.Value;
                var defender = b.Value;

                float multiplier = GetMultiplier(attacker.type, defender.type);
                float baseDamage = UnityEngine.Random.Range(4f, 10f);
                float damage = baseDamage * multiplier;

                defender.health -= damage;

                if (defender.health <= 0f) _gridManager.RemovePieceAt(defender.position);
                else _gridManager.PlacePieceAt(defender.position, defender);

                // Publish
                try
                {
                    _eventBus?.Publish("OnDuelResolved", new { winner = (damage > 0f ? attacker.owner : -1), damage });
                    OnDuelResolvedLocal?.Invoke(attacker.owner, damage);
                }
                catch (Exception ex) { Debug.LogWarning($"Publish OnDuelResolved failed: {ex}"); }
            }
            catch (Exception ex)
            {
                Debug.LogError($"ResolveAdjacentDuel exception: {ex}");
            }
        }

        /// <summary>
        /// Triggers a small chain reaction starting from origin.
        /// </summary>
        public void TriggerChainReaction(Vector2Int origin)
        {
            var triggered = new List<Vector2Int>();
            var neighbors = _gridManager.GetAdjacentCells(origin);
            foreach (var n in neighbors)
            {
                var p = _gridManager.GetPieceAt(n);
                if (p != null && p.Value.health < 3f)
                {
                    var nbors = _gridManager.GetAdjacentCells(n);
                    foreach (var nn in nbors)
                    {
                        var t = _gridManager.GetPieceAt(nn);
                        if (t != null)
                        {
                            var temp = t.Value;
                            temp.health -= 1.5f;
                            if (temp.health <= 0f) _gridManager.RemovePieceAt(nn);
                            else _gridManager.PlacePieceAt(nn, temp);
                            triggered.Add(nn);
                        }
                    }
                }
            }

            try
            {
                _eventBus?.Publish("OnChainReactionTriggered", triggered);
                OnChainReactionTriggeredLocal?.Invoke(triggered);
            }
            catch (Exception ex) { Debug.LogWarning($"Publish chain failed: {ex}"); }

            SyncProgression();
        }

        private void InitializePayoffMatrix()
        {
            _payoffMatrix[(PieceType.Rock, PieceType.Scissors)] = 1.2f;
            _payoffMatrix[(PieceType.Rock, PieceType.Paper)] = 0.8f;

            _payoffMatrix[(PieceType.Paper, PieceType.Rock)] = 1.1f;
            _payoffMatrix[(PieceType.Paper, PieceType.Scissors)] = 0.9f;

            _payoffMatrix[(PieceType.Scissors, PieceType.Paper)] = 1.15f;
            _payoffMatrix[(PieceType.Scissors, PieceType.Rock)] = 0.85f;

            _payoffMatrix[(PieceType.Rock, PieceType.Rock)] = 0f;
            _payoffMatrix[(PieceType.Paper, PieceType.Paper)] = 0f;
            _payoffMatrix[(PieceType.Scissors, PieceType.Scissors)] = 0f;
        }

        private float GetMultiplier(PieceType a, PieceType b)
        {
            if (_payoffMatrix.TryGetValue((a, b), out var m)) return m;
            return 0f;
        }

        private void SyncProgression()
        {
            try
            {
                var snapshot = new { Timestamp = DateTime.UtcNow, Pieces = _gridManager.GetAllPieces() };
                try { _progressionData.CurrentGameState = snapshot; } catch { try { _progressionData.UpdateGridState?.Invoke(snapshot); } catch { } }
            }
            catch (Exception ex) { Debug.LogWarning($"SyncProgression failed: {ex}"); }
        }

        /*
         EXAMPLE USAGE:
         // Turn 1: Player places Rock at (0,0)
         //   → Check adjacents, no duels
         // Turn 2: Player places Paper at (1,0)
         //   → Paper adjacent to Rock → ResolveAdjacentDuel((0,0), (1,0))
         //   → Paper wins 1.1x damage multiplier
         //   → Rock health -= damage, Chain check triggered
         // Turn 3: Opponent places Scissors at (0,1)
         //   → Adjacent to Rock and Paper, both duels resolve
         //   → Cascading phase: all adjacent duels resolve simultaneously
         */
    }
}
