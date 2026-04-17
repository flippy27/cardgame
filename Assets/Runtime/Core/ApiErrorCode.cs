namespace Flippy.CardDuelMobile.Core
{
    public enum ApiErrorCode
    {
        // Authentication (AUTH_xxx)
        AUTH_INVALID_CREDENTIALS = 1001,
        AUTH_TOKEN_EXPIRED = 1002,
        AUTH_UNAUTHORIZED = 1003,
        AUTH_FORBIDDEN = 1004,

        // Network (NET_xxx)
        NET_TIMEOUT = 2001,
        NET_UNREACHABLE = 2002,
        NET_CIRCUIT_BREAKER_OPEN = 2003,
        NET_CONNECTION_LOST = 2004,

        // Server (SRV_xxx)
        SRV_NOT_FOUND = 3001,
        SRV_CONFLICT = 3002,
        SRV_INTERNAL_ERROR = 3003,
        SRV_UNAVAILABLE = 3004,

        // Validation (VAL_xxx)
        VAL_INVALID_INPUT = 4001,
        VAL_MISSING_FIELD = 4002,
        VAL_OUT_OF_RANGE = 4003,

        // Parse (PARSE_xxx)
        PARSE_JSON_ERROR = 5001,
        PARSE_INVALID_FORMAT = 5002,

        // Unknown
        UNKNOWN = 9999
    }

    public class ApiException : System.Exception
    {
        public ApiErrorCode Code { get; }
        public int HttpStatus { get; }

        public ApiException(ApiErrorCode code, string message, int httpStatus = 0)
            : base($"[{code}] {message}")
        {
            Code = code;
            HttpStatus = httpStatus;
        }
    }

    public static class ErrorCodeMapper
    {
        public static ApiErrorCode FromHttpStatus(int status) => status switch
        {
            401 => ApiErrorCode.AUTH_UNAUTHORIZED,
            403 => ApiErrorCode.AUTH_FORBIDDEN,
            404 => ApiErrorCode.SRV_NOT_FOUND,
            409 => ApiErrorCode.SRV_CONFLICT,
            500 => ApiErrorCode.SRV_INTERNAL_ERROR,
            503 => ApiErrorCode.SRV_UNAVAILABLE,
            _ => ApiErrorCode.UNKNOWN
        };
    }
}
