using System.IO;
using Newtonsoft.Json;

namespace PipeWiseClient.Helpers
{
    public static class JsonHelper
    {
        public static T? LoadFromFile<T>(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void SaveToFile<T>(string path, T obj)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }
}