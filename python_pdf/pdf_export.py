"""Gerador de PDF da escala, replicando fielmente o layout oficial do CBMMG
com TODAS as cores, itálicos, fundos coloridos e formatação do Excel original.
"""
from __future__ import annotations
from datetime import date, datetime, timedelta
from pathlib import Path
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.platypus import (
    SimpleDocTemplate, Table, TableStyle, Paragraph, Spacer,
    Image, PageBreak,
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT, TA_JUSTIFY

from models import Militar, EscalaMensal, AlaConfig
from escala_logic import (
    dias_da_ala, nome_dia_semana, montar_grade_ala, resumir_ala,
    parse_data_br, ausencia_no_dia,
)

BASE_DIR = Path(__file__).parent
LOGO_PATH = BASE_DIR / "assets" / "cbmmg_logo.png"

MESES_PT = ["", "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO",
            "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO"]
MES_ABREV = ["", "Jan", "Fev", "Mar", "Abr", "Mai", "Jun",
             "Jul", "Ago", "Set", "Out", "Nov", "Dez"]


# ============== PALETA DE CORES OFICIAL CBMMG (extraída do Excel) ==============
COR_HEADER_TAB = colors.HexColor("#D9D9D9")      # cinza claro dos cabeçalhos
COR_HEADER_CINZA_ESCURO = colors.HexColor("#BFBFBF")

# Cores de status nas células de dias
COR_FA = colors.HexColor("#ED7D31")              # laranja forte para FA/FP
COR_LD = colors.HexColor("#C00000")              # vermelho para Licença/Dispensa
COR_FOLGA_FD = colors.HexColor("#00B0F0")        # azul claro FD
COR_FOLGA_FN = colors.HexColor("#FFFF00")        # amarelo FN
COR_O = colors.HexColor("#FFC000")               # amarelo escuro - Outro
COR_MO = colors.HexColor("#BF8F00")              # amarelo escuro - Movimentado
COR_T = colors.HexColor("#7B7B7B")               # cinza - Trânsito
COR_LN = colors.HexColor("#F2F2F2")              # cinza claro - LN
COR_REMANEJ_ALA = colors.HexColor("#5B9BD5")     # azul forte para "Nª Ala"
COR_ALA_ORIGEM = colors.HexColor("#E7E6E6")      # cinza muito claro para visitante fora
COR_S_FUNDO = colors.white                       # branco para S normal

# Cores de texto
COR_TEXTO_S = colors.HexColor("#1F4E79")         # azul escuro para letras S
COR_DATAS = colors.HexColor("#C00000")           # vermelho escuro para datas
COR_OBS_VERMELHO = colors.HexColor("#C00000")    # vermelho das observações
COR_FUNCAO_AZUL = colors.HexColor("#1F4E79")     # azul para funções (CMT GU, Armador)
COR_FUNCAO_VERMELHO = colors.HexColor("#C00000") # vermelho para Motorista

# Cores do resumo (rodapé da ala) - padrão CBMMG
COR_RESUMO_HEADER = colors.HexColor("#F2F2F2")
COR_RESUMO_VERDE_CLARO = colors.HexColor("#A9D08E")  # DIA - verde claro
COR_RESUMO_VERDE_ESCURO = colors.HexColor("#70AD47") # NOITE - verde escuro
COR_RESUMO_TEXTO = colors.HexColor("#C00000")        # vermelho para TOTAL

# Cores da legenda — padrão CBMMG (idêntico ao Excel original)
COR_LEGENDA_HEAD = colors.HexColor("#D9D9D9")    # cinza claro do header
LEGENDA_CORES = {
    "S":  (colors.white, colors.black),                     # branco
    "R":  (colors.white, colors.black),                     # branco
    "D":  (colors.HexColor("#FF0000"), colors.white),       # vermelho
    "L":  (colors.HexColor("#BFBFBF"), colors.black),       # cinza
    "FD": (colors.HexColor("#00B0F0"), colors.black),       # azul claro
    "FN": (colors.HexColor("#FFFF00"), colors.black),       # amarelo
    "FR": (colors.HexColor("#92D050"), colors.black),       # verde claro
    "FA": (colors.HexColor("#ED7D31"), colors.white),       # laranja
    "FP": (colors.HexColor("#ED7D31"), colors.white),       # laranja (mesma cor de FA)
    "LN": (colors.HexColor("#ED7D31"), colors.white),       # laranja (mesma cor de FA)
    "T":  (colors.HexColor("#FF00FF"), colors.black),       # magenta/rosa
    "O":  (colors.HexColor("#8B0000"), colors.white),       # vermelho vinho
    "MO": (colors.HexColor("#FFFF00"), colors.black),       # amarelo
    "1ª Ala": (colors.HexColor("#5B9BD5"), colors.white),
    "2ª Ala": (colors.HexColor("#5B9BD5"), colors.white),
    "3ª Ala": (colors.HexColor("#5B9BD5"), colors.white),
    "4ª Ala": (colors.HexColor("#5B9BD5"), colors.white),
}


def _cor_celula(valor: str, cor_estilo: str):
    """Define (cor_fundo, cor_texto, eh_bold) para uma célula da escala."""
    if not valor:
        return (None, None, False)
    if valor in LEGENDA_CORES:
        cor_fundo, cor_texto = LEGENDA_CORES[valor]
        return (cor_fundo, cor_texto, valor not in ("S", "R"))
    if valor in ("FA", "FP"):
        return (COR_FA, colors.white, True)
    if valor in ("L", "D"):
        return (COR_LD, colors.white, True)
    if valor == "FD":
        return (COR_FOLGA_FD, colors.white, True)
    if valor == "FN":
        return (COR_FOLGA_FN, colors.black, True)
    if valor == "FR":
        # Folga obrigatória após cobertura adjacente — tom verde claro
        return (colors.HexColor("#92D050"), colors.black, True)
    if valor == "O":
        return (COR_O, colors.black, True)
    if valor == "MO":
        return (COR_MO, colors.white, True)
    if valor == "T":
        return (COR_T, colors.white, True)
    if valor == "LN":
        return (COR_LN, colors.black, True)
    if cor_estilo == "unidade_destino":
        return (COR_REMANEJ_ALA, colors.white, True)
    if valor.endswith("ª Ala"):
        if cor_estilo == "ala_origem":
            return (COR_ALA_ORIGEM, colors.HexColor("#7B7B7B"), False)
        return (COR_REMANEJ_ALA, colors.white, True)
    return (None, None, False)


def _funcao_cor(funcao: str):
    """Retorna cor para o texto da função."""
    fl = funcao.lower()
    if "motorista" in fl:
        return COR_FUNCAO_VERMELHO
    if any(w in fl for w in ("cmt", "armador", "ch.", "chefe", "aux")):
        return COR_FUNCAO_AZUL
    return colors.black


# ----------------------- Estilos -----------------------
def _styles():
    ss = getSampleStyleSheet()
    if "EBM_TituloUnidade" not in ss.byName:
        ss.add(ParagraphStyle(name="EBM_TituloUnidade", parent=ss["Title"],
                              fontName="Helvetica-Bold", fontSize=12,
                              alignment=TA_CENTER, spaceAfter=2, leading=14))
        ss.add(ParagraphStyle(name="EBM_SubTitulo", parent=ss["Title"],
                              fontName="Helvetica-Bold", fontSize=11,
                              alignment=TA_CENTER, spaceAfter=6, leading=13))
        ss.add(ParagraphStyle(name="EBM_SecaoTitulo", parent=ss["Heading3"],
                              fontName="Helvetica-BoldOblique", fontSize=10,
                              alignment=TA_CENTER, spaceAfter=2, leading=12))
        ss.add(ParagraphStyle(name="EBM_AlaTitulo", parent=ss["Heading3"],
                              fontName="Helvetica-BoldOblique", fontSize=11,
                              alignment=TA_CENTER, spaceAfter=4, leading=13))
        ss.add(ParagraphStyle(name="EBM_ObsItem", parent=ss["Normal"],
                              fontName="Helvetica", fontSize=8,
                              alignment=TA_LEFT, leftIndent=12, leading=11))
        ss.add(ParagraphStyle(name="EBM_ObsItemBold", parent=ss["Normal"],
                              fontName="Helvetica-Bold", fontSize=8,
                              alignment=TA_LEFT, leftIndent=12, leading=11))
        ss.add(ParagraphStyle(name="EBM_Assinatura", parent=ss["Normal"],
                              fontName="Helvetica-Bold", fontSize=10,
                              alignment=TA_CENTER, leading=12))
        ss.add(ParagraphStyle(name="EBM_DataCidade", parent=ss["Normal"],
                              fontName="Helvetica-BoldOblique", fontSize=10,
                              alignment=TA_RIGHT, leading=12))
        ss.add(ParagraphStyle(name="EBM_Homologo", parent=ss["Normal"],
                              fontName="Helvetica-Bold", fontSize=10,
                              alignment=TA_LEFT, leading=12))
        ss.add(ParagraphStyle(name="EBM_ObsVermelho", parent=ss["Normal"],
                              fontName="Helvetica", fontSize=7,
                              textColor=COR_OBS_VERMELHO,
                              alignment=TA_LEFT, leading=8))
        ss.add(ParagraphStyle(name="EBM_Pequeno", parent=ss["Normal"],
                              fontName="Helvetica", fontSize=7, leading=8,
                              alignment=TA_LEFT))
    return ss


# ----------------------- Cabeçalho da página 1 -----------------------
def _header_unidade(escala: EscalaMensal, com_logo: bool = True):
    ss = _styles()
    items = []
    if com_logo and LOGO_PATH.exists():
        img = Image(str(LOGO_PATH), width=26*mm, height=26*mm)
        img.hAlign = "CENTER"
        items.append(img)
        items.append(Spacer(1, 3))
    items.append(Paragraph(f"<b>{escala.unidade}</b>", ss["EBM_TituloUnidade"]))
    items.append(Paragraph(
        f"<b>ESCALA MENSAL - {MESES_PT[escala.mes]} {escala.ano}</b>",
        ss["EBM_SubTitulo"],
    ))
    return items


# ----------------------- Observação formatada -----------------------
def _format_obs_ausencias(ausencias, mes: int | None = None, ano: int | None = None) -> str:
    textos = []
    for a in ausencias:
        if mes is not None and ano is not None and not _ausencia_sobrepoe_periodo(a, mes, ano):
            continue
        if getattr(a, "cobertura_automatica", False) or _ala_num_from_tipo(a.tipo) is not None:
            texto = _format_obs_remanejamento(a.observacao)
        else:
            texto = _format_obs_ausencia(a)
        if texto:
            textos.append(texto)
    return "; ".join(textos)


def _format_obs_linha_ala(
    militar: Militar,
    ala: int,
    remanejamentos,
    mes: int,
    ano: int,
    todos_militares: list[Militar] | None = None,
) -> str:
    textos = []
    for a in militar.ausencias:
        if not _ausencia_sobrepoe_periodo(a, mes, ano):
            continue
        ala_tipo = _ala_num_from_tipo(a.tipo)
        if ala_tipo is None:
            texto = _format_obs_ausencia(a)
        elif ala_tipo == ala:
            texto = _format_obs_remanejamento(a.observacao)
        else:
            texto = ""
        if texto:
            textos.append(texto)

    for rem in remanejamentos or []:
        if rem.militar_numero != militar.numero or rem.para_ala != ala:
            continue
        dt = parse_data_br(rem.data)
        if not dt or dt.month != mes or dt.year != ano:
            continue
        texto = _format_obs_remanejamento(rem.motivo)
        if texto == "Remanejado para cobrir":
            motivo = _motivo_ausencia_coberta(rem, todos_militares or [])
            if motivo:
                texto = f"{texto} - {motivo}"
        if texto:
            textos.append(texto)

    textos = list(dict.fromkeys(textos))
    if any(t.startswith("Remanejado para cobrir -") for t in textos):
        textos = [t for t in textos if t != "Remanejado para cobrir"]
    return "; ".join(textos)


def _ala_num_from_tipo(tipo: str | None) -> int | None:
    if not tipo:
        return None
    tipo = tipo.strip()
    if len(tipo) >= 6 and tipo[0] in "1234" and tipo.endswith("ª Ala"):
        return int(tipo[0])
    return None


def _format_obs_remanejamento(texto: str | None) -> str:
    texto = (texto or "").strip()
    prefixo = "Remanejado para cobrir"
    if texto.lower().startswith(prefixo.lower()):
        return texto
    return prefixo


def _motivo_ausencia_coberta(rem, todos_militares: list[Militar]) -> str:
    dt = parse_data_br(rem.data)
    if not dt:
        return ""
    for militar in todos_militares:
        if militar.ala != rem.para_ala:
            continue
        ausencia = ausencia_no_dia(militar, dt)
        if not ausencia:
            continue
        if getattr(ausencia, "cobertura_automatica", False):
            continue
        if _ala_num_from_tipo(ausencia.tipo) is not None:
            continue
        return (ausencia.tipo or "").strip()
    return ""


def _ausencia_sobrepoe_periodo(a, mes: int, ano: int) -> bool:
    inicio = parse_data_br(a.data_inicio)
    fim = parse_data_br(a.data_fim) or inicio
    if not inicio or not fim:
        return True

    periodo_inicio = date(ano, mes, 1)
    proximo_mes = date(ano + (1 if mes == 12 else 0), 1 if mes == 12 else mes + 1, 1)
    periodo_fim = proximo_mes - timedelta(days=1)
    return inicio <= periodo_fim and fim >= periodo_inicio


def _format_obs_ausencia(a) -> str:
    rotulos = {"FA": "Férias anuais", "FP": "Férias prêmio",
               "L": "Licença", "D": "Dispensa",
               "FD": "Folga diurna", "FN": "Folga noturna",
               "FR": "Folga obrigatória", "LN": "Licença núpcias",
               "T": "Trânsito", "O": "Outro", "MO": "Movimentado",
               "1ª Ala": "Remanejamento 1ª Ala",
               "2ª Ala": "Remanejamento 2ª Ala",
               "3ª Ala": "Remanejamento 3ª Ala",
               "4ª Ala": "Remanejamento 4ª Ala"}
    rotulo = rotulos.get(a.tipo, "")
    obs = (a.observacao or "").strip()
    if rotulo and obs.lower().startswith(rotulo.lower()):
        texto = obs
    else:
        texto = f"{rotulo} - {obs}" if obs and rotulo else (obs or rotulo)
    if a.data_inicio:
        sep = " " if texto and not texto.endswith("-") else ""
        texto = f"{texto}{sep}{a.data_inicio} a {a.data_fim}".strip()
    return texto


def _extrair_nome_guerra(nome_completo: str) -> str:
    if not nome_completo:
        return ""
    palavras = nome_completo.split()
    caps = [p for p in palavras
            if p.isupper() and p not in ("DE", "DA", "DO", "DOS", "DAS", "E")]
    if caps:
        return caps[0].capitalize()
    return palavras[-1].capitalize()


# ----------------------- Tabelas Administração / GPV -----------------------
def _tabela_secao(titulo: str, militares: list[Militar]) -> list:
    ss = _styles()
    out = [Paragraph(f"<b><i>{titulo}</i></b>", ss["EBM_SecaoTitulo"])]
    headers = ["NÚMERO", "POSTO / GRADUAÇÃO", "NOME", "MOT.", "FUNÇÃO", "OBSERVAÇÕES"]
    data = [headers]

    for i, m in enumerate(militares, start=1):
        obs_text = _format_obs_ausencias(m.ausencias)
        obs_par = (Paragraph(obs_text, ss["EBM_ObsVermelho"]) if obs_text
                   else Paragraph(m.observacoes or "", ss["EBM_Pequeno"]))
        data.append([m.numero, m.posto, m.nome, m.categoria_cnh, m.funcao, obs_par])

    larguras = [22*mm, 32*mm, 75*mm, 12*mm, 45*mm, 65*mm]
    t = Table(data, colWidths=larguras, repeatRows=1)
    style = TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), COR_HEADER_TAB),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTNAME", (0, 1), (-1, -1), "Helvetica"),
        ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ALIGN", (2, 1), (2, -1), "LEFT"),
        ("ALIGN", (4, 1), (4, -1), "CENTER"),
        ("ALIGN", (5, 1), (5, -1), "LEFT"),
        ("GRID", (0, 0), (-1, -1), 0.5, colors.black),
        ("LEFTPADDING", (0, 0), (-1, -1), 3),
        ("RIGHTPADDING", (0, 0), (-1, -1), 3),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ])
    t.setStyle(style)
    out.append(t)
    out.append(Spacer(1, 8))
    return out


