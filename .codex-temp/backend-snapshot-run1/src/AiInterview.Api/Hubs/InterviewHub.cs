using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AiInterview.Api.Hubs;

[Authorize]
public class InterviewHub : Hub<IInterviewClient>
{
    public async Task JoinInterview(JoinInterviewRequest request)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, BuildRoomName(request.InterviewId));
    }

    public async Task LeaveInterview(JoinInterviewRequest request)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildRoomName(request.InterviewId));
    }

    public Task SendHeartbeat()
    {
        return Task.CompletedTask;
    }

    public static string BuildRoomName(Guid interviewId) => $"interview:{interviewId}";
}

public class JoinInterviewRequest
{
    public Guid InterviewId { get; set; }
}
