using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EscalaBMC;

public static class PdfExport
{
    private const float Mm = 72f / 25.4f;

    private const string HeaderTab = "#D9D9D9";
    private const string HeaderDark = "#BFBFBF";
    private const string CorFa = "#ED7D31";
    private const string CorLd = "#C00000";
    private const string CorFolgaFd = "#00B0F0";
    private const string CorFolgaFn = "#FFFF00";
    private const string CorO = "#FFC000";
    private const string CorMo = "#BF8F00";
    private const string CorT = "#7B7B7B";
    private const string CorLn = "#F2F2F2";
    private const string CorRemanejAla = "#5B9BD5";
    private const string CorAlaOrigem = "#E7E6E6";
    private const string CorDatas = "#C00000";
    private const string CorObsVermelho = "#C00000";
    private const string CorFuncaoAzul = "#1F4E79";
    private const string CorFuncaoVermelho = "#C00000";
    private const string CorTextoS = "#1F4E79";
    private const string CorResumoHeader = "#F2F2F2";
    private const string CorResumoVerdeClaro = "#A9D08E";
    private const string CorResumoVerdeEscuro = "#70AD47";
    private const string CorResumoTexto = "#C00000";

    public static string LogoPath => Path.Combine(Storage.AssetsDir, "cbmmg_logo.png");

    private static readonly Dictionary<string, (string Bg, string Text)> LegendaCores = new()
    {
        ["S"] = ("#FFFFFF", "#000000"),
        ["R"] = ("#FFFFFF", "#000000"),
        ["D"] = ("#FF0000", "#FFFFFF"),
        ["L"] = ("#BFBFBF", "#000000"),
        ["FD"] = ("#00B0F0", "#000000"),
        ["FN"] = ("#FFFF00", "#000000"),
        ["FR"] = ("#92D050", "#000000"),
        ["FA"] = ("#ED7D31", "#FFFFFF"),
        ["FP"] = ("#ED7D31", "#FFFFFF"),
        ["LN"] = ("#ED7D31", "#FFFFFF"),
        ["T"] = ("#FF00FF", "#000000"),
        ["O"] = ("#8B0000", "#FFFFFF"),
        ["MO"] = ("#FFFF00", "#000000"),
        ["1ª Ala"] = ("#5B9BD5", "#FFFFFF"),
        ["2ª Ala"] = ("#5B9BD5", "#FFFFFF"),
        ["3ª Ala"] = ("#5B9BD5", "#FFFFFF"),
        ["4ª Ala"] = ("#5B9BD5", "#FFFFFF"),
    };

