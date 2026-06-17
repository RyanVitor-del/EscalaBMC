"""Lógica de geração e rebalanceamento automático da escala mensal.

Regras críticas:
- 4 alas operacionais, escala 24x72h.
- Ala N trabalha no dia D quando ((D - N) % 4 == 0), a partir do dia N.
- Militar nunca pode ser remanejado para a ala IMEDIATAMENTE seguinte
  (ex: 3ª ala não vai para a 4ª no dia seguinte — folgaria só 24h).
- Alas "fantasma" (opostas, 1↔3 e 2↔4) permitem dois cenários:
    a) Folga curta (48h) → registra banco de horas (24h extras)
    b) Folga longa (5 dias) → vai trabalhar somente no próximo serviço da ala
- Distribuição de motoristas categoria D balanceada entre as 4 alas.
- Redistribuição preferindo os mais MODERNOS (menos antigos) para rotear.
"""
from __future__ import annotations
from datetime import date
from calendar import monthrange
from collections import defaultdict
from dataclasses import dataclass
from models import Militar, Ausencia, RemanejamentoLog, ALAS_FANTASMA


DIAS_PT = ["SEG.", "TER.", "QUA.", "QUI.", "SEX.", "SÁB.", "DOM."]


def dias_da_ala(ala: int, mes: int, ano: int) -> list[date]:
    """Retorna os dias do mês em que a ala está de serviço (8 dias normalmente)."""
    _, last = monthrange(ano, mes)
    out = []
    for d in range(1, last + 1):
        dt = date(ano, mes, d)
        if ala_do_dia(dt) == ala:
            out.append(dt)
    # acrescenta o primeiro dia do mês seguinte se a sequência continuar
    return out


def ala_do_dia(dt: date) -> int:
    diff = (dt - date(2026, 5, 1)).days
    return diff % 4 + 1


def nome_dia_semana(dt: date) -> str:
    return DIAS_PT[dt.weekday()]


def parse_data_br(s: str) -> date | None:
    """Converte 'DD/MM' ou 'DD/MM/YYYY' ou 'DD/MM/YY' em date. Retorna None se vazio."""
    if not s or not s.strip():
        return None
    s = s.strip()
    parts = s.split("/")
    try:
        d = int(parts[0]); m = int(parts[1])
        y = int(parts[2]) if len(parts) > 2 else date.today().year
        if y < 100:
            y += 2000
        return date(y, m, d)
    except Exception:
        return None


def fmt_data_br(dt: date) -> str:
    return dt.strftime("%d/%m/%Y")


def ausencia_no_dia(militar: Militar, dt: date) -> Ausencia | None:
    """Retorna a primeira ausência que cobre o dia dt, ou None."""
    for a in militar.ausencias:
        di = parse_data_br(a.data_inicio)
        df = parse_data_br(a.data_fim)
        if di and df and di <= dt <= df:
            return a
        if di and not df and di == dt:
            return a
    return None


@dataclass
class CelulaEscala:
    valor: str
    cor: str = "normal"  # normal | ausencia | remanejado | ala_origem


