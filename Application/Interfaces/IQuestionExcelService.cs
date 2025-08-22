using Employee_Survey.Domain;

namespace Employee_Survey.Application;
public interface IQuestionExcelService
{
    Task<byte[]> ExportAsync(IEnumerable<Question> data);
    Task<ImportResult> ImportAsync(Stream fileStream, string actor);
}
public class ImportResult
{
    public int Total { get; set; }
    public int Success { get; set; }
    public List<string> Errors { get; set; } = new();
}
