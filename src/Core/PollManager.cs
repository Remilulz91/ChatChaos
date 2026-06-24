using System;
using System.Collections.Generic;
using ChatChaos.Config;
using ChatChaos.Localization;
using ChatChaos.Logging;
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
        private enum Phase { Idle, Scheduled, Voting, Result, Cancelled, WaitAfternoon }

        private static Phase _phase = Phase.Idle;
        private static bool _landed;
        private static bool _pollsThisMoon;
        private static int _pollsThisMoonCount;
        private static string _currentMoon = "?";

        private static float _nextPollTime;
        private static float _pollEndTime;
        private static float _resultEndTime;
        private static float _lastCountsBroadcast;
        private static bool _endingAnnounced;
        private static bool _connTipShown;
        private static bool _clientNoticeDone;

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
            _connTipShown = false;          // re-show the "connected" tip for this session
            DoubleOrNothing.Reset();        // clear any armed gamble at game start
            TimeFreeze.Reset();             // clear any leftover time-freeze timer
            StaminaBoost.Reset();           // clear any leftover stamina boost
            ShipLock.Reset();               // clear any leftover ship lock
            Berserk.Reset();                // clear any leftover berserk state
            SpeedBoost.Reset();             // clear any leftover speed boost
            MicMute.Reset();                // make sure the mic isn't left muted
            SoundMute.Reset();              // make sure game sound isn't left muted
            WinterSale.Reset();             // restore store prices if a sale was running
            EffectTimers.Reset();           // clear any leftover countdowns
            EventGuard.Reset();             // clear any event locks
            TwitchClient.StartFromConfig();
        }

        /// <summary>True if a chat source (Twitch) is connected.</summary>
        private static bool IsAccountConnected()
        {
            var tw = TwitchClient.Instance;
            return tw != null && tw.IsConnected;
        }

        /// <summary>
        /// Client-side one-time notice: if a NON-host player configured an account, tell
        /// them it is ignored (only the host's chat drives the votes). Local tip + log;
        /// called every frame from the ticker but acts once per session. Runs for everyone.
        /// </summary>
        public static void TickClientNotice()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient)
            {
                _clientNoticeDone = false;   // reset for the next session
                return;
            }
            if (_clientNoticeDone) return;

            if (nm.IsHost || nm.IsServer) { _clientNoticeDone = true; return; }  // host: no notice
            if (!ClientHasConfiguredAccount()) { _clientNoticeDone = true; return; }
            if (HUDManager.Instance == null) return;   // wait until the HUD exists

            _clientNoticeDone = true;
            Plugin.Log.LogInfo("ChatChaos: you are not the host — your account is ignored. " +
                               "Only the host's chat drives the votes.");
            GameTips.Show(Loc.Get("tip.client.header"), Loc.Get("tip.client.body"));
        }

        private static bool ClientHasConfiguredAccount()
        {
            if (!ModConfig.TwitchEnabled.Value) return false;
            return !string.IsNullOrWhiteSpace(ModConfig.TwitchChannel.Value)
                || !string.IsNullOrWhiteSpace(ModConfig.TwitchOAuthToken.Value);
        }

        /// <summary>Ship just landed on a moon (host only does the work).</summary>
        public static void OnLanded(bool isCompanyMoon, string moonName)
        {
            _landed = true;
            if (!IsHost) return;

            DoubleOrNothing.OnLanded(isCompanyMoon);   // arm-aware: shows the warning at the Company

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
            if (ModConfig.RequireConnectedAccount.Value && !IsAccountConnected())
            {
                Plugin.Log.LogInfo("ChatChaos: no chat account connected — no poll this landing " +
                                   "(RequireConnectedAccount is on).");
                _pollsThisMoon = false;
                _phase = Phase.Idle;
                return;
            }

            int delay = Mathf.RoundToInt(Mathf.Max(0f, ModConfig.PollDelayAfterLanding.Value));
            string moon = string.IsNullOrWhiteSpace(moonName) ? "?" : moonName.Trim();
            _currentMoon = moon;

            // Clean slate: clear any leftover panel (a frozen/cancelled or result panel
            // still fading from a previous moon) before scheduling the next poll.
            UiHide();

            _pollsThisMoon = true;
            _pollsThisMoonCount = 0;
            _nextPollTime = Time.time + delay;
            _phase = Phase.Scheduled;   // poll 1 (morning)

            // Chat + on-screen confirmation (also a live "is the sync working?" check).
            Announce(Loc.Format("chat.landed", moon, delay));
            ShowTip(Loc.Get("tip.header"), Loc.Format("tip.landed", moon, delay));

            Plugin.Log.LogInfo($"ChatChaos: landed on '{moon}', first poll in {delay}s.");
        }

        /// <summary>Ship left the moon — announce, then cancel the current poll.</summary>
        public static void OnTookOff()
        {
            _landed = false;
            if (!IsHost) return;   // clients update their HUD from the host's messages

            DoubleOrNothing.OnTookOff();   // resolve the gamble if leaving the Company while armed

            if (_pollsThisMoon)
                Announce(Loc.Get("chat.takeoff"));
            _pollsThisMoon = false;

            switch (_phase)
            {
                case Phase.Voting:
                    // Freeze the poll panel (counter + counts) and keep it on screen, then
                    // clear it after the result duration. The poll is CANCELLED: no winner
                    // is chosen and no event is applied.
                    UiPause();
                    _resultEndTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
                    _phase = Phase.Cancelled;
                    Plugin.Log.LogInfo("ChatChaos: ship left mid-poll — poll cancelled (frozen, no effect).");
                    break;

                case Phase.Scheduled:
                case Phase.WaitAfternoon:
                    _phase = Phase.Idle;   // poll not shown yet, nothing to freeze
                    break;

                case Phase.Result:
                    // Winner already shown (and applied): let its own timer clear it.
                    break;

                // Cancelled / Idle: nothing to do.
            }
        }

        // ----------------------------------------------------------------- per-frame

        public static void Tick()
        {
            if (!IsHost) return;

            ShowConnectionTipOnce();
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
                        // If we still have a poll left for this moon, wait for the afternoon
                        // (the in-game clock) to open it; otherwise we're done for this moon.
                        if (_landed && _pollsThisMoonCount < ModConfig.PollsPerDay.Value && EventRegistry.Count > 0)
                            _phase = Phase.WaitAfternoon;
                        else
                            _phase = Phase.Idle;
                    }
                    break;

                case Phase.WaitAfternoon:
                    if (_landed && IsAfternoon()) StartPoll();   // poll 2 (afternoon)
                    break;

                case Phase.Cancelled:
                    // Poll was cancelled by takeoff: clear the frozen panel after the delay.
                    if (Time.time >= _resultEndTime)
                    {
                        UiHide();
                        _phase = Phase.Idle;
                    }
                    break;
            }
        }

        // ----------------------------------------------------------------- internals

        /// <summary>
        /// Once the Twitch connection is up, show the "connected as {user}" tip on
        /// screen (synced to all players), like the moment you join a game. Shown once
        /// per session; reset in OnHostReady.
        /// </summary>
        private static void ShowConnectionTipOnce()
        {
            if (_connTipShown || !ModConfig.TwitchEnabled.Value) return;

            var tw = TwitchClient.Instance;
            if (tw == null || !tw.IsConnected) return;

            _connTipShown = true;
            string body = tw.CanPost
                ? Loc.Format("tip.connected.body", ModConfig.EffectiveUsername())
                : Loc.Get("tip.connected.readonly");
            ShowTip(Loc.Get("tip.connected.header"), body);
        }

        /// <summary>True once the in-game clock has reached the afternoon threshold.</summary>
        private static bool IsAfternoon()
        {
            var tod = TimeOfDay.Instance;
            if (tod == null) return false;
            return tod.normalizedTimeOfDay >= ModConfig.AfternoonPollTime.Value;
        }

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
            _pollsThisMoonCount++;

            UiStart();

            // Announce in chat: "a new vote starts" + the three options.
            Announce(Loc.Get("chat.start"));
            Announce(Loc.Format("chat.options", Label(0), Label(1), Label(2)));

            Log.Info("Poll", $"Opened on '{_currentMoon}' — options: " +
                             $"[1) {Label(0)} | 2) {Label(1)} | 3) {Label(2)}] for " +
                             $"{ModConfig.PollDuration.Value:0}s.");
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
                if (!_voters.Add(line.User.ToLowerInvariant()))
                {
                    Log.Debug("Poll", $"vote ignored (already voted): {line.User}");
                    continue;
                }
                _counts[choice - 1]++;
                Log.Debug("Poll", $"vote: {line.User} -> {choice} ('{Label(choice - 1)}')");
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

            var ev = _options[winner];
            Log.Info("Poll", $"Closed on '{_currentMoon}'. Winner: '{ev.Id}' ({ev.Label}) — " +
                             $"{winnerCount} vote(s){(noVotes ? ", random (nobody voted)" : "")}.");

            // Apply the winning event on the host (errors are isolated so one bad event
            // can never crash the run — the log names exactly which event failed).
            Log.Info("Event", $"Applying '{ev.Id}'...");
            try
            {
                ev.Apply();
                Log.Info("Event", $"'{ev.Id}' applied OK.");
            }
            catch (Exception e)
            {
                Log.Error("Event", $"'{ev.Id}' FAILED to apply: {e}");
            }

            UiEnd(winner, winnerCount);

            _resultEndTime = Time.time + Mathf.Max(1f, ModConfig.ResultDisplayDuration.Value);
            _phase = Phase.Result;
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
        private static void Announce(string text, bool inGame = true) => ChatAnnounce.Say(text, inGame);

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

        private static void UiPause()
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastPause();
            else PollHud.Instance?.Pause();
        }

        private static void ShowTip(string header, string body)
        {
            var n = ChatChaosNetworker.Active;
            if (n != null) n.BroadcastTip(header, body);
            else GameTips.Show(header, body);
        }
    }
}