def montar_grade_ala(
    militares_ala: list[Militar],
    todos_militares: list[Militar],
    ala: int,
    mes: int,
    ano: int,
    remanejamentos: list[RemanejamentoLog] | None = None,
    composicoes_unidade: list | None = None,
) -> tuple[list[date], dict[str, list[CelulaEscala]]]:
    """Gera a grade da ala: para cada militar uma lista de células (8 dias)."""
    dias = dias_da_ala(ala, mes, ano)
    grade: dict[str, list[CelulaEscala]] = {}
    remanejamentos = remanejamentos or []
    composicoes_unidade = composicoes_unidade or []
    mapa_todos = {m.numero: m for m in todos_militares}

    for m in militares_ala:
        linha = []
        for dt in dias:
            rem = remanejamento_no_dia(remanejamentos, m.numero, dt, de_ala=ala)
            if rem:
                linha.append(CelulaEscala(f"{rem.para_ala}ª Ala", "ala_origem"))
                continue

            aus = ausencia_no_dia(m, dt)
            if aus:
                linha.append(CelulaEscala(aus.tipo, "ausencia"))
            else:
                linha.append(CelulaEscala("S", "normal"))
        grade[m.numero] = linha

    # Remanejamentos registrados na escala: militares de OUTRAS alas que cobrem esta ala.
    for rem in remanejamentos:
        if rem.para_ala != ala:
            continue
        dt = parse_data_br(rem.data)
        if dt not in dias:
            continue
        m_out = mapa_todos.get(rem.militar_numero)
        if not m_out or m_out.ala == ala or m_out.ala == 0:
            continue
        ala_origem = f"{m_out.ala}ª Ala"
        if m_out.numero not in grade:
            grade[m_out.numero] = [
                CelulaEscala(ala_origem, "ala_origem") for _ in dias
            ]
        grade[m_out.numero][dias.index(dt)] = CelulaEscala("S", "remanejado")

    # Remanejamentos legados salvos como ausência no cadastro do militar.
    for m_out in todos_militares:
        if m_out.ala == ala or m_out.ala == 0:
            continue
        for aus in m_out.ausencias:
            if aus.tipo == f"{ala}ª Ala":
                di = parse_data_br(aus.data_inicio)
                df = parse_data_br(aus.data_fim)
                if not di:
                    continue
                if not df:
                    df = di
                dias_cobertos = [dt for dt in dias if di <= dt <= df]
                if dias_cobertos:
                    ala_origem = f"{m_out.ala}ª Ala"
                    if m_out.numero not in grade:
                        grade[m_out.numero] = [
                            CelulaEscala(ala_origem, "ala_origem") for _ in dias
                        ]
                    for i, dt in enumerate(dias):
                        if dt in dias_cobertos:
                            grade[m_out.numero][i] = CelulaEscala("S", "remanejado")

    for comp in composicoes_unidade:
        if getattr(comp, "ala", 0) != ala:
            continue
        dt = parse_data_br(getattr(comp, "data", ""))
        if dt not in dias:
            continue
        idx = dias.index(dt)
        papel = getattr(comp, "papel_local", "").lower()
        numero = getattr(comp, "militar_numero", "")
        if papel == "origem":
            if numero in grade:
                grade[numero][idx] = CelulaEscala(_sigla_unidade(getattr(comp, "destino_nome", "")), "unidade_destino")
            continue
        if papel != "destino":
            continue
        if numero not in grade:
            grade[numero] = [
                CelulaEscala(_sigla_unidade(getattr(comp, "origem_nome", "")), "ala_origem") for _ in dias
            ]
        grade[numero][idx] = CelulaEscala("S", "composicao_unidade")
    return dias, grade


def _sigla_unidade(nome: str) -> str:
    if not nome:
        return "EXT"
    lower = nome.lower()
    if "formiga" in lower:
        return "FOR"
    if "arcos" in lower:
        return "ARC"
    letras = "".join(ch for ch in nome if ch.isalnum())[:3]
    return letras.upper() if letras else "EXT"


def remanejamento_no_dia(
    remanejamentos: list[RemanejamentoLog],
    militar_numero: str,
    dt: date,
    de_ala: int | None = None,
    para_ala: int | None = None,
) -> RemanejamentoLog | None:
    for rem in remanejamentos:
        if rem.militar_numero != militar_numero:
            continue
        if de_ala is not None and rem.de_ala != de_ala:
            continue
        if para_ala is not None and rem.para_ala != para_ala:
            continue
        if parse_data_br(rem.data) == dt:
            return rem
    return None


