namespace AiInterview.Api.Services.Interfaces;

public interface ISeedDataService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
