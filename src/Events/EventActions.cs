using System.Collections.Generic;
using ChatChaos.Networking;
using UnityEngine;

namespace ChatChaos.Events
{
    /// <summary>
    /// Reusable building blocks for event effects. These run ON THE HOST (an event's
    /// Apply() is only called on the host), and use the networker to mirror the effect
    /// to every player.
    /// </summary>
    public static class EventActions
    {
        /// <summary>
        /// Kills one random ALIVE player. Dead players (and empty slots) are excluded,
        /// so with 2 living players out of 3, one of the two living ones dies. Does
        /// nothing if no player is alive.
        /// </summary>
        public static void KillRandomAlivePlayer()
        {
            var sor = StartOfRound.Instance;
            if (sor == null || sor.allPlayerScripts == null) return;

            // Collect the indices of living, active players.
            var alive = new List<int>();
            for (int i = 0; i < sor.allPlayerScripts.Length; i++)
            {
                var p = sor.allPlayerScripts[i];
                if (p != null && p.isPlayerControlled && !p.isPlayerDead)
                    alive.Add(i);
            }

            if (alive.Count == 0)
            {
                Plugin.Log.LogInfo("random_death: no living player to kill.");
                return;
            }

            int index = alive[Random.Range(0, alive.Count)];
            Plugin.Log.LogInfo($"random_death: killing player index {index} " +
                               $"(of {alive.Count} alive).");

            var n = ChatChaosNetworker.Active;
            if (n != null)
            {
                n.KillPlayer(index);
            }
            else
            {
                // Fallback (no networker, e.g. very early/solo): kill locally if it's us.
                var t = sor.allPlayerScripts[index];
                if (t == sor.localPlayerController && !t.isPlayerDead)
                    t.KillPlayer(Vector3.zero, true, CauseOfDeath.Unknown, 0, default);
            }
        }
    }
}
