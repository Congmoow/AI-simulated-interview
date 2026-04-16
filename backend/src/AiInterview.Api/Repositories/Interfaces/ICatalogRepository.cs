using AiInterview.Api.Models.Entities;

namespace AiInterview.Api.Repositories.Interfaces;

public interface ICatalogRepository
{
    Task<List<Position>> GetActivePositionsAsync(CancellationToken cancellationToken = default);

    Task<Position?> GetPositionByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<List<QuestionBank>> GetQuestionsAsync(string? positionCode, string? type, string? difficulty, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> CountQuestionsAsync(string? positionCode, string? type, string? difficulty, CancellationToken cancellationToken = default);

    Task<QuestionBank?> GetQuestionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<QuestionBank?> GetRandomQuestionAsync(string positionCode, IEnumerable<string> questionTypes, IEnumerable<Guid> excludedQuestionIds, CancellationToken cancellationToken = default);

    Task<List<QuestionBank>> GetQuestionsByPositionAsync(string positionCode, IEnumerable<string> questionTypes, CancellationToken cancellationToken = default);

    Task<List<LearningResource>> GetLearningResourcesAsync(string? positionCode, IEnumerable<string>? dimensions, int limit, CancellationToken cancellationToken = default);

    Task<int> CountQuestionsByPositionAsync(string positionCode, CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetQuestionTypeCountsAsync(string positionCode, CancellationToken cancellationToken = default);
}