def resumir_ala(
    militares_ala: list[Militar],
    grade: dict[str, list[CelulaEscala]],
    dias: list[date],
    todos_militares: list[Militar] | None = None,
) -> dict:
    n_dias = len(dias)
    total = [0] * n_dias
    motoristas_d = [0] * n_dias
    oficiais = [0] * n_dias
    subten_sgt = [0] * n_dias
    cb_sd = [0] * n_dias
    sd_2cl = [0] * n_dias

    mapa = {m.numero: m for m in (todos_militares or militares_ala)}

    for num, linha in grade.items():
        militar = mapa.get(num)
        if not militar:
            continue
        for i, cel in enumerate(linha):
            if cel.valor == "S":
                total[i] += 1
                if militar.eh_motorista_d:
                    motoristas_d[i] += 1
                grupo = militar.grupo_posto
                if grupo == "OFICIAIS":
                    oficiais[i] += 1
                elif grupo == "SUBTEN/SGT":
                    subten_sgt[i] += 1
                elif grupo == "SD 2ª CL":
                    sd_2cl[i] += 1
                else:
                    cb_sd[i] += 1

    return {
        "total": total,
        "motoristas_d": motoristas_d,
        "oficiais": oficiais,
        "subten_sgt": subten_sgt,
        "cb_sd": cb_sd,
        "sd_2cl": sd_2cl,
        "n_servico_op": sum(1 for m in militares_ala if any(c.valor == "S" for c in grade.get(m.numero, []))),
        "n_motoristas_d": sum(1 for m in militares_ala if m.eh_motorista_d and any(c.valor == "S" for c in grade.get(m.numero, []))),
    }


# =========================================================================
#                 REGRAS DE REMANEJAMENTO
# =========================================================================

def validar_remanejamento(
    militar: Militar,
    ala_origem: int,
    ala_destino: int,
    data_destino: date,
    mes: int,
    ano: int,
) -> tuple[bool, str, int]:
    """Valida se um remanejamento é permitido.
    Retorna (valido, motivo, folga_horas_sugerida).

    Regras:
    - Ala destino imediatamente seguinte (D+1) = PROIBIDO (folga só 24h).
    - Ala fantasma (oposta, D+2) = permitido como "folga 48h + banco horas" OU "folga 5 dias".
    - Ala D+3 ou anterior (D-1) = permitido normal.
    """
    if ala_origem == ala_destino:
        return (False, "Mesma ala", 72)

    # Dia do último serviço do militar na ala origem ANTES de data_destino
    dias_origem = dias_da_ala(ala_origem, mes, ano)
    dias_origem_antes = [d for d in dias_origem if d <= data_destino]
    if not dias_origem_antes:
        return (True, "Sem serviço anterior na origem", 72)
    ultimo_servico = dias_origem_antes[-1]
    delta = (data_destino - ultimo_servico).days

    if delta == 1:
        return (False, f"Ala destino é o dia seguinte ({ultimo_servico.strftime('%d/%m')} → {data_destino.strftime('%d/%m')}). Militar não pode dobrar serviço (24h apenas).", 24)
    if delta == 2:
        # Ala fantasma — folga 48h
        return (True, f"Ala fantasma (folga de 48h após {ultimo_servico.strftime('%d/%m')}). Recomendado registrar +24h no banco de horas.", 48)
    if delta == 3:
        # Próximo serviço na ala fantasma normal
        return (True, f"Folga normal de 72h até {data_destino.strftime('%d/%m')}.", 72)
    return (True, f"Folga estendida de {delta * 24}h.", 72)


def alas_fantasma(ala: int) -> int:
    """Retorna a ala 'oposta' (fantasma) da ala dada."""
    return ALAS_FANTASMA.get(ala, 0)


# =========================================================================
#                 REDISTRIBUIÇÃO AUTOMÁTICA
# =========================================================================

