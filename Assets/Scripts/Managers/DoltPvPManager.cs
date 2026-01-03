using System;
using System.Collections.Generic;
using UnityEngine;
using StevensMathOS.Core;
using StevensMathOS.Data;

namespace StevensMathOS.GameModes.DoltPvP
{
    /// <summary>
    /// Main game controller for Dolt PvP mode.
    /// Manages 3x3 grid-based Rock/Paper/Scissors combat with impulse-based physics.
    /// 
    /// EXAMPLE USAGE (3-turn cascade with chain reaction):
    /// Turn 1: Player 1 places Rock at (1,1)
    ///   - No adjacent enemy pieces → no duel
    /// Turn 2: Player 2 places Paper at (1,2) adjacent to Player 1's Rock
    ///   - ResolveAdjacentDuel((1,1), (1,2)) triggered
    ///   - Paper beats Rock at 1.1x multiplier → damage = 1.1 * baseDamage
    ///   - Player 1's Rock health drops, OnDuelResolved event fired
    /// Turn 3: Player 2 places Scissors at (2,2) adjacent to Player 1's damaged Rock
    ///   - Chain reaction begins: Rock loses to Paper AND Scissors
    ///   - TriggerChainReaction() cascades, destroying Rock, damaging adjacent Paper
    ///   - OnChainReactionTriggered event fires with all affected positions
    /// </summary>
    public class DoltPvPManager
    {
        // ============================================================================
        // CONSTANTS & NON-TRANSITIVE PAYOFF MATRIX
        // ============================================================================
        
        private const float BASE_DAMAGE = 5.0f;
        private const int GRID_SIZE = 3;
        private const float HEALTH_THRESHOLD = 0.01f; // Piece destroyed at or below this HP

        /// <summary>
        /// Non-transitive payoff multipliers (winner damage = BASE_DAMAGE * multiplier).
        /// Rock: beats Scissors (1.2x), loses to Paper (0.8x)
        /// Paper: beats Rock (1.1x), loses to Scissors (0.9x)
        /// Scissors: beats Paper (1.15x), loses to Rock (0.85x)
        /// </summary>
        private static readonly Dictionary<(PieceType, PieceType), float> PayoffMatrix =
            new Dictionary<(PieceType, PieceType), float>
            {
                // Rock payoffs
                { (PieceType.Rock, PieceType.Scissors), 1.2f },
                { (PieceType.Rock, PieceType.Paper), 0.8f },
                
                // Paper payoffs
                { (PieceType.Paper, PieceType.Rock), 1.1f },
                { (PieceType.Paper, PieceType.Scissors), 0.9f },
                
                // Scissors payoffs
                { (PieceType.Scissors, PieceType.Paper), 1.15f },
                { (PieceType.Scissors, PieceType.Rock), 0.85f }
            };

        // ============================================================================
        // DEPENDENCIES & STATE
        // ============================================================================

        private GridManager gridManager;
        private PlayerProgressionData playerProgressionData;
        private GameEventBus eventBus;

        /// <summary>Current game board state: position (x,y) → Piece.</summary>
        private Dictionary<Vector2Int, Piece> boardState;

        /// <summary>Tracks pieces pending destruction from cascade (populated during chain reactions).</summary>
        private HashSet<Vector2Int> pendingDestroyPositions;

        private bool isGameActive;
        private int currentTurn;

        // ============================================================================
        // CONSTRUCTOR
        // ============================================================================

        /// <summary>
        /// Initializes DoltPvPManager with dependency injection.
        /// </summary>
        /// <param name="gridManager">GridManager instance for grid queries.</param>
        /// <param name="playerProgressionData">PlayerProgressionData for state persistence.</param>
        /// <param name="eventBus">GameEventBus for event publishing (nullable).</param>
        public DoltPvPManager(GridManager gridManager, PlayerProgressionData playerProgressionData, GameEventBus eventBus = null)
        {
            this.gridManager = gridManager ?? throw new ArgumentNullException(nameof(gridManager));
            this.playerProgressionData = playerProgressionData ?? throw new ArgumentNullException(nameof(playerProgressionData));
            this.eventBus = eventBus;

            boardState = new Dictionary<Vector2Int, Piece>(GRID_SIZE * GRID_SIZE);
            pendingDestroyPositions = new HashSet<Vector2Int>();
            isGameActive = false;
            currentTurn = 0;
        }

        // ============================================================================
        // GAME LOOP
        // ============================================================================

        /// <summary>
        /// Initializes a new game. Clears board, resets turn counter, publishes OnGameInitialized event.
        /// </summary>
        public void Initialize()
        {
            try
            {
                boardState.Clear();
                pendingDestroyPositions.Clear();
                currentTurn = 0;
                isGameActive = true;

                PublishEvent("OnGameInitialized", new { });
                SyncGameState();

                Debug.Log("[DoltPvPManager] Game initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoltPvPManager] Initialize failed: {ex.Message}");
                isGameActive = false;
            }
        }

