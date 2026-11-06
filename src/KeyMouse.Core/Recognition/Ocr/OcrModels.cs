using KeyMouse.Product;

namespace KeyMouse.Recognition.Ocr;

/// <summary>
/// Where the OCR models live, and what they are called.
///
/// The models are deliberately not in the repository: they are 15 MB of binaries that change only
/// when we change OCR, and a history of model blobs is a history nobody can read. They are fetched
/// once by `tools/build/fetch-ocr-models.ps1` into `artifacts/models/ocr`, and the same file names
/// are looked for next to an installed build.
///
/// Order is "most specific first": an explicit override, then the folder next to the executable
/// (that is what the installer ships), then the development tree, then the user's local folder.
/// The first folder holding **both** files wins - mixing a det from one place with a rec from
/// another is never intended.
/// </summary>
internal static class OcrModels
{
    internal const string DetFile = "det.onnx";
    internal const string RecFile = "rec.onnx";

    /// <summary>排障与实验用的覆盖开关：指哪儿用哪儿。</summary>
    internal const string OverrideVariable = "KEYMOUSE_OCR_MODELS";

    internal sealed record Location(string Directory, string DetPath, string RecPath);

    /// <summary>Folders to search, in order. Missing folders are skipped, not errors.</summary>
    internal static IEnumerable<string> SearchPath()
    {
        string? overridden = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridden)) yield return overridden;

        yield return Path.Combine(AppContext.BaseDirectory, "models", "ocr");

        if (ProductLayout.RepositoryRoot is { } repository &&
            ProductLayout.Environment == ProductEnvironment.Development)
        {
            yield return Path.Combine(repository, "artifacts", "models", "ocr");
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyMouse", "ocr");
    }

    /// <summary>Both files present, or null. Never throws: a missing model is an ordinary state.</summary>
    internal static Location? Locate()
    {
        foreach (string directory in SearchPath())
        {
            string det = Path.Combine(directory, DetFile);
            string rec = Path.Combine(directory, RecFile);
            if (File.Exists(det) && File.Exists(rec)) return new Location(directory, det, rec);
        }
        return null;
    }

    /// <summary>Identity for `probe --report`: which files are being used and how big they are.</summary>
    internal static IEnumerable<(string Name, long Bytes)> Describe(Location location)
    {
        foreach (string path in new[] { location.DetPath, location.RecPath })
        {
            var info = new FileInfo(path);
            yield return (Path.GetFileName(path), info.Exists ? info.Length : 0);
        }
    }
}