def _tabela_esforco(escala: EscalaMensal, militares: list[Militar]) -> list:
    ss = _styles()
    out = [Paragraph(
        "<b><i>ESCALA DE 2º ESFORÇO PARA OS MILITARES DA ADMINISTRAÇÃO E GPV</i></b>",
        ss["EBM_SecaoTitulo"],
    )]
    headers = ["MILITAR EMPENHADO", "DE", "ATÉ"]
    data = [headers]
    mapa = {m.numero: m for m in militares}
    for e in sorted(escala.escala_2esforco, key=lambda item: _parse_cbmmg(item.get("de", "")) or datetime.max):
        nome = ""
        m = mapa.get(e.get("militar_numero", ""))
        if m:
            ng = m.nome_guerra.strip() if m.nome_guerra else ""
            if not ng:
                ng = _extrair_nome_guerra(m.nome)
            nome = f"{m.posto} BM {ng}"
        else:
            nome = e.get("nome_manual", "")
        data.append([nome, e.get("de", ""), e.get("ate", "")])
    larguras = [70*mm, 50*mm, 50*mm]
    t = Table(data, colWidths=larguras, repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), COR_HEADER_TAB),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTNAME", (0, 1), (-1, -1), "Helvetica"),
        ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("ALIGN", (0, 1), (0, -1), "LEFT"),
        ("GRID", (0, 0), (-1, -1), 0.5, colors.black),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 3),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3),
    ]))
    composto = Table([[t]], colWidths=[170*mm])
    composto.setStyle(TableStyle([("ALIGN", (0, 0), (-1, -1), "CENTER")]))
    out.append(composto)
    return out


