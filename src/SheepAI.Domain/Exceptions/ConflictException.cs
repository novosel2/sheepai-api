namespace SheepAI.Domain.Exceptions;

public sealed class ConflictException(string message) : AppException(message, 409);
