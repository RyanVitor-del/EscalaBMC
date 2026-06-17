using System.Drawing;
using System.Globalization;

namespace EscalaBMC;

internal sealed class MilitarDialog : Form
{
    private readonly Militar? _original;
    private readonly TextBox _numero = new();
    private readonly ComboBox _posto = new();
    private readonly TextBox _nome = new();
    private readonly TextBox _nomeGuerra = new();
    private readonly ComboBox _cnh = new();
    private readonly ComboBox _secao = new();
    private readonly ComboBox _ala = new();
    private readonly NumericUpDown _ordem = new();
    private readonly ComboBox _funcao = new();
    private readonly TextBox _obs = new();

    public Militar? Militar { get; private set; }

    public MilitarDialog(Militar? militar)
    {
        _original = militar;
        Text = militar is null ? "Cadastro de Militar" : $"Editar - {militar.Posto} {militar.Nome}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 760;
        Height = 500;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 11 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(root);

        ConfigureCombo(_posto, ModelConstants.TodosPostos);
        ConfigureCombo(_cnh, ModelConstants.CategoriasCnh);
        ConfigureCombo(_secao, ModelConstants.Secoes);
        ConfigureCombo(_ala, ["0", "1", "2", "3", "4"]);
        _funcao.Items.AddRange(new object[]
        {
            "", "CH. Serviço", "CMT. GU", "Motorista", "Armador",
            "Aux. ADM", "Vistoriador", "CMT 4ª CIA / 10º BBM",
            "CMT 1º PEL / 4ª CIA", "Aux. ADM / CMT. GU",
        });

        AddField(root, "Número de Matrícula:", _numero, 0, 0);
        AddField(root, "Posto / Graduação:", _posto, 0, 1);
        AddField(root, "Nome completo:", _nome, 2, 0);
        AddField(root, "Nome de guerra:", _nomeGuerra, 2, 1);
        AddField(root, "Categoria CNH:", _cnh, 4, 0);
        AddField(root, "Seção:", _secao, 4, 1);
        AddField(root, "Ala (0=sem ala, 1-4):", _ala, 6, 0);
        AddField(root, "Ordem (antiguidade dentro do posto):", _ordem, 6, 1);
        AddField(root, "Função:", _funcao, 8, 0);
        AddField(root, "Observações:", _obs, 8, 1);

        _ordem.Maximum = 9999;
        _ordem.Minimum = 0;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
        var save = Button("Salvar", DialogSave);
        var cancel = Button("Cancelar", () => DialogResult = DialogResult.Cancel);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        if (militar is not null)
        {
            var del = Button("Excluir", DialogDelete);
            del.BackColor = ColorTranslator.FromHtml("#C62828");
            del.ForeColor = Color.White;
            buttons.Controls.Add(del);
        }
        Controls.Add(buttons);

        LoadValues(militar);
    }

    private void LoadValues(Militar? militar)
    {
        _numero.Text = militar?.Numero ?? "";
        _posto.SelectedItem = militar?.Posto ?? "CB";
        _nome.Text = militar?.Nome ?? "";
        _nomeGuerra.Text = militar?.NomeGuerra ?? "";
        _cnh.SelectedItem = militar?.CategoriaCnh ?? "-";
        _secao.SelectedItem = militar?.Secao ?? "OPERACIONAL";
        _ala.SelectedItem = (militar?.Ala ?? 0).ToString(CultureInfo.InvariantCulture);
        _ordem.Value = militar?.Ordem ?? 0;
        _funcao.Text = militar?.Funcao ?? "";
        _obs.Text = militar?.Observacoes ?? "";
    }

