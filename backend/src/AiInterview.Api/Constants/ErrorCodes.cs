namespace AiInterview.Api.Constants;

public static class ErrorCodes
{
    public const int InvalidToken = 10001;
    public const int ExpiredToken = 10002;
    public const int InvalidRefreshToken = 10003;
    public const int InvalidCredentials = 10004;
    public const int UserDisabled = 10005;
    public const int UserNotFound = 20001;
    public const int Forbidden = 20002;
    public const int PositionNotFound = 30001;
    public const int QuestionNotFound = 40001;
    public const int QuestionValidationFailed = 40002;
    public const int InterviewNotFound = 50001;
    public const int InterviewNotFinished = 50002;
    public const int AnswerEmpty = 50003;
    public const int ReportNotGenerated = 60001;
    public const int DocumentNotFound = 70001;
    public const int DocumentProcessingFailed = 70002;
    public const int InternalServerError = 90001;
    public const int ServiceUnavailable = 90002;
}
