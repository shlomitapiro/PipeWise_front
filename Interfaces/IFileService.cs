using System.Collections.Generic;
using System.Threading.Tasks;

namespace PipeWiseClient.Interfaces
{
    public interface IFileService
    {
        Task<List<string>> DetectColumnsAsync(string filePath);
        Task<Dictionary<string, string>> DetectColumnTypesAsync(string filePath, List<string> columnNames);
        string GetFileType(string filePath);
        bool IsFileSupported(string filePath);
    }
}

