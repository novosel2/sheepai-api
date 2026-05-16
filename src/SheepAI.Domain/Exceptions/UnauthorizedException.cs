namespace SheepAI.Domain.Exceptions;

public sealed class UnauthorizedException(string message) : AppException(message, 401);
