using System.IO;

namespace Kevast
{
    public interface IKevastSerializer
    {
        public void Write(Stream stream, object? value);
        public object? Read(Stream stream);
    }
}
