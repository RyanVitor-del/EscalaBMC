using System.Globalization;

namespace EscalaBMC;

public static class EscalaLogic
{
    private static readonly DateTime DataReferenciaAla = new(2026, 5, 1);
    private const int AlaReferencia = 1;
    private const string OrigemCoberturaAutomatica = "equalizacao_efetivo";
    private const string AprovadorAutomatico = "AUTOMATICO";

    public static readonly string[] DiasPt = ["SEG.", "TER.", "QUA.", "QUI.", "SEX.", "SÁB.", "DOM."];
    public static readonly string[] MesesPt =
    [
        "", "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO",
        "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO",
    ];

    public static readonly string[] MesAbrev =
    [
        "", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun",
        "Jul", "Ago", "Set", "Out", "Nov", "Dez",
    ];

    public static List<DateTime> DiasDaAla(int ala, int mes, int ano)
    {
        var last = DateTime.DaysInMonth(ano, mes);
        var outList = new List<DateTime>();
        for (var d = 1; d <= last; d++)
        {
            var dt = new DateTime(ano, mes, d);
            if (AlaDoDia(dt) == ala)
                outList.Add(dt);
        }

        return outList;
    }

    public static int AlaDoDia(DateTime dt)
    {
        var diff = (dt.Date - DataReferenciaAla.Date).Days;
        return Mod(AlaReferencia - 1 + diff, 4) + 1;
    }

    private static int Mod(int value, int divisor) =>
        ((value % divisor) + divisor) % divisor;

    public static string NomeDiaSemana(DateTime dt)
    {
        var idx = ((int)dt.DayOfWeek + 6) % 7;
        return DiasPt[idx];
    }

    public static DateTime? ParseDataBr(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Trim().Split('/');
        try
        {
            var d = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var m = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var y = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : DateTime.Today.Year;
            if (y < 100)
                y += 2000;
            return new DateTime(y, m, d);
        }
        catch
        {
            return null;
        }
    }

    public static string FmtDataBr(DateTime dt) => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FmtDataHoraCbmmg(DateTime dt)
    {
        var dias = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sab" };
        return $"{dt:ddHHmm}{MesAbrev[dt.Month]}{dt:yy}-{dias[(int)dt.DayOfWeek]}";
    }

