using System.IO;

namespace Kevast
{
    public interface IKevastSerializer
    {
        public void Write(Stream stream, object? value);
        public bool TryRead(Stream stream, out object? value);
    }
}
