namespace AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}