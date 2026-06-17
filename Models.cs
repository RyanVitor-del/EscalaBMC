using System.Text.Json.Serialization;

namespace EscalaBMC;

public static class ModelConstants
{
    public static readonly string[] OrdemPostos =
    [
        "CEL", "TEN CEL", "MAJ", "CAP", "1º TEN", "2º TEN", "ASP",
        "SUBTEN", "1º SGT", "2º SGT", "3º SGT",
        "CB", "SD", "SD 2ª CL",
    ];

    public static readonly string[] PostosOficiais =
    [
        "CEL", "TEN CEL", "MAJ", "CAP", "1º TEN", "2º TEN", "ASP",
    ];

    public static readonly string[] PostosSubtenSgt =
    [
        "SUBTEN", "1º SGT", "2º SGT", "3º SGT",
    ];

    public static readonly string[] TodosPostos = OrdemPostos;
    public static readonly string[] CategoriasCnh = ["A", "B", "C", "D", "E", "A/B", "A/D", "B/D", "A/B/D", "-"];
    public static readonly string[] Secoes = ["ADMINISTRAÇÃO", "GPV", "OPERACIONAL"];
    public static readonly Dictionary<int, int> AlasFantasma = new() { [1] = 3, [2] = 4, [3] = 1, [4] = 2 };

    public static int AntiguidadePosto(string? posto)
    {
        if (string.IsNullOrWhiteSpace(posto))
            return 999;

        var idx = Array.IndexOf(OrdemPostos, posto);
        return idx >= 0 ? idx : 999;
    }
}

public sealed class Ausencia
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "";

    [JsonPropertyName("data_inicio")]
    public string DataInicio { get; set; } = "";

    [JsonPropertyName("data_fim")]
    public string DataFim { get; set; } = "";

    [JsonPropertyName("observacao")]
    public string Observacao { get; set; } = "";

    [JsonPropertyName("cobertura_automatica")]
    public bool CoberturaAutomatica { get; set; }

    [JsonPropertyName("origem_automatica")]
    public string OrigemAutomatica { get; set; } = "";

    public Ausencia Clone() => new()
    {
        Tipo = Tipo,
        DataInicio = DataInicio,
        DataFim = DataFim,
        Observacao = Observacao,
        CoberturaAutomatica = CoberturaAutomatica,
        OrigemAutomatica = OrigemAutomatica,
    };
}

public sealed class Militar
{
    [JsonPropertyName("numero")]
    public string Numero { get; set; } = "";

    [JsonPropertyName("posto")]
    public string Posto { get; set; } = "";

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("categoria_cnh")]
    public string CategoriaCnh { get; set; } = "-";

    [JsonPropertyName("funcao")]
    public string Funcao { get; set; } = "";

    [JsonPropertyName("secao")]
    public string Secao { get; set; } = "OPERACIONAL";

    [JsonPropertyName("ala")]
    public int Ala { get; set; }

    [JsonPropertyName("ordem")]
    public int Ordem { get; set; }

    [JsonPropertyName("ausencias")]
    public List<Ausencia> Ausencias { get; set; } = [];

    [JsonPropertyName("observacoes")]
    public string Observacoes { get; set; } = "";

    [JsonPropertyName("nome_guerra")]
    public string NomeGuerra { get; set; } = "";

    [JsonPropertyName("horas_extras_min")]
    public int HorasExtrasMin { get; set; }

    [JsonIgnore]
    public string GrupoPosto
    {
        get
        {
            if (ModelConstants.PostosOficiais.Contains(Posto))
                return "OFICIAIS";
            if (ModelConstants.PostosSubtenSgt.Contains(Posto))
                return "SUBTEN/SGT";
            if (Posto == "SD 2ª CL")
                return "SD 2ª CL";
            return "CB/SD";
        }
    }

    [JsonIgnore]
    public bool EhMotoristaD => (CategoriaCnh ?? "").Contains('D', StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public (int Posto, int Ordem) ChaveAntiguidade => (ModelConstants.AntiguidadePosto(Posto), Ordem);

    public string DisplayNome() => $"{Posto} {Nome}".Trim();

    public override string ToString() => DisplayNome();

    public string BancoHorasStr()
    {
        var abs = Math.Abs(HorasExtrasMin);
        var h = abs / 60;
        var m = abs % 60;
        var sinal = HorasExtrasMin >= 0 ? "+" : "-";
        return $"{sinal}{h:00}h{m:00}min";
    }

    public Militar Clone() => new()
    {
        Numero = Numero,
        Posto = Posto,
        Nome = Nome,
        CategoriaCnh = CategoriaCnh,
        Funcao = Funcao,
        Secao = Secao,
        Ala = Ala,
        Ordem = Ordem,
        Ausencias = Ausencias.Select(a => a.Clone()).ToList(),
        Observacoes = Observacoes,
        NomeGuerra = NomeGuerra,
        HorasExtrasMin = HorasExtrasMin,
    };
}

public sealed class AlaConfig
{
    [JsonPropertyName("numero")]
    public int Numero { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("chefe_servico_numero")]
    public string ChefeServicoNumero { get; set; } = "";

    [JsonPropertyName("cmt_gu_numero")]
    public string CmtGuNumero { get; set; } = "";
}

public sealed class RemanejamentoLog
{
    [JsonPropertyName("militar_numero")]
    public string MilitarNumero { get; set; } = "";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("de_ala")]
    public int DeAla { get; set; }

    [JsonPropertyName("para_ala")]
    public int ParaAla { get; set; }

    [JsonPropertyName("motivo")]
    public string Motivo { get; set; } = "";

    [JsonPropertyName("folga_horas")]
    public int FolgaHoras { get; set; } = 72;

    [JsonPropertyName("aprovado_por")]
    public string AprovadoPor { get; set; } = "";
}