def _parse_cbmmg(value: str) -> datetime | None:
    try:
        principal = value.split("-")[0].strip()
        dd = int(principal[:2])
        hh = int(principal[2:4])
        mmn = int(principal[4:6])
        mes_abrev = principal[6:9]
        yy = int(principal[9:11])
        mes = next(i for i, nome in enumerate(MES_ABREV) if nome.lower() == mes_abrev.lower())
        return datetime(2000 + yy, mes, dd, hh, mmn)
    except Exception:
        return None


def _militares_com_composicoes_externas(todos_militares, composicoes, ala: int, mes: int, ano: int):
    lista = list(todos_militares)
    numeros = {m.numero for m in lista}
    for comp in composicoes or []:
        if getattr(comp, "papel_local", "").lower() != "destino" or getattr(comp, "ala", 0) != ala:
            continue
        dt = parse_data_br(getattr(comp, "data", ""))
        if not dt or dt.month != mes or dt.year != ano:
            continue
        numero = getattr(comp, "militar_numero", "")
        if not numero or numero in numeros:
            continue
        lista.append(Militar(
            numero=numero,
            posto=getattr(comp, "militar_posto", ""),
            nome=getattr(comp, "militar_nome", ""),
            categoria_cnh=getattr(comp, "militar_cnh", "-") or "-",
            funcao=getattr(comp, "militar_funcao", ""),
            secao="OPERACIONAL",
            ala=0,
            ordem=999,
            observacoes=f"Origem: {getattr(comp, 'origem_nome', '')}",
        ))
        numeros.add(numero)
    return lista