    private void DialogSave()
    {
        if (string.IsNullOrWhiteSpace(_numero.Text) || string.IsNullOrWhiteSpace(_posto.Text) || string.IsNullOrWhiteSpace(_nome.Text))
        {
            MessageBox.Show(this, "Número, Posto e Nome são obrigatórios.", "Erro de validação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Militar = _original?.Clone() ?? new Militar();
        Militar.Numero = _numero.Text.Trim();
        Militar.Posto = _posto.Text.Trim();
        Militar.Nome = _nome.Text.Trim();
        Militar.NomeGuerra = _nomeGuerra.Text.Trim();
        Militar.CategoriaCnh = _cnh.Text;
        Militar.Secao = _secao.Text;
        Militar.Ala = int.TryParse(_ala.Text, out var ala) ? ala : 0;
        Militar.Ordem = (int)_ordem.Value;
        Militar.Funcao = _funcao.Text.Trim();
        Militar.Observacoes = _obs.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void DialogDelete()
    {
        if (MessageBox.Show(this, "Remover este militar?", "Excluir militar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            DialogResult = DialogResult.Abort;
            Close();
        }
    }

    private static void ConfigureCombo(ComboBox combo, IEnumerable<string> values)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.AddRange(values.Cast<object>().ToArray());
    }

    private static void AddField(TableLayoutPanel root, string label, Control control, int row, int column)
    {
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, column, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(4);
        root.Controls.Add(control, column, row + 1);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 110, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }
}

internal sealed class UnidadeDialog : Form
{
    private readonly TextBox _nome = new();
    private readonly TextBox _cidade = new();

    public UnidadeCadastro? Unidade { get; private set; }

    public UnidadeDialog(UnidadeCadastro? unidade = null)
    {
        Text = unidade is null ? "Cadastro de Unidade" : "Editar Unidade";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 620;
        Height = 260;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 5 };
        Controls.Add(root);

        AddField(root, "Nome completo da unidade:", _nome);
        AddField(root, "Cidade:", _cidade);
        _nome.Text = unidade?.Nome ?? "";
        _cidade.Text = unidade?.Cidade ?? "";

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
        footer.Controls.Add(Button("Salvar", Save));
        footer.Controls.Add(Button("Cancelar", () => DialogResult = DialogResult.Cancel));
        Controls.Add(footer);
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(_nome.Text) || string.IsNullOrWhiteSpace(_cidade.Text))
        {
            MessageBox.Show(this, "Nome completo e cidade sao obrigatorios.", "Erro de validacao", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Unidade = new UnidadeCadastro
        {
            Nome = _nome.Text.Trim(),
            Cidade = _cidade.Text.Trim(),
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddField(TableLayoutPanel root, string label, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(4);
        root.Controls.Add(control);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 110, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }
}

internal sealed class AusenciasManagerDialog : Form
{
    private readonly Militar _militar;
    private readonly DataGridView _grid = new();

    public AusenciasManagerDialog(Militar militar)
    {
        _militar = militar;
        Text = $"Ausências - {militar.Posto} {militar.Nome}";
        StartPosition = FormStartPosition.CenterParent;
        Width = 720;
        Height = 420;
        Font = new Font("Segoe UI", 9F);
        BackColor = ColorTranslator.FromHtml("#F4F6F9");

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.GridColor = ColorTranslator.FromHtml("#D5DCE3");
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 28;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.RowTemplate.Height = 28;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.Black,
            SelectionBackColor = ColorTranslator.FromHtml("#E8F1FA"),
            SelectionForeColor = Color.Black,
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ColorTranslator.FromHtml("#F7F9FB"),
            ForeColor = Color.Black,
            SelectionBackColor = ColorTranslator.FromHtml("#E8F1FA"),
            SelectionForeColor = Color.Black,
        };
        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ColorTranslator.FromHtml("#0F3057"),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            SelectionBackColor = ColorTranslator.FromHtml("#0F3057"),
            SelectionForeColor = Color.White,
        };
        _grid.Columns.Add("tipo", "Tipo");
        _grid.Columns.Add("inicio", "Início");
        _grid.Columns.Add("fim", "Fim");
        _grid.Columns.Add("obs", "Observação");
        _grid.Columns["tipo"]!.FillWeight = 60;
        _grid.Columns["inicio"]!.FillWeight = 80;
        _grid.Columns["fim"]!.FillWeight = 80;
        _grid.Columns["obs"]!.FillWeight = 200;
        _grid.Columns["tipo"]!.HeaderCell.Style = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.Black,
            SelectionBackColor = Color.White,
            SelectionForeColor = Color.Black,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        };
        _grid.DoubleClick += (_, _) => Edit();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(12, 10, 8, 6), BackColor = ColorTranslator.FromHtml("#F4F6F9") };
        toolbar.Controls.Add(Button("Adicionar", Add, primary: true));
        toolbar.Controls.Add(Button("Editar", Edit));
        toolbar.Controls.Add(Button("Remover", Remove));

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 8, 10, 10), BackColor = ColorTranslator.FromHtml("#F4F6F9") };
        footer.Controls.Add(Button("Fechar", () => { DialogResult = DialogResult.OK; Close(); }));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = ColorTranslator.FromHtml("#F4F6F9"),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.Controls.Add(toolbar, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var ausencia in _militar.Ausencias)
        {
            var row = _grid.Rows.Add(TipoDisplay(ausencia.Tipo), ausencia.DataInicio, ausencia.DataFim, ausencia.Observacao);
            _grid.Rows[row].Tag = ausencia;
        }
        _grid.ClearSelection();
    }

    private void Add()
    {
        using var dlg = new AusenciaDialog(null, null);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Ausencia is not null)
        {
            _militar.Ausencias.Add(dlg.Ausencia);
            RefreshGrid();
        }
    }

    private void Edit()
    {
        if (_grid.CurrentRow?.Tag is not Ausencia ausencia)
            return;
        using var dlg = new AusenciaDialog(ausencia, null);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Ausencia is not null)
        {
            var idx = _militar.Ausencias.IndexOf(ausencia);
            if (idx >= 0)
                _militar.Ausencias[idx] = dlg.Ausencia;
            RefreshGrid();
        }
    }

    private void Remove()
    {
        if (_grid.CurrentRow?.Tag is Ausencia ausencia &&
            MessageBox.Show(this, "Remover esta ausência?", "Excluir ausência", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            _militar.Ausencias.Remove(ausencia);
            RefreshGrid();
        }
    }

    private static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            Width = 100,
            Height = 30,
            Margin = new Padding(4),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? ColorTranslator.FromHtml("#0F3057") : Color.White,
            ForeColor = primary ? Color.White : Color.Black,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        button.FlatAppearance.BorderColor = primary ? ColorTranslator.FromHtml("#0F3057") : ColorTranslator.FromHtml("#B8C2CC");
        button.FlatAppearance.BorderSize = 1;
        button.Cursor = Cursors.Hand;
        button.Click += (_, _) => action();
        return button;
    }

    private static string TipoDisplay(string tipo)
    {
        var labels = new Dictionary<string, string>
        {
            ["FA"] = "FA - Férias Anuais",
            ["FP"] = "FP - Férias Prêmio",
            ["L"] = "L - Licença Médica",
            ["D"] = "D - Dispensa Médica",
            ["FD"] = "FD - Folga Diurna",
            ["FN"] = "FN - Folga Noturna",
            ["FR"] = "FR - Folga Obrigatória",
            ["LN"] = "LN - Licença Núpcias",
            ["T"] = "T - Trânsito",
            ["MO"] = "MO - Movimentado",
            ["O"] = "O - Outro",
            ["1ª Ala"] = "1ª Ala - Remanejamento",
            ["2ª Ala"] = "2ª Ala - Remanejamento",
            ["3ª Ala"] = "3ª Ala - Remanejamento",
            ["4ª Ala"] = "4ª Ala - Remanejamento",
        };
        return labels.TryGetValue(tipo, out var label) ? label : tipo;
    }
}

