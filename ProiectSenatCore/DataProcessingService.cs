namespace ProiectSenatCore
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using ProiectSenatCore.Embedding;

    public class DataProcessingService
    {
        private List<Dictionary<string, string>> _projects = new List<Dictionary<string, string>>();

        public event Action<string>? ProgressUpdated;
        public event Action<string>? LogMessageReceived;

        private void ReportProgress(string message)
        {
            LogMessageReceived?.Invoke(message);
            Console.WriteLine(message);
        }

        private void UpdateProgress(string status)
        {
            ProgressUpdated?.Invoke(status);
            ReportProgress(status);
        }

        // Luam toate proiectele din 1990-2025, le descarcam PDF-urile si le convertim in text
        public async Task<bool> DataSetupAsync()
        {
            try
            {
                UpdateProgress("== Data Setup ==");
                UpdateProgress("Starting data setup...");

                /*
                foreach (int an in Enumerable.Range(1990, 36))
                {
                    UpdateProgress($"Fetching projects for year {an}...");
                    var result = await ProjectsService.GetProjectsAsync(an.ToString());
                    if (result.Any())
                    {
                        _projects.AddRange(result);
                    }
                    else
                    {
                        ReportProgress($"No projects found for year {an}");
                    }
                }

                UpdateProgress($"Total projects fetched: {_projects.Count}");

                foreach (var project in _projects)
                {
                    var projectUrls = await ProjectsService.GetAllPdfUrlsAsync(project["nr_cls"], project["an_cls"]);
                    foreach (var url in projectUrls)
                    {
                        ReportProgress($"Downloading PDF from URL: {url}");
                        PdfService.DownloadFromUrl(url);
                    }
                }
                

                // Convertim PDF-urile in fisiere text
                UpdateProgress("Converting PDFs to text...");
                PdfService.ConvertToText();
                

                // Impartim fisierele text in bucati mai mici
                UpdateProgress("Chunking text files...");
                ChunkTextFiles.ChunkText();
                */

                // Generam embedding-urile pentru fiecare bucata de text
                UpdateProgress("Generating embeddings...");
                await EmbeddingApiClient.EmbedBatchAsync(Directories.ChunkedTxtDirPath);
                

                // Luam punctele din fisierul JSON si le urcam in baza de date vectoriala Qdrant
                UpdateProgress("Uploading to vector database...");
                var path = Path.Combine(Directories.BaseDirPath, "embeddings.json");
                List<QdrantPoint> points = PointService.LoadPoints(path);
                var uploader = new QdrantUploader("localhost", 6334, "proiect-senat");
                await uploader.UploadPointsAsync(points);
                
                UpdateProgress("Processing complete.");
                return true;
            }
            catch (Exception ex)
            {
                ReportProgress($"Error during data setup: {ex.Message}");
                return false;
            }
        }

        public (bool Success, string Message) TestOcrProcessor()
        {
            try
            {
                ReportProgress("== OCR Processor Testing ==");
                ReportProgress("Testing PdfOcrProcessor...");

                // Test if tessdata is accessible
                if (Directory.Exists(Directories.TessdataDirPath))
                {
                    ReportProgress("Tessdata directory found.");
                    var files = Directory.GetFiles(Directories.TessdataDirPath, "*.traineddata");
                    ReportProgress($"Found {files.Length} trained data files:");
                    foreach (var file in files)
                    {
                        ReportProgress($"  - {Path.GetFileName(file)}");
                    }
                }
                else
                {
                    var message = "Tessdata directory not found.";
                    ReportProgress(message);
                    return (false, message);
                }

                var successMessage = "OCR test completed successfully.";
                ReportProgress(successMessage);
                return (true, successMessage);
            }
            catch (Exception e)
            {
                var errorMessage = $"OCR test failed: {e.Message}";
                ReportProgress(errorMessage);
                ReportProgress($"Stack trace: {e.StackTrace}");
                return (false, errorMessage);
            }
        }

        public int GetProjectCount() => _projects.Count;

        public List<Dictionary<string, string>> GetProjects() => _projects.ToList();
    }
}