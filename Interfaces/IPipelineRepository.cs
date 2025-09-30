using System.Collections.Generic;
using System.Threading.Tasks;
using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface IPipelineRepository
    {
        Task<PipelineConfig?> LoadAsync(string identifier);
        Task SaveAsync(PipelineConfig config, string identifier);
        Task<IEnumerable<string>> ListAsync(string directory);
        Task<bool> DeleteAsync(string identifier);
        Task<bool> ExistsAsync(string identifier);
    }
}