internal sealed class AusenciaDialog : Form
{
    private static readonly (string Tipo, string Label)[] Tipos =
    [
        ("FA", "FA - Férias Anuais"),
        ("FP", "FP - Férias Prêmio"),
        ("L", "L - Licença Médica"),
        ("D", "D - Dispensa Médica"),
        ("FD", "FD - Folga DIURNA (Reposição 12H)"),
        ("FN", "FN - Folga NOTURNA (Reposição 12H)"),
        ("FR", "FR - Folga Obrigatória (após cobertura adjacente)"),
        ("LN", "LN - Licença Núpcias"),
        ("T", "T - Trânsito"),
        ("O", "O - Outro (especificar em obs.)"),
        ("MO", "MO - Movimentado"),
        ("1ª Ala", "1ª Ala - Remanejamento para 1ª Ala"),
        ("2ª Ala", "2ª Ala - Remanejamento para 2ª Ala"),
        ("3ª Ala", "3ª Ala - Remanejamento para 3ª Ala"),
        ("4ª Ala", "4ª Ala - Remanejamento para 4ª Ala"),
    ];

    private readonly ComboBox _tipo = new();
    private readonly DateTimePicker _inicio = new();
    private readonly DateTimePicker _fim = new();
    private readonly TextBox _obs = new();

    public Ausencia? Ausencia { get; private set; }

    public AusenciaDialog(Ausencia? ausencia, DateTime? diaInicial)
    {
        Text = "Ausência / Remanejamento";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Width = 520;
        Height = 360;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1, RowCount = 9 };
        Controls.Add(root);

