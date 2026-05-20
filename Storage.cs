using System.Text.Encodings.Web;
using System.Text.Json;

namespace EscalaBMC;

public static class Storage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true,
    };

    public static readonly string BaseDir = ResolveBaseDir();
    public static readonly string DataDir = Path.Combine(BaseDir, "data");
    public static readonly string OutputDir = Path.Combine(BaseDir, "output");
    public static readonly string AssetsDir = Path.Combine(BaseDir, "assets");
    public static readonly string EscalasDir = Path.Combine(DataDir, "escalas");

    private static string MilitaresPath => Path.Combine(DataDir, "militares.json");
    private static string AlasPath => Path.Combine(DataDir, "alas.json");
    private static string ConfigPath => Path.Combine(DataDir, "config.json");

    static Storage()
    {
        SeedInstalledFoldersIfNeeded();
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(EscalasDir);
    }

    private static string ResolveBaseDir()
    {
        var app = AppContext.BaseDirectory;
        if (Directory.Exists(Path.Combine(app, "assets")) || File.Exists(Path.Combine(app, "EscalaBMC.exe")))
            return app;

        var current = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(current, "assets")))
            return current;

        return app;
    }

    private static void SeedInstalledFoldersIfNeeded()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            return;

        var previousDir = Path.Combine(documents, "EscalaBMC");
        var previousDataDir = Path.Combine(previousDir, "data");
        var previousOutputDir = Path.Combine(previousDir, "output");

        if (!HasDataFiles(DataDir) && HasDataFiles(previousDataDir))
            CopyDirectory(previousDataDir, DataDir);

        if (!HasFiles(OutputDir) && HasFiles(previousOutputDir))
            CopyDirectory(previousOutputDir, OutputDir);
    }

    private static bool HasDataFiles(string path)
    {
        if (!Directory.Exists(path))
            return false;

        if (File.Exists(Path.Combine(path, "militares.json")) || File.Exists(Path.Combine(path, "alas.json")) || File.Exists(Path.Combine(path, "config.json")))
            return true;

        var escalas = Path.Combine(path, "escalas");
        return Directory.Exists(escalas) && Directory.EnumerateFiles(escalas, "escala_*.json").Any();
    }

    private static bool HasFiles(string path) =>
        Directory.Exists(path) && Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static T LoadJson<T>(string path, T fallback)
    {
        if (!File.Exists(path))
            return fallback;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void SaveJson<T>(string path, T data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static List<Militar> LoadMilitares()
    {
        var militares = LoadJson(MilitaresPath, new List<Militar>());
        foreach (var militar in militares)
            militar.Ausencias ??= [];
        return militares;
    }

    public static void SaveMilitares(IEnumerable<Militar> militares) =>
        SaveJson(MilitaresPath, militares.ToList());

    public static List<AlaConfig> LoadAlas()
    {
        var alas = LoadJson(AlasPath, new List<AlaConfig>());
        if (alas.Count > 0)
            return alas;

        return
        [
            new() { Numero = 1, Nome = "1ª ALA OPERACIONAL" },
            new() { Numero = 2, Nome = "2ª ALA OPERACIONAL" },
            new() { Numero = 3, Nome = "3ª ALA OPERACIONAL" },
            new() { Numero = 4, Nome = "4ª ALA OPERACIONAL" },
        ];
    }

    public static void SaveAlas(IEnumerable<AlaConfig> alas) =>
        SaveJson(AlasPath, alas.ToList());

    private static string EscalaPath(int mes, int ano) =>
        Path.Combine(EscalasDir, $"escala_{ano}_{mes:00}.json");

    public static EscalaMensal? LoadEscala(int mes, int ano)
    {
        var path = EscalaPath(mes, ano);
        if (!File.Exists(path))
            return null;

        var escala = LoadJson<EscalaMensal?>(path, null);
        NormalizeEscala(escala);
        return escala;
    }

    public static void SaveEscala(EscalaMensal escala)
    {
        NormalizeEscala(escala);
        SaveJson(EscalaPath(escala.Mes, escala.Ano), escala);
    }

    public static bool HerdarObservacoesDoMesAnterior(EscalaMensal escala)
    {
        NormalizeEscala(escala);
        if (escala.ObservacoesDefinidas)
            return false;

        var precisaGerais = escala.ObservacoesGerais.Count == 0;
        var precisaAlas = !HasObservacoesAlas(escala.ObservacoesAlas);
        if (!precisaGerais && !precisaAlas)
            return false;

        var fonte = LoadEscalaAnteriorComObservacoes(escala.Mes, escala.Ano);
        if (fonte is null)
            return false;

        var alterou = false;
        if (precisaGerais && fonte.ObservacoesGerais.Count > 0)
        {
            escala.ObservacoesGerais = fonte.ObservacoesGerais.ToList();
            alterou = true;
        }

        if (precisaAlas && HasObservacoesAlas(fonte.ObservacoesAlas))
        {
            escala.ObservacoesAlas = CloneObservacoesAlas(fonte.ObservacoesAlas);
            alterou = true;
        }

        if (alterou)
            escala.ObservacoesDefinidas = true;

        return alterou;
    }

    public static List<(int Mes, int Ano)> ListEscalas()
    {
        var outList = new List<(int Mes, int Ano)>();
        if (!Directory.Exists(EscalasDir))
            return outList;

        foreach (var file in Directory.EnumerateFiles(EscalasDir, "escala_*.json"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var parts = stem.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[1], out var ano) && int.TryParse(parts[2], out var mes))
                outList.Add((mes, ano));
        }

        return outList.OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();
    }

    private static readonly string[] SecoesPadrao = ["ADMINISTRAÇÃO", "GPV", "OPERACIONAL"];
    private static readonly string[] FuncoesPadrao =
    [
        "CH. Serviço", "CMT. GU", "Motorista", "Armador",
        "Aux. ADM", "Vistoriador", "CMT 4ª CIA / 10º BBM",
        "CMT 1º PEL / 4ª CIA", "Aux. ADM / CMT. GU",
    ];

    public static Dictionary<string, List<string>> LoadConfig()
    {
        var cfg = LoadJson(ConfigPath, new Dictionary<string, List<string>>());
        if (!cfg.ContainsKey("secoes") || cfg["secoes"].Count == 0)
            cfg["secoes"] = [..SecoesPadrao];
        if (!cfg.ContainsKey("funcoes") || cfg["funcoes"].Count == 0)
            cfg["funcoes"] = [..FuncoesPadrao];
        if (!cfg.ContainsKey("secoes_2esforco"))
            cfg["secoes_2esforco"] = cfg["secoes"].Where(SecaoPadraoSegundoEsforco).ToList();
        if (!cfg.ContainsKey("ordem_2esforco"))
            cfg["ordem_2esforco"] = [];
        if (!cfg.ContainsKey("base_2esforco"))
            cfg["base_2esforco"] = [];
        return cfg;
    }

    public static void SaveConfig(Dictionary<string, List<string>> cfg) =>
        SaveJson(ConfigPath, cfg);

    private static void NormalizeEscala(EscalaMensal? escala)
    {
        if (escala is null)
            return;

        escala.ObservacoesGerais ??= [];
        escala.Escala2Esforco ??= [];
        escala.ObservacoesAlas ??= [];
        if (!escala.ObservacoesDefinidas && (escala.ObservacoesGerais.Count > 0 || HasObservacoesAlas(escala.ObservacoesAlas)))
            escala.ObservacoesDefinidas = true;
        escala.Overrides ??= [];
        escala.Remanejamentos ??= [];
        escala.CelulasManuais ??= [];
        escala.InsercoesAla ??= [];
        escala.OcultacoesAla ??= [];
    }

    private static EscalaMensal? LoadEscalaAnteriorComObservacoes(int mes, int ano)
    {
        var alvo = new DateTime(ano, mes, 1);
        foreach (var periodo in ListEscalas().OrderByDescending(x => new DateTime(x.Ano, x.Mes, 1)))
        {
            var data = new DateTime(periodo.Ano, periodo.Mes, 1);
            if (data >= alvo)
                continue;

            var escala = LoadEscala(periodo.Mes, periodo.Ano);
            if (escala is not null && (escala.ObservacoesGerais.Count > 0 || HasObservacoesAlas(escala.ObservacoesAlas)))
                return escala;
        }

        return null;
    }

    private static bool HasObservacoesAlas(Dictionary<string, List<string>> observacoes) =>
        observacoes.Any(x => x.Value.Any(linha => !string.IsNullOrWhiteSpace(linha)));

    private static Dictionary<string, List<string>> CloneObservacoesAlas(Dictionary<string, List<string>> observacoes) =>
        observacoes
            .Select(x => new
            {
                Ala = x.Key,
                Linhas = x.Value.Where(linha => !string.IsNullOrWhiteSpace(linha)).ToList(),
            })
            .Where(x => x.Linhas.Count > 0)
            .ToDictionary(x => x.Ala, x => x.Linhas);

    private static bool SecaoPadraoSegundoEsforco(string secao)
    {
        if (string.IsNullOrWhiteSpace(secao))
            return false;

        var normalized = secao.Trim().ToUpperInvariant();
        return normalized.Contains("ADMINISTRA", StringComparison.Ordinal) || normalized == "GPV";
    }
}
