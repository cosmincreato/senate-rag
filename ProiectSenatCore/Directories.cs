namespace ProiectSenatCore;

public static class Directories
{
    public static readonly string BuildDirPath =
        AppDomain.CurrentDomain.BaseDirectory;

    public static readonly string BaseDirPath =
        Directory.GetParent(BuildDirPath)?.Parent?.Parent?.Parent?.FullName ?? BuildDirPath;

    public static readonly string PdfDirPath =
        Path.Combine(BaseDirPath, "input");

    public static readonly string TxtDirPath =
        Path.Combine(BaseDirPath, "output");
    
    public static readonly string TessdataDirPath =
        Path.Combine(BaseDirPath, "tessdata");
    
    public static readonly string ChunkedTxtDirPath =
        Path.Combine(BaseDirPath, "chunked_output");
    
    public static readonly string ModelsDirPath =
        Path.Combine(BaseDirPath, "models");
}