def diagnosticar(militares: list[Militar], mes: int, ano: int) -> list[dict]:
    """Retorna lista de alertas estruturados sobre a escala.

    Cada alerta: {tipo, severidade, mensagem, ala, data}
    """
    alertas = []
    por_ala = defaultdict(list)
    for m in militares:
        if m.ala in (1, 2, 3, 4):
            por_ala[m.ala].append(m)

    # 0) Equalização do efetivo (Ten Alessandro: alas equalizadas)
    efetivo = {a: len(por_ala[a]) for a in (1, 2, 3, 4)}
    if efetivo and (max(efetivo.values()) - min(efetivo.values()) > 1):
        alertas.append({
            "tipo": "efetivo_desigual",
            "severidade": "media",
            "mensagem": f"Efetivo desigualado entre alas: {efetivo}",
            "ala": None, "data": None,
        })

    # 1) Chefias (2 sargentos mais antigos por ala)
    alertas.extend(validar_chefias(militares))

    # 2) Distribuição de motoristas D
    dist_d = {a: sum(1 for m in por_ala[a] if m.eh_motorista_d) for a in (1, 2, 3, 4)}
    valores = list(dist_d.values())
    if valores and (max(valores) - min(valores) > 1):
        alertas.append({
            "tipo": "balanceamento_d",
            "severidade": "media",
            "mensagem": f"Motoristas categoria D desbalanceados: {dist_d}",
            "ala": None, "data": None,
        })

    # 2) Cobertura por dia
    for ala in (1, 2, 3, 4):
        dias = dias_da_ala(ala, mes, ano)
        for dt in dias:
            ativos = [m for m in por_ala[ala] if not ausencia_no_dia(m, dt)]
            tem_sgt = any(m.grupo_posto == "SUBTEN/SGT" for m in ativos)
            tem_motorista = any(m.eh_motorista_d for m in ativos)
            if not tem_sgt:
                alertas.append({
                    "tipo": "sem_sargento",
                    "severidade": "alta",
                    "mensagem": f"{ala}ª Ala em {dt.strftime('%d/%m')}: sem Sargento de serviço",
                    "ala": ala, "data": dt,
                })
            if not tem_motorista:
                alertas.append({
                    "tipo": "sem_motorista",
                    "severidade": "alta",
                    "mensagem": f"{ala}ª Ala em {dt.strftime('%d/%m')}: sem motorista categoria D",
                    "ala": ala, "data": dt,
                })
            if len(ativos) < 4:
                alertas.append({
                    "tipo": "subdimensionada",
                    "severidade": "media",
                    "mensagem": f"{ala}ª Ala em {dt.strftime('%d/%m')}: apenas {len(ativos)} militares",
                    "ala": ala, "data": dt,
                })
    return alertas


def sugerir_rebalanceamento_d(militares: list[Militar]) -> list[str]:
    """Sugere movimentações de motoristas D para igualar distribuição."""
    por_ala = defaultdict(list)
    for m in militares:
        if m.ala in (1, 2, 3, 4) and m.eh_motorista_d:
            por_ala[m.ala].append(m)
    contagem = {a: len(por_ala[a]) for a in (1, 2, 3, 4)}
    media = sum(contagem.values()) // 4
    sobra = [a for a, v in contagem.items() if v > media]
    falta = [a for a, v in contagem.items() if v < media]
    sugestoes = []
    for a_sob in sobra:
        for a_fal in falta:
            if por_ala[a_sob]:
                m = por_ala[a_sob].pop()
                sugestoes.append(f"Mover {m.posto} {m.nome} da {a_sob}ª Ala para a {a_fal}ª Ala")
    return sugestoes


def aplicar_rebalanceamento_d(militares: list[Militar]) -> int:
    """Move motoristas D entre alas até equilibrar (diferença máx 1). Preferindo
    sempre os mais MODERNOS (último da lista de antiguidade da ala).

    Princípio: menor mudança possível — só move quando estritamente necessário.
    """
    movimentos = 0
    for _ in range(50):
        por_ala = defaultdict(list)
        for m in militares:
            if m.ala in (1, 2, 3, 4) and m.eh_motorista_d:
                por_ala[m.ala].append(m)
        contagem = {a: len(por_ala[a]) for a in (1, 2, 3, 4)}
        if max(contagem.values()) - min(contagem.values()) <= 1:
            break
        a_max = max(contagem, key=contagem.get)
        a_min = min(contagem, key=contagem.get)
        # Move o MAIS MODERNO da ala_max para a_min
        candidatos = sorted(por_ala[a_max], key=lambda x: x.chave_antiguidade, reverse=True)
        if candidatos:
            candidato = candidatos[0]
            candidato.ala = a_min
            movimentos += 1
    return movimentos