# ----------------------- Tabela de uma ALA Operacional -----------------------
def _tabela_ala(ala: AlaConfig, militares_ala: list[Militar],
                todos_militares: list[Militar], mes: int, ano: int,
                ala_obs: list[str] | None = None,
                remanejamentos=None,
                composicoes_unidade=None) -> list:
    ss = _styles()
    titulo_str = f"{ala.numero}ª ALA OPERACIONAL – {MESES_PT[mes].upper()} / {ano}"
    out = [Paragraph(f"<b><i>{titulo_str}</i></b>", ss["EBM_AlaTitulo"])]

    todos_com_externos = _militares_com_composicoes_externas(
        todos_militares, composicoes_unidade or [], ala.numero, mes, ano
    )
    dias, grade = montar_grade_ala(
        militares_ala, todos_com_externos, ala.numero, mes, ano, remanejamentos, composicoes_unidade
    )
    n_dias = len(dias)

    # Cabeçalho 2 linhas
    linha0 = ["DIAS DA SEMANA", "", "", "", ""] \
        + [nome_dia_semana(d) for d in dias] \
        + ["FUNÇÃO", "OBSERVAÇÕES"]
    linha1 = ["ORD.", "Nº", "P/G", "NOME", "MOT\nCAT"] \
        + [f"{d.day}/{MES_ABREV[d.month].lower()}." for d in dias] \
        + ["", ""]
    data = [linha0, linha1]

    mapa_todos = {m.numero: m for m in todos_com_externos}
    numeros_ala = [m.numero for m in militares_ala]
    numeros_visitantes = [n for n in grade.keys() if n not in numeros_ala]

    # Ordena titulares e visitantes JUNTOS por antiguidade (regra militar)
    todos_da_ala = list(militares_ala) + \
        [mapa_todos[n] for n in numeros_visitantes if n in mapa_todos]
    todos_da_ala.sort(key=lambda m: m.chave_antiguidade)
    linhas_render: list[Militar] = todos_da_ala

    estilo_cells = []
    # Numera apenas os TITULARES (1, 2, 3...) — visitantes ficam com "-"
    contador_titular = 0
    set_titulares = set(numeros_ala)
    for idx, m in enumerate(linhas_render, start=1):
        celulas = grade.get(m.numero, [])
        eh_titular = m.numero in set_titulares
        if eh_titular:
            contador_titular += 1
            ord_str = str(contador_titular)
        else:
            ord_str = "-"
        row = [
            ord_str,
            m.numero, m.posto, m.nome, m.categoria_cnh,
        ]
        # Valores das células (S em itálico + azul é aplicado via TableStyle)
        for cel in celulas:
            row.append(cel.valor or "")

        obs_text = _format_obs_linha_ala(
            m, ala.numero, remanejamentos, mes, ano, todos_militares
        )
        row.append(m.funcao)
        row.append(Paragraph(obs_text, ss["EBM_ObsVermelho"]) if obs_text
                   else Paragraph("", ss["EBM_Pequeno"]))
        data.append(row)

        linha_idx = idx + 1  # +1 por causa do linha0+linha1 do header (cabeçalho ocupa 2 linhas)

        # Colorir células de status (S, FA, 2ª Ala, etc.)
        for j, cel in enumerate(celulas):
            col = 5 + j
            if cel.valor == "S":
                # Letra S em itálico preto (padrão do original)
                estilo_cells.append(("FONTNAME", (col, linha_idx), (col, linha_idx), "Helvetica-Oblique"))
                continue
            cor_fundo, cor_texto, eh_bold = _cor_celula(cel.valor, cel.cor)
            if cor_fundo is not None:
                estilo_cells.append(("BACKGROUND", (col, linha_idx), (col, linha_idx), cor_fundo))
            if cor_texto is not None:
                estilo_cells.append(("TEXTCOLOR", (col, linha_idx), (col, linha_idx), cor_texto))
            if eh_bold:
                estilo_cells.append(("FONTNAME", (col, linha_idx), (col, linha_idx), "Helvetica-Bold"))

    larguras = [8*mm, 15*mm, 13*mm, 55*mm, 10*mm] + [11*mm] * n_dias + [22*mm, 36*mm]
    t = Table(data, colWidths=larguras, repeatRows=2)
    style = TableStyle([
        ("BACKGROUND", (0, 0), (-1, 1), COR_HEADER_TAB),
        ("SPAN", (0, 0), (4, 0)),
        ("SPAN", (5 + n_dias, 0), (5 + n_dias, 1)),
        ("SPAN", (6 + n_dias, 0), (6 + n_dias, 1)),
        ("FONTNAME", (0, 0), (-1, 1), "Helvetica-Bold"),
        ("FONTNAME", (0, 2), (-1, -1), "Helvetica"),
        ("FONTSIZE", (0, 0), (-1, -1), 6.5),
        ("FONTSIZE", (0, 0), (-1, 1), 7),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("ALIGN", (3, 2), (3, -1), "LEFT"),
        ("GRID", (0, 0), (-1, -1), 0.5, colors.black),
        ("LEFTPADDING", (0, 0), (-1, -1), 2),
        ("RIGHTPADDING", (0, 0), (-1, -1), 2),
        ("TOPPADDING", (0, 0), (-1, -1), 2),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 2),
    ])
    for cmd in estilo_cells:
        style.add(*cmd)
    t.setStyle(style)
    out.append(t)

    out.append(Spacer(1, 4))
    out.append(_resumo_e_legenda_ala(militares_ala, grade, dias, ala_obs, todos_com_externos))
    out.append(Spacer(1, 6))
    return out


