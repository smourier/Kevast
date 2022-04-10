using System;
using System.Diagnostics;

namespace Kevast.Temp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var dic = new KevastDictionary<string, object>();

            var sw = new Stopwatch();
            sw.Start();
            for (var i = 0; i < 10_000_000; i++)
            {
                dic["test" + i] = i;
                if (i % 1000000 == 0)
                {
                    Console.WriteLine(i);
                }
            }

            Console.WriteLine(sw.Elapsed);
            sw.Restart();

            Console.WriteLine(dic.Count);
            dic.Save("test.bin");
            Console.WriteLine(sw.Elapsed);
        }
    }
}