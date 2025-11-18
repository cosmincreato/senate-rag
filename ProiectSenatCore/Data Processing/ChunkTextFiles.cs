namespace ProiectSenatCore;

public static class ChunkTextFiles
{
    public static void ChunkText()
    {
        Console.WriteLine("Starting text chunking process...");
        int chunkSize = 1000; // ~characters per chunk

        if (!Directory.Exists(Directories.ChunkedTxtDirPath))
            Directory.CreateDirectory(Directories.ChunkedTxtDirPath);

        foreach (var filePath in Directory.GetFiles(Directories.TxtDirPath, "*.txt"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string text = File.ReadAllText(filePath);

            // Cleanup whitespace si newlines
            text = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();

            int startIndex = 0;
            int chunkIndex = 1;
            while (startIndex < text.Length)
            {
                int endIndex = Math.Min(startIndex + chunkSize, text.Length);

                if (endIndex < text.Length)
                {
                    int lastSpace = text.LastIndexOf(' ', endIndex - 1, endIndex - startIndex);
                    if (lastSpace > startIndex)
                        endIndex = lastSpace + 1;
                }

                string chunk = text.Substring(startIndex, endIndex - startIndex);

                string chunkFileName = $"{fileName}_chunk{chunkIndex}.txt";
                string outputPath = Path.Combine(Directories.ChunkedTxtDirPath, chunkFileName);

                File.WriteAllText(outputPath, chunk);

                startIndex = endIndex;
                chunkIndex++;
            }
        }
    }
}