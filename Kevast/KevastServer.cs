using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Kevast.Utilities;

namespace Kevast
{
    public class KevastServer : WebServer
    {
        private static readonly byte[] _true = new byte[] { 0x74, 0x72, 0x75, 0x65 }; // "true"
        private static readonly byte[] _false = new byte[] { 0x66, 0x61, 0x6C, 0x73, 0x65 }; // "false"
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
#if DEBUG
            WriteIndented = true,
#endif
        };

        private readonly KevastDictionary<Uri, KevastRemoteServer> _remoteServers = new KevastDictionary<Uri, KevastRemoteServer>();
        private readonly KevastDictionary<string, KevastDictionary<string, object?>> _dictionaries = new KevastDictionary<string, KevastDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
        private long _totalCountOfRequests;

        public KevastServer(string prefix)
            : base(prefix)
        {
            StartTimeUtc = DateTimeOffset.UtcNow;
        }

        public IReadOnlyCollection<KevastRemoteServer> RemoteServers => (IReadOnlyCollection<KevastRemoteServer>)_remoteServers.Values;
        public DateTimeOffset StartTimeUtc { get; }
        public long TotalCountOfItems
        {
            get
            {
                var count = 0L;
                foreach (var kv in _dictionaries)
                {
                    count += kv.Value.Count;
                }
                return count;
            }
        }

        public virtual void ConvergeWithRemoteServers()
        {
        }

        public virtual KevastRemoteServer AddRemoteServer(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            var server = new KevastRemoteServer(uri);
            var result = _remoteServers.AddOrUpdate(uri, server, (k, o) => o);
            if (result != server)
            {
                ConvergeWithRemoteServers();
            }
            return result;
        }

        public virtual bool RemoveRemoteServer(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (!_remoteServers.TryRemove(uri, out var server))
                return false;

            ConvergeWithRemoteServers();
            return true;
        }

        private class NameKeyValue
        {
            public NameKeyValue(string name)
            {
                Name = name;
            }

            public NameKeyValue(string name, string key)
                : this(name)
            {
                Key = key;
            }

            public string Name;
            public string? Key;
            public object? Value;
        }

        private static async Task<NameKeyValue?> TryGetRequestValueAsync(HttpListenerContext context, string[] segments)
        {
            NameKeyValue value;
            if (context.Request.ContentType == "application/json")
            {
                if (segments.Length < 3)
                    return null;

                value = new NameKeyValue(segments[1], segments[2]);
                value.Value = await JsonSerializer.DeserializeAsync<object>(context.Request.InputStream, _jsonSerializerOptions).ConfigureAwait(false);
                return value;
            }

            if (context.Request.ContentType == "application/x-www-form-urlencoded")
            {
                if (segments.Length < 2)
                    return null;

                value = new NameKeyValue(segments[1]);
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding, true);
                var str = await reader.ReadToEndAsync().ConfigureAwait(false);
                value.Value = HttpUtility.ParseQueryString(HttpUtility.UrlDecode(str));
                return value;
            }

