using System;
using System.Collections.Generic;

namespace StevensMathOS.Data
{
    /// <summary>
    /// Tracks player progression, talents, and game state across all game modes.
    /// </summary>
    public class PlayerProgressionData
    {
        public int WinSeeds { get; set; }
        public object CurrentGameState { get; set; }
        private HashSet<int> _unlockedTalents = new HashSet<int>();

        /// <summary>
        /// Updates grid state snapshot.
        /// </summary>
        public void UpdateGridState(object gridSnapshot)
        {
            CurrentGameState = gridSnapshot;
        }

        /// <summary>
        /// Check if talent node is unlocked.
        /// </summary>
        public bool HasTalentUnlocked(int nodeId)
        {
            return _unlockedTalents.Contains(nodeId);
        }

        /// <summary>
        /// Mark talent as unlocked.
        /// </summary>
        public void UnlockTalent(int nodeId)
        {
            _unlockedTalents.Add(nodeId);
        }
    }
}
