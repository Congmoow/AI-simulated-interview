namespace AiInterview.Api.Constants;

public static class QuestionDifficulties
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Easy] = "初级",
        [Medium] = "中级",
        [Hard] = "高级"
    };
}
