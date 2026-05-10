using AiInterview.Api.DTOs.Interviews;

namespace AiInterview.Api.Hubs;

public interface IInterviewClient
{
    Task ReceiveQuestion(SignalRQuestionPayload payload);

    Task ReceiveFollowUp(SignalRFollowUpPayload payload);

    Task TypingIndicator(object payload);

    Task InterviewStatusChanged(object payload);

    Task ReportProgress(object payload);

    Task ReportReady(object payload);

    Task VoiceTranscription(object payload);

    Task ErrorOccurred(object payload);

    Task ReceiveContentChunk(object payload);
}
