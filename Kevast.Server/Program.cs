using System;

namespace Kevast.Temp
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            using (var kv = new KevastServer("http://localhost:5002/api/"))
            {
                kv.Start();
                Console.WriteLine("Started " + kv.Prefix);
                Console.ReadLine();
            }
        }
    }
}
