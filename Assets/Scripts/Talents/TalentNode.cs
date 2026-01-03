using System;
using System.Collections.Generic;
using UnityEngine;

namespace StevensMathOS.GameModes.DoltPvP.Talents
{
    /// <summary>
    /// Lightweight struct representing talent effects.
    /// </summary>
    public struct TalentEffect
    {
        public float duelDamageMultiplier; // 1.0 = neutral
        public float evasionChance; // 0.0 - 1.0
        public int winSeedBonus;
    }

    public enum TalentBranch { DoltPvP, Topological, PrimeTheory }

    /// <summary>
    /// Single node in the talent hypergraph.
    /// </summary>
    public class TalentNode
    {
        public int nodeId { get; private set; }
        public string name { get; private set; }
        public int costInWinSeeds { get; private set; }
        public List<int> prerequisiteNodeIds { get; private set; }
        public TalentBranch branch { get; private set; }
        public TalentEffect effect { get; private set; }
        public bool isUnlocked { get; private set; }
        public bool isActive { get; private set; }

        public event Action<int> OnTalentActivated;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TalentNode(int nodeId, string name, int cost, List<int> prerequisites, TalentBranch branch, TalentEffect effect)
        {
            this.nodeId = nodeId;
            this.name = name ?? "Unnamed";
            costInWinSeeds = cost;
            prerequisiteNodeIds = prerequisites ?? new List<int>();
            this.branch = branch;
            this.effect = effect;
            isUnlocked = false;
            isActive = false;
        }

        /// <summary>
        /// Checks whether the player can unlock this node (prereqs met + enough seeds).
        /// </summary>
        public bool CanUnlock(PlayerProgressionData player)
        {
            if (player == null) return false;

            // Check prerequisites
            foreach (var id in prerequisiteNodeIds)
            {
                if (!player.HasTalentUnlocked(id)) return false;
            }

            // Check currency
            return player.WinSeeds >= costInWinSeeds;
        }

        /// <summary>
        /// Deducts cost from player and unlocks this node. Returns true on success.
        /// </summary>
        public bool Unlock(PlayerProgressionData player)
        {
            if (player == null) return false;
            if (isUnlocked) return false;
            if (!CanUnlock(player)) return false;

            player.WinSeeds -= costInWinSeeds;
            isUnlocked = true;
            return true;
        }

        /// <summary>
        /// Activates the talent (equip). Publishes OnTalentActivated.
        /// </summary>
        public void Activate()
        {
            if (!isUnlocked) throw new InvalidOperationException("Cannot activate locked talent.");
            isActive = true;
            try { OnTalentActivated?.Invoke(nodeId); } catch { }
        }

        /// <summary>
        /// Returns node cost.
        /// </summary>
        public int GetCost() => costInWinSeeds;

        /// <summary>
        /// Returns dependent nodes that list this node as a prerequisite.
        /// </summary>
        public List<TalentNode> GetDependents(TalentNode[] allNodes)
        {
            var deps = new List<TalentNode>();
            if (allNodes == null) return deps;
            foreach (var n in allNodes)
            {
                if (n.prerequisiteNodeIds.Contains(this.nodeId)) deps.Add(n);
            }
            return deps;
        }

        /*
         EXAMPLE:
         // TalentEffect e = new TalentEffect { duelDamageMultiplier = 1.2f, evasionChance = 0.05f, winSeedBonus = 1 };
         // TalentNode node = new TalentNode(0, "Void Rift", 50, new List<int>{}, TalentBranch.DoltPvP, e);
         // if (node.CanUnlock(playerData)) node.Unlock(playerData);
         // node.Activate();
         */
    }
}