        /// <summary>
        /// Places a piece on the grid at (x, y) for the specified player.
        /// Triggers duel resolution with all adjacent enemy pieces.
        /// </summary>
        /// <param name="x">Grid X coordinate (0-2).</param>
        /// <param name="y">Grid Y coordinate (0-2).</param>
        /// <param name="pieceType">Rock, Paper, or Scissors.</param>
        /// <param name="playerId">Owner player ID.</param>
        public void PlacePiece(int x, int y, PieceType pieceType, int playerId)
        {
            if (!isGameActive)
            {
                Debug.LogWarning("[DoltPvPManager] Cannot place piece: game not active");
                return;
            }

            Vector2Int position = new Vector2Int(x, y);

            // Validate grid bounds
            if (x < 0 || x >= GRID_SIZE || y < 0 || y >= GRID_SIZE)
            {
                Debug.LogWarning($"[DoltPvPManager] Invalid position ({x}, {y})");
                return;
            }

            // Validate unoccupied cell
            if (boardState.ContainsKey(position))
            {
                Debug.LogWarning($"[DoltPvPManager] Cell ({x}, {y}) already occupied");
                return;
            }

            try
            {
                // Create and place piece
                Piece newPiece = new Piece
                {
                    position = position,
                    type = pieceType,
                    owner = playerId,
                    health = 100.0f
                };
                boardState[position] = newPiece;

                // Check adjacent cells for duels
                List<Vector2Int> adjacentPositions = GetAdjacentCellPositions(position);
                foreach (Vector2Int adjPos in adjacentPositions)
                {
                    if (boardState.TryGetValue(adjPos, out Piece adjacentPiece))
                    {
                        // Only duel with enemy pieces
                        if (adjacentPiece.owner != playerId)
                        {
                            ResolveAdjacentDuel(position, adjPos);
                        }
                    }
                }

                currentTurn++;
                SyncGameState();

                PublishEvent("OnPiecePlaced", new { position, pieceType, playerId });
                Debug.Log($"[DoltPvPManager] Piece placed at ({x}, {y}) by player {playerId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoltPvPManager] PlacePiece failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves combat between two adjacent pieces.
        /// Applies damage based on non-transitive payoff matrix.
        /// Triggers chain reaction if health falls below threshold.
        /// </summary>
        /// <param name="pos1">First piece position.</param>
        /// <param name="pos2">Second piece position.</param>
        public void ResolveAdjacentDuel(Vector2Int pos1, Vector2Int pos2)
        {
            if (!boardState.TryGetValue(pos1, out Piece piece1) || !boardState.TryGetValue(pos2, out Piece piece2))
            {
                Debug.LogWarning("[DoltPvPManager] One or both pieces not found in duel");
                return;
            }

            try
            {
                // Determine winner and damage
                float damage1To2 = GetDamageMultiplier(piece1.type, piece2.type) * BASE_DAMAGE;
                float damage2To1 = GetDamageMultiplier(piece2.type, piece1.type) * BASE_DAMAGE;

                // Apply damage
                Piece updatedPiece1 = piece1;
                Piece updatedPiece2 = piece2;

                updatedPiece2.health -= damage1To2;
                updatedPiece1.health -= damage2To1;

                boardState[pos1] = updatedPiece1;
                boardState[pos2] = updatedPiece2;

                // Publish duel events
                PublishEvent("OnDuelResolved", new 
                { 
                    attacker = piece1.owner, 
                    defender = piece2.owner, 
                    damage = damage1To2, 
                    position = pos2 
                });
                
                PublishEvent("OnDuelResolved", new 
                { 
                    attacker = piece2.owner, 
                    defender = piece1.owner, 
                    damage = damage2To1, 
                    position = pos1 
                });

                // Check for destroyed pieces and trigger cascade
                if (updatedPiece1.health <= HEALTH_THRESHOLD)
                {
                    pendingDestroyPositions.Add(pos1);
                }
                if (updatedPiece2.health <= HEALTH_THRESHOLD)
                {
                    pendingDestroyPositions.Add(pos2);
                }

                if (pendingDestroyPositions.Count > 0)
                {
                    TriggerChainReaction();
                }

                Debug.Log($"[DoltPvPManager] Duel resolved: ({pos1}) vs ({pos2}), damage {damage1To2:F2} / {damage2To1:F2}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoltPvPManager] ResolveAdjacentDuel failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cascades damage to adjacent pieces when a piece is destroyed.
        /// Continues recursively until no more pieces fall below health threshold.
        /// </summary>
        private void TriggerChainReaction()
        {
            if (pendingDestroyPositions.Count == 0)
            {
                return;
            }

            try
            {
                List<Vector2Int> affectedPositions = new List<Vector2Int>(pendingDestroyPositions);
                
                // Process all pending destructions
                foreach (Vector2Int pos in affectedPositions)
                {
                    if (boardState.ContainsKey(pos))
                    {
                        boardState.Remove(pos);
                    }
                }

                pendingDestroyPositions.Clear();

                // Cascade damage to adjacent pieces
                foreach (Vector2Int destroyedPos in affectedPositions)
                {
                    List<Vector2Int> adjacentPositions = GetAdjacentCellPositions(destroyedPos);
                    foreach (Vector2Int adjPos in adjacentPositions)
                    {
                        if (boardState.TryGetValue(adjPos, out Piece adjacentPiece))
                        {
                            // Apply cascade damage (20% of base)
                            Piece damaged = adjacentPiece;
                            damaged.health -= BASE_DAMAGE * 0.2f;
                            boardState[adjPos] = damaged;

                            if (damaged.health <= HEALTH_THRESHOLD)
                            {
                                pendingDestroyPositions.Add(adjPos);
                            }
                        }
                    }
                }

                PublishEvent("OnChainReactionTriggered", new { positions = affectedPositions });

                // Recursive cascade
                if (pendingDestroyPositions.Count > 0)
                {
                    TriggerChainReaction();
                }

                Debug.Log($"[DoltPvPManager] Chain reaction: {affectedPositions.Count} pieces destroyed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoltPvPManager] TriggerChainReaction failed: {ex.Message}");
                pendingDestroyPositions.Clear();
            }
        }

        // ============================================================================
        // HELPER METHODS
        // ============================================================================

        /// <summary>
        /// Gets damage multiplier for attacker type vs defender type using non-transitive payoff matrix.
        /// </summary>
        /// <returns>Damage multiplier (0.8-1.2 range). Returns 1.0 if type matchup undefined.</returns>
        private float GetDamageMultiplier(PieceType attackerType, PieceType defenderType)
        {
            if (attackerType == defenderType)
            {
                return 1.0f; // No advantage in mirror matchup
            }

            if (PayoffMatrix.TryGetValue((attackerType, defenderType), out float multiplier))
            {
                return multiplier;
            }

            Debug.LogWarning($"[DoltPvPManager] Undefined payoff: {attackerType} vs {defenderType}");
            return 1.0f;
        }

        /// <summary>
        /// Retrieves orthogonal adjacent cell positions (up, down, left, right) for a given position.
        /// </summary>
        /// <returns>List of valid adjacent positions within grid bounds.</returns>
        private List<Vector2Int> GetAdjacentCellPositions(Vector2Int position)
        {
            List<Vector2Int> adjacent = new List<Vector2Int>(4);
            
            Vector2Int[] directions = new[]
            {
                new Vector2Int(0, 1),   // up
                new Vector2Int(0, -1),  // down
                new Vector2Int(1, 0),   // right
                new Vector2Int(-1, 0)   // left
            };

            foreach (Vector2Int direction in directions)
            {
                Vector2Int adjPos = position + direction;
                if (adjPos.x >= 0 && adjPos.x < GRID_SIZE && adjPos.y >= 0 && adjPos.y < GRID_SIZE)
                {
                    adjacent.Add(adjPos);
                }
            }

            return adjacent;
        }

        /// <summary>
        /// Syncs all game state to PlayerProgressionData for persistence.
        /// Converts boardState dictionary to serializable format.
        /// </summary>
        private void SyncGameState()
        {
            if (playerProgressionData == null)
            {
                return;
            }

            try
            {
                // Store current board state in progression data
                if (playerProgressionData.CurrentGameState != null)
                {
                    playerProgressionData.CurrentGameState.LastTurnNumber = currentTurn;
                    playerProgressionData.CurrentGameState.IsGameActive = isGameActive;
                }

                Debug.Log($"[DoltPvPManager] Game state synced (turn {currentTurn}, {boardState.Count} pieces)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DoltPvPManager] SyncGameState failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Publishes an event to the EventBus if available.
        /// Gracefully degrades if EventBus is null.
        /// </summary>
        private void PublishEvent(string eventName, object data)
        {
            if (eventBus != null)
            {
                try
                {
                    eventBus.Publish(eventName, data);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DoltPvPManager] Event publish failed: {eventName}, {ex.Message}");
                }
            }
        }

        // ============================================================================
        // GETTERS
        // ============================================================================

        /// <summary>
        /// Gets the current board state (read-only copy).
        /// </summary>
        public Dictionary<Vector2Int, Piece> GetBoardState()
        {
            return new Dictionary<Vector2Int, Piece>(boardState);
        }

        /// <summary>
        /// Gets the current turn number.
        /// </summary>
        public int GetCurrentTurn() => currentTurn;

        /// <summary>
        /// Checks if game is currently active.
        /// </summary>
        public bool IsGameActive() => isGameActive;

        /// <summary>
        /// Ends the current game. Clears board and stops accepting moves.
        /// </summary>
        public void EndGame()
        {
            isGameActive = false;
            boardState.Clear();
            pendingDestroyPositions.Clear();
            
            PublishEvent("OnGameEnded", new { finalTurn = currentTurn });
            Debug.Log($"[DoltPvPManager] Game ended at turn {currentTurn}");
        }
    }
}
