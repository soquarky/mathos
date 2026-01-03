using System;
using System.Collections.Generic;
using UnityEngine;

namespace StevensMathOS.GameModes.DoltPvP.Core
{
    /// <summary>
    /// Manages a 3x3 grid for Dolt PvP.
    /// </summary>
    public class GridManager
    {
        private readonly Dictionary<Vector2Int, Piece> _cellGrid;
        private readonly GameEventBus _eventBus;
        private readonly PlayerProgressionData _progressionData;

        /// <summary>
        /// Constructs a new GridManager.
        /// </summary>
        /// <param name="eventBus">Optional event bus (may be null).</param>
        /// <param name="progressionData">Player progression data for sync (required).</param>
        public GridManager(GameEventBus eventBus, PlayerProgressionData progressionData)
        {
            _eventBus = eventBus;
            _progressionData = progressionData ?? throw new ArgumentNullException(nameof(progressionData));
            _cellGrid = new Dictionary<Vector2Int, Piece>(9);
            ClearGrid();
        }

        /// <summary>
        /// Returns 4-directional neighbors for a given position.
        /// </summary>
        /// <param name="pos">Position to query.</param>
        /// <returns>List of neighbor coordinates (may be empty).</returns>
        public List<Vector2Int> GetAdjacentCells(Vector2Int pos)
        {
            var list = new List<Vector2Int>(4);
            var up = new Vector2Int(pos.x, pos.y - 1);
            var down = new Vector2Int(pos.x, pos.y + 1);
            var left = new Vector2Int(pos.x - 1, pos.y);
            var right = new Vector2Int(pos.x + 1, pos.y);

            if (IsValidPosition(up)) list.Add(up);
            if (IsValidPosition(down)) list.Add(down);
            if (IsValidPosition(left)) list.Add(left);
            if (IsValidPosition(right)) list.Add(right);

            return list;
        }

        /// <summary>
        /// Checks whether a position lies inside the 3x3 grid.
        /// </summary>
        public bool IsValidPosition(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x <= 2 && pos.y >= 0 && pos.y <= 2;
        }

        /// <summary>
        /// Attempts to place a piece at the given position if empty and valid.
        /// </summary>
        public bool PlacePieceAt(Vector2Int pos, Piece piece)
        {
            if (!IsValidPosition(pos))
            {
                _eventBus?.Publish("OnInvalidPlacementAttempted", new { pos, reason = "InvalidPosition" });
                Debug.LogWarning($"PlacePieceAt: invalid position {pos}");
                return false;
            }

            if (IsOccupied(pos))
            {
                _eventBus?.Publish("OnInvalidPlacementAttempted", new { pos, reason = "Occupied" });
                Debug.LogWarning($"PlacePieceAt: already occupied {pos}");
                return false;
            }

            _cellGrid[pos] = piece;
            _eventBus?.Publish("OnCellOccupied", new { pos, piece });
            PublishGridChanged();
            return true;
        }

        /// <summary>
        /// Returns the piece at position or null if empty.
        /// </summary>
        public Piece? GetPieceAt(Vector2Int pos)
        {
            if (!IsValidPosition(pos)) return null;
            if (_cellGrid.TryGetValue(pos, out var p)) return p;
            return null;
        }

        /// <summary>
        /// Removes a piece at the given position if present.
        /// </summary>
        public bool RemovePieceAt(Vector2Int pos)
        {
            if (!IsValidPosition(pos)) return false;
            if (_cellGrid.Remove(pos))
            {
                _eventBus?.Publish("OnCellCleared", pos);
                PublishGridChanged();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the cell contains a piece.
        /// </summary>
        public bool IsOccupied(Vector2Int pos)
        {
            if (!IsValidPosition(pos)) return false;
            return _cellGrid.ContainsKey(pos);
        }

        /// <summary>
        /// Clears the entire grid.
        /// </summary>
        public void ClearGrid()
        {
            _cellGrid.Clear();
            // Initialize empty keys to be explicit (not required, but keeps snapshot predictable)
            PublishGridChanged();
        }

        /// <summary>
        /// Snapshot of all non-empty pieces.
        /// </summary>
        public List<Piece> GetAllPieces()
        {
            var list = new List<Piece>(_cellGrid.Count);
            foreach (var kv in _cellGrid)
            {
                list.Add(kv.Value);
            }
            return list;
        }

        /// <summary>
        /// Returns a list of occupied positions.
        /// </summary>
        public List<Vector2Int> GetOccupiedPositions()
        {
            var list = new List<Vector2Int>(_cellGrid.Count);
            foreach (var kv in _cellGrid) list.Add(kv.Key);
            return list;
        }

        private void PublishGridChanged()
        {
            try
            {
                _eventBus?.Publish("OnGridStateChanged", null);
                // After any state change, sync to progression data
                try
                {
                    _progressionData.UpdateGridState(GetGridSnapshot());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"GridManager: PlayerProgressionData.UpdateGridState failed: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PublishGridChanged event failed: {ex}");
            }
        }

        private object GetGridSnapshot()
        {
            return new { Cells = GetAllPieces(), Timestamp = DateTime.UtcNow };
        }

        /*
         EXAMPLE:
         // GridManager grid = new GridManager(eventBus, progressionData);
         // Piece rockPiece = new Piece { position = new Vector2Int(0,0), type = PieceType.Rock, owner = 1, health = 10 };
         // bool placed = grid.PlacePieceAt(new Vector2Int(0,0), rockPiece);  // true
         // List<Vector2Int> neighbors = grid.GetAdjacentCells(new Vector2Int(0,0));  // [(1,0), (0,1)]
         // bool invalid = grid.PlacePieceAt(new Vector2Int(0,0), rockPiece);  // false - already occupied
         */
    }
}