# ----------------------- Resumo (verde + texto vermelho) + Legenda colorida ----
def _resumo_e_legenda_ala(militares_ala, grade, dias, observacoes_ala=None, todos_militares=None):
    r = resumir_ala(militares_ala, grade, dias, todos_militares)
    n = len(dias)

    blocos = [
        ("BM'S SERVIÇO OPERACIONAL", r["n_servico_op"], r["total"], True),
        ("MOTORISTAS CATEGORIA \"D\"", r["n_motoristas_d"], r["motoristas_d"], True),
        ("OFICIAIS",
         sum(1 for m in militares_ala if m.grupo_posto == "OFICIAIS"),
         r["oficiais"], False),
        ("SUBTEN/SGT",
         sum(1 for m in militares_ala if m.grupo_posto == "SUBTEN/SGT"),
         r["subten_sgt"], False),
        ("CB/SD",
         sum(1 for m in militares_ala if m.grupo_posto == "CB/SD"),
         r["cb_sd"], False),
        ("SD 2ª CL",
         sum(1 for m in militares_ala if m.grupo_posto == "SD 2ª CL"),
         r["sd_2cl"], False),
    ]

    rows = []
    tipo_linhas = []  # 'total' | 'dia' | 'noite' | 'obs_head' | 'obs_linha'
    spans_verticais = []
    linha_corrente = 0
    for nome, total_cat, totais, com_dia_noite in blocos:
        rows.append([nome, total_cat, "TOTAL:"] + list(totais))
        tipo_linhas.append("total")
        if com_dia_noite:
            rows.append(["", "", "DIA:"] + list(totais))
            tipo_linhas.append("dia")
            rows.append(["", "", "NOITE:"] + list(totais))
            tipo_linhas.append("noite")
            spans_verticais.append(("SPAN", (0, linha_corrente), (0, linha_corrente + 2)))
            spans_verticais.append(("SPAN", (1, linha_corrente), (1, linha_corrente + 2)))
            linha_corrente += 3
        else:
            linha_corrente += 1

    # Linha cabeçalho "OBSERVAÇÕES GERAIS"
    linha_obs_head = linha_corrente
    rows.append(["OBSERVAÇÕES GERAIS"] + [""] * (2 + n))
    tipo_linhas.append("obs_head")
    spans_verticais.append(("SPAN", (0, linha_obs_head), (-1, linha_obs_head)))
    linha_corrente += 1

    # Linhas brancas para observações da ala (preenchidas se houver, brancas se vazias).
    # Quantidade calculada para alinhar com a altura total da legenda.
    NUM_LINHAS_LEGENDA = 18
    n_obs_lines = max(0, NUM_LINHAS_LEGENDA - (linha_corrente))
    obs_list = list(observacoes_ala or [])
    for i in range(n_obs_lines):
        texto = obs_list[i] if i < len(obs_list) else ""
        rows.append([texto] + [""] * (2 + n))
        tipo_linhas.append("obs_linha")
        spans_verticais.append(("SPAN", (0, linha_corrente), (-1, linha_corrente)))
        linha_corrente += 1

    ALTURA_LINHA = 4.2 * mm
    larguras = [(8 + 15 + 13)*mm, 55*mm, 10*mm] + [11*mm] * n
    t_resumo = Table(rows, colWidths=larguras,
                     rowHeights=[ALTURA_LINHA] * len(rows))
    style_resumo = TableStyle([
        ("FONTSIZE", (0, 0), (-1, -1), 6.5),
        ("FONTNAME", (0, 0), (1, -1), "Helvetica-Bold"),
        ("FONTNAME", (2, 0), (2, -1), "Helvetica-Bold"),
        ("FONTNAME", (3, 0), (-1, -1), "Helvetica-Bold"),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("GRID", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEBEFORE", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEAFTER", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEBELOW", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEABOVE", (0, 0), (-1, -1), 1.0, colors.black),
        # Categoria (col 0) + número (col 1): fundo cinza, número em vermelho
        ("BACKGROUND", (0, 0), (1, linha_obs_head - 1), COR_RESUMO_HEADER),
        ("TEXTCOLOR", (1, 0), (1, linha_obs_head - 1), COR_RESUMO_TEXTO),
        ("LEFTPADDING", (0, 0), (-1, -1), 2),
        ("RIGHTPADDING", (0, 0), (-1, -1), 2),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
    ])
    # Aplica cor por tipo de linha (rótulo TOTAL/DIA/NOITE + valores)
    for i, tipo in enumerate(tipo_linhas):
        if tipo == "total":
            style_resumo.add("BACKGROUND", (2, i), (-1, i), colors.white)
            style_resumo.add("TEXTCOLOR", (2, i), (-1, i), COR_RESUMO_TEXTO)
        elif tipo == "dia":
            style_resumo.add("BACKGROUND", (2, i), (-1, i), COR_RESUMO_VERDE_CLARO)
            style_resumo.add("TEXTCOLOR", (2, i), (-1, i), colors.black)
        elif tipo == "noite":
            style_resumo.add("BACKGROUND", (2, i), (-1, i), COR_RESUMO_VERDE_ESCURO)
            style_resumo.add("TEXTCOLOR", (2, i), (-1, i), colors.black)
        elif tipo == "obs_head":
            # OBSERVACOES GERAIS: fundo cinza, texto preto bold centralizado
            style_resumo.add("BACKGROUND", (0, i), (-1, i), COR_HEADER_CINZA_ESCURO)
            style_resumo.add("TEXTCOLOR", (0, i), (-1, i), colors.black)
            style_resumo.add("FONTNAME", (0, i), (-1, i), "Helvetica-Bold")
        elif tipo == "obs_linha":
            # Linha em branco para observações editáveis pelo comandante
            style_resumo.add("BACKGROUND", (0, i), (-1, i), colors.white)
            style_resumo.add("TEXTCOLOR", (0, i), (-1, i), colors.black)
            style_resumo.add("FONTNAME", (0, i), (-1, i), "Helvetica")
            style_resumo.add("ALIGN", (0, i), (-1, i), "LEFT")
            style_resumo.add("LEFTPADDING", (0, i), (-1, i), 6)

    for cmd in spans_verticais:
        style_resumo.add(*cmd)
    t_resumo.setStyle(style_resumo)

    # Legenda colada no resumo (sem padding entre as duas tabelas)
    larg_resumo_mm = 8 + 15 + 13 + 55 + 10 + 11 * n
    larg_legenda_mm = 22 + 36
    composto = Table(
        [[t_resumo, _legenda_colorida(ALTURA_LINHA, [15*mm, (larg_legenda_mm - 15)*mm])]],
        colWidths=[larg_resumo_mm*mm, larg_legenda_mm*mm],
    )
    composto.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
    ]))
    return composto


