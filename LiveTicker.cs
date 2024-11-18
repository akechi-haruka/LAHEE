
using Microsoft.Extensions.Logging;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace LAHEE {
    internal class LiveTicker {

        private static List<LiveTickerWS> connecteds = new List<LiveTickerWS>();

        internal static void Initialize() {
            Log.Network.LogDebug("Initalizing websocket...");

            WebSocketServer wssv = new WebSocketServer(8001);

            wssv.AddWebSocketService<LiveTickerWS>("/");
            wssv.Start();
            Log.Network.LogDebug("Websocket initialized.");
        }

        public static void BroadcastPing() {
            lock (connecteds) {
                Log.Network.LogDebug("Pinging {n} websockets...", connecteds.Count);
                foreach (LiveTickerWS ws in connecteds) {
                    ws.SendMessage("{}");
                }
            }
        }

        public class LiveTickerWS : WebSocketBehavior {

            private string ip;

            protected override void OnOpen() {
                base.OnOpen();
                lock (connecteds) {
                    connecteds.Add(this);
                }
                ip = Context.UserEndPoint.ToString();
                LAHEE.Log.Network.LogInformation("Connection to LiveTickerWS: {ip}",ip);
            }

            protected override void OnClose(CloseEventArgs e) {
                base.OnClose(e);
                lock (connecteds) {
                    connecteds.Remove(this);
                }
                LAHEE.Log.Network.LogInformation("Disconnected from LiveTickerWS: {ip} / {reason} ({code})", ip, e.Reason, e.Code);
            }

            protected override void OnMessage(MessageEventArgs e) {
                if (e.IsText) {
                    LAHEE.Log.Network.LogDebug("Incoming WS message: " + e.Data);
                }
            }

            public void SendMessage(string str) {
                Send(str);
            }

        }
    }
}