using AiInterview.Api.Services.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AiInterview.Api.Services;

public sealed class InterviewReportGenerationQueue : IInterviewReportGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, byte> _queuedIds = new();

    public async ValueTask<bool> EnqueueAsync(Guid interviewId, CancellationToken cancellationToken = default)
    {
        if (!_queuedIds.TryAdd(interviewId, 0))
        {
            return false;
        }

        await _channel.Writer.WriteAsync(interviewId, cancellationToken);
        return true;
    }

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public bool IsQueued(Guid interviewId)
    {
        return _queuedIds.ContainsKey(interviewId);
    }

    public void MarkCompleted(Guid interviewId)
    {
        _queuedIds.TryRemove(interviewId, out _);
    }
}