def aplicar_rebalanceamento_efetivo(militares: list[Militar]) -> int:
    """Equaliza o número TOTAL de militares por ala, preferindo mover os mais
    MODERNOS. Mantém a continuidade dos mais antigos em suas alas originais.
    """
    movimentos = 0
    for _ in range(50):
        por_ala = defaultdict(list)
        for m in militares:
            if m.ala in (1, 2, 3, 4):
                por_ala[m.ala].append(m)
        contagem = {a: len(por_ala[a]) for a in (1, 2, 3, 4)}
        if max(contagem.values()) - min(contagem.values()) <= 1:
            break
        a_max = max(contagem, key=contagem.get)
        a_min = min(contagem, key=contagem.get)
        # Move o MAIS MODERNO que não seja sgt-chefe nem motorista único
        candidatos = sorted(por_ala[a_max], key=lambda x: x.chave_antiguidade,
                            reverse=True)
        for cand in candidatos:
            # Preserva sargento mais antigo (chefia) e motoristas D únicos
            sgts_da_ala = [m for m in por_ala[a_max]
                           if m.grupo_posto == "SUBTEN/SGT"]
            if cand.grupo_posto == "SUBTEN/SGT" and len(sgts_da_ala) <= 2:
                continue
            mot_da_ala = [m for m in por_ala[a_max] if m.eh_motorista_d]
            if cand.eh_motorista_d and len(mot_da_ala) <= 1:
                continue
            cand.ala = a_min
            movimentos += 1
            break
        else:
            break  # ninguém pode ser movido sem violar regras
    return movimentos


def validar_chefias(militares: list[Militar]) -> list[dict]:
    """Verifica se cada ala tem ao menos 2 sargentos sendo os mais antigos
    como chefias (CH. Serviço e CMT GU)."""
    alertas = []
    por_ala = defaultdict(list)
    for m in militares:
        if m.ala in (1, 2, 3, 4):
            por_ala[m.ala].append(m)
    for ala in (1, 2, 3, 4):
        sgts = [m for m in por_ala[ala] if m.grupo_posto == "SUBTEN/SGT"]
        if len(sgts) < 2:
            alertas.append({
                "tipo": "chefia_insuficiente",
                "severidade": "alta",
                "mensagem": f"{ala}ª Ala tem apenas {len(sgts)} sargento(s). "
                            "É necessário pelo menos 2 (Ch. Serviço e CMT GU).",
                "ala": ala, "data": None,
            })
            continue
        # Verifica que os mais antigos estão como chefes
        sgts_ordenados = sorted(sgts, key=lambda m: m.chave_antiguidade)
        mais_antigos = sgts_ordenados[:2]
        for sgt in mais_antigos:
            f = sgt.funcao.lower()
            if not any(t in f for t in ("ch.", "chefe", "cmt")):
                alertas.append({
                    "tipo": "chefia_fora_antiguidade",
                    "severidade": "media",
                    "mensagem": f"{ala}ª Ala: {sgt.posto} {sgt.nome} é dos mais "
                                "antigos mas não está como chefia.",
                    "ala": ala, "data": None,
                })
                break
    return alertas


def calcular_folgas_pos_cobertura(
    militar_substituto: Militar,
    dias_cobertura: list[date],
    mes: int,
    ano: int,
) -> list[date]:
    """Identifica dias da ALA ORIGINAL do substituto que ficariam dobrados
    (folga < 72h) com algum dia da cobertura — esses dias devem virar FOLGA
    OBRIGATÓRIA automaticamente para evitar dobra de serviço.

    Regra: para CADA dia coberto, verifica os próximos dias da ala original
    que estariam a 1 ou 2 dias de distância (folga 24h ou 48h). Esses são
    marcados como folga obrigatória.

    Nota: 48h também é dobrado para escala 24x72 — embora "ala fantasma"
    aceite 48h com banco de horas EM UM ÚNICO DIA, ao cobrir MÚLTIPLOS dias
    consecutivos isso vira sequência exaustiva. Para simplificar, marcamos
    como folga obrigatória qualquer dia que dê delta < 3 do último dia coberto.
    """
    if not dias_cobertura:
        return []
    dias_ala_orig = dias_da_ala(militar_substituto.ala, mes, ano)
    folgas = set()
    for dia_cob in dias_cobertura:
        for dia_orig in dias_ala_orig:
            if dia_orig == dia_cob:
                continue
            delta = abs((dia_orig - dia_cob).days)
            if delta < 3:  # menos de 72h = dobra serviço
                folgas.add(dia_orig)
    return sorted(folgas)


