using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public static class ApiClient
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task<string> SendPipelineRequestAsync(PipelineConfig config)
        {
            var json = JsonConvert.SerializeObject(config);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("http://localhost:8000/pipeline/run", content);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