    public static string GerarPdf(EscalaMensal escala, IReadOnlyList<Militar> militares, IReadOnlyList<AlaConfig> alas, string caminhoSaida)
    {
        var dirSaida = Path.GetDirectoryName(caminhoSaida);
        if (!string.IsNullOrEmpty(dirSaida))
            Directory.CreateDirectory(dirSaida);
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginVertical(8 * Mm);
                page.MarginHorizontal(10 * Mm);
                page.DefaultTextStyle(_ => TextStyle.Default.FontFamily("Arial").FontSize(8));

                page.Content().Column(column =>
                {
                    HeaderUnidade(column, escala);

                    var admins = militares.Where(m => m.Secao.Equals("ADMINISTRAÇÃO", StringComparison.OrdinalIgnoreCase)).ToList();
                    var gpvs = militares.Where(m => m.Secao.Equals("GPV", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (admins.Count > 0)
                        TabelaSecao(column, "ADMINISTRAÇÃO", admins, escala.Mes, escala.Ano);
                    if (gpvs.Count > 0)
                        TabelaSecao(column, "1º PELOTÃO/GPV", gpvs, escala.Mes, escala.Ano);
                    if (escala.Escala2Esforco.Count > 0)
                    {
                        column.Item().Height(4);
                        TabelaEsforco(column, escala, militares);
                    }

                    column.Item().PageBreak();

                    foreach (var ala in alas.OrderBy(a => a.Numero))
                    {
                        var militaresAla = militares
                            .Where(m => m.Ala == ala.Numero)
                            .OrderBy(m => m.ChaveAntiguidade.Posto)
                            .ThenBy(m => m.ChaveAntiguidade.Ordem)
                            .ToList();
                        escala.ObservacoesAlas.TryGetValue(ala.Numero.ToString(), out var obsAla);
                        TabelaAla(column, ala, militaresAla, militares, escala.Mes, escala.Ano, obsAla, escala);
                        column.Item().PageBreak();
                    }

                    ObservacoesFinais(column, escala, militares);
                });
            });
        }).GeneratePdf(caminhoSaida);

        return caminhoSaida;
    }

    private static void HeaderUnidade(ColumnDescriptor column, EscalaMensal escala)
    {
        if (File.Exists(LogoPath))
            column.Item().AlignCenter().Width(26 * Mm).Height(26 * Mm).Image(LogoPath).FitArea();

        column.Item().AlignCenter().Text(escala.Unidade).Style(Ts(12, bold: true));
        column.Item().AlignCenter().Text($"ESCALA MENSAL - {EscalaLogic.MesesPt[escala.Mes]} {escala.Ano}").Style(Ts(11, bold: true));
        column.Item().Height(6);
    }

    private static void TabelaSecao(ColumnDescriptor column, string titulo, IReadOnlyList<Militar> militares, int mes, int ano)
    {
        column.Item().AlignCenter().Text(titulo).Style(Ts(10, bold: true, italic: true));
        column.Item().AlignCenter().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(22 * Mm);
                cols.ConstantColumn(32 * Mm);
                cols.ConstantColumn(75 * Mm);
                cols.ConstantColumn(12 * Mm);
                cols.ConstantColumn(45 * Mm);
                cols.ConstantColumn(65 * Mm);
            });

            foreach (var h in new[] { "NÚMERO", "POSTO / GRADUAÇÃO", "NOME", "MOT.", "FUNÇÃO", "OBSERVAÇÕES" })
                TableTextCell(table, h, HeaderTab, bold: true, size: 7);

            foreach (var militar in militares)
            {
                var obsText = FormatObsAusencias(militar.Ausencias, mes, ano);

                TableTextCell(table, militar.Numero, size: 8);
                TableTextCell(table, militar.Posto, size: 8);
                TableTextCell(table, militar.Nome, size: 8, alignLeft: true);
                TableTextCell(table, militar.CategoriaCnh, size: 8);
                TableTextCell(table, militar.Funcao, size: 8);
                TableTextCell(table, string.IsNullOrWhiteSpace(obsText) ? militar.Observacoes : obsText,
                    textColor: string.IsNullOrWhiteSpace(obsText) ? null : CorObsVermelho, size: 7, alignLeft: true);
            }
        });
        column.Item().Height(8);
    }

    private static void TabelaEsforco(ColumnDescriptor column, EscalaMensal escala, IReadOnlyList<Militar> militares)
    {
        column.Item().AlignCenter().Text("ESCALA DE 2º ESFORÇO PARA OS MILITARES DA ADMINISTRAÇÃO E GPV").Style(Ts(10, bold: true, italic: true));
        column.Item().AlignCenter().Width(170 * Mm).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(70 * Mm);
                cols.ConstantColumn(50 * Mm);
                cols.ConstantColumn(50 * Mm);
            });

            foreach (var h in new[] { "MILITAR EMPENHADO", "DE", "ATÉ" })
                TableTextCell(table, h, HeaderTab, bold: true, size: 8);

            var mapa = militares.ToDictionary(m => m.Numero, m => m);
            foreach (var item in escala.Escala2Esforco.OrderBy(i => i.TryGetValue("de", out var de) ? EscalaLogic.ParseDataHoraCbmmg(de) ?? DateTime.MaxValue : DateTime.MaxValue))
            {
                item.TryGetValue("militar_numero", out var numero);
                item.TryGetValue("de", out var de);
                item.TryGetValue("ate", out var ate);
                var nome = "";
                if (numero is not null && mapa.TryGetValue(numero, out var militar))
                {
                    var ng = string.IsNullOrWhiteSpace(militar.NomeGuerra)
                        ? ExtrairNomeGuerra(militar.Nome)
                        : militar.NomeGuerra.Trim();
                    nome = $"{militar.Posto} BM {ng}";
                }
                else
                {
                    item.TryGetValue("nome_manual", out nome);
                    nome ??= "";
                }

                TableTextCell(table, nome, size: 8, alignLeft: true);
                TableTextCell(table, de ?? "", size: 8);
                TableTextCell(table, ate ?? "", size: 8);
            }

        });
    }

    private static void TabelaAla(
        ColumnDescriptor column,
        AlaConfig ala,
        IReadOnlyList<Militar> militaresAla,
        IReadOnlyList<Militar> todosMilitares,
        int mes,
        int ano,
        IReadOnlyList<string>? observacoesAla,
        EscalaMensal? escala)
    {
        column.Item().AlignCenter().Text($"{ala.Numero}ª ALA OPERACIONAL – {EscalaLogic.MesesPt[mes]} / {ano}")
            .Style(Ts(11, bold: true, italic: true));

        var todosComExternos = MilitaresComComposicoesExternas(todosMilitares, escala, ala.Numero, mes, ano);
        var (dias, grade) = EscalaLogic.MontarGradeAla(militaresAla, todosComExternos, ala.Numero, mes, ano, escala);
        var nDias = dias.Count;
        var mapaTodos = todosComExternos
            .GroupBy(m => m.Numero, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var numerosAla = militaresAla.Select(m => m.Numero).ToHashSet();
        var visitantes = grade.Keys.Where(k => !numerosAla.Contains(k) && mapaTodos.ContainsKey(k)).Select(k => mapaTodos[k]).ToList();
        var linhas = militaresAla
            .Concat(visitantes)
            .OrderBy(m => m.ChaveAntiguidade.Posto)
            .ThenBy(m => m.ChaveAntiguidade.Ordem)
            .ToList();

        // Centralizada na página (igual ao padrão reportlab, cujo hAlign default é CENTER).
        column.Item().AlignCenter().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(8 * Mm);
                cols.ConstantColumn(15 * Mm);
                cols.ConstantColumn(13 * Mm);
                cols.ConstantColumn(55 * Mm);
                cols.ConstantColumn(10 * Mm);
                for (var i = 0; i < nDias; i++)
                    cols.ConstantColumn(11 * Mm);
                cols.ConstantColumn(22 * Mm);
                cols.ConstantColumn(36 * Mm);
            });

            table.Cell().ColumnSpan(5).Element(c => Cell(c, HeaderTab)).Text("DIAS DA SEMANA").Style(Ts(7, bold: true));
            foreach (var dia in dias)
                TableTextCell(table, EscalaLogic.NomeDiaSemana(dia), HeaderTab, bold: true, size: 7);
            table.Cell().RowSpan(2).Element(c => Cell(c, HeaderTab)).Text("FUNÇÃO").Style(Ts(7, bold: true));
            table.Cell().RowSpan(2).Element(c => Cell(c, HeaderTab)).Text("OBSERVAÇÕES").Style(Ts(7, bold: true));

            foreach (var h in new[] { "ORD.", "Nº", "P/G", "NOME", "MOT\nCAT" })
                TableTextCell(table, h, HeaderTab, bold: true, size: 7);
            foreach (var dia in dias)
                TableTextCell(table, $"{dia.Day}/{EscalaLogic.MesAbrev[dia.Month].ToLowerInvariant()}.", HeaderTab, bold: true, size: 7);

            var ord = 1;
            foreach (var militar in linhas)
            {
                var titular = numerosAla.Contains(militar.Numero);
                var cells = grade.GetValueOrDefault(militar.Numero, []);
                TableTextCell(table, titular ? ord.ToString() : "-", size: 6.5f);
                TableTextCell(table, militar.Numero, size: 6.5f);
                TableTextCell(table, militar.Posto, size: 6.5f);
                TableTextCell(table, militar.Nome, size: 6.5f, alignLeft: true);
                TableTextCell(table, militar.CategoriaCnh, size: 6.5f);

                foreach (var cell in cells)
                {
                    var (bg, fg, bold, italic) = CorCelula(cell.Valor, cell.Cor);
                    TableTextCell(table, cell.Valor, bg, fg, bold, italic || cell.Valor == "S", 6.5f);
                }

                var obsText = FormatObsAusencias(militar.Ausencias, mes, ano);

                TableTextCell(table, militar.Funcao, textColor: FuncaoCor(militar.Funcao), size: 5.8f);
                TableTextCell(table, obsText, textColor: CorObsVermelho, size: 6.2f, alignLeft: true);
                if (titular)
                    ord++;
            }

        });

        column.Item().Height(4);
        column.Item().AlignCenter().Row(row =>
        {
            // Resumo (36+55+10+11*dias) + legenda (22+36=58) = largura total da tabela de militares,
            // para os blocos de baixo alinharem na mesma largura/borda direita da tabela.
            row.ConstantItem((36 + 55 + 10 + 11 * nDias) * Mm).Element(c => ResumoAla(c, militaresAla, grade, dias, observacoesAla, todosComExternos));
            row.ConstantItem(58 * Mm).Element(LegendaColorida);
        });
        column.Item().Height(6);
    }

    private static List<Militar> MilitaresComComposicoesExternas(
        IEnumerable<Militar> militares,
        EscalaMensal? escala,
        int ala,
        int mes,
        int ano)
    {
        var lista = militares.ToList();
        if (escala is null)
            return lista;

        foreach (var composicao in escala.ComposicoesUnidade)
        {
            if (!string.Equals(composicao.PapelLocal, "destino", StringComparison.OrdinalIgnoreCase) || composicao.Ala != ala)
                continue;

            var dt = EscalaLogic.ParseDataBr(composicao.Data);
            if (!dt.HasValue || dt.Value.Month != mes || dt.Value.Year != ano)
                continue;
            if (lista.Any(m => string.Equals(m.Numero, composicao.MilitarNumero, StringComparison.OrdinalIgnoreCase)))
                continue;

            lista.Add(new Militar
            {
                Numero = composicao.MilitarNumero,
                Posto = composicao.MilitarPosto,
                Nome = composicao.MilitarNome,
                CategoriaCnh = string.IsNullOrWhiteSpace(composicao.MilitarCnh) ? "-" : composicao.MilitarCnh,
                Funcao = composicao.MilitarFuncao,
                Secao = "OPERACIONAL",
                Ala = 0,
                Ordem = 999,
                Observacoes = $"Origem: {composicao.OrigemNome}",
            });
        }

        return lista;
    }

    private static void ResumoAla(
        IContainer container,
        IReadOnlyList<Militar> militaresAla,
        Dictionary<string, List<CelulaEscala>> grade,
        IReadOnlyList<DateTime> dias,
        IReadOnlyList<string>? observacoesAla,
        IReadOnlyList<Militar> todosMilitares)
    {
        var resumo = EscalaLogic.ResumirAla(militaresAla, grade, dias, todosMilitares);
        var n = dias.Count;
        var blocks = new List<(string Nome, int TotalCat, int[] Totais, bool DiaNoite)>
        {
            ("BM'S SERVIÇO OPERACIONAL", (int)resumo["n_servico_op"], (int[])resumo["total"], true),
            ("MOTORISTAS CATEGORIA \"D\"", (int)resumo["n_motoristas_d"], (int[])resumo["motoristas_d"], true),
            ("OFICIAIS", militaresAla.Count(m => m.GrupoPosto == "OFICIAIS"), (int[])resumo["oficiais"], false),
            ("SUBTEN/SGT", militaresAla.Count(m => m.GrupoPosto == "SUBTEN/SGT"), (int[])resumo["subten_sgt"], false),
            ("CB/SD", militaresAla.Count(m => m.GrupoPosto == "CB/SD"), (int[])resumo["cb_sd"], false),
            ("SD 2ª CL", militaresAla.Count(m => m.GrupoPosto == "SD 2ª CL"), (int[])resumo["sd_2cl"], false),
        };

        container.Table(table =>
        {
            // Larguras alinhadas com a tabela de militares para o resumo casar coluna a coluna:
            // 36 = ORD+Nº+P/G (8+15+13), 55 = NOME, 10 = MOT CAT, 11 por dia (igual aos dias da tabela).
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(36 * Mm);
                cols.ConstantColumn(55 * Mm);
                cols.ConstantColumn(10 * Mm);
                for (var i = 0; i < n; i++)
                    cols.ConstantColumn(11 * Mm);
            });

            foreach (var block in blocks)
            {
                if (block.DiaNoite)
                {
                    // Rótulo (col 0) e total da categoria (col 1) MESCLADOS nas 3 linhas TOTAL/DIA/NOITE,
                    // igual ao padrão. A célula do total fica centralizada verticalmente nas 3 linhas.
                    table.Cell().RowSpan(3).Element(c => ResumoCell(c, CorResumoHeader)).Text(block.Nome).Style(Ts(6.5f, bold: true));
                    table.Cell().RowSpan(3).Element(c => ResumoCell(c, CorResumoHeader)).Text(block.TotalCat.ToString()).Style(Ts(6.5f, bold: true, color: CorResumoTexto));
                    AddResumoValues(table, "TOTAL:", block.Totais, "#FFFFFF", CorResumoTexto);
                    AddResumoValues(table, "DIA:", block.Totais, CorResumoVerdeClaro, "#000000");
                    AddResumoValues(table, "NOITE:", block.Totais, CorResumoVerdeEscuro, "#000000");
                }
                else
                {
                    table.Cell().Element(c => ResumoCell(c, CorResumoHeader)).Text(block.Nome).Style(Ts(6.5f, bold: true));
                    table.Cell().Element(c => ResumoCell(c, CorResumoHeader)).Text(block.TotalCat.ToString()).Style(Ts(6.5f, bold: true, color: CorResumoTexto));
                    AddResumoValues(table, "TOTAL:", block.Totais, "#FFFFFF", CorResumoTexto);
                }
            }

            table.Cell().ColumnSpan((uint)(3 + n)).Element(c => ResumoCell(c, HeaderDark)).Text("OBSERVAÇÕES GERAIS").Style(Ts(6.5f, bold: true));
            var obsList = observacoesAla ?? [];
            // A legenda tem 18 linhas (1 cabeçalho "LEGENDA:" + 17 itens, incluindo FR).
            // O resumo ocupa 11 linhas antes das observações (10 do resumo + 1 do cabeçalho
            // OBSERVAÇÕES GERAIS), então faltam 7 linhas de observação para a borda inferior
            // bater com a legenda — senão sobra um vão SEM GRADE no canto inferior esquerdo.
            const int linhasLegenda = 18;
            const int linhasResumoAntesObs = 11;
            var lines = Math.Max(0, linhasLegenda - linhasResumoAntesObs);
            for (var i = 0; i < lines; i++)
            {
                table.Cell().ColumnSpan((uint)(3 + n)).Element(c => ResumoCell(c, "#FFFFFF", alignLeft: true))
                    .Text(i < obsList.Count ? obsList[i] : "").Style(Ts(6.5f));
            }
        });
    }

    // Célula do resumo: borda mais grossa (1pt) para reproduzir o "grid escuro" do padrão.
    private static IContainer ResumoCell(IContainer container, string? background = null, bool alignLeft = false)
    {
        var styled = container.Border(1f).BorderColor("#000000");
        if (!string.IsNullOrWhiteSpace(background))
            styled = styled.Background(background);
        styled = styled.Padding(2).MinHeight(12).AlignMiddle();
        return alignLeft ? styled.AlignLeft() : styled.AlignCenter();
    }

    private static void AddResumoValues(TableDescriptor table, string rotulo, int[] totais, string bg, string textColor)
    {
        table.Cell().Element(c => ResumoCell(c, bg)).Text(rotulo).Style(Ts(6.5f, bold: true, color: textColor));
        foreach (var total in totais)
            table.Cell().Element(c => ResumoCell(c, bg)).Text(total.ToString()).Style(Ts(6.5f, bold: true, color: textColor));
    }

    private static void LegendaColorida(IContainer container)
    {
        var itens = new[]
        {
            ("S", "Serviço Operacional (24H X 72H)"),
            ("R", "Reforço Serviço Operacional (12H)"),
            ("D", "Dispensa Médica"),
            ("L", "Licença Médica"),
            ("FD", "Folga - Reposição (12H) - DIURNO"),
            ("FN", "Folga - Reposição (12H) - NOTURNO"),
            ("FR", "Folga - Reposição Obrigatória"),
            ("FA", "Férias Anuais"),
            ("FP", "Férias Prêmio"),
            ("LN", "Licença Núpcias"),
            ("T", "Trânsito"),
            ("O", "Outro (Especificar em Observações)"),
            ("MO", "Movimentado"),
            ("1ª Ala", "Serviço na 1ª Ala"),
            ("2ª Ala", "Serviço na 2ª Ala"),
            ("3ª Ala", "Serviço na 3ª Ala"),
            ("4ª Ala", "Serviço na 4ª Ala"),
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(15 * Mm);
                cols.ConstantColumn(43 * Mm);
            });
            table.Cell().ColumnSpan(2).Element(c => Cell(c, HeaderTab)).Text("LEGENDA:").Style(Ts(6.5f, bold: true, italic: true));
            foreach (var (cod, desc) in itens)
            {
                var (bg, fg) = LegendaCores[cod];
                TableTextCell(table, cod, bg, fg, bold: true, size: 6.5f);
                TableTextCell(table, desc, "#FFFFFF", size: 6.5f, alignLeft: true);
            }
        });
    }

    private static void ObservacoesFinais(ColumnDescriptor column, EscalaMensal escala, IReadOnlyList<Militar> militares)
    {
        column.Item().Text("OBSERVAÇÕES GERAIS").Style(Ts(10, bold: true));
        var obs = escala.ObservacoesGerais.Count > 0 ? escala.ObservacoesGerais : ObservacoesPadrao();
        for (var i = 0; i < obs.Count; i++)
        {
            var item = obs[i].Trim().Replace("<b>", "").Replace("</b>", "").Trim('*');
            var bold = obs[i].Contains("<b>", StringComparison.OrdinalIgnoreCase) || (obs[i].StartsWith('*') && obs[i].EndsWith('*'));
            column.Item().PaddingLeft(12).Text($"{i + 1}   {item}").Style(Ts(8, bold: bold));
        }

        column.Item().Height(40);
        var mapa = militares.ToDictionary(m => m.Numero, m => m);
        mapa.TryGetValue(escala.CmtPelNumero, out var cmtPel);
        mapa.TryGetValue(escala.CmtCiaNumero, out var cmtCia);
        var dataHom = string.IsNullOrWhiteSpace(escala.DataHomologacao)
            ? $"{DateTime.Today.Day} de {CultureTitle(EscalaLogic.MesesPt[escala.Mes])} de {escala.Ano}"
            : escala.DataHomologacao;

        column.Item().AlignRight().Text($"Quartel em {escala.Cidade}, {dataHom}").Style(Ts(10, bold: true, italic: true));
        column.Item().Height(30);

        if (cmtPel is not null)
        {
            column.Item().AlignCenter().Text($"{cmtPel.Nome.ToUpperInvariant()}, {cmtPel.Posto} BM.").Style(Ts(10, bold: true));
            column.Item().AlignCenter().Text($"***{cmtPel.Funcao}***").Style(Ts(10, bold: true));
            column.Item().Height(26);
        }

        column.Item().AlignLeft().Text("HOMOLOGO").Style(Ts(10, bold: true));
        column.Item().Height(18);

        if (cmtCia is not null)
        {
            column.Item().AlignCenter().Text($"{cmtCia.Nome.ToUpperInvariant()}, {cmtCia.Posto} BM.").Style(Ts(10, bold: true));
            column.Item().AlignCenter().Text($"***{cmtCia.Funcao}***").Style(Ts(10, bold: true));
        }
    }

    private static List<string> ObservacoesPadrao() =>
    [
        "A SAO e a Seção de Mergulho ficarão a cargo das Alas Operacionais, sob a supervisão dos Chefes de Serviço.",
        "O Chefe de Serviço deverá confeccionar escala diária de conservação, arrumação e limpeza dos alojamentos, rancho, sop, etc",
        "O Chefe de Serviço deverá primar pelo cumprimento da NGA da 4ª CIA BM, bem como do 10ºBBM;",
        "O Chefe de Serviço deverá fiscalizar o lançamento e encerramento das ocorrências no Cad;",
        "O Chefe de Serviço deverá preencher e conferir a ficha de controle dos REDS;",
        "O Chefe de Serviço deverá fiscalizar o lançamento das viaturas da 4ª CIA BM no SIAD e Módulo Frota de Abastecimento;",
        "Todos os militares deverão acessar quando estiverem de serviço Intranet, Celotex Digital, SEI e Email Funcional;",
        "*As reposições de horas poderão ser cassadas por necessidade do serviço;*",
        "*Esta escala poderá sofrer alterações, de acordo com a necessidade da 4ª CIA BM.*",
    ];

    private static string FormatObsAusencias(IEnumerable<Ausencia> ausencias, int? mes = null, int? ano = null)
    {
        var textos = ausencias
            .Where(a => !mes.HasValue || !ano.HasValue || AusenciaSobrepoePeriodo(a, mes.Value, ano.Value))
            .Select(FormatObsAusencia)
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return string.Join("; ", textos);
    }

    private static bool AusenciaSobrepoePeriodo(Ausencia ausencia, int mes, int ano)
    {
        var inicio = EscalaLogic.ParseDataBr(ausencia.DataInicio);
        var fim = EscalaLogic.ParseDataBr(ausencia.DataFim) ?? inicio;
        if (!inicio.HasValue || !fim.HasValue)
            return true;

        var periodoInicio = new DateTime(ano, mes, 1);
        var periodoFim = periodoInicio.AddMonths(1).AddDays(-1);
        return inicio.Value.Date <= periodoFim && fim.Value.Date >= periodoInicio;
    }

    private static string FormatObsAusencia(Ausencia ausencia)
    {
        if (TipoRemanejamentoPdf(ausencia.Tipo))
            return "";

        var rotulos = new Dictionary<string, string>
        {
            ["FA"] = "Férias anuais",
            ["FP"] = "Férias prêmio",
            ["L"] = "Licença",
            ["D"] = "Dispensa",
            ["FD"] = "Folga diurna",
            ["FN"] = "Folga noturna",
            ["FR"] = "Folga obrigatória",
            ["LN"] = "Licença núpcias",
            ["T"] = "Trânsito",
            ["O"] = "Outro",
            ["MO"] = "Movimentado",
            ["1ª Ala"] = "Remanejamento 1ª Ala",
            ["2ª Ala"] = "Remanejamento 2ª Ala",
            ["3ª Ala"] = "Remanejamento 3ª Ala",
            ["4ª Ala"] = "Remanejamento 4ª Ala",
        };
        rotulos.TryGetValue(ausencia.Tipo, out var rotulo);
        var obs = (ausencia.Observacao ?? "").Trim();
        var texto = "";
        if (!string.IsNullOrWhiteSpace(rotulo) && obs.StartsWith(rotulo, StringComparison.OrdinalIgnoreCase))
            texto = obs;
        else
            texto = !string.IsNullOrWhiteSpace(rotulo) && !string.IsNullOrWhiteSpace(obs)
                ? $"{rotulo} - {obs}"
                : (obs.Length > 0 ? obs : rotulo ?? "");

        if (!string.IsNullOrWhiteSpace(ausencia.DataInicio))
        {
            var sep = texto.Length > 0 && !texto.EndsWith("-") ? " " : "";
            texto = $"{texto}{sep}{ausencia.DataInicio} a {ausencia.DataFim}".Trim();
        }

        return texto;
    }

    private static bool TipoRemanejamentoPdf(string? tipo) =>
        tipo is "1ª Ala" or "2ª Ala" or "3ª Ala" or "4ª Ala";

    private static string ExtrairNomeGuerra(string nomeCompleto)
    {
        if (string.IsNullOrWhiteSpace(nomeCompleto))
            return "";
        var palavras = nomeCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var caps = palavras.FirstOrDefault(p => p.All(ch => !char.IsLetter(ch) || char.IsUpper(ch)) && !new[] { "DE", "DA", "DO", "DOS", "DAS", "E" }.Contains(p));
        return CultureTitle(caps ?? palavras[^1]);
    }

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return char.ToUpper(value[0]) + value[1..].ToLowerInvariant();
    }

    private static string? FuncaoCor(string funcao)
    {
        var f = funcao.ToLowerInvariant();
        if (f.Contains("motorista"))
            return CorFuncaoVermelho;
        return new[] { "cmt", "armador", "ch.", "chefe", "aux" }.Any(f.Contains) ? CorFuncaoAzul : null;
    }

    private static (string? Bg, string? Fg, bool Bold, bool Italic) CorCelula(string valor, string corEstilo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return (null, null, false, false);
        if (valor == "S")
            return ("#FFFFFF", CorTextoS, false, false); // S: fundo branco, letra azul (padrão)
        if (LegendaCores.TryGetValue(valor, out var legendaCor))
            return (legendaCor.Bg, legendaCor.Text, valor is not "S" and not "R", false);
        if (valor is "FA" or "FP")
            return (CorFa, "#FFFFFF", true, false);
        if (valor is "L" or "D")
            return (CorLd, "#FFFFFF", true, false);
        if (valor == "FD")
            return (CorFolgaFd, "#FFFFFF", true, false);
        if (valor == "FN")
            return (CorFolgaFn, "#000000", true, false);
        if (valor == "O")
            return (CorO, "#000000", true, false);
        if (valor == "MO")
            return (CorMo, "#FFFFFF", true, false);
        if (valor == "T")
            return (CorT, "#FFFFFF", true, false);
        if (valor == "LN")
            return (CorLn, "#000000", true, false);
        if (corEstilo == "unidade_destino")
            return (CorRemanejAla, "#FFFFFF", true, false);
        if (valor.EndsWith("ª Ala", StringComparison.Ordinal))
            return corEstilo == "ala_origem" ? (CorAlaOrigem, "#7B7B7B", false, false) : (CorRemanejAla, "#FFFFFF", true, false);
        return (null, null, false, false);
    }

    private static TextStyle Ts(float size, bool bold = false, bool italic = false, string? color = null)
    {
        var style = TextStyle.Default.FontFamily("Arial").FontSize(size);
        if (bold)
            style = style.Bold();
        if (italic)
            style = style.Italic();
        if (!string.IsNullOrWhiteSpace(color))
            style = style.FontColor(color);
        return style;
    }

    private static IContainer Cell(IContainer container, string? background = null, bool alignLeft = false)
    {
        // O fundo é aplicado ANTES do Padding para preencher a célula inteira (igual ao padrão).
        // Se aplicado depois, a cor fica recuada (com borda branca interna).
        var styled = container.Border(0.5f).BorderColor("#000000");
        if (!string.IsNullOrWhiteSpace(background))
            styled = styled.Background(background);
        styled = styled.Padding(2).MinHeight(12).AlignMiddle();
        return alignLeft ? styled.AlignLeft() : styled.AlignCenter();
    }

    private static void TableTextCell(
        TableDescriptor table,
        string text,
        string? background = null,
        string? textColor = null,
        bool bold = false,
        bool italic = false,
        float size = 8,
        bool alignLeft = false)
    {
        table.Cell().Element(c => Cell(c, background, alignLeft)).Text(text ?? "").Style(Ts(size, bold, italic, textColor));
    }
}
