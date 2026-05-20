namespace EscalaBMC;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--export-pdf")
        {
            ExportPdfFromCommandLine(args);
            return;
        }
        if (args.Length > 0 && args[0] == "--recalc-coverages")
        {
            RecalcularCoberturasCommandLine(args);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void ExportPdfFromCommandLine(string[] args)
    {
        var mes = args.Length > 1 && int.TryParse(args[1], out var parsedMes) ? parsedMes : DateTime.Today.Month;
        var ano = args.Length > 2 && int.TryParse(args[2], out var parsedAno) ? parsedAno : DateTime.Today.Year;
        var escala = Storage.LoadEscala(mes, ano) ?? new EscalaMensal { Mes = mes, Ano = ano };
        var militares = Storage.LoadMilitares();
        var alas = Storage.LoadAlas();
        var destino = args.Length > 3
            ? args[3]
            : Path.Combine(Storage.OutputDir, $"ESCALA - {escala.Cidade.ToUpperInvariant()} - {CultureTitle(EscalaLogic.MesesPt[mes])} {ano}.pdf");
        if (!PythonPdfExporter.TryGenerate(escala, destino, out _))
            PdfExport.GerarPdf(escala, militares, alas, destino);
    }

    private static void RecalcularCoberturasCommandLine(string[] args)
    {
        var mes = args.Length > 1 && int.TryParse(args[1], out var parsedMes) ? parsedMes : DateTime.Today.Month;
        var ano = args.Length > 2 && int.TryParse(args[2], out var parsedAno) ? parsedAno : DateTime.Today.Year;
        var militares = Storage.LoadMilitares();
        var escala = Storage.LoadEscala(mes, ano) ?? new EscalaMensal { Mes = mes, Ano = ano };
        var resultado = EscalaLogic.AplicarCoberturasAutomaticas(militares, escala, mes, ano);
        Storage.SaveMilitares(militares);
        Storage.SaveEscala(escala);
        Console.WriteLine($"Coberturas: {resultado.DiasCobertos}; intervalos: {resultado.Intervalos}; pendencias: {resultado.Pendencias}");
    }

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return char.ToUpper(value[0]) + value[1..].ToLowerInvariant();
    }
}
