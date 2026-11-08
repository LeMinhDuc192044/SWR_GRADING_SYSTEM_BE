namespace Application.Interfaces;

public sealed record StudentCodeRow(int RowNumber, string Code);

public interface IStudentCodeExcelReader
{
    /// <exception cref="InvalidDataException">The file is not a valid .xlsx.</exception>
    IReadOnlyList<StudentCodeRow> Read(Stream stream);
}