    public static DateTime? ParseDataHoraCbmmg(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            var principal = value.Split('-')[0].Trim();
            var dd = int.Parse(principal[..2], CultureInfo.InvariantCulture);
            var hh = int.Parse(principal.Substring(2, 2), CultureInfo.InvariantCulture);
            var mm = int.Parse(principal.Substring(4, 2), CultureInfo.InvariantCulture);
            var mesAbrev = principal.Substring(6, 3);
            var yy = int.Parse(principal.Substring(9, 2), CultureInfo.InvariantCulture);
            var mes = Array.FindIndex(MesAbrev, m => m.Equals(mesAbrev, StringComparison.OrdinalIgnoreCase));
            if (mes <= 0)
                return null;
            return new DateTime(2000 + yy, mes, dd, hh, mm, 0);
        }
        catch
        {
            return null;
        }
    }

    public static Ausencia? AusenciaNoDia(Militar militar, DateTime dt)
    {
        foreach (var ausencia in militar.Ausencias)
        {
            var inicio = ParseDataBr(ausencia.DataInicio);
            var fim = ParseDataBr(ausencia.DataFim);
            if (inicio.HasValue && fim.HasValue && inicio.Value.Date <= dt.Date && dt.Date <= fim.Value.Date)
                return ausencia;
            if (inicio.HasValue && !fim.HasValue && inicio.Value.Date == dt.Date)
                return ausencia;
        }

        return null;
    }

    public static (List<DateTime> Dias, Dictionary<string, List<CelulaEscala>> Grade) MontarGradeAla(
        IReadOnlyList<Militar> militaresAla,
        IReadOnlyList<Militar> todosMilitares,
        int ala,
        int mes,
        int ano,
        EscalaMensal? escala = null)
    {
        var dias = DiasDaAla(ala, mes, ano);
        var grade = new Dictionary<string, List<CelulaEscala>>();

        foreach (var militar in militaresAla)
        {
            var linha = new List<CelulaEscala>();
            foreach (var dt in dias)
            {
                var ausencia = AusenciaNoDia(militar, dt);
                linha.Add(ausencia is not null
                    ? new CelulaEscala(ausencia.Tipo, "ausencia")
                    : new CelulaEscala("S", "normal"));
            }
            grade[militar.Numero] = linha;
        }

        foreach (var visitante in todosMilitares)
        {
            if (visitante.Ala == ala || visitante.Ala == 0)
                continue;

            foreach (var ausencia in visitante.Ausencias)
            {
                if (ausencia.Tipo != $"{ala}ª Ala")
                    continue;

                var inicio = ParseDataBr(ausencia.DataInicio);
                var fim = ParseDataBr(ausencia.DataFim) ?? inicio;
                if (!inicio.HasValue || !fim.HasValue)
                    continue;

                var cobreAlgum = dias.Any(d => inicio.Value.Date <= d.Date && d.Date <= fim.Value.Date);
                if (!cobreAlgum)
                    continue;

                var alaOrigem = $"{visitante.Ala}ª Ala";
                if (!grade.ContainsKey(visitante.Numero))
                    grade[visitante.Numero] = dias.Select(_ => new CelulaEscala(alaOrigem, "ala_origem")).ToList();

                for (var i = 0; i < dias.Count; i++)
                {
                    if (inicio.Value.Date <= dias[i].Date && dias[i].Date <= fim.Value.Date)
                        grade[visitante.Numero][i] = new CelulaEscala("S", "remanejado");
                }
            }
        }

        if (escala is not null)
        {
            var mapaTodos = todosMilitares.ToDictionary(m => m.Numero, m => m);

            foreach (var ins in escala.InsercoesAla.Where(i => i.Ala == ala))
            {
                if (!mapaTodos.TryGetValue(ins.MilitarNumero, out var mil))
                    continue;
                if (grade.ContainsKey(ins.MilitarNumero))
                    continue;
                var rotulo = mil.Ala > 0 ? $"{mil.Ala}ª Ala" : "—";
                grade[ins.MilitarNumero] = dias.Select(_ => new CelulaEscala(rotulo, "ala_origem")).ToList();
            }

            foreach (var cm in escala.CelulasManuais.Where(c => c.Ala == ala))
            {
                var dt = ParseDataBr(cm.Data);
                if (!dt.HasValue)
                    continue;
                var idx = dias.FindIndex(d => d.Date == dt.Value.Date);
                if (idx < 0)
                    continue;
                if (!grade.TryGetValue(cm.MilitarNumero, out var linha))
                {
                    if (!mapaTodos.ContainsKey(cm.MilitarNumero))
                        continue;
                    var mil = mapaTodos[cm.MilitarNumero];
                    var rotulo = mil.Ala > 0 ? $"{mil.Ala}ª Ala" : "—";
                    linha = dias.Select(_ => new CelulaEscala(rotulo, "ala_origem")).ToList();
                    grade[cm.MilitarNumero] = linha;
                }
                linha[idx] = new CelulaEscala(cm.Valor, "manual");
            }

            foreach (var oc in escala.OcultacoesAla.Where(o => o.Ala == ala))
                grade.Remove(oc.MilitarNumero);
        }

        return (dias, grade);
    }

    public static Dictionary<string, object> ResumirAla(
        IReadOnlyList<Militar> militaresAla,
        Dictionary<string, List<CelulaEscala>> grade,
        IReadOnlyList<DateTime> dias)
    {
        var n = dias.Count;
        var total = new int[n];
        var motoristasD = new int[n];
        var oficiais = new int[n];
        var subtenSgt = new int[n];
        var cbSd = new int[n];
        var sd2Cl = new int[n];
        var mapa = militaresAla.ToDictionary(m => m.Numero, m => m);

        foreach (var (numero, linha) in grade)
        {
            if (!mapa.TryGetValue(numero, out var militar))
                continue;

            for (var i = 0; i < linha.Count; i++)
            {
                if (linha[i].Valor != "S")
                    continue;

                total[i]++;
                if (militar.EhMotoristaD)
                    motoristasD[i]++;

                switch (militar.GrupoPosto)
                {
                    case "OFICIAIS":
                        oficiais[i]++;
                        break;
                    case "SUBTEN/SGT":
                        subtenSgt[i]++;
                        break;
                    case "SD 2ª CL":
                        sd2Cl[i]++;
                        break;
                    default:
                        cbSd[i]++;
                        break;
                }
            }
        }

        return new Dictionary<string, object>
        {
            ["total"] = total,
            ["motoristas_d"] = motoristasD,
            ["oficiais"] = oficiais,
            ["subten_sgt"] = subtenSgt,
            ["cb_sd"] = cbSd,
            ["sd_2cl"] = sd2Cl,
            ["n_servico_op"] = militaresAla.Count(m => grade.TryGetValue(m.Numero, out var cells) && cells.Any(c => c.Valor == "S")),
            ["n_motoristas_d"] = militaresAla.Count(m => m.EhMotoristaD && grade.TryGetValue(m.Numero, out var cells) && cells.Any(c => c.Valor == "S")),
        };
    }

    public static (bool Valido, string Motivo, int FolgaHoras) ValidarRemanejamento(
        Militar militar,
        int alaOrigem,
        int alaDestino,
        DateTime dataDestino,
        int mes,
        int ano)
    {
        if (alaOrigem == alaDestino)
            return (false, "Mesma ala", 72);

        var diasOrigem = DiasDaAla(alaOrigem, mes, ano);
        var diasAntes = diasOrigem.Where(d => d.Date <= dataDestino.Date).ToList();
        if (diasAntes.Count == 0)
            return (true, "Sem serviço anterior na origem", 72);

        var ultimoServico = diasAntes[^1];
        var delta = (dataDestino.Date - ultimoServico.Date).Days;

        return delta switch
        {
            1 => (false, $"Ala destino é o dia seguinte ({ultimoServico:dd/MM} → {dataDestino:dd/MM}). Militar não pode dobrar serviço (24h apenas).", 24),
            2 => (true, $"Ala fantasma (folga de 48h após {ultimoServico:dd/MM}). Recomendado registrar +24h no banco de horas.", 48),
            3 => (true, $"Folga normal de 72h até {dataDestino:dd/MM}.", 72),
            _ => (true, $"Folga estendida de {delta * 24}h.", 72),
        };
    }

    public static int AlasFantasma(int ala) =>
        ModelConstants.AlasFantasma.TryGetValue(ala, out var oposta) ? oposta : 0;

    public static List<AlertaEscala> Diagnosticar(IReadOnlyList<Militar> militares, int mes, int ano)
    {
        var alertas = new List<AlertaEscala>();
        var porAla = militares
            .Where(m => m.Ala is >= 1 and <= 4)
            .GroupBy(m => m.Ala)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var ala in Enumerable.Range(1, 4))
            porAla.TryAdd(ala, []);

        var efetivo = Enumerable.Range(1, 4).ToDictionary(a => a, a => porAla[a].Count);
        if (efetivo.Values.Max() - efetivo.Values.Min() > 1)
        {
            alertas.Add(new AlertaEscala
            {
                Tipo = "efetivo_desigual",
                Severidade = "media",
                Mensagem = $"Efetivo desigualado entre alas: {FormatDict(efetivo)}",
            });
        }

        alertas.AddRange(ValidarChefias(militares));

        var distD = Enumerable.Range(1, 4).ToDictionary(a => a, a => porAla[a].Count(m => m.EhMotoristaD));
        if (distD.Values.Max() - distD.Values.Min() > 1)
        {
            alertas.Add(new AlertaEscala
            {
                Tipo = "balanceamento_d",
                Severidade = "media",
                Mensagem = $"Motoristas categoria D desbalanceados: {FormatDict(distD)}",
            });
        }

        foreach (var ala in Enumerable.Range(1, 4))
        {
            foreach (var dt in DiasDaAla(ala, mes, ano))
            {
                var ativos = porAla[ala].Where(m => AusenciaNoDia(m, dt) is null).ToList();
                var temSgt = ativos.Any(m => m.GrupoPosto == "SUBTEN/SGT");
                var temMotorista = ativos.Any(m => m.EhMotoristaD);
                if (!temSgt)
                {
                    alertas.Add(new AlertaEscala
                    {
                        Tipo = "sem_sargento",
                        Severidade = "alta",
                        Mensagem = $"{ala}ª Ala em {dt:dd/MM}: sem Sargento de serviço",
                        Ala = ala,
                        Data = dt,
                    });
                }

                if (!temMotorista)
                {
                    alertas.Add(new AlertaEscala
                    {
                        Tipo = "sem_motorista",
                        Severidade = "alta",
                        Mensagem = $"{ala}ª Ala em {dt:dd/MM}: sem motorista categoria D",
                        Ala = ala,
                        Data = dt,
                    });
                }

                if (ativos.Count < 4)
                {
                    alertas.Add(new AlertaEscala
                    {
                        Tipo = "subdimensionada",
                        Severidade = "media",
                        Mensagem = $"{ala}ª Ala em {dt:dd/MM}: apenas {ativos.Count} militares",
                        Ala = ala,
                        Data = dt,
                    });
                }
            }
        }

        return alertas;
    }

    public static List<string> SugerirRebalanceamentoD(IReadOnlyList<Militar> militares)
    {
        var porAla = Enumerable.Range(1, 4)
            .ToDictionary(a => a, a => militares.Where(m => m.Ala == a && m.EhMotoristaD).ToList());
        var contagem = porAla.ToDictionary(k => k.Key, k => k.Value.Count);
        var media = contagem.Values.Sum() / 4;
        var sobra = contagem.Where(kv => kv.Value > media).Select(kv => kv.Key).ToList();
        var falta = contagem.Where(kv => kv.Value < media).Select(kv => kv.Key).ToList();
        var sugestoes = new List<string>();

        foreach (var aSob in sobra)
        {
            foreach (var aFal in falta)
            {
                if (porAla[aSob].Count == 0)
                    continue;
                var m = porAla[aSob][^1];
                porAla[aSob].RemoveAt(porAla[aSob].Count - 1);
                sugestoes.Add($"Mover {m.Posto} {m.Nome} da {aSob}ª Ala para a {aFal}ª Ala");
            }
        }

        return sugestoes;
    }

    public static int AplicarRebalanceamentoD(IList<Militar> militares)
    {
        var movimentos = 0;
        for (var pass = 0; pass < 50; pass++)
        {
            var porAla = Enumerable.Range(1, 4)
                .ToDictionary(a => a, a => militares.Where(m => m.Ala == a && m.EhMotoristaD).ToList());
            var contagem = porAla.ToDictionary(k => k.Key, k => k.Value.Count);
            if (contagem.Values.Max() - contagem.Values.Min() <= 1)
                break;

            var aMax = contagem.MaxBy(kv => kv.Value).Key;
            var aMin = contagem.MinBy(kv => kv.Value).Key;
            var candidato = porAla[aMax]
                .OrderByDescending(m => m.ChaveAntiguidade.Posto)
                .ThenByDescending(m => m.ChaveAntiguidade.Ordem)
                .FirstOrDefault();
            if (candidato is null)
                break;

            candidato.Ala = aMin;
            movimentos++;
        }

        return movimentos;
    }

    public static int AplicarRebalanceamentoEfetivo(IList<Militar> militares)
    {
        var movimentos = 0;
        for (var pass = 0; pass < 50; pass++)
        {
            var porAla = Enumerable.Range(1, 4)
                .ToDictionary(a => a, a => militares.Where(m => m.Ala == a).ToList());
            var contagem = porAla.ToDictionary(k => k.Key, k => k.Value.Count);
            if (contagem.Values.Max() - contagem.Values.Min() <= 1)
                break;

            var aMax = contagem.MaxBy(kv => kv.Value).Key;
            var aMin = contagem.MinBy(kv => kv.Value).Key;
            var candidatos = porAla[aMax]
                .OrderByDescending(m => m.ChaveAntiguidade.Posto)
                .ThenByDescending(m => m.ChaveAntiguidade.Ordem)
                .ToList();

            var moveu = false;
            foreach (var candidato in candidatos)
            {
                var sgts = porAla[aMax].Where(m => m.GrupoPosto == "SUBTEN/SGT").ToList();
                if (candidato.GrupoPosto == "SUBTEN/SGT" && sgts.Count <= 2)
                    continue;

                var motoristas = porAla[aMax].Where(m => m.EhMotoristaD).ToList();
                if (candidato.EhMotoristaD && motoristas.Count <= 1)
                    continue;

                candidato.Ala = aMin;
                movimentos++;
                moveu = true;
                break;
            }

            if (!moveu)
                break;
        }

        return movimentos;
    }

    public static ResultadoCoberturaAutomatica AplicarCoberturasAutomaticas(
        IList<Militar> militares,
        EscalaMensal escala,
        int mes,
        int ano)
    {
        RemoverCoberturasAutomaticas(militares, escala, mes, ano);

        var planejadas = new List<CoberturaPlanejada>();
        var cobridorPorAusencia = new Dictionary<string, Militar>(StringComparer.OrdinalIgnoreCase);
        var diasMes = Enumerable.Range(1, DateTime.DaysInMonth(ano, mes))
            .Select(d => new DateTime(ano, mes, d))
            .ToList();

        foreach (var dt in diasMes)
        {
            var ala = AlaDoDia(dt);
            var alvo = militares.Count(m => m.Ala == ala);
            if (alvo == 0)
                continue;

            while (ContarAtivosAlaNoDia(militares, planejadas, ala, dt) < alvo)
            {
                var adicionou = false;
                var vagas = ObterVagasCobertura(militares, planejadas, ala, dt)
                    .OrderByDescending(v => PrecisaSargentoNaCobertura(militares, planejadas, v))
                    .ThenBy(v => v.Coberto.ChaveAntiguidade.Posto)
                    .ThenBy(v => v.Coberto.ChaveAntiguidade.Ordem)
                    .ToList();
                if (vagas.Count == 0)
                    break;

                foreach (var vaga in vagas)
                {
                    var chaveAusencia = ChaveAusenciaCobertura(vaga);
                    if (!cobridorPorAusencia.TryGetValue(chaveAusencia, out var candidato))
                    {
                        candidato = EscolherCandidatoCobertura(militares, planejadas, vaga);
                        if (candidato is not null)
                            cobridorPorAusencia[chaveAusencia] = candidato;
                    }

                    if (candidato is null || !PodeUsarCandidatoCobertura(militares, planejadas, candidato, vaga))
                        continue;

                    planejadas.Add(new CoberturaPlanejada(candidato, candidato.Ala, ala, dt, vaga.Coberto));
                    adicionou = true;
                    break;
                }

                if (!adicionou)
                    break;
            }
        }

        var intervalos = PersistirCoberturasAutomaticas(planejadas, escala);
        var pendencias = ContarPendenciasCobertura(militares, planejadas, mes, ano);
        return new ResultadoCoberturaAutomatica(planejadas.Count, intervalos, pendencias);
    }

    public static int DesfazerCoberturasAutomaticas(IList<Militar> militares, EscalaMensal escala, int mes, int ano) =>
        RemoverCoberturasAutomaticas(militares, escala, mes, ano);

    private static int RemoverCoberturasAutomaticas(IList<Militar> militares, EscalaMensal escala, int mes, int ano)
    {
        var removidas = 0;
        foreach (var militar in militares)
            removidas += militar.Ausencias.RemoveAll(a => EhCoberturaAutomatica(a) && AusenciaSobrepoePeriodo(a, mes, ano));

        removidas += escala.Remanejamentos.RemoveAll(r =>
            string.Equals(r.AprovadoPor, AprovadorAutomatico, StringComparison.OrdinalIgnoreCase)
            && DataNoPeriodo(r.Data, mes, ano));
        return removidas;
    }

    private static int PersistirCoberturasAutomaticas(List<CoberturaPlanejada> planejadas, EscalaMensal escala)
    {
        var intervalos = 0;
        foreach (var grupo in planejadas.GroupBy(p => new { p.Militar.Numero, p.DeAla, p.ParaAla }))
        {
            var militar = grupo.First().Militar;
            var datas = grupo.Select(ServicoCompensadoAla).Distinct().OrderBy(d => d).ToList();
            foreach (var (inicio, fim) in AgruparDatasCobertura(militar, datas))
            {
                var obs = ObservacaoCobertura(grupo
                    .Where(p =>
                    {
                        var compensado = ServicoCompensadoAla(p);
                        return inicio.Date <= compensado.Date && compensado.Date <= fim.Date;
                    }));

                militar.Ausencias.Add(new Ausencia
                {
                    Tipo = $"{grupo.Key.ParaAla}ª Ala",
                    DataInicio = FmtDataBr(inicio),
                    DataFim = FmtDataBr(fim),
                    Observacao = obs,
                    CoberturaAutomatica = true,
                    OrigemAutomatica = OrigemCoberturaAutomatica,
                });
                intervalos++;
            }
        }

        foreach (var item in planejadas)
        {
            escala.Remanejamentos.Add(new RemanejamentoLog
            {
                MilitarNumero = item.Militar.Numero,
                Data = FmtDataBr(item.Data),
                DeAla = item.DeAla,
                ParaAla = item.ParaAla,
                Motivo = ObservacaoCobertura([item]),
                FolgaHoras = 0,
                AprovadoPor = AprovadorAutomatico,
            });
        }

        return intervalos;
    }

    private static string ObservacaoCobertura(IEnumerable<CoberturaPlanejada> coberturas)
    {
        var motivos = coberturas
            .Select(MotivoCobertura)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return motivos.Count == 0
            ? "Remanejado para cobrir"
            : $"Remanejado para cobrir - {string.Join("/", motivos)}";
    }

    private static string MotivoCobertura(CoberturaPlanejada cobertura)
    {
        if (cobertura.Coberto is null)
            return "";

        var ausencia = AusenciaRealNoDia(cobertura.Coberto, cobertura.Data);
        return ausencia?.Tipo?.Trim() ?? "";
    }

    private static int ContarPendenciasCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int mes,
        int ano)
    {
        var pendencias = 0;
        for (var dia = 1; dia <= DateTime.DaysInMonth(ano, mes); dia++)
        {
            var dt = new DateTime(ano, mes, dia);
            var ala = AlaDoDia(dt);
            if (ObterVagasCobertura(militares, planejadas, ala, dt).Count == 0)
                continue;

            var alvo = militares.Count(m => m.Ala == ala);
            if (alvo == 0)
                continue;

            var ativos = ContarAtivosAlaNoDia(militares, planejadas, ala, dt);
            if (ativos < alvo)
                pendencias += alvo - ativos;
        }

        return pendencias;
    }

    private static int ContarAtivosAlaNoDia(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int ala,
        DateTime data) =>
        MilitaresAtivosAlaNoDia(militares, planejadas, ala, data).Count;

    private static List<VagaCobertura> ObterVagasCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int ala,
        DateTime data) =>
        militares
            .Where(m => m.Ala == ala)
            .Where(m => AusenciaRealNoDia(m, data) is not null)
            .Where(m => !planejadas.Any(p =>
                p.Data.Date == data.Date
                && p.Coberto is not null
                && string.Equals(p.Coberto.Numero, m.Numero, StringComparison.OrdinalIgnoreCase)))
            .Select(m => new VagaCobertura(ala, data, m))
            .ToList();

    private static string ChaveAusenciaCobertura(VagaCobertura vaga)
    {
        var ausencia = AusenciaRealNoDia(vaga.Coberto, vaga.Data);
        return string.Join("|",
            vaga.Ala,
            vaga.Coberto.Numero,
            ausencia?.Tipo?.Trim() ?? "",
            ausencia?.DataInicio ?? "",
            ausencia?.DataFim ?? "");
    }

    private static Militar? EscolherCandidatoCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        VagaCobertura vaga)
    {
        var listaMilitares = militares.ToList();
        var requerSargento = PrecisaSargentoNaCobertura(listaMilitares, planejadas, vaga);
        var adjacentes = AlasAdjacentes(vaga.Ala).ToHashSet();
        var candidatos = militares
            .Where(m => PodeUsarCandidatoCobertura(listaMilitares, planejadas, m, vaga, adjacentes, requerSargento))
            .ToList();

        var semChefia = candidatos.Where(m => !EhChefiaOperacional(m)).ToList();
        if (semChefia.Count > 0)
            candidatos = semChefia;

        return candidatos
            .OrderByDescending(m => MargemEquilibrioCobertura(listaMilitares, planejadas, m, vaga))
            .ThenBy(m => planejadas.Count(p => p.Militar.Numero == m.Numero))
            .ThenByDescending(m => m.ChaveAntiguidade.Posto)
            .ThenByDescending(m => m.ChaveAntiguidade.Ordem)
            .FirstOrDefault();
    }

    private static bool PodeUsarCandidatoCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        Militar candidato,
        VagaCobertura vaga)
    {
        var adjacentes = AlasAdjacentes(vaga.Ala).ToHashSet();
        var requerSargento = PrecisaSargentoNaCobertura(militares, planejadas, vaga);
        return PodeUsarCandidatoCobertura(militares, planejadas, candidato, vaga, adjacentes, requerSargento);
    }

    private static bool PodeUsarCandidatoCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        Militar candidato,
        VagaCobertura vaga,
        ISet<int> adjacentes,
        bool requerSargento) =>
        adjacentes.Contains(candidato.Ala)
        && (!requerSargento || candidato.GrupoPosto == "SUBTEN/SGT")
        && AusenciaNoDia(candidato, vaga.Data) is null
        && !TemRemanejamentoParaAlaNoDia(candidato, vaga.Ala, vaga.Data)
        && PodeAdicionarCobertura(candidato, planejadas, vaga.Ala, vaga.Data)
        && PodeEmprestarParaCobertura(militares, planejadas, candidato, vaga.Data)
        && MargemEquilibrioCobertura(militares, planejadas, candidato, vaga) >= 0;

    private static int MargemEquilibrioCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        Militar candidato,
        VagaCobertura vaga)
    {
        var destinoDepois = ContarAtivosAlaNoDia(militares, planejadas, vaga.Ala, vaga.Data) + 1;
        var servicoCompensado = ServicoCompensadoAla(candidato.Ala, vaga.Data);

        var origemDepois = MilitaresAtivosAlaNoDia(
            militares,
            planejadas,
            candidato.Ala,
            servicoCompensado,
            candidato.Numero).Count;

        return origemDepois - destinoDepois;
    }

    private static bool PrecisaSargentoNaCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        VagaCobertura vaga)
    {
        var sargentosAtivos = ContarSargentosAtivosAlaNoDia(militares, planejadas, vaga.Ala, vaga.Data);
        return sargentosAtivos < 2 || !ChefiasMinimasAtivas(militares, planejadas, vaga.Ala, vaga.Data);
    }

    private static bool PodeEmprestarParaCobertura(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        Militar candidato,
        DateTime dataCobertura)
    {
        if (MilitarTrabalhaNoDia(candidato, planejadas, dataCobertura))
            return false;

        var servicoCompensado = ServicoCompensadoAla(candidato.Ala, dataCobertura);
        if (TemCoberturaPlanejadaNoDia(planejadas, candidato.Numero, servicoCompensado))
            return false;

        var inicioIntervalo = dataCobertura.Date < servicoCompensado.Date ? dataCobertura : servicoCompensado;
        var fimIntervalo = dataCobertura.Date > servicoCompensado.Date ? dataCobertura : servicoCompensado;
        if (TemAusenciaNaoAutomaticaNoIntervalo(candidato, inicioIntervalo, fimIntervalo))
            return false;

        if (candidato.GrupoPosto != "SUBTEN/SGT")
            return true;

        var sargentosOrigem = ContarSargentosAtivosAlaNoDia(militares, planejadas, candidato.Ala, servicoCompensado, candidato.Numero);
        return sargentosOrigem >= 2 && ChefiasMinimasAtivas(militares, planejadas, candidato.Ala, servicoCompensado, candidato.Numero);
    }

    private static bool PodeAdicionarCobertura(
        Militar militar,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int alaDestino,
        DateTime data)
    {
        if (planejadas.Any(p => p.Militar.Numero == militar.Numero && p.Data.Date == data.Date))
            return false;
        var servicoCompensado = ServicoCompensadoAla(militar.Ala, data);
        if (TemCoberturaPlanejadaNoDia(planejadas, militar.Numero, servicoCompensado))
            return false;

        var mesmas = planejadas
            .Where(p => p.Militar.Numero == militar.Numero && p.ParaAla == alaDestino)
            .Select(p => ServicoCompensadoAla(p))
            .Append(servicoCompensado.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        var grupoNovo = AgruparDatasCobertura(militar, mesmas)
            .FirstOrDefault(g => g.Inicio <= data.Date && data.Date <= g.Fim);

        if (grupoNovo != default && TemAusenciaNaoAutomaticaNoIntervalo(militar, grupoNovo.Inicio, grupoNovo.Fim))
            return false;

        var outrosIntervalos = IntervalosPlanejados(planejadas
            .Where(p => p.Militar.Numero == militar.Numero && p.ParaAla != alaDestino));
        return grupoNovo == default || outrosIntervalos.All(i => i.Fim < grupoNovo.Inicio || i.Inicio > grupoNovo.Fim);
    }

    private static IEnumerable<(DateTime Inicio, DateTime Fim)> AgruparDatasCobertura(Militar militar, IReadOnlyList<DateTime> datas)
    {
        if (datas.Count == 0)
            yield break;

        var inicio = datas[0].Date;
        var ultimaCobertura = inicio;
        for (var i = 1; i < datas.Count; i++)
        {
            var proxima = datas[i].Date;
            var consecutiva = (proxima - ultimaCobertura).Days == 4;
            var fimAtual = ultimaCobertura;
            var conflito = consecutiva && TemAusenciaNaoAutomaticaNoIntervalo(militar, fimAtual.AddDays(1), proxima.AddDays(-1));
            if (consecutiva && !conflito)
            {
                ultimaCobertura = proxima;
                continue;
            }

            yield return (inicio, ultimaCobertura);
            inicio = proxima;
            ultimaCobertura = proxima;
        }

        yield return (inicio, ultimaCobertura);
    }

    private static List<(DateTime Inicio, DateTime Fim)> IntervalosPlanejados(IEnumerable<CoberturaPlanejada> planejadas) =>
        planejadas
            .GroupBy(p => p.ParaAla)
            .SelectMany(g => AgruparDatasCobertura(g.First().Militar, g.Select(ServicoCompensadoAla).Distinct().OrderBy(d => d).ToList()))
            .ToList();

    private static bool TemCoberturaPlanejadaNoDia(IReadOnlyList<CoberturaPlanejada> planejadas, string militarNumero, DateTime data) =>
        planejadas
            .Where(p => string.Equals(p.Militar.Numero, militarNumero, StringComparison.OrdinalIgnoreCase))
            .Select(ServicoCompensadoAla)
            .Any(d => d.Date == data.Date);

    private static bool MilitarTrabalhaNoDia(Militar militar, IReadOnlyList<CoberturaPlanejada> planejadas, DateTime data)
    {
        if (planejadas.Any(p => p.Militar.Numero == militar.Numero && p.Data.Date == data.Date))
            return true;

        var alaDia = AlaDoDia(data);
        if (TemRemanejamentoParaAlaNoDia(militar, alaDia, data))
            return true;

        return militar.Ala == alaDia
            && AusenciaNoDia(militar, data) is null
            && !TemCoberturaPlanejadaNoDia(planejadas, militar.Numero, data);
    }

    private static List<Militar> MilitaresAtivosAlaNoDia(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int ala,
        DateTime data,
        string excluirNumero = "")
    {
        var ativos = new Dictionary<string, Militar>(StringComparer.OrdinalIgnoreCase);
        foreach (var militar in militares)
        {
            if (!string.IsNullOrWhiteSpace(excluirNumero)
                && string.Equals(militar.Numero, excluirNumero, StringComparison.OrdinalIgnoreCase))
                continue;

            var titularDisponivel = militar.Ala == ala
                && AusenciaNoDia(militar, data) is null
                && !TemCoberturaPlanejadaNoDia(planejadas, militar.Numero, data);
            var visitanteManual = militar.Ala != ala && TemRemanejamentoParaAlaNoDia(militar, ala, data);
            var visitanteAutomatico = planejadas.Any(p =>
                p.ParaAla == ala
                && p.Data.Date == data.Date
                && string.Equals(p.Militar.Numero, militar.Numero, StringComparison.OrdinalIgnoreCase));

            if (titularDisponivel || visitanteManual || visitanteAutomatico)
                ativos[militar.Numero] = militar;
        }

        return ativos.Values.ToList();
    }

    private static int ContarSargentosAtivosAlaNoDia(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int ala,
        DateTime data,
        string excluirNumero = "") =>
        MilitaresAtivosAlaNoDia(militares, planejadas, ala, data, excluirNumero)
            .Count(m => m.GrupoPosto == "SUBTEN/SGT");

    private static bool ChefiasMinimasAtivas(
        IEnumerable<Militar> militares,
        IReadOnlyList<CoberturaPlanejada> planejadas,
        int ala,
        DateTime data,
        string excluirNumero = "")
    {
        var ativos = MilitaresAtivosAlaNoDia(militares, planejadas, ala, data, excluirNumero);

        return ativos.Any(EhChefeServico) && ativos.Any(EhCmtGu);
    }

    private static DateTime FimIntervaloCobertura(Militar militar, DateTime dataCobertura) =>
        ProximoServicoAla(militar.Ala, dataCobertura) ?? dataCobertura.Date;

    private static DateTime ServicoCompensadoAla(CoberturaPlanejada cobertura) =>
        ServicoCompensadoAla(cobertura.DeAla, cobertura.Data);

    private static DateTime ServicoCompensadoAla(int alaOrigem, DateTime dataCobertura)
    {
        var anterior = dataCobertura.Date.AddDays(-1);
        if (AlaDoDia(anterior) == alaOrigem)
            return anterior;

        var proximo = dataCobertura.Date.AddDays(1);
        if (AlaDoDia(proximo) == alaOrigem)
            return proximo;

        return ProximoServicoAla(alaOrigem, dataCobertura) ?? dataCobertura.Date;
    }

    private static DateTime? ProximoServicoAla(int ala, DateTime data)
    {
        for (var i = 1; i <= 4; i++)
        {
            var dt = data.Date.AddDays(i);
            if (AlaDoDia(dt) == ala)
                return dt;
        }

        return null;
    }

    private static DateTime? ServicoAnteriorAla(int ala, DateTime data)
    {
        for (var i = 1; i <= 4; i++)
        {
            var dt = data.Date.AddDays(-i);
            if (AlaDoDia(dt) == ala)
                return dt;
        }

        return null;
    }

    private static bool TemRemanejamentoParaAlaNoDia(Militar militar, int ala, DateTime data)
    {
        var tipo = $"{ala}ª Ala";
        foreach (var ausencia in militar.Ausencias)
        {
            if (!string.Equals(ausencia.Tipo, tipo, StringComparison.OrdinalIgnoreCase))
                continue;
            var inicio = ParseDataBr(ausencia.DataInicio);
            var fim = ParseDataBr(ausencia.DataFim) ?? inicio;
            if (inicio.HasValue && fim.HasValue && inicio.Value.Date <= data.Date && data.Date <= fim.Value.Date)
                return true;
        }

        return false;
    }

    private static Ausencia? AusenciaRealNoDia(Militar militar, DateTime data)
    {
        var ausencia = AusenciaNoDia(militar, data);
        if (ausencia is null || EhCoberturaAutomatica(ausencia) || EhTipoRemanejamentoAla(ausencia.Tipo))
            return null;
        return ausencia;
    }

    private static bool TemAusenciaNaoAutomaticaNoIntervalo(Militar militar, DateTime inicio, DateTime fim)
    {
        if (inicio.Date > fim.Date)
            return false;

        foreach (var ausencia in militar.Ausencias)
        {
            if (EhCoberturaAutomatica(ausencia))
                continue;

            var de = ParseDataBr(ausencia.DataInicio);
            var ate = ParseDataBr(ausencia.DataFim) ?? de;
            if (de.HasValue && ate.HasValue && de.Value.Date <= fim.Date && ate.Value.Date >= inicio.Date)
                return true;
        }

        return false;
    }

    private static bool AusenciaSobrepoePeriodo(Ausencia ausencia, int mes, int ano)
    {
        var inicio = ParseDataBr(ausencia.DataInicio);
        var fim = ParseDataBr(ausencia.DataFim) ?? inicio;
        if (!inicio.HasValue || !fim.HasValue)
            return true;

        var periodoInicio = new DateTime(ano, mes, 1);
        var periodoFim = periodoInicio.AddMonths(1).AddDays(-1);
        return inicio.Value.Date <= periodoFim && fim.Value.Date >= periodoInicio;
    }

    private static bool DataNoPeriodo(string data, int mes, int ano)
    {
        var dt = ParseDataBr(data);
        return dt.HasValue && dt.Value.Month == mes && dt.Value.Year == ano;
    }

    private static bool EhCoberturaAutomatica(Ausencia ausencia) =>
        ausencia.CoberturaAutomatica
        && string.Equals(ausencia.OrigemAutomatica, OrigemCoberturaAutomatica, StringComparison.OrdinalIgnoreCase);

    private static bool EhTipoRemanejamentoAla(string? tipo) =>
        tipo is "1ª Ala" or "2ª Ala" or "3ª Ala" or "4ª Ala";

    private static bool EhChefiaOperacional(Militar militar)
    {
        var funcao = militar.Funcao.ToLowerInvariant();
        return EhChefeServico(militar) || EhCmtGu(militar);
    }

    private static bool EhChefeServico(Militar militar)
    {
        var funcao = militar.Funcao.ToLowerInvariant();
        return funcao.Contains("ch. serviço") || funcao.Contains("chefe");
    }

    private static bool EhCmtGu(Militar militar)
    {
        var funcao = militar.Funcao.ToLowerInvariant();
        return funcao.Contains("cmt. gu") || funcao.Contains("comandante de gu");
    }

    private static string NomeCurto(Militar militar) =>
        string.IsNullOrWhiteSpace(militar.NomeGuerra)
            ? militar.DisplayNome()
            : $"{militar.Posto} {militar.NomeGuerra}".Trim();

    private static int[] AlasAdjacentes(int ala) => ala switch
    {
        1 => [2, 4],
        2 => [1, 3],
        3 => [2, 4],
        4 => [3, 1],
        _ => [],
    };

    public static List<AlertaEscala> ValidarChefias(IReadOnlyList<Militar> militares)
    {
        var alertas = new List<AlertaEscala>();
        var porAla = Enumerable.Range(1, 4)
            .ToDictionary(a => a, a => militares.Where(m => m.Ala == a).ToList());

        foreach (var ala in Enumerable.Range(1, 4))
        {
            var sgts = porAla[ala].Where(m => m.GrupoPosto == "SUBTEN/SGT").ToList();
            if (sgts.Count < 2)
            {
                alertas.Add(new AlertaEscala
                {
                    Tipo = "chefia_insuficiente",
                    Severidade = "alta",
                    Mensagem = $"{ala}ª Ala tem apenas {sgts.Count} sargento(s). É necessário pelo menos 2 (Ch. Serviço e CMT GU).",
                    Ala = ala,
                });
                continue;
            }

            var maisAntigos = sgts
                .OrderBy(m => m.ChaveAntiguidade.Posto)
                .ThenBy(m => m.ChaveAntiguidade.Ordem)
                .Take(2);
            foreach (var sgt in maisAntigos)
            {
                var funcao = sgt.Funcao.ToLowerInvariant();
                if (!new[] { "ch.", "chefe", "cmt" }.Any(funcao.Contains))
                {
                    alertas.Add(new AlertaEscala
                    {
                        Tipo = "chefia_fora_antiguidade",
                        Severidade = "media",
                        Mensagem = $"{ala}ª Ala: {sgt.Posto} {sgt.Nome} é dos mais antigos mas não está como chefia.",
                        Ala = ala,
                    });
                    break;
                }
            }
        }

        return alertas;
    }

    public static List<SugestaoCobertura> SugerirRemanejamentoMinimo(
        IReadOnlyList<Militar> militares,
        int alaDescoberta,
        DateTime data,
        int mes,
        int ano,
        string requisito = "qualquer")
    {
        var candidatos = new List<(int Score, Militar Militar, string Motivo, int Folga)>();
        var alaFantasma = AlasFantasma(alaDescoberta);

        foreach (var militar in militares)
        {
            if (militar.Ala == alaDescoberta || militar.Ala == 0)
                continue;
            if (requisito == "sargento" && militar.GrupoPosto != "SUBTEN/SGT")
                continue;
            if (requisito == "motorista" && !militar.EhMotoristaD)
                continue;
            if (AusenciaNoDia(militar, data) is not null)
                continue;

            var f = militar.Funcao.ToLowerInvariant();
            var ehChefe = f.Contains("ch. serviço") || f.Contains("cmt. gu");
            if (ehChefe && requisito != "sargento")
                continue;

            var (valido, motivo, folga) = ValidarRemanejamento(militar, militar.Ala, alaDescoberta, data, mes, ano);
            if (!valido)
                continue;

            var score = 0;
            if (militar.Ala == alaFantasma)
                score -= 1000;
            score -= militar.ChaveAntiguidade.Posto * 10;
            score -= militar.ChaveAntiguidade.Ordem;
            if (folga == 48)
                score += 50;
            candidatos.Add((score, militar, motivo, folga));
        }

        return candidatos
            .OrderBy(c => c.Score)
            .Select(c => new SugestaoCobertura(c.Militar, c.Motivo, c.Folga))
            .ToList();
    }

    public static List<Militar> MilitaresPorAntiguidade(IEnumerable<Militar> militares) =>
        militares
            .OrderBy(m => m.ChaveAntiguidade.Posto)
            .ThenBy(m => m.ChaveAntiguidade.Ordem)
            .ToList();

    public static List<SugestaoCobertura> SugerirCobertura(
        IReadOnlyList<Militar> militares,
        int alaDescoberta,
        DateTime data,
        int mes,
        int ano,
        string requisito = "qualquer")
    {
        var candidatos = new List<SugestaoCobertura>();
        foreach (var militar in militares)
        {
            if (militar.Ala == alaDescoberta || militar.Ala == 0)
                continue;
            if (requisito == "sargento" && militar.GrupoPosto != "SUBTEN/SGT")
                continue;
            if (requisito == "motorista" && !militar.EhMotoristaD)
                continue;
            if (AusenciaNoDia(militar, data) is not null)
                continue;

            var (valido, motivo, folga) = ValidarRemanejamento(militar, militar.Ala, alaDescoberta, data, mes, ano);
            if (valido)
                candidatos.Add(new SugestaoCobertura(militar, motivo, folga));
        }

        return candidatos
            .OrderByDescending(s => s.Militar.ChaveAntiguidade.Posto)
            .ThenByDescending(s => s.Militar.ChaveAntiguidade.Ordem)
            .ToList();
    }

    private static string FormatDict(Dictionary<int, int> values) =>
        "{" + string.Join(", ", values.Select(kv => $"{kv.Key}: {kv.Value}")) + "}";

    private sealed record VagaCobertura(int Ala, DateTime Data, Militar Coberto);

    private sealed record CoberturaPlanejada(Militar Militar, int DeAla, int ParaAla, DateTime Data, Militar? Coberto);
}
