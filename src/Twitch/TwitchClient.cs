using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ChatChaos.Config;

namespace ChatChaos.Twitch
{
    /// <summary>
    /// A minimal, dependency-free Twitch IRC client.
    ///
    /// It runs on a BACKGROUND thread so the game is never blocked. Incoming chat
    /// messages are pushed into a thread-safe queue that the poll logic drains on
    /// the main thread (TryDequeue). Outgoing messages (poll announcements) are sent
    /// under a lock.
    ///
    /// Two modes:
    ///   - Authenticated (token set): reads votes AND posts messages as your account.
    ///   - Anonymous (no token): read-only. Votes are still counted, but the mod
    ///     cannot post announcements in chat.
    ///
    /// SECURITY: the OAuth token is only ever used to log in to Twitch chat. It is
    /// read from the local config and never logged.
    /// </summary>
    public class TwitchClient
    {
        public static TwitchClient? Instance { get; private set; }

        private const string Host = "irc.chat.twitch.tv";

        private TcpClient? _tcp;
        private Stream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private Thread? _thread;

        private volatile bool _running;
        private volatile bool _connected;
        private volatile bool _canPost;

        private string _channel = "";
        private string _nick = "";
        private string _token = "";   // "oauth:...." or empty for anonymous
        private bool _ssl;

        private readonly object _writeLock = new();
        private readonly ConcurrentQueue<ChatLine> _incoming = new();

        /// <summary>True once logged in and joined.</summary>
        public bool IsConnected => _connected;

        /// <summary>True when the client is authenticated and allowed to post.</summary>
        public bool CanPost => _connected && _canPost;

        public readonly struct ChatLine
        {
            public readonly string User;
            public readonly string Message;
            public ChatLine(string user, string message) { User = user; Message = message; }
        }

        /// <summary>Start (or restart) the connection using the current config.</summary>
        public static void StartFromConfig()
        {
            Stop();

            if (!ModConfig.TwitchEnabled.Value)
            {
                Plugin.Log.LogInfo("Twitch: integration disabled in config. Polls will pick random options.");
                return;
            }

            string channel = (ModConfig.TwitchChannel.Value ?? "").Trim().TrimStart('#').ToLowerInvariant();
            if (channel.Length == 0)
            {
                Plugin.Log.LogWarning("Twitch: no Channel set in config — cannot connect. " +
                                      "Set Twitch/Channel (and OAuthToken to post messages).");
                return;
            }

            string token = NormalizeToken(ModConfig.TwitchOAuthToken.Value);
            string nick = token.Length > 0
                ? ModConfig.EffectiveUsername().Trim().ToLowerInvariant()
                : "justinfan" + new System.Random().Next(10000, 99999);
            if (nick.Length == 0) nick = channel; // authenticated but no username -> use channel

            Instance = new TwitchClient
            {
                _channel = channel,
                _nick = nick,
                _token = token,
                _ssl = ModConfig.TwitchUseSsl.Value,
            };
            Instance.Start();
        }

        public static void Stop()
        {
            Instance?.Dispose();
            Instance = null;
        }

        private void Start()
        {
            _running = true;
            _thread = new Thread(RunLoop) { IsBackground = true, Name = "ChatChaos-Twitch" };
            _thread.Start();
        }

        /// <summary>Main background loop with simple auto-reconnect.</summary>
        private void RunLoop()
        {
            int attempt = 0;
            while (_running)
            {
                try
                {
                    Connect();
                    attempt = 0;
                    ReadUntilClosed();
                }
                catch (Exception e)
                {
                    if (_running)
                        Plugin.Log.LogWarning($"Twitch: connection error: {e.Message}");
                }

                _connected = false;
                CloseSocket();

                if (!_running) break;

                // Backoff: 3s, 6s, 12s, capped at 30s.
                attempt++;
                int delay = Math.Min(30, 3 * (int)Math.Pow(2, Math.Min(attempt - 1, 3)));
                for (int i = 0; i < delay * 10 && _running; i++) Thread.Sleep(100);
            }
        }

        private void Connect()
        {
            int port = _ssl ? 6697 : 6667;
            _tcp = new TcpClient();
            _tcp.Connect(Host, port);

            Stream netStream = _tcp.GetStream();
            if (_ssl)
            {
                var ssl = new SslStream(netStream, false, (s, c, ch, e) => true);
                ssl.AuthenticateAsClient(Host);
                netStream = ssl;
            }
            _stream = netStream;
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

            // Login.
            if (_token.Length > 0)
                SendRaw("PASS " + _token);
            SendRaw("NICK " + _nick);
            SendRaw("JOIN #" + _channel);

            _canPost = _token.Length > 0;
            _connected = true;
            Plugin.Log.LogInfo($"Twitch: connected to #{_channel} as {_nick} " +
                               $"({(_canPost ? "read + post" : "read-only")}).");
        }

        private void ReadUntilClosed()
        {
            string? line;
            while (_running && _reader != null && (line = _reader.ReadLine()) != null)
            {
                HandleLine(line);
            }
        }

        private void HandleLine(string line)
        {
            // Keep-alive: Twitch pings, we must pong back with the same payload.
            if (line.StartsWith("PING"))
            {
                string payload = line.Length > 5 ? line.Substring(5) : ":tmi.twitch.tv";
                SendRaw("PONG " + payload);
                return;
            }

            // We only care about chat messages:
            //   :nick!nick@nick.tmi.twitch.tv PRIVMSG #channel :the message text
            int privIdx = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
            if (privIdx < 0 || !line.StartsWith(":")) return;

            int bang = line.IndexOf('!');
            if (bang <= 1) return;
            string user = line.Substring(1, bang - 1);

            int textStart = line.IndexOf(" :", privIdx, StringComparison.Ordinal);
            if (textStart < 0) return;
            string message = line.Substring(textStart + 2).Trim();

            if (user.Length > 0 && message.Length > 0)
                _incoming.Enqueue(new ChatLine(user, message));
        }

        /// <summary>Drain one queued chat line (call on the main thread).</summary>
        public bool TryDequeue(out ChatLine line) => _incoming.TryDequeue(out line);

        /// <summary>Post a message in the channel (no-op if anonymous/not connected).</summary>
        public void SendMessage(string text)
        {
            if (!_canPost || !_connected || string.IsNullOrEmpty(text)) return;
            try
            {
                lock (_writeLock)
                    _writer?.WriteLine($"PRIVMSG #{_channel} :{text}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Twitch: failed to send message: {e.Message}");
            }
        }

        private void SendRaw(string raw)
        {
            lock (_writeLock)
                _writer?.WriteLine(raw);
        }

        private void CloseSocket()
        {
            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            _reader = null; _writer = null; _stream = null; _tcp = null;
        }

        public void Dispose()
        {
            _running = false;
            _connected = false;
            CloseSocket();
            try { _thread?.Join(500); } catch { }
            _thread = null;
        }

        /// <summary>Ensures the token has the required "oauth:" prefix; empty stays empty.</summary>
        private static string NormalizeToken(string? raw)
        {
            string t = (raw ?? "").Trim();
            if (t.Length == 0) return "";
            if (t.StartsWith("oauth:", StringComparison.OrdinalIgnoreCase)) return t;
            return "oauth:" + t;
        }
    }
}
