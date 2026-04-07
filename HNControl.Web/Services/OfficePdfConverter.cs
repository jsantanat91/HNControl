using System.Diagnostics;

namespace HNControl.Web.Services;

public class OfficePdfConverter : IOfficePdfConverter
{
    private readonly ILogger<OfficePdfConverter> _logger;
    private readonly IConfiguration _cfg;

    public OfficePdfConverter(ILogger<OfficePdfConverter> logger, IConfiguration cfg)
    {
        _logger = logger;
        _cfg = cfg;
    }

    public async Task<byte[]?> TryConvertDocxToPdfAsync(byte[] docxBytes, string baseFileName, CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "hncontrol_docx_pdf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var safeBase = string.Join("_", (baseFileName ?? "documento").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(safeBase))
                safeBase = "documento";

            var docxPath = Path.Combine(tempRoot, safeBase + ".docx");
            var pdfPath = Path.Combine(tempRoot, safeBase + ".pdf");
            await File.WriteAllBytesAsync(docxPath, docxBytes, cancellationToken);

            var command = ResolveOfficeCommand();
            if (string.IsNullOrWhiteSpace(command))
                return null;

            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = $"--headless --convert-to pdf --outdir \"{tempRoot}\" \"{docxPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            if (!proc.Start())
                return null;

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                _logger.LogWarning("No se pudo convertir DOCX a PDF con {Command}. ExitCode={Code}. Out={Out}. Err={Err}", command, proc.ExitCode, stdout, stderr);
                return null;
            }

            if (!File.Exists(pdfPath))
            {
                var anyPdf = Directory.GetFiles(tempRoot, "*.pdf", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(anyPdf))
                    pdfPath = anyPdf;
            }

            if (!File.Exists(pdfPath))
            {
                _logger.LogWarning("La conversion DOCX->PDF termino sin archivo de salida en {Path}", tempRoot);
                return null;
            }

            return await File.ReadAllBytesAsync(pdfPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al convertir DOCX a PDF");
            return null;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
            catch
            {
            }
        }
    }

    private string? ResolveOfficeCommand()
    {
        var configured = (_cfg["Office:Command"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            candidates.Add("soffice.com");
            candidates.Add("soffice.exe");
            candidates.Add(@"C:\Program Files\LibreOffice\program\soffice.com");
            candidates.Add(@"C:\Program Files\LibreOffice\program\soffice.exe");
            candidates.Add(@"C:\Program Files (x86)\LibreOffice\program\soffice.com");
            candidates.Add(@"C:\Program Files (x86)\LibreOffice\program\soffice.exe");
        }
        else
        {
            candidates.Add("soffice");
            candidates.Add("/usr/bin/soffice");
            candidates.Add("/snap/bin/libreoffice");
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (!Path.IsPathRooted(candidate))
                return candidate;

            if (File.Exists(candidate))
                return candidate;
        }

        _logger.LogWarning("No se encontró comando de LibreOffice/soffice. Configura Office:Command o agrega soffice al PATH.");
        return null;
    }
}
