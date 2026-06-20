using System;
using System.Collections.Generic;
using ChatChaos.Config;
using ChatChaos.Localization;
using ChatChaos.Networking;
using ChatChaos.Twitch;
using ChatChaos.UI;
using Unity.Netcode;
using UnityEngine;

namespace ChatChaos.Core
{
    /// <summary>
    /// Drives the whole poll lifecycle on the HOST:
    ///   land on a moon  ->  wait DelayAfterLanding  ->  open a poll  ->  collect
    ///   votes for Duration  ->  pick the winner  ->  apply it + announce  ->  show
    ///   the result  ->  (optionally repeat).
    ///
    /// Only the host runs this logic. The host renders the panel locally and mirrors
    /// every step to the other players through ChatChaosNetworker, so everyone sees
    /// the same poll. Clients do nothing here.
    /// </summary>
    public static class PollManager
    {
        private enum Phase { Idle, Scheduled, Voting, Result }

        private static Phase _phase = Phase.Idle;
        private static bool _landed;
        private static bool _pollsThisMoon;

        private static float _nextPollTime;
        private static float _pollEndTime;
        private static float _resultEndTime;
        private static float _lastCountsBroadcast;
        private static bool _endingAnnounced;

        private static List<ChatEvent> _options = new();
        private static int[] _counts = new int[3];
        private static readonly HashSet<string> _voters = new();

        private static bool IsHost =>
            NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

        // ----------------------------------------------------------------- lifecycle

        /// <summary>Called once when the host's network handler is ready.</summary>
        public static void OnHostReady()
        {
            if (!IsHost) return;
            TwitchClient.StartFromConfig();
        }

        /// <summary>Ship just landed on a moon (host only does the work).</summary>
        public static void OnLanded(bool isCompanyMoon, string moonName)
        {
            _landed = true;
            if (!IsHost) return;

            if (isCompanyMoon && ModConfig.SkipCompanyMoon.Value)
            {
                Plugin.Log.LogInfo("ChatChaos: safe moon — no polls here.");
                _pollsThisMoon = false;
                _phase = Phase.Idle;
                return;
            }
            if (EventRegistry.Count == 0)
            {
                Plugin.Log.LogWarning("ChatChaos: no events registered — cannot run a poll.");
                _pollsThisMoon = false;
                _phase = Phase.Idle;
                return;
            }

            int delay = Mathf.RoundToInt(Mathf.Max(0f, ModConfig.PollDelayAfterLanding.Value));
            string moon = string.IsNullOrWhiteSpace(moonName) ? "?" : moonName.Trim();

            _pollsThisMoon = true;
            _nextPollTime = Time.time + delay;
            _phase = Phase.Scheduled;

            // Chat + on-screen confirmation (also a live "is the sync working?" check).
            Announce(Loc.Format("chat.landed", moon, delay));
            ShowTip(Loc.Get("tip.header"), Loc.Format("tip.landed", moon, delay));

            Plugin.Log.LogInfo($"ChatChaos: landed on '{moon}', first poll in {delay}s.");
        }

        /// <summary>Ship left the moon — announce, then cancel everything.</summary>
        public static void OnTookOff()
        {
            _landed = false;

            // Only the host posts in chat, and only if polls were active on this moon.
            if (IsHost && _pollsThisMoon)
                Announce(Loc.Get("chat.takeoff"));
            _pollsThisMoon = false;

            if (_phase != Phase.Idle)
            {
                _phase = Phase.Idle;
                UiHide();
            }
        }

        // ----------------------------------------------------------------- per-frame

        public static void Tick()
        {
            if (!IsHost) return;

            DrainVotes();

            switch (_phase)
            {
                case Phase.Scheduled:
                    if (Time.time >= _nextPollTime) StartPoll();
                    break;

                case Phase.Voting:
                    float remaining = _pollEndTime - Time.time;

                    if (!_endingAnnounced && remaining <= 10f)
                    {
                        _endingAnnounced = true;
                        Announce(Loc.Format("chat.ending", 10), inGame: false);
                    }

                    // Throttle count updates to ~2/sec to keep network traffic light.
                    if (Time.time - _lastCountsBroadcast >= 0.5f)
                    {
                        _lastCountsBroadcast = Time.time;
                        UiCounts();
                    }

                    if (remaining <= 0f) EndPoll();
                    break;

                case Phase.Result:
                    if (Time.time >= _resultEndTime)
                    {
                        UiHide();
                        if (_landed && ModConfig.PollRepeatInterval.Value > 0f && EventRegistry.Count > 0)
                        {
                            _nextPollTime = Time.time + ModConfig.PollRepeatInterval.Value;
                            _phase = Phase.Scheduled;
                        }
                        else _phase = Phase.Idle;
                    }
                    break;
            }
        }

        // ----------------------------------------------------------------- internals

