using Newtonsoft.Json;
using System.Web;
using WatsonWebserver.Core;

namespace LAHEE {
    static class Extensions {

        private const int CHUNK_SIZE = 250_000;

        public static string GetParameter(this HttpRequestBase req, string str) {
            return HttpUtility.ParseQueryString(req.DataAsString)[str];
        }

        public static async Task SendJson(this HttpResponseBase resp, object obj) {
            String data = JsonConvert.SerializeObject(obj);
            await resp.Send(data);
        }

    }
}
