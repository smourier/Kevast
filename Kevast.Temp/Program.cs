using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kevast.Temp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            SaveAndLoad();
            return;

            var sw = new Stopwatch();
            sw.Start();
            var dic = new KevastDictionary<string, object>();

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

        static void MainServer(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();
            FillServer(5002, 100, 1000).Wait();
            Console.WriteLine(sw.Elapsed);
            return;
            var dic = new KevastDictionary<string, object>();

        }

        static async Task FillServer(int port, int dics, int max)
        {
            var client = new HttpClient();
            var api = $"http://localhost:{port}/api";

            var tick = Environment.TickCount;
            for (var d = 0; d < dics; d++)
            {
                for (var i = 0; i < max; i++)
                {
                    await client.GetAsync(api + $"/set/dic{d}?key{tick}{i}=value{i}");
                }
            }
        }

        private struct Test
        {
            public int Code;
            public long Stuff;
        }

        static void SaveAndLoad()
        {
            var dic = new KevastDictionary<string, object>();
            //for (var i = 0; i < 10000; i++)
            //{
            //    dic["héllo" + i] = "hallo" + i;
            //}
            dic["dec"] = 1234567890m;
            dic["dtu"] = DateTime.UtcNow;

            dic["struct"] = new Test { Code = 123, Stuff = 567 };

            dic.Save("sal.bin");

            var dic2 = KevastDictionary<string, object>.Load("sal.bin");
        }

        static void OopMain()
        {
            var client = new HttpClient();
            // internal dictionary API
            client.PostAsync("http://localhost:5002/api/[internal]/set/[dictionary]/[key]", null);
            client.GetAsync("http://localhost:5002/api/[internal]/get/[dictionary]/[key]");
            client.DeleteAsync("http://localhost:5002/api/[internal]/del/[dictionary]/[key]");

            // server API
            client.PostAsync("http://localhost:5002/api/server/connect/[server]", null);
            client.GetAsync("http://localhost:5002/api/server");

            // dictionary API
            client.PostAsync("http://localhost:5002/api/set/[dictionary]/[key]", null);
            client.GetAsync("http://localhost:5002/api/get/[dictionary]/[key]");
            client.DeleteAsync("http://localhost:5002/api/del/[dictionary]/[key]");
        }
    }
}