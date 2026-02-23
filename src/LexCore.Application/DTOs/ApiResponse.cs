namespace LexCore.Application.DTOs;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? Code { get; set; }
    public int? StatusCode { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operation successful")
    {
        return new ApiResponse<T> { Success = true, Data = data, Message = message };
    }

    public static ApiResponse<T> ErrorResponse(string message, string code, int statusCode)
    {
        return new ApiResponse<T> { Success = false, Message = message, Code = code, StatusCode = statusCode };
    }
}

public class PagedResponse<T>
{
    public bool Success { get; set; } = true;
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public string Message { get; set; } = "Operation successful";
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