def encontrar_coberturas_orfas(
    militar_que_estava_ausente: Militar,
    ausencia_removida: Ausencia,
    militares: list[Militar],
) -> list[tuple[Militar, Ausencia]]:
    """Quando uma ausência é deletada, encontra as coberturas criadas para
    suprir essa ausência específica (para que sejam revertidas).
    Inclui também as folgas FR (Folga obrigatória após cobertura adjacente)
    que foram criadas em conjunto.
    """
    ala = militar_que_estava_ausente.ala
    if ala not in (1, 2, 3, 4):
        return []
    tipo_cobertura = f"{ala}ª Ala"
    di_rem = parse_data_br(ausencia_removida.data_inicio)
    df_rem = parse_data_br(ausencia_removida.data_fim) or di_rem
    if not di_rem:
        return []

    nome_chave = (militar_que_estava_ausente.nome or "").lower()

    encontradas = []
    for sub in militares:
        if sub.numero == militar_que_estava_ausente.numero:
            continue
        for aus in list(sub.ausencias):
            obs = (aus.observacao or "").lower()
            # Caso 1: ausência principal de cobertura "Nª Ala"
            if aus.tipo == tipo_cobertura:
                if "cobertura de" not in obs and nome_chave not in obs:
                    continue
                di_c = parse_data_br(aus.data_inicio)
                df_c = parse_data_br(aus.data_fim) or di_c
                if not di_c:
                    continue
                if df_c < di_rem or di_c > df_rem:
                    continue
                encontradas.append((sub, aus))
            # Caso 2: folga obrigatória "FR" associada à cobertura
            elif aus.tipo == "FR":
                if nome_chave not in obs:
                    continue
                di_c = parse_data_br(aus.data_inicio)
                df_c = parse_data_br(aus.data_fim) or di_c
                if not di_c:
                    continue
                # FR pode cair fora do período da ausência original (logo após).
                # Aceita se a observação menciona o militar ausente.
                encontradas.append((sub, aus))
    return encontradas


def calcular_banco_horas_da_cobertura(
    substituto: Militar,
    ausencia_cobertura: Ausencia,
    mes: int,
    ano: int,
) -> int:
    """Retorna o total de minutos extras gerados por uma cobertura específica
    (24h por cada dia que foi cumprido com folga curta de 48h)."""
    di = parse_data_br(ausencia_cobertura.data_inicio)
    df = parse_data_br(ausencia_cobertura.data_fim) or di
    if not di:
        return 0
    try:
        ala_destino = int(ausencia_cobertura.tipo[0])
    except (ValueError, IndexError):
        return 0
    dias_destino = dias_da_ala(ala_destino, mes, ano)
    minutos = 0
    for d in dias_destino:
        if di <= d <= df:
            _, _, folga = validar_remanejamento(
                substituto, substituto.ala, ala_destino, d, mes, ano)
            if folga == 48:
                minutos += 24 * 60
    return minutos