def _legenda_colorida(altura_linha=None, larguras=None):
    """Legenda com cor de fundo em cada código. Se altura_linha for fornecida,
    aplica a mesma altura em todas as linhas para alinhar com o resumo."""
    itens = [
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
    ]
    # Header da legenda: span horizontal sobre 2 colunas
    rows = [["LEGENDA:", ""]]
    for cod, desc in itens:
        rows.append([cod, desc])

    rh = [altura_linha] * len(rows) if altura_linha else None
    # Coluna 0 mais larga para caber "1ª Ala" / "LEGENDA:" sem quebra
    t = Table(rows, colWidths=larguras or [15*mm, 47*mm], rowHeights=rh)
    style = TableStyle([
        ("FONTSIZE", (0, 0), (-1, -1), 6.5),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-BoldOblique"),
        ("FONTNAME", (0, 1), (0, -1), "Helvetica-Bold"),
        ("FONTNAME", (1, 1), (1, -1), "Helvetica"),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("GRID", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEBEFORE", (0, 0), (-1, -1), 1.0, colors.black),
        ("LINEAFTER", (0, 0), (-1, -1), 1.0, colors.black),
        ("BACKGROUND", (0, 0), (-1, 0), COR_LEGENDA_HEAD),
        ("SPAN", (0, 0), (1, 0)),  # LEGENDA: ocupa as 2 colunas
        ("ALIGN", (0, 0), (-1, 0), "CENTER"),
        ("ALIGN", (0, 1), (0, -1), "CENTER"),
        ("ALIGN", (1, 1), (1, -1), "LEFT"),
        ("LEFTPADDING", (0, 0), (-1, -1), 3),
        ("RIGHTPADDING", (0, 0), (-1, -1), 3),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 0),
    ])
    for i, (cod, _) in enumerate(itens, start=1):
        cor_fundo, cor_texto = LEGENDA_CORES.get(cod, (None, colors.black))
        if cor_fundo:
            style.add("BACKGROUND", (0, i), (0, i), cor_fundo)
            style.add("TEXTCOLOR", (0, i), (0, i), cor_texto)
    t.setStyle(style)
    return t