        _tipo.DropDownStyle = ComboBoxStyle.DropDownList;
        _tipo.Items.AddRange(Tipos.Select(t => t.Label).Cast<object>().ToArray());
        AddField(root, "Tipo:", _tipo);
        ConfigureDate(_inicio);
        ConfigureDate(_fim);
        AddField(root, "Data início:", _inicio);
        AddField(root, "Data fim:", _fim);
        AddField(root, "Observação:", _obs);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(Button("Salvar", Save));
        footer.Controls.Add(Button("Cancelar", () => DialogResult = DialogResult.Cancel));
        Controls.Add(footer);

        var start = diaInicial ?? EscalaLogic.ParseDataBr(ausencia?.DataInicio) ?? DateTime.Today;
        var end = diaInicial ?? EscalaLogic.ParseDataBr(ausencia?.DataFim) ?? start;
        _inicio.Value = start;
        _fim.Value = end;
        _obs.Text = ausencia?.Observacao ?? "";
        _tipo.SelectedItem = Tipos.FirstOrDefault(t => t.Tipo == ausencia?.Tipo).Label ?? Tipos[0].Label;
    }

    private void Save()
    {
        var label = _tipo.Text;
        var tipo = Tipos.FirstOrDefault(t => t.Label == label).Tipo ?? label.Split(" - ")[0];
        Ausencia = new Ausencia
        {
            Tipo = tipo,
            DataInicio = _inicio.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            DataFim = _fim.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            Observacao = _obs.Text.Trim(),
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static void ConfigureDate(DateTimePicker picker)
    {
        picker.Format = DateTimePickerFormat.Custom;
        picker.CustomFormat = "dd/MM/yyyy";
        picker.Dock = DockStyle.Top;
    }

    private static void AddField(TableLayoutPanel root, string label, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        control.Dock = DockStyle.Top;
        root.Controls.Add(control);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }
}

internal sealed class EsforcoDialog : Form
{
    private readonly ComboBox _militar = new();
    private readonly DateTimePicker _deData = new();
    private readonly DateTimePicker _ateData = new();
    private readonly NumericUpDown _deHora = new();
    private readonly NumericUpDown _deMin = new();
    private readonly NumericUpDown _ateHora = new();
    private readonly NumericUpDown _ateMin = new();

    public Dictionary<string, string>? Value { get; private set; }

    public EsforcoDialog(IReadOnlyList<Militar> militares, Dictionary<string, string>? current)
    {
        Text = "Lançar 2º Esforço";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Width = 560;
        Height = 360;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1 };
        Controls.Add(root);

        var ordenados = militares
            .OrderBy(m => m.ChaveAntiguidade.Posto)
            .ThenBy(m => m.ChaveAntiguidade.Ordem)
            .ThenBy(m => m.Nome)
            .ToList();
        _militar.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var m in ordenados)
            _militar.Items.Add(new ComboItem(m.Numero, $"{m.Numero} - {m.Posto} {m.Nome}"));
        AddField(root, "Militar:", _militar);

        ConfigureDate(_deData);
        ConfigureDate(_ateData);
        var dePanel = TimePanel(_deData, _deHora, _deMin);
        var atePanel = TimePanel(_ateData, _ateHora, _ateMin);
        AddField(root, "DE:", dePanel);
        AddField(root, "ATÉ:", atePanel);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(Button("Salvar", Save));
        footer.Controls.Add(Button("Cancelar", () => DialogResult = DialogResult.Cancel));
        Controls.Add(footer);

        _deHora.Value = _ateHora.Value = 8;
        _deMin.Value = _ateMin.Value = 0;
        if (current is not null)
            LoadCurrent(current);
    }

    private void LoadCurrent(Dictionary<string, string> current)
    {
        current.TryGetValue("militar_numero", out var numero);
        foreach (ComboItem item in _militar.Items)
        {
            if (item.Value == numero)
            {
                _militar.SelectedItem = item;
                break;
            }
        }
        if (current.TryGetValue("de", out var de))
            ParseCbmmg(de, _deData, _deHora, _deMin);
        if (current.TryGetValue("ate", out var ate))
            ParseCbmmg(ate, _ateData, _ateHora, _ateMin);
    }

    private void Save()
    {
        if (_militar.SelectedItem is not ComboItem item)
        {
            MessageBox.Show(this, "Selecione um militar.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Value = new Dictionary<string, string>
        {
            ["militar_numero"] = item.Value,
            ["de"] = FormatCbmmg(_deData.Value, (int)_deHora.Value, (int)_deMin.Value),
            ["ate"] = FormatCbmmg(_ateData.Value, (int)_ateHora.Value, (int)_ateMin.Value),
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Panel TimePanel(DateTimePicker date, NumericUpDown hour, NumericUpDown min)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, FlowDirection = FlowDirection.LeftToRight };
        date.Width = 140;
        hour.Minimum = 0;
        hour.Maximum = 23;
        hour.Width = 50;
        min.Minimum = 0;
        min.Maximum = 59;
        min.Width = 50;
        panel.Controls.Add(date);
        panel.Controls.Add(hour);
        panel.Controls.Add(new Label { Text = ":", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        panel.Controls.Add(min);
        return panel;
    }

    private static void ConfigureDate(DateTimePicker picker)
    {
        picker.Format = DateTimePickerFormat.Custom;
        picker.CustomFormat = "dd/MM/yyyy";
    }

    private static string FormatCbmmg(DateTime date, int hour, int min)
    {
        var dias = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
        return $"{date:dd}{hour:00}{min:00}{EscalaLogic.MesAbrev[date.Month]}{date:yy}-{dias[(int)date.DayOfWeek]}";
    }

    private static void ParseCbmmg(string value, DateTimePicker date, NumericUpDown hour, NumericUpDown min)
    {
        try
        {
            var principal = value.Split('-')[0];
            var dd = int.Parse(principal[..2], CultureInfo.InvariantCulture);
            var hh = int.Parse(principal.Substring(2, 2), CultureInfo.InvariantCulture);
            var mm = int.Parse(principal.Substring(4, 2), CultureInfo.InvariantCulture);
            var mesAbrev = principal.Substring(6, 3);
            var yy = int.Parse(principal.Substring(9, 2), CultureInfo.InvariantCulture);
            var mes = Array.IndexOf(EscalaLogic.MesAbrev, mesAbrev);
            if (mes <= 0)
                mes = DateTime.Today.Month;
            date.Value = new DateTime(2000 + yy, mes, dd);
            hour.Value = hh;
            min.Value = mm;
        }
        catch
        {
            // Mantém valores padrão.
        }
    }

    private static void AddField(TableLayoutPanel root, string label, Control control)
    {
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        control.Dock = DockStyle.Top;
        root.Controls.Add(control);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record ComboItem(string Value, string Text)
    {
        public override string ToString() => Text;
    }
}

internal sealed class AlasDialog : Form
{
    private readonly IReadOnlyList<AlaConfig> _alas;
    private readonly Dictionary<int, TextBox> _nomes = [];
    private readonly Dictionary<int, ComboBox> _chefes = [];
    private readonly Dictionary<int, ComboBox> _cmts = [];

    public AlasDialog(IReadOnlyList<AlaConfig> alas, IReadOnlyList<Militar> militares)
    {
        _alas = alas;
        Text = "Configuração de Alas";
        StartPosition = FormStartPosition.CenterParent;
        Width = 820;
        Height = 380;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 4 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        Controls.Add(root);

        AddHeader(root, "Ala");
        AddHeader(root, "Nome");
        AddHeader(root, "Chefe de Serviço");
        AddHeader(root, "CMT GU");

        foreach (var ala in alas.OrderBy(a => a.Numero))
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.Controls.Add(new Label { Text = ala.Numero.ToString(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, root.RowCount);
            var nome = new TextBox { Text = ala.Nome, Dock = DockStyle.Fill, Margin = new Padding(4, 8, 4, 4) };
            var chefe = Combo(militares);
            var cmt = Combo(militares);
            Select(chefe, ala.ChefeServicoNumero);
            Select(cmt, ala.CmtGuNumero);
            var row = root.RowCount++;
            root.Controls.Add(nome, 1, row);
            root.Controls.Add(chefe, 2, row);
            root.Controls.Add(cmt, 3, row);
            _nomes[ala.Numero] = nome;
            _chefes[ala.Numero] = chefe;
            _cmts[ala.Numero] = cmt;
        }

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(Button("Salvar", Save));
        footer.Controls.Add(Button("Cancelar", () => DialogResult = DialogResult.Cancel));
        Controls.Add(footer);
    }

    private void Save()
    {
        foreach (var ala in _alas)
        {
            ala.Nome = _nomes[ala.Numero].Text.Trim();
            ala.ChefeServicoNumero = (_chefes[ala.Numero].SelectedItem as ComboItem)?.Value ?? "";
            ala.CmtGuNumero = (_cmts[ala.Numero].SelectedItem as ComboItem)?.Value ?? "";
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static ComboBox Combo(IReadOnlyList<Militar> militares)
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 8, 4, 4) };
        combo.Items.Add(new ComboItem("", ""));
        foreach (var militar in EscalaLogic.MilitaresPorAntiguidade(militares))
            combo.Items.Add(new ComboItem(militar.Numero, $"{militar.Posto} {militar.Nome}"));
        return combo;
    }

    private static void Select(ComboBox combo, string numero)
    {
        foreach (ComboItem item in combo.Items)
        {
            if (item.Value == numero)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void AddHeader(TableLayoutPanel root, string text) =>
        root.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record ComboItem(string Value, string Text)
    {
        public override string ToString() => Text;
    }
}

internal sealed class CelulaManualDialog : Form
{
    private static readonly (string Codigo, string Label)[] Valores =
    [
        ("", "(remover edição manual)"),
        ("S", "S - Serviço Operacional"),
        ("R", "R - Reforço 12H"),
        ("FD", "FD - Folga Reposição DIURNA"),
        ("FN", "FN - Folga Reposição NOTURNA"),
        ("FR", "FR - Folga Obrigatória"),
        ("FA", "FA - Férias Anuais"),
        ("FP", "FP - Férias Prêmio"),
        ("L", "L - Licença Médica"),
        ("D", "D - Dispensa Médica"),
        ("T", "T - Trânsito"),
        ("MO", "MO - Movimentado"),
        ("LN", "LN - Licença Núpcias"),
        ("O", "O - Outro"),
        ("1ª Ala", "1ª Ala (remanejado)"),
        ("2ª Ala", "2ª Ala (remanejado)"),
        ("3ª Ala", "3ª Ala (remanejado)"),
        ("4ª Ala", "4ª Ala (remanejado)"),
    ];

    private readonly ComboBox _valor = new();

    public string? Valor { get; private set; }

    public CelulaManualDialog(Militar militar, int ala, DateTime dia, string? atual)
    {
        Text = "Editar célula manualmente";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Width = 480;
        Height = 260;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 1 };
        Controls.Add(root);

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.Controls.Add(new Label
        {
            Text = $"{militar.DisplayNome()} — {ala}ª Ala — {dia:dd/MM/yyyy}",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        });

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.Controls.Add(new Label { Text = "Valor:", Dock = DockStyle.Fill });
        _valor.DropDownStyle = ComboBoxStyle.DropDownList;
        _valor.Dock = DockStyle.Top;
        foreach (var v in Valores)
            _valor.Items.Add(v.Label);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.Controls.Add(_valor);

        var idx = Array.FindIndex(Valores, v => v.Codigo == (atual ?? ""));
        _valor.SelectedIndex = idx >= 0 ? idx : 1;

        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        footer.Controls.Add(MakeButton("Salvar", Save));
        footer.Controls.Add(MakeButton("Cancelar", () => DialogResult = DialogResult.Cancel));
        Controls.Add(footer);
    }

    private void Save()
    {
        if (_valor.SelectedIndex < 0)
            return;
        Valor = Valores[_valor.SelectedIndex].Codigo;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }
}

internal sealed class SelecionarMilitarDialog : Form
{
    private readonly ListBox _lista = new();

    public string? MilitarNumero { get; private set; }

    public SelecionarMilitarDialog(IReadOnlyList<Militar> militares, string titulo)
    {
        Text = titulo;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Width = 520;
        Height = 480;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        _lista.Dock = DockStyle.Fill;
        _lista.Font = new Font("Segoe UI", 10F);
        foreach (var m in militares)
            _lista.Items.Add(new Item(m.Numero, $"{m.Numero} - {m.Posto} {m.Nome}  ({(m.Ala > 0 ? m.Ala + "ª Ala" : "sem ala")})"));
        if (_lista.Items.Count > 0)
            _lista.SelectedIndex = 0;
        _lista.DoubleClick += (_, _) => Save();
        root.Controls.Add(_lista, 0, 0);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        footer.Controls.Add(MakeButton("Inserir", Save));
        footer.Controls.Add(MakeButton("Cancelar", () => DialogResult = DialogResult.Cancel));
        root.Controls.Add(footer, 0, 1);
    }

    private void Save()
    {
        if (_lista.SelectedItem is not Item it)
            return;
        MilitarNumero = it.Numero;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, Action action)
    {
        var button = new Button { Text = text, Width = 100, Height = 30, Margin = new Padding(4) };
        button.Click += (_, _) => action();
        return button;
    }

    private sealed record Item(string Numero, string Label)
    {
        public override string ToString() => Label;
    }
}
