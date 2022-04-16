using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using Kevast.Utilities;

namespace Kevast.Temp
{
    [MemoryDiagnoser]
    [TailCallDiagnoser]
    [EtwProfiler]
    [ConcurrencyVisualizerProfiler]
    [NativeMemoryProfiler]
    [ThreadingDiagnoser]
    public class StringAsStream
    {
        private static string _text;

        static StringAsStream()
        {
            _text = File.ReadAllText("SampleText.txt");
        }

        public static string text => _text;

        [Benchmark]
        public string Advanced()
        {
            using var us = new MemoryStream();
            _text.AsStream(Encoding.UTF8).CopyTo(us);
            us.Position = 0;
            return Encoding.UTF8.GetString(us.ToArray());
        }

        [Benchmark]
        public async Task<string> AdvancedAsync()
        {
            using var us = new MemoryStream();
            await _text.AsStream(Encoding.UTF8).CopyToAsync(us).ConfigureAwait(false);
            us.Position = 0;
            return Encoding.UTF8.GetString(us.ToArray());
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();
            Console.WriteLine(StringAsStream.text.Length);
            var s = new StringAsStream();
            for (var i = 0; i < 1000; i++)
            {
                //s.Simple();
                await s.AdvancedAsync();
            }

            Console.WriteLine(sw.Elapsed);
            //var summary = BenchmarkRunner.Run<StringAsStream>();
        }
    }
}