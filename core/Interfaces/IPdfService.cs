namespace Task_corectev.core.Interfaces
{
    public interface IPdfService
    {
        Task<string> ExtractTextFromPdfAsync(IFormFile file);
    }
}