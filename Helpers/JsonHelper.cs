
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PipeWiseClient.Helpers
{
    public static class JsonHelper
    {
        public static T? LoadFromFile<T>(string path)
        {
            var json = File.ReadAllText(path);
            
            if (typeof(T) == typeof(Models.PipelineConfig))
            {
                json = FixOperationsInJson(json);
            }
            
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static void SaveToFile<T>(string path, T obj)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        
        private static string FixOperationsInJson(string json)
        {
            try
            {
                var jsonObj = JObject.Parse(json);
                
                var processors = jsonObj["processors"] as JArray;
                if (processors == null) return json;
                
                foreach (var processor in processors)
                {
                    var config = processor["config"] as JObject;
                    if (config == null) continue;
                    
                    var operations = config["operations"];
                    if (operations == null) continue;
                    
                    if (operations is JObject opObj && opObj.ContainsKey("ValueKind"))
                    {
                        config["operations"] = new JArray();
                        continue;
                    }
                    
                    if (operations is JArray opArray)
                    {
                        var fixedOperations = new JArray();
                        
                        foreach (var op in opArray)
                        {
                            if (op is JObject opObject)
                            {
                                if (opObject.ContainsKey("ValueKind"))
                                {
                                    continue;
                                }
                                
                                fixedOperations.Add(op);
                            }
                            else if (op is JValue jValue && jValue.Type == JTokenType.String)
                            {
                                fixedOperations.Add(new JObject
                                {
                                    ["action"] = JToken.FromObject(jValue.Value ?? "")  
                                });
                            }
                        }
                        
                        config["operations"] = fixedOperations;
                    }
                }
                
                return jsonObj.ToString();
            }
            catch
            {
                return json;
            }
        }
    }
}