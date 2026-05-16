namespace SheepAI.API.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public PaginationMeta? Meta { get; init; }
}

public static class Api
{
    public static ApiResponse<T> Data<T>(string message, T data) =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Paged<T>(string message, T data, int page, int perPage, int total) =>
        new()
        {
            Success = true,
            Message = message,
            Data    = data,
            Meta    = new PaginationMeta
            {
                Page       = page,
                PerPage    = perPage,
                Total      = total,
                TotalPages = (int)Math.Ceiling((double)total / perPage)
            }
        };
}

public sealed class PaginationMeta
{
    public int Page { get; init; }
    public int PerPage { get; init; }
    public int Total { get; init; }
    public int TotalPages { get; init; }
}
