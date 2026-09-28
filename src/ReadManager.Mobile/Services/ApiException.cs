using ReadManager.Mobile.Models.Common;

namespace ReadManager.Mobile.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiError? Error { get; }

    public ApiException(int statusCode, string message, ApiError? error = null)
        : base(message)
    {
        StatusCode = statusCode;
        Error = error;
    }
}
