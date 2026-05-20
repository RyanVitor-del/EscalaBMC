"""Modelos de dados do sistema de Escala BM."""
from dataclasses import dataclass, field, asdict
from typing import Optional
from enum import Enum


# Ordem de antiguidade (do mais antigo para o mais moderno)
ORDEM_POSTOS = [
    "CEL", "TEN CEL", "MAJ", "CAP", "1º TEN", "2º TEN", "ASP",
    "SUBTEN", "1º SGT", "2º SGT", "3º SGT",
    "CB", "SD", "SD 2ª CL",
]

POSTOS_OFICIAIS = ["CEL", "TEN CEL", "MAJ", "CAP", "1º TEN", "2º TEN", "ASP"]
POSTOS_SUBTEN_SGT = ["SUBTEN", "1º SGT", "2º SGT", "3º SGT"]
POSTOS_CB_SD = ["CB", "SD"]
POSTOS_SD_2A = ["SD 2ª CL"]
TODOS_POSTOS = ORDEM_POSTOS

CATEGORIAS_CNH = ["A", "B", "C", "D", "E", "A/B", "A/D", "B/D", "A/B/D", "-"]

# Alas "fantasma" — opostas no calendário 24x72h
ALAS_FANTASMA = {1: 3, 2: 4, 3: 1, 4: 2}


def antiguidade_posto(posto: str) -> int:
    """Retorna o índice do posto na ordem de antiguidade (0 = mais antigo)."""
    try:
        return ORDEM_POSTOS.index(posto)
    except ValueError:
        return 999


@dataclass
class Ausencia:
    """Representa um período de ausência ou status especial do militar."""
    tipo: str
    data_inicio: str
    data_fim: str
    observacao: str = ""
    cobertura_automatica: bool = False
    origem_automatica: str = ""

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        d = dict(d)
        d.setdefault("cobertura_automatica", False)
        d.setdefault("origem_automatica", "")
        return cls(**d)


@dataclass
class Militar:
    """Cadastro de um militar."""
    numero: str
    posto: str
    nome: str
    categoria_cnh: str = "-"
    funcao: str = ""
    secao: str = "OPERACIONAL"
    ala: int = 0
    ordem: int = 0  # ordem dentro do mesmo posto (mais antigo = menor)
    ausencias: list = field(default_factory=list)
    observacoes: str = ""
    nome_guerra: str = ""
    horas_extras_min: int = 0   # banco de horas em minutos (positivo = a receber)

    def to_dict(self):
        d = asdict(self)
        d["ausencias"] = [a if isinstance(a, dict) else a.to_dict() for a in self.ausencias]
        return d

    @classmethod
    def from_dict(cls, d):
        d = dict(d)
        d["ausencias"] = [Ausencia.from_dict(a) if isinstance(a, dict) else a for a in d.get("ausencias", [])]
        d.setdefault("horas_extras_min", 0)
        return cls(**d)

    @property
    def grupo_posto(self) -> str:
        if self.posto in POSTOS_OFICIAIS:
            return "OFICIAIS"
        if self.posto in POSTOS_SUBTEN_SGT:
            return "SUBTEN/SGT"
        if self.posto in POSTOS_SD_2A:
            return "SD 2ª CL"
        return "CB/SD"

    @property
    def eh_motorista_d(self) -> bool:
        return "D" in self.categoria_cnh.upper()

    @property
    def chave_antiguidade(self) -> tuple:
        """Tupla para ordenação por antiguidade (menor = mais antigo)."""
        return (antiguidade_posto(self.posto), self.ordem)

    def display_nome(self) -> str:
        return f"{self.posto} {self.nome}"

    def banco_horas_str(self) -> str:
        h, m = divmod(abs(self.horas_extras_min), 60)
        sinal = "+" if self.horas_extras_min >= 0 else "-"
        return f"{sinal}{h:02d}h{m:02d}min"


@dataclass
class AlaConfig:
    """Configuração de uma ala operacional."""
    numero: int
    nome: str
    chefe_servico_numero: str = ""
    cmt_gu_numero: str = ""

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class RemanejamentoLog:
    """Registro de um remanejamento (para banco de horas e histórico)."""
    militar_numero: str
    data: str           # DD/MM/AAAA
    de_ala: int
    para_ala: int
    motivo: str = ""
    folga_horas: int = 72   # horas de folga (24 = dobrou, 48 = folga curta, 72 = normal)
    aprovado_por: str = ""

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class UnidadeConfig:
    """Configuração da unidade (BBM/CIA/PEL). Editável pelo comando."""
    nome_completo: str = "10º BBM / 4ª CIA / 1º PELOTÃO - FORMIGA"
    cidade: str = "Formiga"
    bbm: str = "10º BBM"
    cia: str = "4ª CIA"
    pelotao: str = "1º PELOTÃO"

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class CelulaManual:
    ala: int
    militar_numero: str
    data: str
    valor: str

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class InsercaoAla:
    ala: int
    militar_numero: str

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class OcultacaoAla:
    ala: int
    militar_numero: str

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)


@dataclass
class EscalaMensal:
    """Escala mensal completa."""
    mes: int
    ano: int
    unidade: str = "10º BBM / 4ª CIA / 1º PELOTÃO - FORMIGA"
    cidade: str = "Formiga"
    data_homologacao: str = ""
    cmt_pel_numero: str = ""
    cmt_cia_numero: str = ""
    observacoes_gerais: list = field(default_factory=list)
    escala_2esforco: list = field(default_factory=list)
    observacoes_alas: dict = field(default_factory=dict)
    observacoes_definidas: bool = False
    # Overrides manuais por data: {"DD/MM/AAAA": {"militar_numero": "novo_status_ou_ala"}}
    overrides: dict = field(default_factory=dict)
    # Histórico de remanejamentos do mês
    remanejamentos: list = field(default_factory=list)
    celulas_manuais: list = field(default_factory=list)
    insercoes_ala: list = field(default_factory=list)
    ocultacoes_ala: list = field(default_factory=list)

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        d = dict(d)
        d.setdefault("observacoes_alas", {})
        d.setdefault("observacoes_definidas", False)
        d.setdefault("overrides", {})
        d.setdefault("remanejamentos", [])
        d.setdefault("celulas_manuais", [])
        d.setdefault("insercoes_ala", [])
        d.setdefault("ocultacoes_ala", [])
        d["remanejamentos"] = [
            RemanejamentoLog.from_dict(item) if isinstance(item, dict) else item
            for item in d.get("remanejamentos", [])
        ]
        d["celulas_manuais"] = [
            CelulaManual.from_dict(item) if isinstance(item, dict) else item
            for item in d.get("celulas_manuais", [])
        ]
        d["insercoes_ala"] = [
            InsercaoAla.from_dict(item) if isinstance(item, dict) else item
            for item in d.get("insercoes_ala", [])
        ]
        d["ocultacoes_ala"] = [
            OcultacaoAla.from_dict(item) if isinstance(item, dict) else item
            for item in d.get("ocultacoes_ala", [])
        ]
        return cls(**d)


@dataclass
class Esforco:
    """Registro de 2º esforço para administração/GPV."""
    militar_numero: str
    de: str
    ate: str

    def to_dict(self):
        return asdict(self)

    @classmethod
    def from_dict(cls, d):
        return cls(**d)
