using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using WatsonWebserver.Core;

namespace LAHEE {
    internal static class Extensions {

        private const int CHUNK_SIZE = 250_000;

        public static string GetParameter(this HttpRequestBase req, string str) {
            return HttpUtility.ParseQueryString(req.DataAsString)[str];
        }

        public static async Task SendJson(this HttpResponseBase resp, object obj) {
            String data = JsonConvert.SerializeObject(obj);
            /*if (data.Length > CHUNK_SIZE) {
                resp.ChunkedTransfer = true;
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                byte[] block = new byte[CHUNK_SIZE];
                int pos = 0;
                do {
                    Array.Copy(bytes, pos, block, 0, CHUNK_SIZE);
                    await resp.SendChunk(block);
                } while ((pos += CHUNK_SIZE) < resp.ContentLength);
                byte[] fin = new byte[bytes.Length - (pos - CHUNK_SIZE)];
                Array.Copy(bytes, pos - CHUNK_SIZE, fin, 0, fin.Length);
                await resp.SendFinalChunk(fin);
            } else {
                await resp.Send(data);
            }*/
            await resp.Send(data);
        }

    }
}
