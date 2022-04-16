using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Kevast.Utilities
{
    // use WpfTraceSpy to read these traces: https://github.com/smourier/TraceSpy with the guid value as an ETW Filter.
    public sealed class EventProvider : IDisposable
    {
        public static readonly EventProvider Default = new EventProvider(new Guid("964d4572-adb9-4f3a-8170-fcbecec27465"));
        private long _handle;

        public EventProvider(Guid id)
        {
            Id = id;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var hr = EventRegister(id, IntPtr.Zero, IntPtr.Zero, out _handle);
            if (hr != 0)
                throw new Win32Exception(hr);
        }

        public Guid Id { get; }

        public void LogInfo(object message, [CallerMemberName] string? methodName = null) => Log(TraceLevel.Info, message, methodName);
        public void LogError(object message, [CallerMemberName] string? methodName = null) => Log(TraceLevel.Error, message, methodName);
        public void LogWarning(object message, [CallerMemberName] string? methodName = null) => Log(TraceLevel.Warning, message, methodName);
        public void LogVerbose(object message, [CallerMemberName] string? methodName = null) => Log(TraceLevel.Verbose, message, methodName);
        public void Log(TraceLevel level, object message, [CallerMemberName] string? methodName = null)
        {
            if (methodName == null)
            {
                WriteMessageEvent(message?.ToString(), (byte)level);
                return;
            }

            WriteMessageEvent(methodName + ": " + message, (byte)level);
        }

        public bool WriteMessageEvent(string? text, byte level = 0, long keywords = 0)
        {
            if (_handle == 0 || text == null)
                return false;

            return EventWriteString(_handle, level, keywords, text) == 0;
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, 0);
            if (handle != 0)
            {
                _ = EventUnregister(handle);
            }
        }

        [DllImport("advapi32")]
        private static extern int EventRegister([MarshalAs(UnmanagedType.LPStruct)] Guid ProviderId, IntPtr EnableCallback, IntPtr CallbackContext, out long RegHandle);

        [DllImport("advapi32")]
        private static extern int EventUnregister(long RegHandle);

        [DllImport("advapi32")]
        private static extern int EventWriteString(long RegHandle, byte Level, long Keyword, [MarshalAs(UnmanagedType.LPWStr)] string String);
    }
}