# ----------------------- Observações finais + assinaturas -----------------------
def _observacoes_finais(escala: EscalaMensal, militares: list[Militar]) -> list:
    ss = _styles()
    out = [Paragraph("<b>OBSERVAÇÕES GERAIS</b>",
                     ParagraphStyle(name="HeadObs", parent=ss["EBM_Homologo"],
                                    fontSize=10))]
    obs = escala.observacoes_gerais or _observacoes_padrao()

    import re
    for i, item in enumerate(obs, 1):
        item_text = item.strip()
        # Aceita *texto em negrito* (formato Markdown-like, mais natural)
        # OU <b>texto</b> (formato legado) — converte ambos para negrito do reportlab
        item_md = re.sub(r"\*([^*\n]+)\*", r"<b>\1</b>", item_text)
        # Linha inteira em negrito se começar e terminar com *
        linha_toda_bold = item_text.startswith("*") and item_text.endswith("*") and item_text.count("*") == 2
        style_obj = ss["EBM_ObsItemBold"] if linha_toda_bold else ss["EBM_ObsItem"]
        out.append(Paragraph(f"{i}&nbsp;&nbsp;&nbsp;{item_md}", style_obj))

    out.append(Spacer(1, 40))

    mapa = {m.numero: m for m in militares}
    cmt_pel = mapa.get(escala.cmt_pel_numero)
    cmt_cia = mapa.get(escala.cmt_cia_numero)
    data_hom = escala.data_homologacao or f"{date.today().day} de {MESES_PT[escala.mes].title()} de {escala.ano}"

    out.append(Paragraph(f"<b><i>Quartel em {escala.cidade}, {data_hom}</i></b>",
                         ss["EBM_DataCidade"]))
    out.append(Spacer(1, 30))

    if cmt_pel:
        out.append(Paragraph(
            f"{cmt_pel.nome.upper()}, {cmt_pel.posto} BM.",
            ss["EBM_Assinatura"]))
        out.append(Paragraph(f"***{cmt_pel.funcao}***", ss["EBM_Assinatura"]))
        out.append(Spacer(1, 26))

    out.append(Paragraph("<b>HOMOLOGO</b>", ss["EBM_Homologo"]))
    out.append(Spacer(1, 18))

    if cmt_cia:
        out.append(Paragraph(
            f"{cmt_cia.nome.upper()}, {cmt_cia.posto} BM.",
            ss["EBM_Assinatura"]))
        out.append(Paragraph(f"***{cmt_cia.funcao}***", ss["EBM_Assinatura"]))

    return out


