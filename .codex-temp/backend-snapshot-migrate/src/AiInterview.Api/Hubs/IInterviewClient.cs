namespace AiInterview.Api.Hubs;

public interface IInterviewClient
{
    Task ReceiveQuestion(object payload);

    Task ReceiveFollowUp(object payload);

    Task TypingIndicator(object payload);

    Task InterviewStatusChanged(object payload);

    Task ReportProgress(object payload);

    Task ReportReady(object payload);

    Task VoiceTranscription(object payload);

    Task ErrorOccurred(object payload);
}
