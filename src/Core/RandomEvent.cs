using ChatChaos.Localization;
using ChatChaos.Logging;

namespace ChatChaos.Core
{
    /// <summary>
    /// "Random event": picks one OTHER event at random, announces it in chat
    /// ("Random event launched: X"), then runs it. Excludes itself to avoid recursion.
    /// </summary>
    public static class RandomEvent
    {
        public static void Trigger()
        {
            var picked = EventRegistry.PickRandomExcluding("random_event");
            if (picked == null)
            {
                Log.Warn("Event", "random event: no other event available to pick.");
                return;
            }

            ChatAnnounce.Say(Loc.Format("chat.random_event", picked.Label));
            Log.Info("Event", $"random event picked '{picked.Id}' ({picked.Label}).");

            try { picked.Apply(); }
            catch (System.Exception e) { Log.Error("Event", $"random event '{picked.Id}' FAILED: {e}"); }
        }
    }
}
