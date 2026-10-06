using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Moongate.Admin.Tests.TestSupport.Logging;

public sealed class MemoryLogSink : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();

    public void Emit(LogEvent logEvent)
    {
        Events.Enqueue(logEvent);
    }
}