def _observacoes_padrao():
    return [
        "A SAO e a Seção de Mergulho ficarão a cargo das Alas Operacionais, sob a supervisão dos Chefes de Serviço.",
        "O Chefe de Serviço deverá confeccionar escala diária de conservação, arrumação e limpeza dos alojamentos, rancho, sop, etc",
        "O Chefe de Serviço deverá primar pelo cumprimento da NGA da 4ª CIA BM, bem como do 10ºBBM;",
        "O Chefe de Serviço deverá fiscalizar o lançamento e encerramento das ocorrências no Cad;",
        "O Chefe de Serviço deverá preencher e conferir a ficha de controle dos REDS;",
        "O Chefe de Serviço deverá fiscalizar o lançamento das viaturas da 4ª CIA BM no SIAD e Módulo Frota de Abastecimento;",
        "Todos os militares deverão acessar quando estiverem de serviço Intranet, Celotex Digital, SEI e Email Funcional;",
        "*As reposições de horas poderão ser cassadas por necessidade do serviço;*",
        "*Esta escala poderá sofrer alterações, de acordo com a necessidade da 4ª CIA BM.*",
    ]


# ----------------------- Função pública -----------------------
def gerar_pdf(
    escala: EscalaMensal,
    militares: list[Militar],
    alas: list[AlaConfig],
    caminho_saida: str | Path,
):
    caminho_saida = Path(caminho_saida)
    doc = SimpleDocTemplate(
        str(caminho_saida),
        pagesize=landscape(A4),
        leftMargin=10*mm, rightMargin=10*mm,
        topMargin=8*mm, bottomMargin=8*mm,
        title=f"Escala {MESES_PT[escala.mes]} {escala.ano} - {escala.cidade}",
        author="Sistema EscalaBMC",
    )

    story = []

    story.extend(_header_unidade(escala, com_logo=True))
    admins = [m for m in militares if m.secao.upper() == "ADMINISTRAÇÃO"]
    gpvs = [m for m in militares if m.secao.upper() == "GPV"]
    if admins:
        story.extend(_tabela_secao("ADMINISTRAÇÃO", admins))
    if gpvs:
        story.extend(_tabela_secao("1º PELOTÃO/GPV", gpvs))
    if escala.escala_2esforco:
        story.append(Spacer(1, 4))
        story.extend(_tabela_esforco(escala, militares))
    story.append(PageBreak())

    for ala in sorted(alas, key=lambda a: a.numero):
        militares_ala = [m for m in militares if m.ala == ala.numero]
        ala_obs = escala.observacoes_alas.get(str(ala.numero), [])
        story.extend(_tabela_ala(ala, militares_ala, militares,
                                 escala.mes, escala.ano, ala_obs,
                                 escala.remanejamentos,
                                 escala.composicoes_unidade))
        story.append(PageBreak())

    story.extend(_observacoes_finais(escala, militares))

    doc.build(story)
    return caminho_saida
