using System.Collections.Generic;

namespace AiInterview.Api.Services.Interfaces;

public interface IInterviewReportGenerationQueue
{
    ValueTask<bool> EnqueueAsync(Guid interviewId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default);

    bool IsQueued(Guid interviewId);

    void MarkCompleted(Guid interviewId);
}