            if (context.Request.ContentType?.StartsWith("text/") == true)
            {
                if (segments.Length < 3)
                    return null;

                value = new NameKeyValue(segments[1], segments[2]);
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding, true);
                value.Value = await reader.ReadToEndAsync().ConfigureAwait(false);
                return value;
            }

            if (segments.Length < 3)
                return null;

            value = new NameKeyValue(segments[1], segments[2]);
            using var ms = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
            value.Value = ms.ToArray();
            return value;
        }

        private static async Task<object?> TryGetRequestValueAsync(HttpListenerContext context)
        {
            if (context.Request.ContentType == "application/json")
                return await JsonSerializer.DeserializeAsync<object>(context.Request.InputStream, _jsonSerializerOptions).ConfigureAwait(false);

            if (context.Request.ContentType?.StartsWith("text/") == true)
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding, true);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            using var ms = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        private static ValueTask WriteBooleanJson(HttpListenerContext context, bool value)
        {
            context.Response.ContentType = "application/json";
            if (value)
                return context.Response.OutputStream.WriteAsync(_true);

            return context.Response.OutputStream.WriteAsync(_false);
        }

        protected override async Task<HttpStatusCode> HandleRequestAsync(HttpListenerContext context, string[] segments)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Length == 0)
                return HttpStatusCode.BadRequest;

            if (context.Request.Url == null)
                return HttpStatusCode.BadRequest;

            Interlocked.Increment(ref _totalCountOfRequests);
            Stream os;
            KevastDictionary<string, object?>? dic;
            string name;
            string? key;
            Uri? uri;
            object? value;
            switch (segments[0])
            {
                case "set":
                    // /set/[dictionary]?key=value
                    if (context.Request.HttpMethod == "GET" && segments.Length == 2)
                    {
                        os = context.Response.OutputStream;
                        if (!_dictionaries.TryGetValue(segments[1], out dic))
                        {
                            dic = new KevastDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            dic = _dictionaries.AddOrUpdate(segments[1], dic, (k, o) => o);
                        }

                        var qs = context.Request.QueryString;
                        foreach (var k in qs.AllKeys)
                        {
                            if (k == null)
                                continue;

                            dic[k] = qs[k];
                        }
                        os.Dispose();
                        return HttpStatusCode.OK;
                    }

                    if (context.Request.HttpMethod != "POST")
                        return HttpStatusCode.BadRequest;

                    var v = await TryGetRequestValueAsync(context, segments);
                    if (v == null)
                        return HttpStatusCode.BadRequest;

                    os = context.Response.OutputStream;
                    if (!_dictionaries.TryGetValue(v.Name, out dic))
                    {
                        dic = new KevastDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        dic = _dictionaries.AddOrUpdate(v.Name, dic, (k, o) => o);
                    }

                    if (v.Value is NameValueCollection coll)
                    {
                        foreach (var k in coll.AllKeys)
                        {
                            if (k == null)
                                continue;

                            dic[k] = coll[k];
                        }
                    }
                    else if (v.Key != null)
                    {
                        dic[v.Key] = v.Value;
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
                        context.Response.ContentType = "application/json";
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
                    var result = false;
                    if (context.Request.HttpMethod == "GET" && segments.Length == 2)
                    {
                        os = context.Response.OutputStream;
                        if (_dictionaries.TryGetValue(segments[1], out dic))
                        {
                            result = true;
                            var qs = context.Request.QueryString;
                            foreach (var k in qs.AllKeys)
                            {
                                if (k == null)
                                    continue;

                                if (!dic.TryRemove(k, out _))
                                {
                                    result = false;
                                }
                            }
                        }
                        await WriteBooleanJson(context, result).ConfigureAwait(false);
                        os.Dispose();
                        return HttpStatusCode.OK;
                    }

                    if (segments.Length < 1 || context.Request.HttpMethod != "DELETE")
                        return HttpStatusCode.BadRequest;

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
                    await WriteBooleanJson(context, result).ConfigureAwait(false);
                    os.Dispose();
                    return HttpStatusCode.OK;

                case "server":
                    if (segments.Length < 2)
                        return HttpStatusCode.BadRequest;

                    name = segments[1];
                    switch (name)
                    {
                        case "get":
                            if (context.Request.HttpMethod != "GET")
                                return HttpStatusCode.BadRequest;

                            if (segments.Length != 2)
                                return HttpStatusCode.BadRequest;

                            var dictionaries = _dictionaries.Select(d => new { Name = d.Key, d.Value.Count });
                            os = context.Response.OutputStream;
                            context.Response.ContentType = "application/json";
                            var server = new
                            {
                                DateTimeOffset.UtcNow,
                                StartTimeUtc,
                                totalCountOfRequests = _totalCountOfRequests,
                                TotalCountOfItems,
                                RemoteServers,
                                dictionaries,
                            };
                            await JsonSerializer.SerializeAsync(os, server, _jsonSerializerOptions).ConfigureAwait(false);
                            os.Dispose();
                            return HttpStatusCode.OK;

                        case "set":
                            if (context.Request.HttpMethod != "POST")
                                return HttpStatusCode.BadRequest;

                            value = await TryGetRequestValueAsync(context).ConfigureAwait(false);
                            if (value == null)
                                return HttpStatusCode.BadRequest;

                            if (!Uri.TryCreate(value.ToString(), UriKind.Absolute, out uri))
                                return HttpStatusCode.BadRequest;

                            var remote = AddRemoteServer(uri);
                            os = context.Response.OutputStream;
                            context.Response.ContentType = "application/json";
                            await JsonSerializer.SerializeAsync(os, remote, _jsonSerializerOptions).ConfigureAwait(false);
                            os.Dispose();
                            return HttpStatusCode.OK;

                        case "del":
                            if (context.Request.HttpMethod != "DELETE")
                                return HttpStatusCode.BadRequest;

                            value = await TryGetRequestValueAsync(context).ConfigureAwait(false);
                            if (value == null)
                                return HttpStatusCode.BadRequest;

                            if (!Uri.TryCreate(value.ToString(), UriKind.Absolute, out uri))
                                return HttpStatusCode.BadRequest;

                            var res = RemoveRemoteServer(uri);
                            os = context.Response.OutputStream;
                            await WriteBooleanJson(context, res).ConfigureAwait(false);
                            os.Dispose();
                            return HttpStatusCode.OK;

                        default:
                            return HttpStatusCode.BadRequest;
                    }
            }
            return HttpStatusCode.NotFound;
        }
    }
}
