using AiInterview.Api.DTOs.Knowledge;
using AiInterview.Api.Models.Entities;
using AiInterview.Api.Repositories.Interfaces;
using AiInterview.Api.Services;
using FluentAssertions;

namespace AiInterview.Api.Tests.Services;

file sealed class StubKnowledgeRepository(List<KnowledgeChunk> data) : IKnowledgeRepository
{
    public Task<List<KnowledgeChunk>> SearchByKeywordAsync(
        string query, Guid? documentId, string? positionCode, int topK, CancellationToken cancellationToken = default)
    {
        var result = data.AsEnumerable();
        if (documentId.HasValue)
            result = result.Where(x => x.DocumentId == documentId.Value);
        if (!string.IsNullOrWhiteSpace(positionCode))
            result = result.Where(x => x.Document?.PositionCode == positionCode);
        if (!string.IsNullOrWhiteSpace(query))
            result = result.Where(x => x.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result.Take(topK).ToList());
    }
}

public class KnowledgeServiceTests
{
    private static List<KnowledgeChunk> BuildChunks(Guid docId, string positionCode, int count = 5)
    {
        var doc = new KnowledgeDocument { Id = docId, Title = "测试文档", PositionCode = positionCode };
        return Enumerable.Range(0, count).Select(i => new KnowledgeChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = docId,
            ChunkIndex = i,
            Content = $"知识片段内容 {i + 1}，包含关键词：Java 并发",
            ContentHash = $"hash{i}",
            Document = doc
        }).ToList();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnMatchingChunks()
    {
        var docId = Guid.NewGuid();
        var chunks = BuildChunks(docId, "java-backend");
        var service = new KnowledgeService(new StubKnowledgeRepository(chunks));

        var result = await service.SearchAsync(new KnowledgeSearchRequest { Query = "Java", TopK = 10 });

        result.Items.Should().HaveCount(5);
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task SearchAsync_TopK_ShouldLimitResults()
    {
        var docId = Guid.NewGuid();
        var chunks = BuildChunks(docId, "java-backend", count: 10);
        var service = new KnowledgeService(new StubKnowledgeRepository(chunks));

        var result = await service.SearchAsync(new KnowledgeSearchRequest { Query = "Java", TopK = 3 });

        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_ScoreShouldDecreaseByRank()
    {
        var docId = Guid.NewGuid();
        var chunks = BuildChunks(docId, "java-backend", count: 3);
        var service = new KnowledgeService(new StubKnowledgeRepository(chunks));

        var result = await service.SearchAsync(new KnowledgeSearchRequest { Query = "Java", TopK = 3 });

        result.Items[0].Score.Should().BeGreaterThan(result.Items[1].Score);
        result.Items[1].Score.Should().BeGreaterThan(result.Items[2].Score);
    }

    [Fact]
    public async Task SearchAsync_ShouldMapDocumentTitleCorrectly()
    {
        var docId = Guid.NewGuid();
        var chunks = BuildChunks(docId, "java-backend", count: 1);
        var service = new KnowledgeService(new StubKnowledgeRepository(chunks));

        var result = await service.SearchAsync(new KnowledgeSearchRequest { Query = "Java", TopK = 5 });

        result.Items[0].DocumentTitle.Should().Be("测试文档");
        result.Items[0].DocumentId.Should().Be(docId);
    }

    [Fact]
    public async Task SearchAsync_TopK_ShouldBeClampedTo20()
    {
        var docId = Guid.NewGuid();
        var chunks = BuildChunks(docId, "java-backend", count: 5);
        var service = new KnowledgeService(new StubKnowledgeRepository(chunks));

        var result = await service.SearchAsync(new KnowledgeSearchRequest { Query = "Java", TopK = 999 });

        result.Items.Should().HaveCountLessOrEqualTo(20);
    }
}
