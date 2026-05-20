# EscalaBMC

Sistema desktop para montagem, controle e exportacao de escala mensal de Bombeiros Militares.

O projeto e uma aplicacao Windows Forms em C#/.NET 8, com exportador de PDF em Python empacotado junto na build final. A pasta publicada funciona sem Python instalado no computador de destino.

## Funcionalidades

- Cadastro de militares, secoes, funcoes, CNH, alas e antiguidade.
- Kanban de alas com movimentacao visual.
- Geracao e visualizacao da escala mensal.
- Lancamento de ausencias, ferias, folgas, transito, licencas e remanejamentos.
- Cobertura automatica de ausencias entre alas adjacentes.
- Escala automatica de 2º esforco para ADM/GPV.
- Observacoes gerais e por ala persistentes entre meses.
- Exportacao de PDF no layout operacional da unidade.
- Build portavel para computadores sem Python.

## Privacidade dos dados

Os arquivos de dados reais ficam em `data/` e os PDFs gerados em `output/`. Essas pastas estao ignoradas pelo Git para evitar publicar nomes, matriculas, ferias, afastamentos e escalas reais.

Ao levar o sistema para outro computador, copie a pasta publicada inteira:

```text
dist/EscalaBMC
```

Nessa pasta ficam o executavel, os dados e os PDFs gerados.

## Requisitos para desenvolvimento

- Windows 10 ou superior.
- .NET SDK 8.
- Python 3.11 ou superior, apenas para recompilar o exportador PDF.

Instale as dependencias Python:

```bat
py -m pip install -r requirements.txt
```

## Executar em modo desenvolvimento

```bat
iniciar.bat
```

Ou diretamente:

```bat
dotnet run --project EscalaBMC.csproj
```

## Gerar build final

```bat
build.bat
```

A build final sera publicada em:

```text
dist/EscalaBMC/EscalaBMC.exe
```

O script tambem recompila o exportador Python em `pdf_exporter/` e o inclui na pasta publicada.

## Gerar PDF via linha de comando

```bat
dist\EscalaBMC\EscalaBMC.exe --export-pdf 6 2026
```

O arquivo sera criado em:

```text
dist/EscalaBMC/output
```

## Recalcular coberturas via linha de comando

```bat
dist\EscalaBMC\EscalaBMC.exe --recalc-coverages 6 2026
```

## Estrutura principal

- `MainForm.cs`: interface principal WinForms.
- `Dialogs.cs`: janelas auxiliares de cadastro, ausencias, alas e 2º esforco.
- `EscalaLogic.cs`: regras de escala, diagnostico, remanejamentos e coberturas.
- `Models.cs`: modelos de dados serializados em JSON.
- `Storage.cs`: persistencia local em `data/` e `output/`.
- `PythonPdfExporter.cs`: integracao do app C# com o exportador Python.
- `python_pdf/`: fonte do gerador PDF.
- `assets/`: icone e logo usados pelo app/PDF.
