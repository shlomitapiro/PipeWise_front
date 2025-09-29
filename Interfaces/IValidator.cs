using PipeWiseClient.Models;

namespace PipeWiseClient.Interfaces
{
    public interface IValidator<T>
    {
        ValidationResult Validate(T item);
    }
}

