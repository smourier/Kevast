using System;
using System.Diagnostics;
using System.Net.Http;

namespace Kevast.Temp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (var kv = new KevastServer("http://localhost:5002/api/"))
            //using (var kv2 = new KevastServer("http://localhost:5003/api/"))
            {
                kv.Start();
                //kv2.Start();
                Console.ReadLine();
            }
            return;
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