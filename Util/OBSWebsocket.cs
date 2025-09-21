using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace LAHEE.Util;

public class OBSWebsocket {
    
    public Uri Uri { get; }

    private ClientWebSocket ws;
    
    public OBSWebsocket(Uri uri) {
        Uri = uri;
    }

    private async Task<T> ReadMessage<T>() {
        byte[] bytes = new byte[1024];
        WebSocketReceiveResult result = await ws.ReceiveAsync(bytes, CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close) {
            throw new IOException("Connection terminated: " + result.CloseStatus + " / " + result.CloseStatusDescription);
        }
        string res = Encoding.UTF8.GetString(bytes, 0, result.Count);
        Log.Websocket.LogDebug("OBS ReadMessage: {resp}", res);
        OBSMessage<T> obj = JsonConvert.DeserializeObject<OBSMessage<T>>(res);
        return obj.d;
    }

    private async void SendMessage<T>(int op, T message) {
        OBSMessage<T> obj = new OBSMessage<T>() {
            op = op,
            d = message
        };
        string str = JsonConvert.SerializeObject(obj);
        Log.Websocket.LogDebug("OBS SendMessage: {resp}", str);
        await ws.SendAsync(Encoding.UTF8.GetBytes(str), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async void ConnectAndSendAsync(string parameter) {
        
        

        try {

            ws = new ClientWebSocket();
            await ws.ConnectAsync(Uri, CancellationToken.None);

            OBSHello h = await ReadMessage<OBSHello>();
            if (h.authentication != null) {
                Log.Websocket.LogError("Authentication is not supported. Please disable authentication in the OBS websocket settings.");
                return;
            }
            
            SendMessage(1, new OBSIdentify() {
                rpcVersion = h.rpcVersion
            });
            OBSIdentified _ = await ReadMessage<OBSIdentified>();
            
            SendMessage(6, new OBSRequest<object>() {
                requestType = parameter,
                requestId = "0"
            });
            OBSResponse resp = await ReadMessage<OBSResponse>();
            Log.Websocket.LogInformation("Succeeded: {s}", resp.requestStatus?.result);
            Log.Websocket.LogDebug("Status: {s} / {c}", resp.requestStatus?.code, resp.requestStatus?.comment);
            
        } catch (Exception ex) {
            Log.Websocket.LogCritical(ex, "Failed to communicate with {u}", Uri);
        } finally {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
        }
    }
}