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
        if (args.Length > 0 && args[0] == "--recalc-all")
        {
            RecalcularTudoCommandLine(args);
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
        var escala = Storage.LoadEscala(mes, ano) ?? Storage.NewEscala(mes, ano);
        var militares = Storage.LoadMilitares();
        var alas = Storage.LoadAlas();
        var destino = args.Length > 3
            ? args[3]
            : Path.Combine(Storage.OutputDir, $"ESCALA - {escala.Cidade.ToUpperInvariant()} - {CultureTitle(EscalaLogic.MesesPt[mes])} {ano}.pdf");
        PdfExport.GerarPdf(escala, militares, alas, destino);
        Console.WriteLine($"PDF gerado: {destino}");
    }

    private static void RecalcularCoberturasCommandLine(string[] args)
    {
        var mes = args.Length > 1 && int.TryParse(args[1], out var parsedMes) ? parsedMes : DateTime.Today.Month;
        var ano = args.Length > 2 && int.TryParse(args[2], out var parsedAno) ? parsedAno : DateTime.Today.Year;
        var militares = Storage.LoadMilitares();
        var escala = Storage.LoadEscala(mes, ano) ?? Storage.NewEscala(mes, ano);
        var resultado = EscalaLogic.AplicarCoberturasAutomaticas(militares, escala, mes, ano);
        Storage.SaveMilitares(militares);
        Storage.SaveEscala(escala);
        Console.WriteLine($"Coberturas: {resultado.DiasCobertos}; intervalos: {resultado.Intervalos}; pendencias: {resultado.Pendencias}");
    }

    // Recalcula coberturas de todas as unidades + composição entre unidades (sem interface),
    // aplicando todas as sugestões. Útil para automação e testes.
    private static void RecalcularTudoCommandLine(string[] args)
    {
        var mes = args.Length > 1 && int.TryParse(args[1], out var parsedMes) ? parsedMes : DateTime.Today.Month;
        var ano = args.Length > 2 && int.TryParse(args[2], out var parsedAno) ? parsedAno : DateTime.Today.Year;
        var (coberturas, composicoes, purgadas) = MainForm.RecalcularTudoSemInteracao(mes, ano);
        Console.WriteLine($"Todas as unidades recalculadas para {mes:00}/{ano}: coberturas={coberturas}; composicoes={composicoes}; composicoes_antigas_removidas={purgadas}");
    }

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return char.ToUpper(value[0]) + value[1..].ToLowerInvariant();
    }
}
