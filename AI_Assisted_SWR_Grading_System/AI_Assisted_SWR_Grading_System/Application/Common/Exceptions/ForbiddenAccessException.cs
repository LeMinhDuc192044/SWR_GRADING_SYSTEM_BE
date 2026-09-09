namespace AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;

public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() { }
    public ForbiddenAccessException(string message) : base(message) { }
}