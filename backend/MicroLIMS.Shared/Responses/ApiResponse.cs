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

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };

    public static ApiResponse<T> Fail(string message, List<string>? errors, List<FieldError>? fieldErrors) =>
        new() { Success = false, Message = message, Errors = errors, FieldErrors = fieldErrors };
}
