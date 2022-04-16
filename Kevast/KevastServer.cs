using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Kevast.Utilities;

namespace Kevast
{
    public class KevastServer : WebServer
    {
        private readonly KevastDictionary<string, KevastRemoteServer> _remoteServers = new KevastDictionary<string, KevastRemoteServer>(StringComparer.OrdinalIgnoreCase);
        private readonly KevastDictionary<string, KevastDictionary<string, object?>> _dictionaries = new KevastDictionary<string, KevastDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        private static readonly byte[] _true = new byte[] { 0x74, 0x72, 0x75, 0x65 }; // "true"
        private static readonly byte[] _false = new byte[] { 0x66, 0x61, 0x6C, 0x73, 0x65 }; // "false"
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
#if DEBUG
            WriteIndented = true,
#endif
        };

        public KevastServer(string prefix)
            : base(prefix)
        {
        }
        public IReadOnlyCollection<KevastRemoteServer> RemoteServers => (IReadOnlyCollection<KevastRemoteServer>)_remoteServers.Values;

        public virtual KevastRemoteServer AddRemoteServer(KevastRemoteServer server)
        {
            if (server == null)
                throw new ArgumentNullException(nameof(server));

            return _remoteServers.AddOrUpdate(server.BaseUri.ToString(), server, (k, o) => o);
        }

        protected override async Task<HttpStatusCode> HandleRequestAsync(HttpListenerContext context, string[] segments)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Length == 0)
                throw new ArgumentException(null, nameof(segments));

            if (context.Request.Url == null)
                throw new ArgumentException(null, nameof(context));

            Stream os;
            KevastDictionary<string, object?>? dic;
            string name;
            string? key;
            object? value;
            switch (segments[0])
            {
                case "set":
                    if (context.Request.HttpMethod != "POST")
                        return HttpStatusCode.BadRequest;

                    if (context.Request.ContentType == "application/json")
                    {
                        if (segments.Length < 3)
                            return HttpStatusCode.BadRequest;

                        name = segments[1];
                        key = segments[2];
                        value = JsonSerializer.DeserializeAsync<object>(context.Request.InputStream, _jsonSerializerOptions);
                    }
                    else if (context.Request.ContentType == "application/x-www-form-urlencoded")
                    {
                        if (segments.Length < 2)
                            return HttpStatusCode.BadRequest;

                        name = segments[1];
                        key = null;
                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding, true);
                        var str = await reader.ReadToEndAsync().ConfigureAwait(false);
                        value = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(str));
                    }
                    else if (context.Request.ContentType?.StartsWith("text/") == true)
                    {
                        if (segments.Length < 3)
                            return HttpStatusCode.BadRequest;

                        name = segments[1];
                        key = segments[2];

                        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding, true);
                        value = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        if (segments.Length < 3)
                            return HttpStatusCode.BadRequest;

                        name = segments[1];
                        key = segments[2];

                        using var ms = new MemoryStream();
                        await context.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                        value = ms.ToArray();
                    }

                    os = context.Response.OutputStream;
                    if (!_dictionaries.TryGetValue(name, out dic))
                    {
                        dic = new KevastDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        dic = _dictionaries.AddOrUpdate(name, dic, (k, o) => o);
                    }

                    if (value is NameValueCollection coll)
                    {
                        foreach (var k in coll.AllKeys)
                        {
                            if (k == null)
                                continue;

                            dic[k] = coll[k];
                        }
                    }
                    else if (key != null)
                    {
                        dic[key] = value;
                    }
                    os.Dispose();
                    return HttpStatusCode.OK;

                case "get":
                    if (segments.Length < 1 || context.Request.HttpMethod != "GET")
                        return HttpStatusCode.BadRequest;

                    os = context.Response.OutputStream;
                    if (segments.Length == 1)
                    {
                        await JsonSerializer.SerializeAsync(os, _dictionaries, _jsonSerializerOptions).ConfigureAwait(false);
                    }
                    else
                    {
                        name = segments[1];
                        if (segments.Length == 2)
                        {
                            if (_dictionaries.TryGetValue(name, out dic))
                            {
                                await JsonSerializer.SerializeAsync(os, dic, _jsonSerializerOptions).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            key = segments[2];
                            if (_dictionaries.TryGetValue(name, out dic) && dic.TryGetValue(key, out value))
                            {
                                await JsonSerializer.SerializeAsync(os, value, _jsonSerializerOptions).ConfigureAwait(false);
                            }
                        }
                    }
                    os.Dispose();
                    return HttpStatusCode.OK;

                case "del":
                    if (segments.Length < 1 || context.Request.HttpMethod != "DELETE")
                        return HttpStatusCode.BadRequest;

                    var result = false;
                    if (segments.Length == 1)
                    {
                        // note this is slightly incorrect (race condition, no lock)
                        result = _dictionaries.Count > 0;
                        _dictionaries.Clear();
                    }
                    else
                    {
                        name = segments[1];
                        if (segments.Length == 2)
                        {
                            result = _dictionaries.TryRemove(name, out _);
                        }
                        else
                        {
                            key = segments[2];
                            if (_dictionaries.TryGetValue(name, out dic) && dic.TryRemove(key, out _))
                            {
                                result = true;
                            }
                        }
                    }

                    os = context.Response.OutputStream;
                    if (result)
                    {
                        await os.WriteAsync(_true).ConfigureAwait(false);
                    }
                    else
                    {
                        await os.WriteAsync(_false).ConfigureAwait(false);
                    }
                    os.Dispose();
                    return HttpStatusCode.OK;
            }
            return HttpStatusCode.NotFound;
        }
    }
}
