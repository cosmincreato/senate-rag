using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;
using System.Text;
using Tesseract;

namespace ProiectSenatCore;

public class PdfOcrProcessor
{
    private readonly string _tessdataPath;

    public PdfOcrProcessor(string tessdataPath = null)
    {
        _tessdataPath = tessdataPath ?? Directories.TessdataDirPath;
        if (!Directory.Exists(_tessdataPath))
        {
            throw new DirectoryNotFoundException($"Tessdata directory not found at {_tessdataPath}");
        }
    }

    public string ExtractTextFromPdf(string pdfPath)
    {
        var extractedText = new StringBuilder();

        try
        {
            using (var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
            {
                int pageCount = docReader.GetPageCount();
                for (int i = 0; i < pageCount; i++)
                {
                    using (var pageReader = docReader.GetPageReader(i))
                    {
                        var rawBytes = pageReader.GetImage();
                        int width = pageReader.GetPageWidth();
                        int height = pageReader.GetPageHeight();

                        using (var image = Image.LoadPixelData<Rgba32>(rawBytes, width, height))
                        {
                            string pageText = ExtractTextFromImage(image);
                            extractedText.AppendLine(pageText);
                            extractedText.AppendLine();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing PDF: {ex.Message}");
        }

        return extractedText.ToString();
    }

    private string ExtractTextFromImage(Image<Rgba32> image)
    {
        try
        {
            using (var engine = new TesseractEngine(_tessdataPath, "ron", EngineMode.Default))
            {
                engine.SetVariable("tessedit_char_whitelist",
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                    "ĂÂÎȘȚăâîșț0123456789.,;:!?()-/\\ ");

                byte[] imageBytes = GetImageBytes(image);
                using (var pix = Pix.LoadFromMemory(imageBytes))
                {
                    using (var page = engine.Process(pix))
                    {
                        return page.GetText();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OCR Error: {ex.Message}");
            return string.Empty;
        }
    }

    private byte[] GetImageBytes(Image<Rgba32> image)
    {
        using (var ms = new MemoryStream())
        {
            image.Save(ms, new PngEncoder());
            return ms.ToArray();
        }
    }
}