        private static void StartPoll()
        {
            _options = EventRegistry.PickRandom(3);
            if (_options.Count == 0) { _phase = Phase.Idle; return; }

            _counts = new int[_options.Count];
            _voters.Clear();
            _endingAnnounced = false;
            _pollEndTime = Time.time + Mathf.Max(5f, ModConfig.PollDuration.Value);
            _lastCountsBroadcast = 0f;
            _phase = Phase.Voting;

            UiStart();

            // Announce in chat: "a new vote starts" + the three options.
            Announce(Loc.Get("chat.start"));
            Announce(Loc.Format("chat.options", Label(0), Label(1), Label(2)));

            Plugin.Log.LogInfo($"ChatChaos: poll opened ({_options.Count} options, " +
                               $"{ModConfig.PollDuration.Value:0}s).");
        }

        private static void DrainVotes()
        {
            var tw = TwitchClient.Instance;
            if (tw == null) return;

            while (tw.TryDequeue(out var line))
            {
                if (_phase != Phase.Voting) continue; // drop messages outside voting

                int choice = ParseChoice(line.Message);
                if (choice < 1 || choice > _options.Count) continue;

                // One vote per person: first vote counts, later ones are ignored.
                if (!_voters.Add(line.User.ToLowerInvariant())) continue;
                _counts[choice - 1]++;
            }
        }

        private static void EndPoll()
        {
            int winner = DecideWinner(out bool noVotes);
            int winnerCount = (winner >= 0 && winner < _counts.Length) ? _counts[winner] : 0;

            // Announce the winner.
            if (noVotes)
                Announce(Loc.Format("chat.winner.novote", Label(winner)));
            else
                Announce(Loc.Format("chat.winner", Label(winner), winnerCount));

            // Apply the winning event on the host.
            try { _options[winner].Apply(); }
            catch (Exception e) { Plugin.Log.LogError($"ChatChaos: event '{_options[winner].Id}' threw: {e}"); }

            UiEnd(winner, winnerCount);

            _resultEndTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            _phase = Phase.Result;

            Plugin.Log.LogInfo($"ChatChaos: winner = '{_options[winner].Id}' ({winnerCount} votes" +
                               (noVotes ? ", random — nobody voted)." : ")."));
        }

        /// <summary>Highest count wins; ties broken randomly; no votes -> fully random.</summary>
        private static int DecideWinner(out bool noVotes)
        {
            int max = -1;
            for (int i = 0; i < _counts.Length; i++) if (_counts[i] > max) max = _counts[i];

            noVotes = max <= 0;

            var leaders = new List<int>();
            for (int i = 0; i < _counts.Length; i++)
                if (noVotes || _counts[i] == max) leaders.Add(i);

            return leaders[UnityEngine.Random.Range(0, leaders.Count)];
        }

        private static int ParseChoice(string message)
        {
            if (string.IsNullOrEmpty(message)) return -1;
            // Accept "1", "2", "3" possibly followed by more text ("1 lol").
            string first = message.Trim().Split(' ')[0];
            return int.TryParse(first, out int n) ? n : -1;
        }

        private static string Label(int i) =>
            (i >= 0 && i < _options.Count) ? _options[i].Label : "";

        // ---- chat + UI helpers (route through the networker when available) ----

        /// <summary>
        /// Sends a message to the Twitch chat (plain text, under the host's account)
        /// AND, when enabled, to the in-game chat box (rich text, seen by all players).
        /// Set <paramref name="inGame"/> to false for Twitch-only messages.
        /// </summary>
        private static void Announce(string text, bool inGame = true)
        {
            // Twitch chat (plain text).
            if (ModConfig.AnnounceInChat.Value)
            {
                string prefix = (ModConfig.ChatPrefix.Value ?? "").Trim();
                TwitchClient.Instance?.SendMessage(prefix.Length > 0 ? prefix + " " + text : text);
            }

            // In-game chat box (rich text, broadcast to every player by the host).
            if (inGame && ModConfig.ShowInGameChat.Value)
                PostInGame(text);
        }

        /// <summary>Posts to the in-game chat with a coloured mod prefix.</summary>
        private static void PostInGame(string text)
        {
            string prefix = (ModConfig.ChatPrefix.Value ?? "").Trim();
            string color = (ModConfig.InGameChatColorHex.Value ?? "").Trim();
            string head = prefix.Length == 0
                ? ""
                : (color.Length > 0 ? $"<color=#{color}>{prefix}</color> " : prefix + " ");
            GameChat.Show(head + text);
        }

        private static void UiStart()
        {
            float dur = ModConfig.PollDuration.Value;
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastStart(Label(0), Label(1), Label(2), dur);
            else PollHud.Instance?.ShowPoll(Label(0), Label(1), Label(2), dur);
        }

        private static void UiCounts()
        {
            int c0 = _counts.Length > 0 ? _counts[0] : 0;
            int c1 = _counts.Length > 1 ? _counts[1] : 0;
            int c2 = _counts.Length > 2 ? _counts[2] : 0;
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastCounts(c0, c1, c2);
            else PollHud.Instance?.UpdateCounts(c0, c1, c2);
        }

        private static void UiEnd(int winner, int winnerCount)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastEnd(winner, winnerCount);
            else PollHud.Instance?.ShowResult(winner, winnerCount);
        }

        private static void UiHide()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastHide();
            else PollHud.Instance?.Hide();
        }

        private static void ShowTip(string header, string body)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastTip(header, body);
            else GameTips.Show(header, body);
        }
    }
}
