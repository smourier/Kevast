using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Kevast.Utilities
{
    public class WebServer : IDisposable
    {
        private readonly HttpListener _listener;
        private bool _disposedValue;

        public WebServer(string prefix)
        {
            Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
            Id = Guid.NewGuid();

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);

            // we just want the path
            PathSegments = new Uri(prefix.Replace('*', 'x').Replace('+', 'x')).Segments;
        }

        public Guid Id { get; }
        public string Prefix { get; }
        public string[] PathSegments { get; }

        public void Stop() => _listener.Stop();
        public void Start()
        {
            _listener.Start();
            EventProvider.Default.LogInfo("Server started on " + string.Join("; ", _listener.Prefixes));
            _listener.BeginGetContext(ProcessRequest, null);
        }

        private async void ProcessRequest(IAsyncResult result)
        {
            if (!_listener.IsListening)
                return;

            var ctx = _listener.EndGetContext(result);
            _listener.BeginGetContext(ProcessRequest, null);
            await ProcessRequestAsync(ctx).ConfigureAwait(false);
        }

        protected virtual async Task ProcessRequestAsync(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            EventProvider.Default.LogInfo("Url: " + context.Request.Url);
            AddHeaders(context);
            if (context.Request.Url == null || context.Request.Url.Segments.Length < PathSegments.Length)
            {
                await WriteErrorAsync(context, HttpStatusCode.NotFound).ConfigureAwait(false);
                return;
            }

            // note we're case-sensitive
            for (var i = 0; i < PathSegments.Length; i++)
            {
                if (PathSegments[i] != context.Request.Url.Segments[i])
                {
                    await WriteErrorAsync(context, HttpStatusCode.NotFound).ConfigureAwait(false);
                    return;
                }
            }

            static string normSeg(string s)
            {
                if (s.EndsWith('/'))
                    return s.Substring(0, s.Length - 1);

                return s;
            }
            var segments = context.Request.Url.Segments.Skip(PathSegments.Length).Select(s => normSeg(s)).ToArray();

            EventProvider.Default.LogInfo("Segments: " + string.Join(string.Empty, segments));
            try
            {
                var code = await HandleRequestAsync(context, segments).ConfigureAwait(false);
                if ((int)code >= (int)HttpStatusCode.BadRequest)
                {
                    await WriteErrorAsync(context, code).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex)
            {
                EventProvider.Default.LogInfo("Error: " + ex);
                await WriteErrorAsync(context, HttpStatusCode.InternalServerError).ConfigureAwait(false);
            }
        }

        protected virtual Task<HttpStatusCode> HandleRequestAsync(HttpListenerContext context, string[] segments) => Task.FromResult(HttpStatusCode.NotFound);

        protected virtual Task WriteErrorAsync(HttpListenerContext context, HttpStatusCode code)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.Response.StatusCode = (int)code;
            context.Response.StatusDescription = code.ToString();
            context.Response.ContentType = "text/html; charset=us-ascii";
            var html = $@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01//EN""""http://www.w3.org/TR/html4/strict.dtd""><HTML><HEAD><TITLE>{code}</TITLE><META HTTP-EQUIV=""Content-Type"" Content=""text/html; charset=us-ascii""></HEAD><BODY><h2>{code}</h2><hr><p>HTTP Error {(int)code}.</p></BODY></HTML>";
            using var os = context.Response.OutputStream;
            return html.AsStream().CopyToAsync(context.Response.OutputStream);
        }

        protected virtual void AddHeaders(HttpListenerContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.Response.Headers["Server"] = "Kevast/1.0";
        }

        public override string ToString() => Prefix + " (" + Id + ")";

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects)
                    ((IDisposable)_listener)?.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                _disposedValue = true;
            }
        }

        ~WebServer() { Dispose(disposing: false); }
        public void Dispose() { Dispose(disposing: true); GC.SuppressFinalize(this); }
    }
}