public sealed class UnidadeConfig
{
    [JsonPropertyName("nome_completo")]
    public string NomeCompleto { get; set; } = "10º BBM / 4ª CIA / 1º PELOTÃO - FORMIGA";

    [JsonPropertyName("cidade")]
    public string Cidade { get; set; } = "Formiga";

    [JsonPropertyName("bbm")]
    public string Bbm { get; set; } = "10º BBM";

    [JsonPropertyName("cia")]
    public string Cia { get; set; } = "4ª CIA";

    [JsonPropertyName("pelotao")]
    public string Pelotao { get; set; } = "1º PELOTÃO";
}

public sealed class UnidadeCadastro
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("cidade")]
    public string Cidade { get; set; } = "";
}

public sealed class EscalaMensal
{
    [JsonPropertyName("mes")]
    public int Mes { get; set; }

    [JsonPropertyName("ano")]
    public int Ano { get; set; }

    [JsonPropertyName("unidade")]
    public string Unidade { get; set; } = "10º BBM / 4ª CIA / 1º PELOTÃO - FORMIGA";

    [JsonPropertyName("cidade")]
    public string Cidade { get; set; } = "Formiga";

    [JsonPropertyName("data_homologacao")]
    public string DataHomologacao { get; set; } = "";

    [JsonPropertyName("cmt_pel_numero")]
    public string CmtPelNumero { get; set; } = "";

    [JsonPropertyName("cmt_cia_numero")]
    public string CmtCiaNumero { get; set; } = "";

    [JsonPropertyName("observacoes_gerais")]
    public List<string> ObservacoesGerais { get; set; } = [];

    [JsonPropertyName("escala_2esforco")]
    public List<Dictionary<string, string>> Escala2Esforco { get; set; } = [];

    [JsonPropertyName("observacoes_alas")]
    public Dictionary<string, List<string>> ObservacoesAlas { get; set; } = [];

    [JsonPropertyName("observacoes_definidas")]
    public bool ObservacoesDefinidas { get; set; }

    [JsonPropertyName("overrides")]
    public Dictionary<string, Dictionary<string, string>> Overrides { get; set; } = [];

    [JsonPropertyName("remanejamentos")]
    public List<RemanejamentoLog> Remanejamentos { get; set; } = [];

    [JsonPropertyName("celulas_manuais")]
    public List<CelulaManual> CelulasManuais { get; set; } = [];

    [JsonPropertyName("insercoes_ala")]
    public List<InsercaoAla> InsercoesAla { get; set; } = [];

    [JsonPropertyName("ocultacoes_ala")]
    public List<OcultacaoAla> OcultacoesAla { get; set; } = [];

    [JsonPropertyName("composicoes_unidade")]
    public List<ComposicaoUnidade> ComposicoesUnidade { get; set; } = [];
}

public sealed class CelulaManual
{
    [JsonPropertyName("ala")]
    public int Ala { get; set; }

    [JsonPropertyName("militar_numero")]
    public string MilitarNumero { get; set; } = "";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("valor")]
    public string Valor { get; set; } = "";
}

public sealed class InsercaoAla
{
    [JsonPropertyName("ala")]
    public int Ala { get; set; }

    [JsonPropertyName("militar_numero")]
    public string MilitarNumero { get; set; } = "";
}

public sealed class OcultacaoAla
{
    [JsonPropertyName("ala")]
    public int Ala { get; set; }

    [JsonPropertyName("militar_numero")]
    public string MilitarNumero { get; set; } = "";
}

public sealed class ComposicaoUnidade
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("papel_local")]
    public string PapelLocal { get; set; } = "";

    [JsonPropertyName("origem_unidade_id")]
    public string OrigemUnidadeId { get; set; } = "";

    [JsonPropertyName("origem_nome")]
    public string OrigemNome { get; set; } = "";

    [JsonPropertyName("destino_unidade_id")]
    public string DestinoUnidadeId { get; set; } = "";

    [JsonPropertyName("destino_nome")]
    public string DestinoNome { get; set; } = "";

    [JsonPropertyName("ala")]
    public int Ala { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("militar_numero")]
    public string MilitarNumero { get; set; } = "";

    [JsonPropertyName("militar_posto")]
    public string MilitarPosto { get; set; } = "";

    [JsonPropertyName("militar_nome")]
    public string MilitarNome { get; set; } = "";

    [JsonPropertyName("militar_cnh")]
    public string MilitarCnh { get; set; } = "-";

    [JsonPropertyName("militar_funcao")]
    public string MilitarFuncao { get; set; } = "";

    [JsonPropertyName("motivo")]
    public string Motivo { get; set; } = "";
}

public sealed class CelulaEscala
{
    public string Valor { get; set; } = "";
    public string Cor { get; set; } = "normal";

    public CelulaEscala()
    {
    }

    public CelulaEscala(string valor, string cor = "normal")
    {
        Valor = valor;
        Cor = cor;
    }
}

public sealed class AlertaEscala
{
    public string Tipo { get; set; } = "";
    public string Severidade { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public int? Ala { get; set; }
    public DateTime? Data { get; set; }
}

public sealed record SugestaoCobertura(Militar Militar, string Motivo, int FolgaHoras);

public sealed record ResultadoCoberturaAutomatica(int DiasCobertos, int Intervalos, int Pendencias);
