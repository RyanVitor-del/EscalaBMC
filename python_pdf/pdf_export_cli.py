from __future__ import annotations

import json
import sys
from pathlib import Path

from models import AlaConfig, EscalaMensal, Militar
import pdf_export
from pdf_export import gerar_pdf


EXPORTER_VERSION = "2026-05-19-escalabmc-local"


def _load_json(path: Path):
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[1] == "--version":
        print(f"EscalaPdfExporter {EXPORTER_VERSION}")
        return 0

    if len(argv) != 5:
        print("Uso: EscalaPdfExporter <data_dir> <mes> <ano> <output_pdf>", file=sys.stderr)
        return 2

    data_dir = Path(argv[1])
    mes = int(argv[2])
    ano = int(argv[3])
    output_path = Path(argv[4])

    logo_path = Path(__file__).resolve().parent.parent / "assets" / "cbmmg_logo.png"
    if logo_path.exists():
        pdf_export.LOGO_PATH = logo_path

    militares = [Militar.from_dict(item) for item in _load_json(data_dir / "militares.json")]
    alas = [AlaConfig.from_dict(item) for item in _load_json(data_dir / "alas.json")]

    escala_path = data_dir / "escalas" / f"escala_{ano}_{mes:02d}.json"
    if escala_path.exists():
        escala = EscalaMensal.from_dict(_load_json(escala_path))
    else:
        escala = EscalaMensal(mes=mes, ano=ano)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    gerar_pdf(escala, militares, alas, output_path)
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