def detectar_vagas_por_ausencia(
    militar_ausente: Militar,
    data_ini: date,
    data_fim: date,
    militares: list[Militar],
    mes: int,
    ano: int,
) -> dict:
    """Analisa o impacto de uma ausência em curso e detecta vagas que ela cria.

    Retorna dict com:
    - ala_origem: int
    - dias_afetados: list[date]  (dias de serviço da ala dentro do período)
    - perde_sargento: bool       (era o sgt mais antigo?)
    - perde_motorista_d: bool
    - efetivo_dias: list[(date, ativos)]
    - sugestao_tipo: 'sargento' | 'motorista' | 'qualquer'
    """
    ala = militar_ausente.ala
    if ala not in (1, 2, 3, 4):
        return {"vagas": False}

    dias_servico = dias_da_ala(ala, mes, ano)
    dias_afetados = [d for d in dias_servico if data_ini <= d <= data_fim]
    if not dias_afetados:
        return {"vagas": False}

    titulares_ala = [m for m in militares if m.ala == ala]
    n_sgts_ativos = sum(1 for m in titulares_ala
                        if m.grupo_posto == "SUBTEN/SGT"
                        and m.numero != militar_ausente.numero)
    n_mot_d_ativos = sum(1 for m in titulares_ala
                         if m.eh_motorista_d
                         and m.numero != militar_ausente.numero)

    # Efetivo por dia (considerando outras ausências)
    efetivo_dias = []
    for dt in dias_afetados:
        ativos = [
            m for m in titulares_ala
            if m.numero != militar_ausente.numero and not ausencia_no_dia(m, dt)
        ]
        efetivo_dias.append((dt, len(ativos)))

    perde_sargento = (
        militar_ausente.grupo_posto == "SUBTEN/SGT" and n_sgts_ativos < 2
    )
    perde_motorista_d = (
        militar_ausente.eh_motorista_d and n_mot_d_ativos == 0
    )

    # Decide o tipo padrão de cobertura
    if perde_sargento:
        sugestao_tipo = "sargento"
    elif perde_motorista_d:
        sugestao_tipo = "motorista"
    else:
        sugestao_tipo = "qualquer"

    return {
        "vagas": True,
        "ala_origem": ala,
        "dias_afetados": dias_afetados,
        "perde_sargento": perde_sargento,
        "perde_motorista_d": perde_motorista_d,
        "efetivo_dias": efetivo_dias,
        "sugestao_tipo": sugestao_tipo,
        "n_sgts_restantes": n_sgts_ativos,
        "n_mot_d_restantes": n_mot_d_ativos,
    }


def sugerir_substitutos_periodo(
    militares: list[Militar],
    ala_origem: int,
    dias_afetados: list[date],
    mes: int,
    ano: int,
    requisito: str = "qualquer",
    militar_ausente_numero: str = "",
    origem_ala: str = "qualquer",
) -> list[tuple[Militar, str]]:
    """Sugere militares de outras alas para cobrir TODO o período.

    Parâmetro `origem_ala`:
    - "qualquer": qualquer ala disponível (default)
    - "fantasma": apenas a ala fantasma (oposta)
    - "adjacente": apenas alas que NÃO sejam a fantasma (folga normal 72h+)
    """
    ala_fantasma = ALAS_FANTASMA.get(ala_origem)
    candidatos = []
    for m in militares:
        if m.numero == militar_ausente_numero:
            continue
        if m.ala == ala_origem or m.ala == 0:
            continue
        # Filtro por origem da ala
        if origem_ala == "fantasma" and m.ala != ala_fantasma:
            continue
        if origem_ala == "adjacente" and m.ala == ala_fantasma:
            continue
        # Filtro por requisito
        if requisito == "sargento" and m.grupo_posto != "SUBTEN/SGT":
            continue
        if requisito == "motorista" and not m.eh_motorista_d:
            continue
        if requisito == "cb_sd" and m.grupo_posto != "CB/SD":
            continue
        # Não desfalca chefes da ala dele
        f = (m.funcao or "").lower()
        if any(t in f for t in ("ch. serviço", "ch.serviço", "chefe")):
            continue
        # Verifica que não tem ausência nos dias afetados
        if any(ausencia_no_dia(m, d) for d in dias_afetados):
            continue
        # Verifica que cada dia é remanejamento válido (nunca D+1)
        ok = True
        houve_banco_horas = False
        for d in dias_afetados:
            valido, motivo, folga = validar_remanejamento(
                m, m.ala, ala_origem, d, mes, ano)
            if not valido:
                ok = False; break
            if folga == 48:
                houve_banco_horas = True
        if not ok:
            continue
        # Score (menor = melhor)
        score = 0
        if origem_ala == "qualquer":
            # default: ala fantasma primeiro
            if m.ala == ala_fantasma:
                score -= 1000
        # Mais moderno = pontuação mais negativa
        score += -m.chave_antiguidade[0] * 10 - m.chave_antiguidade[1]
        if houve_banco_horas:
            score += 100
        # Identificador da ala
        tipo_ala = ""
        if m.ala == ala_fantasma:
            tipo_ala = " (fantasma)"
        else:
            tipo_ala = " (adjacente)"
        motivo = (f"{m.ala}ª Ala{tipo_ala}  •  Cobre {len(dias_afetados)} dia(s)"
                  + (f"  •  ⚠ banco de horas" if houve_banco_horas else ""))
        candidatos.append((score, m, motivo))
    candidatos.sort(key=lambda t: t[0])
    return [(c[1], c[2]) for c in candidatos]


