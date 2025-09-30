using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PipeWiseClient.Interfaces;
using PipeWiseClient.Models;

namespace PipeWiseClient.Services
{
    public class FilePipelineRepository : IPipelineRepository
    {
        private readonly INotificationService _notifications;
        public FilePipelineRepository(INotificationService notifications)
        {
            _notifications = notifications;
        }

        public async Task<PipelineConfig?> LoadAsync(string identifier)
        {
            if (!File.Exists(identifier)) return null;
            try
            {
                var json = await File.ReadAllTextAsync(identifier);
                return JsonConvert.DeserializeObject<PipelineConfig>(json);
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאת טעינה", "לא ניתן לטעון קובץ קונפיגורציה", ex.Message);
                throw;
            }
        }

        public async Task SaveAsync(PipelineConfig config, string identifier)
        {
            try
            {
                var dir = Path.GetDirectoryName(identifier);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await File.WriteAllTextAsync(identifier, json);
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאת שמירה", "לא ניתן לשמור קובץ קונפיגורציה", ex.Message);
                throw;
            }
        }

        public Task<IEnumerable<string>> ListAsync(string directory)
        {
            if (!Directory.Exists(directory))
                return Task.FromResult(Enumerable.Empty<string>());
            var files = Directory.GetFiles(directory, "*.json");
            return Task.FromResult(files.AsEnumerable());
        }

        public Task<bool> DeleteAsync(string identifier)
        {
            try
            {
                if (File.Exists(identifier))
                {
                    File.Delete(identifier);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _notifications.Error("שגיאת מחיקה", "לא ניתן למחוק קובץ קונפיגורציה", ex.Message);
                return Task.FromResult(false);
            }
        }

        public Task<bool> ExistsAsync(string identifier) => Task.FromResult(File.Exists(identifier));
    }
}


