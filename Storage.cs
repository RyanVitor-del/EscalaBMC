using System.Globalization;
using System.Text;
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
    public const string DefaultUnidadeId = "formiga";

    private static readonly string UnidadesPath = Path.Combine(DataDir, "unidades.json");
    private static readonly string AppConfigPath = Path.Combine(DataDir, "app_config.json");
    private static readonly string UnidadesDir = Path.Combine(DataDir, "unidades");
    private static string _currentUnidadeId = DefaultUnidadeId;

    public static string CurrentUnidadeId => _currentUnidadeId;
    public static string CurrentDataDir => UnidadeDataDir(_currentUnidadeId);
    public static string EscalasDir => Path.Combine(CurrentDataDir, "escalas");

    private static string MilitaresPath => Path.Combine(CurrentDataDir, "militares.json");
    private static string AlasPath => Path.Combine(CurrentDataDir, "alas.json");
    private static string ConfigPath => Path.Combine(CurrentDataDir, "config.json");
    private static string MilitaresPathFor(string unidadeId) => Path.Combine(UnidadeDataDir(unidadeId), "militares.json");
    private static string AlasPathFor(string unidadeId) => Path.Combine(UnidadeDataDir(unidadeId), "alas.json");
    private static string ConfigPathFor(string unidadeId) => Path.Combine(UnidadeDataDir(unidadeId), "config.json");
    private static string EscalasDirFor(string unidadeId) => Path.Combine(UnidadeDataDir(unidadeId), "escalas");

    static Storage()
    {
        SeedInstalledFoldersIfNeeded();
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(UnidadesDir);
        EnsureUnitRegistry();
        _currentUnidadeId = LoadSavedUnidadeId();
        Directory.CreateDirectory(CurrentDataDir);
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

    public static List<UnidadeCadastro> LoadUnidades()
    {
        EnsureUnitRegistry();
        return LoadUnidadesRaw()
            .Where(u => !string.IsNullOrWhiteSpace(u.Id))
            .OrderBy(u => u.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static UnidadeCadastro GetCurrentUnidade()
    {
        var unidade = LoadUnidadesRaw().FirstOrDefault(u => SameId(u.Id, _currentUnidadeId));
        return unidade ?? DefaultUnidade();
    }

    public static UnidadeCadastro? GetUnidade(string unidadeId) =>
        LoadUnidadesRaw().FirstOrDefault(u => SameId(u.Id, unidadeId));

    public static Dictionary<string, string> LoadAppConfig() =>
        LoadJson(AppConfigPath, new Dictionary<string, string>());

    public static void SaveAppConfig(Dictionary<string, string> cfg) =>
        SaveJson(AppConfigPath, cfg);

    public static bool LoadAppFlag(string key)
    {
        var cfg = LoadAppConfig();
        return cfg.TryGetValue(key, out var value)
            && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    public static void SaveAppFlag(string key, bool enabled)
    {
        var cfg = LoadAppConfig();
        cfg[key] = enabled ? "1" : "0";
        SaveAppConfig(cfg);
    }

    public static UnidadeCadastro AddUnidade(string nome, string cidade)
    {
        EnsureUnitRegistry();

        nome = string.IsNullOrWhiteSpace(nome) ? cidade : nome.Trim();
        cidade = string.IsNullOrWhiteSpace(cidade) ? nome : cidade.Trim();

        var unidades = LoadUnidadesRaw();
        var idBase = Slug(cidade);
        if (string.IsNullOrWhiteSpace(idBase))
            idBase = Slug(nome);
        if (string.IsNullOrWhiteSpace(idBase))
            idBase = "unidade";

        var id = idBase;
        var suffix = 2;
        while (unidades.Any(u => SameId(u.Id, id)))
            id = $"{idBase}-{suffix++}";

        var unidade = new UnidadeCadastro { Id = id, Nome = nome, Cidade = cidade };
        unidades.Add(unidade);
        SaveUnidadesRaw(unidades);
        SeedUnitDataFolder(id);
        return unidade;
    }

    public static bool SetCurrentUnidade(string id)
    {
        EnsureUnitRegistry();
        var unidades = LoadUnidadesRaw();
        var unidade = unidades.FirstOrDefault(u => SameId(u.Id, id));
        if (unidade is null)
            return false;

        _currentUnidadeId = unidade.Id;
        var cfg = LoadAppConfig();
        cfg["unidade_atual"] = unidade.Id;
        SaveAppConfig(cfg);
        Directory.CreateDirectory(CurrentDataDir);
        Directory.CreateDirectory(EscalasDir);
        return true;
    }

    public static void UpdateCurrentUnidade(string nome, string cidade)
    {
        if (string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(cidade))
            return;

        EnsureUnitRegistry();
        var unidades = LoadUnidadesRaw();
        var unidade = unidades.FirstOrDefault(u => SameId(u.Id, _currentUnidadeId));
        if (unidade is null)
            return;

        if (!string.IsNullOrWhiteSpace(nome))
            unidade.Nome = nome.Trim();
        if (!string.IsNullOrWhiteSpace(cidade))
            unidade.Cidade = cidade.Trim();
        SaveUnidadesRaw(unidades);
    }

    public static EscalaMensal NewEscala(int mes, int ano)
        => NewEscala(_currentUnidadeId, mes, ano);

    public static EscalaMensal NewEscala(string unidadeId, int mes, int ano)
    {
        var unidade = GetUnidade(unidadeId) ?? DefaultUnidade();
        return new EscalaMensal
        {
            Mes = mes,
            Ano = ano,
            Unidade = string.IsNullOrWhiteSpace(unidade.Nome) ? DefaultUnidade().Nome : unidade.Nome,
            Cidade = string.IsNullOrWhiteSpace(unidade.Cidade) ? DefaultUnidade().Cidade : unidade.Cidade,
        };
    }

    private static void EnsureUnitRegistry()
    {
        var unidades = LoadUnidadesRaw();
        var changed = false;

        if (unidades.All(u => !SameId(u.Id, DefaultUnidadeId)))
        {
            unidades.Insert(0, DefaultUnidade());
            changed = true;
        }

        foreach (var unidade in unidades)
        {
            if (string.IsNullOrWhiteSpace(unidade.Id))
            {
                unidade.Id = Slug(unidade.Cidade);
                if (string.IsNullOrWhiteSpace(unidade.Id))
                    unidade.Id = Slug(unidade.Nome);
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(unidade.Nome))
            {
                unidade.Nome = unidade.Cidade;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(unidade.Cidade))
            {
                unidade.Cidade = unidade.Nome;
                changed = true;
            }
        }

        if (changed || !File.Exists(UnidadesPath))
            SaveUnidadesRaw(unidades);
    }

    private static List<UnidadeCadastro> LoadUnidadesRaw() =>
        LoadJson(UnidadesPath, new List<UnidadeCadastro>());

    private static void SaveUnidadesRaw(IEnumerable<UnidadeCadastro> unidades) =>
        SaveJson(UnidadesPath, unidades
            .Where(u => !string.IsNullOrWhiteSpace(u.Id))
            .GroupBy(u => u.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(u => SameId(u.Id, DefaultUnidadeId) ? 0 : 1)
            .ThenBy(u => u.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ToList());

    private static UnidadeCadastro DefaultUnidade()
    {
        var escala = new EscalaMensal();
        return new UnidadeCadastro
        {
            Id = DefaultUnidadeId,
            Nome = escala.Unidade,
            Cidade = escala.Cidade,
        };
    }

    private static string LoadSavedUnidadeId()
    {
        var cfg = LoadAppConfig();
        if (cfg.TryGetValue("unidade_atual", out var saved)
            && LoadUnidadesRaw().Any(u => SameId(u.Id, saved)))
            return saved;

        cfg["unidade_atual"] = DefaultUnidadeId;
        SaveAppConfig(cfg);
        return DefaultUnidadeId;
    }

    private static string UnidadeDataDir(string id) =>
        SameId(id, DefaultUnidadeId) ? DataDir : Path.Combine(UnidadesDir, id);

    private static void SeedUnitDataFolder(string id)
    {
        var dir = UnidadeDataDir(id);
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "escalas"));

        var militares = Path.Combine(dir, "militares.json");
        if (!File.Exists(militares))
            SaveJson(militares, new List<Militar>());

        var alas = Path.Combine(dir, "alas.json");
        if (!File.Exists(alas))
            SaveJson(alas, DefaultAlas());

        var config = Path.Combine(dir, "config.json");
        if (!File.Exists(config))
            SaveJson(config, DefaultConfig());
    }

    private static bool SameId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var builder = new StringBuilder();
        var previousDash = false;
        foreach (var c in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
                previousDash = false;
            }
            else if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    public static List<Militar> LoadMilitares()
    {
        var militares = LoadJson(MilitaresPath, new List<Militar>());
        foreach (var militar in militares)
            militar.Ausencias ??= [];
        return militares;
    }

    public static List<Militar> LoadMilitares(string unidadeId)
    {
        SeedUnitDataFolder(unidadeId);
        var militares = LoadJson(MilitaresPathFor(unidadeId), new List<Militar>());
        foreach (var militar in militares)
            militar.Ausencias ??= [];
        return militares;
    }

    public static void SaveMilitares(IEnumerable<Militar> militares) =>
        SaveJson(MilitaresPath, militares.ToList());

    public static void SaveMilitares(string unidadeId, IEnumerable<Militar> militares)
    {
        SeedUnitDataFolder(unidadeId);
        SaveJson(MilitaresPathFor(unidadeId), militares.ToList());
    }

    public static List<AlaConfig> LoadAlas()
    {
        var alas = LoadJson(AlasPath, new List<AlaConfig>());
        if (alas.Count > 0)
            return alas;

        return DefaultAlas();
    }

    public static List<AlaConfig> LoadAlas(string unidadeId)
    {
        SeedUnitDataFolder(unidadeId);
        var alas = LoadJson(AlasPathFor(unidadeId), new List<AlaConfig>());
        return alas.Count > 0 ? alas : DefaultAlas();
    }

    public static void SaveAlas(IEnumerable<AlaConfig> alas) =>
        SaveJson(AlasPath, alas.ToList());

    public static void SaveAlas(string unidadeId, IEnumerable<AlaConfig> alas)
    {
        SeedUnitDataFolder(unidadeId);
        SaveJson(AlasPathFor(unidadeId), alas.ToList());
    }

    private static string EscalaPath(int mes, int ano) =>
        Path.Combine(EscalasDir, $"escala_{ano}_{mes:00}.json");

    private static string EscalaPath(string unidadeId, int mes, int ano) =>
        Path.Combine(EscalasDirFor(unidadeId), $"escala_{ano}_{mes:00}.json");

    public static EscalaMensal? LoadEscala(int mes, int ano)
    {
        var path = EscalaPath(mes, ano);
        if (!File.Exists(path))
            return null;

        var escala = LoadJson<EscalaMensal?>(path, null);
        NormalizeEscala(escala);
        return escala;
    }

    public static EscalaMensal? LoadEscala(string unidadeId, int mes, int ano)
    {
        SeedUnitDataFolder(unidadeId);
        var path = EscalaPath(unidadeId, mes, ano);
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

    public static void SaveEscala(string unidadeId, EscalaMensal escala)
    {
        SeedUnitDataFolder(unidadeId);
        NormalizeEscala(escala);
        SaveJson(EscalaPath(unidadeId, escala.Mes, escala.Ano), escala);
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

    private static List<AlaConfig> DefaultAlas() =>
    [
        new() { Numero = 1, Nome = "1\u00AA ALA OPERACIONAL" },
        new() { Numero = 2, Nome = "2\u00AA ALA OPERACIONAL" },
        new() { Numero = 3, Nome = "3\u00AA ALA OPERACIONAL" },
        new() { Numero = 4, Nome = "4\u00AA ALA OPERACIONAL" },
    ];

    private static Dictionary<string, List<string>> DefaultConfig() => new()
    {
        ["secoes"] = [..SecoesPadrao],
        ["funcoes"] = [..FuncoesPadrao],
        ["secoes_2esforco"] = SecoesPadrao.Where(SecaoPadraoSegundoEsforco).ToList(),
        ["ordem_2esforco"] = [],
        ["base_2esforco"] = [],
    };

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
        escala.ComposicoesUnidade ??= [];
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
