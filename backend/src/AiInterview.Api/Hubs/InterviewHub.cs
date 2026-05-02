using AiInterview.Api.Extensions;
using AiInterview.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AiInterview.Api.Hubs;

[Authorize]
public class InterviewHub(IInterviewRepository interviewRepository) : Hub<IInterviewClient>
{
    public async Task JoinInterview(JoinInterviewRequest request)
    {
        var userId = Context.User.GetUserId();
        var interview = await interviewRepository.GetByIdAsync(request.InterviewId);

        if (interview is null || interview.UserId != userId)
        {
            throw new HubException("无权访问该面试");
        }

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
