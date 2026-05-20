using System.Diagnostics;
using System.Text;

namespace EscalaBMC;

public static class PythonPdfExporter
{
    public static bool TryGenerate(EscalaMensal escala, string outputPath, out string error)
    {
        error = "";

        if (TryGenerateWithBundledExporter(escala, outputPath, out error))
            return true;

        var bundledError = error;
        if (TryGenerateWithSystemPython(escala, outputPath, out error))
            return true;

        error = string.IsNullOrWhiteSpace(error) ? bundledError : $"{bundledError} {error}".Trim();
        return false;
    }

    private static bool TryGenerateWithBundledExporter(EscalaMensal escala, string outputPath, out string error)
    {
        var exe = ResolveBundledExporter();
        if (exe is null)
        {
            error = "Exportador Python empacotado não encontrado.";
            return false;
        }

        return RunExporter(exe, Path.GetDirectoryName(exe)!, escala, outputPath, out error);
    }

    private static bool TryGenerateWithSystemPython(EscalaMensal escala, string outputPath, out string error)
    {
        var sourceDir = ResolvePythonSourceDir();
        if (sourceDir is null)
        {
            error = "Fontes Python do exportador não encontradas.";
            return false;
        }

        var script = Path.Combine(sourceDir, "pdf_export_cli.py");
        return RunExporter("python", sourceDir, escala, outputPath, out error, script);
    }

    private static bool RunExporter(string fileName, string workingDirectory, EscalaMensal escala, string outputPath, out string error, string? script = null)
    {
        error = "";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
            };

            if (script is not null)
                psi.ArgumentList.Add(script);
            psi.ArgumentList.Add(Storage.DataDir);
            psi.ArgumentList.Add(escala.Mes.ToString());
            psi.ArgumentList.Add(escala.Ano.ToString());
            psi.ArgumentList.Add(outputPath);

            using var process = Process.Start(psi);
            if (process is null)
            {
                error = "Não foi possível iniciar o exportador Python.";
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0 && File.Exists(outputPath))
                return true;

            error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ResolveBundledExporter()
    {
        var candidates = BaseDirs()
            .Select(dir => Path.Combine(dir, "pdf_exporter", "EscalaPdfExporter.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolvePythonSourceDir()
    {
        var candidates = BaseDirs()
            .Select(dir => Path.Combine(dir, "python_pdf"));
        return candidates.FirstOrDefault(dir =>
            Directory.Exists(dir) &&
            File.Exists(Path.Combine(dir, "pdf_export_cli.py")) &&
            File.Exists(Path.Combine(dir, "pdf_export.py")) &&
            File.Exists(Path.Combine(dir, "models.py")) &&
            File.Exists(Path.Combine(dir, "escala_logic.py")));
    }

    private static IEnumerable<string> BaseDirs()
    {
        yield return AppContext.BaseDirectory;
        yield return Storage.BaseDir;
        yield return Directory.GetCurrentDirectory();
    }
}
