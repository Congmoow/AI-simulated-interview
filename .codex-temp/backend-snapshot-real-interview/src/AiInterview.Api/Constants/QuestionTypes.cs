namespace AiInterview.Api.Constants;

public static class QuestionTypes
{
    public const string Technical = "technical";
    public const string Project = "project";
    public const string Scenario = "scenario";
    public const string Behavioral = "behavioral";

    public static readonly string[] All =
    [
        Technical,
        Project,
        Scenario,
        Behavioral
    ];

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Technical] = "技术知识题",
        [Project] = "项目深挖题",
        [Scenario] = "场景题",
        [Behavioral] = "行为题"
    };
}
