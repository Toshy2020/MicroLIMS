using System.Text.Json.Serialization;

namespace MicroLIMS.Shared.Responses;

// Uniform envelope for every API response.
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FieldError>? FieldErrors { get; set; }

    // Present only for a refusal that carries a message code; an uncoded
    // response has no such key. Errors above stays as it always was.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ApiError>? ErrorDetails { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static ApiResponse<T> Fail(string message, List<string>? errors, List<FieldError>? fieldErrors) =>
        new() { Success = false, Message = message, Errors = errors, FieldErrors = fieldErrors };

    public static ApiResponse<T> Fail(string message, List<string>? errors, List<FieldError>? fieldErrors, List<ApiError>? errorDetails) =>
        new() { Success = false, Message = message, Errors = errors, FieldErrors = fieldErrors, ErrorDetails = errorDetails };
}
