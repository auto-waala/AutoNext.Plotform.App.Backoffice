namespace AutoNext.Plotform.App.Backoffice.Models.Common
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Success")
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Message = message,
                StatusCode = 200,
                Data = data
            };
        }

        public static ApiResponse<T> Created(T data, string message = "Created successfully")
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                Message = message,
                StatusCode = 201,
                Data = data
            };
        }

        public static ApiResponse<T> Error(string message, int statusCode = 400, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode,
                Errors = errors
            };
        }

        public static ApiResponse<T> NotFound(string message = "Resource not found")
        {
            return new ApiResponse<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = 404
            };
        }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public long TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
