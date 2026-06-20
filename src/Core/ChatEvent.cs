using System;
using ChatChaos.Localization;

namespace ChatChaos.Core
{
    /// <summary>
    /// One random event the chat can vote for.
    ///
    /// An event has:
    ///   - Id        : a short stable identifier (for logs/config), e.g. "random_death".
    ///   - LabelEn   : the English label shown on the panel and in chat.
    ///   - LabelFr   : the French label.
    ///   - Apply     : what happens IN THE GAME when this event wins. It runs on the
    ///                 HOST only (the host then syncs the result to everyone). Keep
    ///                 game-state changes server-authoritative inside here.
    ///
    /// You normally never construct this by hand — use EventRegistry.Add(...).
    /// </summary>
    public class ChatEvent
    {
        public string Id { get; }
        public string LabelEn { get; }
        public string LabelFr { get; }
        public Action Apply { get; }

        public ChatEvent(string id, string labelEn, string labelFr, Action apply)
        {
            Id = id;
            LabelEn = labelEn;
            LabelFr = labelFr;
            Apply = apply ?? (() => { });
        }

        /// <summary>The label in the currently selected language.</summary>
        public string Label => Loc.Current == Lang.French ? LabelFr : LabelEn;
    }
}