def sugerir_remanejamento_minimo(
    militares: list[Militar],
    ala_descoberta: int,
    data,
    mes: int,
    ano: int,
    requisito: str = "qualquer",
) -> list[tuple[Militar, str, int]]:
    """Sugere a MENOR mudança possível para cobrir vaga:
    - Primeiro tenta militares da ala fantasma (impacto mínimo)
    - Depois militares mais modernos das outras alas
    - Evita militares-chave (chefes, único motorista)
    """
    candidatos = []
    ala_fantasma = ALAS_FANTASMA.get(ala_descoberta)
    for m in militares:
        if m.ala == ala_descoberta or m.ala == 0:
            continue
        if requisito == "sargento" and m.grupo_posto != "SUBTEN/SGT":
            continue
        if requisito == "motorista" and not m.eh_motorista_d:
            continue
        if ausencia_no_dia(m, data):
            continue
        # Evita mover chefias
        f = m.funcao.lower()
        eh_chefe = any(t in f for t in ("ch. serviço", "cmt. gu"))
        if eh_chefe and requisito != "sargento":
            continue
        valido, motivo, folga = validar_remanejamento(
            m, m.ala, ala_descoberta, data, mes, ano)
        if not valido:
            continue
        # Score: ala fantasma + mais moderno = melhor
        score = 0
        if m.ala == ala_fantasma:
            score -= 1000  # forte preferência por ala fantasma
        score -= m.chave_antiguidade[0] * 10  # mais moderno tem score mais baixo
        score -= m.chave_antiguidade[1]
        # Penaliza se folga curta (banco horas)
        if folga == 48:
            score += 50
        candidatos.append((score, m, motivo, folga))
    candidatos.sort(key=lambda t: t[0])
    return [(c[1], c[2], c[3]) for c in candidatos]


def militares_por_antiguidade(militares: list[Militar]) -> list[Militar]:
    """Retorna lista ordenada do mais antigo para o mais moderno."""
    return sorted(militares, key=lambda m: m.chave_antiguidade)


def sugerir_cobertura(
    militares: list[Militar],
    ala_descoberta: int,
    data: date,
    mes: int,
    ano: int,
    requisito: str = "qualquer",  # "sargento" | "motorista" | "qualquer"
) -> list[tuple[Militar, str, int]]:
    """Sugere militares disponíveis para cobrir uma vaga (mais MODERNOS primeiro).
    Retorna lista de (militar, motivo, folga_horas)."""
    candidatos = []
    for m in militares:
        if m.ala == ala_descoberta or m.ala == 0:
            continue
        if requisito == "sargento" and m.grupo_posto != "SUBTEN/SGT":
            continue
        if requisito == "motorista" and not m.eh_motorista_d:
            continue
        if ausencia_no_dia(m, data):
            continue
        valido, motivo, folga = validar_remanejamento(
            m, m.ala, ala_descoberta, data, mes, ano)
        if valido:
            candidatos.append((m, motivo, folga))
    # Preferir mais modernos (chave_antiguidade maior)
    candidatos.sort(key=lambda t: t[0].chave_antiguidade, reverse=True)
    return candidatos
