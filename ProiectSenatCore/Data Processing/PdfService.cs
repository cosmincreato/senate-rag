namespace ProiectSenatCore;

using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

static class PdfService
{
    static PdfService()
    {
        if (!Directory.Exists(Directories.PdfDirPath))
            Directory.CreateDirectory(Directories.PdfDirPath);


        if (!Directory.Exists(Directories.TxtDirPath))
            Directory.CreateDirectory(Directories.TxtDirPath);
    }

    public static void DownloadFromUrl(string url)
    {
        string fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        string filePath = Path.Combine(Directories.PdfDirPath, fileName);

        if (File.Exists(filePath))
        {
            Console.WriteLine($"Skipping download for {fileName}, already exists in input.");
            return;
        }

        using (HttpClient client = new HttpClient())
        {
            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var fileBytes = response.Content.ReadAsByteArrayAsync().Result;
            File.WriteAllBytes(filePath, fileBytes);
        }
    }

    public static void ConvertToText()
    {
        Console.WriteLine("Starting PDF to text conversion...");
        var pdfs = Directory.EnumerateFiles(Directories.PdfDirPath, "*.pdf");
        Console.WriteLine($"{pdfs.Count().ToString()} PDF files found.");

        // OCR processor ca si fallback pentru PdfPig
        var ocrProcessor = new PdfOcrProcessor();

        foreach (var pdf in pdfs)
        {
            string fileName = Path.GetFileNameWithoutExtension(pdf) + ".txt";
            string outputPath = Path.Combine(Directories.TxtDirPath, fileName);

            if (File.Exists(outputPath))
            {
                Console.WriteLine($"Skipping {pdf} because {outputPath} already exists.");
                continue;
            }

            var sb = new StringBuilder();
            bool hasTextContent = false;

            // Extragere text cu PdfPig
            try
            {
                using (PdfDocument document = PdfDocument.Open(pdf))
                {
                    foreach (Page page in document.GetPages())
                    {
                        IEnumerable<Word> words = page.GetWords();
                        foreach (var word in words)
                        {
                            sb.Append(word.Text);
                            sb.Append(' ');
                            hasTextContent = true;
                        }

                        sb.AppendLine();
                    }
                }

                // Daca nu s-a gasit text sau e prea putin, folosim OCR
                if (!hasTextContent || sb.Length < 50) // Minimal threshold
                {
                    Console.WriteLine($"No text content found in {pdf}, using OCR...");
                    sb.Clear();
                    string ocrText = ocrProcessor.ExtractTextFromPdf(pdf);
                    sb.Append(ocrText);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error with PdfPig extraction for {pdf}: {e.Message}");
                Console.WriteLine("Falling back to OCR...");

                // Fallback la OCR daca PdfPig esueaza
                try
                {
                    sb.Clear();
                    string ocrText = ocrProcessor.ExtractTextFromPdf(pdf);
                    sb.Append(ocrText);
                }
                catch (Exception ocrEx)
                {
                    Console.WriteLine($"OCR also failed for {pdf}: {ocrEx.Message}");
                    sb.AppendLine($"Error processing PDF: {pdf}");
                }
            }
            
            File.WriteAllText(outputPath, sb.ToString());
            Console.WriteLine($"{outputPath}");
        }
    }
}