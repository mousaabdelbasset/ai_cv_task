using System.Text;
using Task_corectev.core.Interfaces;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Task_corectev.Infrastructure.Services
{
    public class PdfService : IPdfService
    {
        public async Task<string> ExtractTextFromPdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("The file is empty or null.");
            }

            if (file.ContentType != "application/pdf")
            {
                throw new ArgumentException("Only PDF files are supported.");
            }

            var textBuilder = new StringBuilder();

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            stream.Position = 0;

            using (var pdfDocument = PdfDocument.Open(stream))
            {
                foreach (var page in pdfDocument.GetPages())
                {
                    var text = ContentOrderTextExtractor.GetText(page);
                    textBuilder.AppendLine(text);
                }
            }

            return textBuilder.ToString();
        }
    }
}