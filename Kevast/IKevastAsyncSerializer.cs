using System.IO;
using System.Threading.Tasks;

namespace Kevast
{
    public interface IKevastAsyncSerializer
    {
        public Task WriteAsync(Stream stream, object? value);
        public Task<bool> TryReadAsync(Stream stream, out object? value);
    }
}
