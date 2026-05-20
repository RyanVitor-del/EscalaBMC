using System.Drawing;
using System.Globalization;

namespace EscalaBMC;

public sealed class MainForm : Form
{
    private static readonly Color CorPrimaria = ColorTranslator.FromHtml("#0F3057");
    private static readonly Color CorPrimariaHover = ColorTranslator.FromHtml("#1B4870");
    private static readonly Color CorFundo = ColorTranslator.FromHtml("#F4F6F9");
    private static readonly Color CorCartao = Color.White;
    private static readonly Color CorBorda = ColorTranslator.FromHtml("#D5DCE3");
    private static readonly Color CorTexto = ColorTranslator.FromHtml("#1B2733");
    private static readonly Color CorTextoMuted = ColorTranslator.FromHtml("#5C6B7A");
    private static readonly Color CorOk = ColorTranslator.FromHtml("#2E7D32");
    private static readonly Color CorAlerta = ColorTranslator.FromHtml("#E65100");
    private static readonly Color CorCritico = ColorTranslator.FromHtml("#C62828");

    private readonly Dictionary<int, Color> _coresAla = new()
    {
        [1] = ColorTranslator.FromHtml("#1F4E79"),
        [2] = ColorTranslator.FromHtml("#2E7D32"),
        [3] = ColorTranslator.FromHtml("#000000"),
        [4] = ColorTranslator.FromHtml("#BF360C"),
    };

    private List<Militar> _militares = [];
    private List<AlaConfig> _alas = [];
    private EscalaMensal? _escalaAtual;

    private ComboBox _cmbMes = null!;
    private ComboBox _cmbAno = null!;
    private Label _status = null!;
    private Label _cardTotal = null!;
    private Label _cardMotoristas = null!;
    private Label _cardAlertas = null!;
    private Label _cardPeriodo = null!;
    private FlowLayoutPanel _resumoAlasPanel = null!;
    private DataGridView _gridMilitares = null!;
    private ComboBox _filtroSecao = null!;
    private ComboBox _filtroAla = null!;
    private readonly Dictionary<int, ListBox> _listasAlas = [];
    private readonly Dictionary<int, DataGridView> _gridsEscala = [];
    private TabControl _tabsEscala = null!;
    private TextBox _txtObsGerais = null!;
    private readonly Dictionary<int, TextBox> _txtObsAlas = [];
    private DataGridView _gridEsforco = null!;
    private TextBox _txtUnidade = null!;
    private TextBox _txtCidade = null!;
    private TextBox _txtDataHomologacao = null!;
    private ComboBox _cmbCmtPel = null!;
    private ComboBox _cmbCmtCia = null!;
    private TextBox _txtDiagnostico = null!;
    private TabControl _tabs = null!;
    private DataGridView _gridRegistro = null!;
    private ComboBox _filtroRegistroTipo = null!;
    private Label _lblRegistroResumo = null!;
    private CheckedListBox _lbSecoes = null!;
    private ListBox _lbFuncoes = null!;
    private bool _recarregandoConfig;

    public MainForm()
    {
        Text = "EscalaBM - Sistema de Escala de Bombeiros Militares";
        Icon = LoadAppIcon();
        Width = 1440;
        Height = 880;
        MinimumSize = new Size(1200, 720);
        BackColor = CorFundo;
        Font = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterScreen;

        _militares = Storage.LoadMilitares();
        _alas = Storage.LoadAlas();

        BuildUi();

        _cmbMes.SelectedItem = DateTime.Today.Month;
        _cmbAno.SelectedItem = DateTime.Today.Year;
        LoadEscalaPeriodo();
    }

    private void BuildUi()
    {
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildTabs());
        Controls.Add(BuildTopBar());
    }

    private Control BuildTopBar()
    {
        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = CorPrimaria,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(12, 0, 10, 0),
        };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = new Label
        {
            Text = "EscalaBM",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(6, 7),
        };
        brand.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Sistema de Escala Mensal - Bombeiros Militares",
            ForeColor = ColorTranslator.FromHtml("#CFE0F2"),
            AutoSize = true,
            Location = new Point(8, 38),
        };
        brand.Controls.Add(subtitle);
        top.Controls.Add(brand, 0, 0);

        var center = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 16, 0, 0),
        };
        center.Controls.Add(new Label { Text = "Mês:", ForeColor = Color.White, AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        _cmbMes = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
        _cmbMes.Items.AddRange(Enumerable.Range(1, 12).Cast<object>().ToArray());
        _cmbMes.SelectedIndexChanged += (_, _) => LoadEscalaPeriodo();
        center.Controls.Add(_cmbMes);
        center.Controls.Add(new Label { Text = "Ano:", ForeColor = Color.White, AutoSize = true, Padding = new Padding(12, 7, 0, 0) });
        _cmbAno = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 86 };
        _cmbAno.Items.AddRange(Enumerable.Range(2024, 11).Cast<object>().ToArray());
        _cmbAno.SelectedIndexChanged += (_, _) => LoadEscalaPeriodo();
        center.Controls.Add(_cmbAno);
        top.Controls.Add(center, 1, 0);

        var rightActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 12, 0, 0),
        };
        var btnPdf = TopButton("Gerar PDF da Escala", GerarPdf);
        btnPdf.Width = 172;
        var btnSave = TopButton("Salvar tudo", SalvarTudo);
        btnSave.Width = 132;
        rightActions.Controls.Add(btnPdf);
        rightActions.Controls.Add(btnSave);
        top.Controls.Add(rightActions, 2, 0);

        return top;
    }

    private Control BuildStatusBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            BackColor = ColorTranslator.FromHtml("#E5E9EE"),
        };
        _status = new Label
        {
            Text = "Pronto.",
            ForeColor = CorTextoMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
        };
        panel.Controls.Add(_status);
        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 6),
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(148, 34),
            SizeMode = TabSizeMode.Fixed,
        };
        tabs.DrawItem += DrawMainTab;
        _tabs = tabs;
        tabs.TabPages.Add(BuildDashboardTab());
        tabs.TabPages.Add(BuildMilitaresTab());
        tabs.TabPages.Add(BuildAlasTab());
        tabs.TabPages.Add(BuildEscalaTab());
        tabs.TabPages.Add(BuildObservacoesTab());
        tabs.TabPages.Add(BuildUnidadeTab());
        tabs.TabPages.Add(BuildRegistroFolgasTab());
        tabs.TabPages.Add(BuildDiagnosticoTab());
        tabs.TabPages.Add(BuildConfiguracoesTab());
        return tabs;
    }

    private TabPage BuildDashboardTab()
    {
        var tab = NewTab("Dashboard");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, BackColor = CorFundo };
        for (var i = 0; i < 4; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _cardTotal = AddCard(cards, "Total militares", CorPrimaria);
        _cardMotoristas = AddCard(cards, "Motoristas D", CorOk);
        _cardAlertas = AddCard(cards, "Alertas críticos", CorAlerta);
        _cardPeriodo = AddCard(cards, "Período", ColorTranslator.FromHtml("#6A1B9A"));
        root.Controls.Add(cards, 0, 0);

        var resumoBox = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = CorCartao,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(8),
        };
        resumoBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        resumoBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resumoBox.Controls.Add(SectionLabel("Distribuição por Ala", DockStyle.Fill), 0, 0);
        _resumoAlasPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            BackColor = CorCartao,
            WrapContents = false,
            AutoScroll = false,
        };
        resumoBox.Controls.Add(_resumoAlasPanel, 0, 1);
        root.Controls.Add(resumoBox, 0, 1);

        var actions = ToolbarPanel();
        actions.Dock = DockStyle.Fill;
        actions.Controls.Add(PrimaryButton("Gerar escala do mês", GerarEscalaAssistente, 190));
        actions.Controls.Add(SecondaryButton("Reequilibrar Motoristas D", RebalancearD, 210));
        actions.Controls.Add(SecondaryButton("Diagnóstico completo", () => { _tabs.SelectedIndex = 7; ExecutarDiagnostico(); }, 190));
        actions.Controls.Add(SecondaryButton("Ver Kanban de Alas", () => _tabs.SelectedIndex = 2, 180));
        actions.Controls.Add(SecondaryButton("Abrir PDFs gerados", AbrirPdfsGerados, 180));
        root.Controls.Add(actions, 0, 2);

        return tab;
    }

    private Label AddCard(TableLayoutPanel parent, string titulo, Color corValor)
    {
        var card = CardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(6);
        card.Controls.Add(new Label
        {
            Text = titulo,
            ForeColor = CorTextoMuted,
            AutoSize = true,
            Location = new Point(14, 12),
        });
        var value = new Label
        {
            Text = "-",
            ForeColor = corValor,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 34),
        };
        card.Controls.Add(value);
        parent.Controls.Add(card);
        return value;
    }

    private TabPage BuildMilitaresTab()
    {
        var tab = NewTab("Militares");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        var toolbar = ToolbarPanel();
        toolbar.Dock = DockStyle.Fill;
        toolbar.Controls.Add(PrimaryButton("Novo militar", NovoMilitar));
        toolbar.Controls.Add(SecondaryButton("Ausências/Férias", AusenciasSelecionado, 150));
        toolbar.Controls.Add(SecondaryButton("Subir", () => MoverMilitar(-1), 90));
        toolbar.Controls.Add(SecondaryButton("Descer", () => MoverMilitar(1), 90));

        toolbar.Controls.Add(new Label { Text = "Filtrar:", AutoSize = true, ForeColor = CorTexto, Padding = new Padding(20, 12, 0, 0) });
        _filtroSecao = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _filtroSecao.Items.AddRange(new object[] { "TODAS", "ADMINISTRAÇÃO", "GPV", "OPERACIONAL" });
        _filtroSecao.SelectedItem = "TODAS";
        _filtroSecao.SelectedIndexChanged += (_, _) => RefreshMilitares();
        toolbar.Controls.Add(_filtroSecao);
        _filtroAla = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        _filtroAla.Items.AddRange(new object[] { "Todas alas", "Sem ala", "1ª Ala", "2ª Ala", "3ª Ala", "4ª Ala" });
        _filtroAla.SelectedItem = "Todas alas";
        _filtroAla.SelectedIndexChanged += (_, _) => RefreshMilitares();
        toolbar.Controls.Add(_filtroAla);
        root.Controls.Add(toolbar, 0, 0);

        _gridMilitares = Grid();
        _gridMilitares.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridMilitares.ScrollBars = ScrollBars.Both;
        _gridMilitares.ReadOnly = true;
        _gridMilitares.AllowUserToResizeRows = false;
        _gridMilitares.AllowUserToResizeColumns = false;
        _gridMilitares.CellBorderStyle = DataGridViewCellBorderStyle.None;
        _gridMilitares.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _gridMilitares.RowTemplate.Height = 28;
        _gridMilitares.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridMilitares.MultiSelect = false;
        _gridMilitares.DoubleClick += (_, _) => EditarMilitarSelecionado();
        _gridMilitares.Columns.Add("antig", "Antiguidade");
        _gridMilitares.Columns.Add("numero", "Número");
        _gridMilitares.Columns.Add("posto", "Posto");
        _gridMilitares.Columns.Add("nome", "Nome");
        _gridMilitares.Columns.Add("cnh", "CNH");
        _gridMilitares.Columns.Add("secao", "Seção");
        _gridMilitares.Columns.Add("ala", "Ala");
        _gridMilitares.Columns.Add("funcao", "Função");
        _gridMilitares.Columns.Add("obs", "Ausências/Obs.");
        _gridMilitares.Columns.Add("banco", "Banco horas");
        SetFill(_gridMilitares, "antig", 70);
        SetFill(_gridMilitares, "numero", 85);
        SetFill(_gridMilitares, "posto", 65);
        SetFill(_gridMilitares, "nome", 220);
        SetFill(_gridMilitares, "cnh", 55);
        SetFill(_gridMilitares, "secao", 105);
        SetFill(_gridMilitares, "ala", 45);
        SetFill(_gridMilitares, "funcao", 135);
        SetFill(_gridMilitares, "obs", 240);
        SetFill(_gridMilitares, "banco", 85);
        root.Controls.Add(_gridMilitares, 0, 1);
        return tab;
    }

    private TabPage BuildAlasTab()
    {
        var tab = NewTab("Alas - Kanban");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        var toolbar = ToolbarPanel();
        toolbar.Dock = DockStyle.Fill;
        toolbar.Controls.Add(PrimaryButton("Editar Chefe/CMT GU das Alas", EditarAlas, 230));
        toolbar.Controls.Add(SecondaryButton("Reequilibrar Motoristas D", RebalancearD, 200));
        toolbar.Controls.Add(SecondaryButton("Equalizar efetivo", EqualizarEfetivo, 160));
        toolbar.Controls.Add(new Label
        {
            Text = "Arraste cartões entre alas. Duplo-clique edita o militar.",
            AutoSize = true,
            ForeColor = CorTextoMuted,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            Padding = new Padding(20, 16, 0, 0),
        });
        root.Controls.Add(toolbar, 0, 0);

        var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, BackColor = CorFundo };
        for (var i = 0; i < 5; i++)
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        root.Controls.Add(cols, 0, 1);

        AddAlaList(cols, 0, "SEM ALA / ADM / GPV", ColorTranslator.FromHtml("#5C6B7A"));
        for (var ala = 1; ala <= 4; ala++)
            AddAlaList(cols, ala, $"{ala}ª ALA OPERACIONAL", _coresAla[ala]);

        return tab;
    }

    private void AddAlaList(TableLayoutPanel parent, int ala, string titulo, Color headerColor)
    {
        var panel = CardPanel();
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(4);
        panel.Padding = new Padding(1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Color.White,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var head = new Label
        {
            Dock = DockStyle.Fill,
            Text = titulo,
            BackColor = headerColor,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        };
        layout.Controls.Add(head, 0, 0);

        var stats = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#F4F6F9"),
            ForeColor = CorPrimaria,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        };
        layout.Controls.Add(stats, 0, 1);

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            IntegralHeight = false,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 76,
            AllowDrop = true,
            BackColor = Color.White,
            Tag = stats,
        };
        list.DrawItem += DrawMilitarListItem;
        Point? dragStart = null;
        list.MouseDown += (_, e) =>
        {
            dragStart = e.Location;
            var index = list.IndexFromPoint(e.Location);
            if (index >= 0)
                list.SelectedIndex = index;
        };
        list.MouseMove += (_, e) =>
        {
            if (dragStart is null || e.Button != MouseButtons.Left) return;
            if (Math.Abs(e.X - dragStart.Value.X) > 4 || Math.Abs(e.Y - dragStart.Value.Y) > 4)
            {
                dragStart = null;
                if (list.SelectedItem is Militar m)
                    list.DoDragDrop(m, DragDropEffects.Move);
            }
        };
        list.MouseUp += (_, _) => dragStart = null;
        list.DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(typeof(Militar)) == true)
                e.Effect = DragDropEffects.Move;
        };
        list.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(typeof(Militar)) is Militar militar && militar.Ala != ala)
            {
                militar.Ala = ala;
                Storage.SaveMilitares(_militares);
                RefreshAll();
                Status($"{militar.DisplayNome()} movido para {(ala == 0 ? "sem ala" : $"{ala}ª Ala")}.");
            }
        };
        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItem is Militar militar)
                EditarMilitar(militar);
        };
        layout.Controls.Add(list, 0, 2);
        _listasAlas[ala] = list;
        parent.Controls.Add(panel);
    }

    private static readonly (string Cod, string Desc, string Bg, string Fg)[] LegendaItens =
    [
        ("S", "Serviço Operacional (24H x 72H)", "#FFFFFF", "#000000"),
        ("R", "Reforço Serviço Operacional (12H)", "#FFFFFF", "#000000"),
        ("D", "Dispensa Médica", "#FF0000", "#FFFFFF"),
        ("L", "Licença Médica", "#BFBFBF", "#000000"),
        ("FD", "Folga - Reposição (12H) - DIURNO", "#00B0F0", "#000000"),
        ("FN", "Folga - Reposição (12H) - NOTURNO", "#FFFF00", "#000000"),
        ("FR", "Folga Obrigatória (após cobertura)", "#4A148C", "#FFFFFF"),
        ("FA", "Férias Anuais", "#ED7D31", "#FFFFFF"),
        ("FP", "Férias Prêmio", "#ED7D31", "#FFFFFF"),
        ("LN", "Licença Núpcias", "#ED7D31", "#FFFFFF"),
        ("T", "Trânsito", "#FF00FF", "#000000"),
        ("O", "Outro (Especificar em Obs.)", "#8B0000", "#FFFFFF"),
        ("MO", "Movimentado", "#FFFF00", "#000000"),
        ("1ª~4ª Ala", "Remanejamento p/ outra Ala", "#5B9BD5", "#FFFFFF"),
    ];

    private TabPage BuildEscalaTab()
    {
        var tab = NewTab("Escala do mês");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = CorFundo,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
        tab.Controls.Add(root);

        var toolbar = ToolbarPanel();
        toolbar.Dock = DockStyle.Fill;
        toolbar.Controls.Add(PrimaryButton("Gerar escala assistida", GerarEscalaAssistente, 190));
        toolbar.Controls.Add(SecondaryButton("Atualizar visualização", RefreshEscalaView, 190));
        toolbar.Controls.Add(SecondaryButton("Desfazer coberturas", DesfazerCoberturasAutomaticas, 180));
        toolbar.Controls.Add(SecondaryButton("Gerar PDF da Escala", GerarPdf, 180));
        toolbar.Controls.Add(SecondaryButton("Inserir militar na ala...", InserirMilitarNaAlaAtual, 200));
        toolbar.Controls.Add(SecondaryButton("Limpar edições manuais", LimparEdicoesManuaisAla, 190));
        root.Controls.Add(toolbar, 0, 0);

        _tabsEscala = new TabControl { Dock = DockStyle.Fill };
        root.Controls.Add(_tabsEscala, 0, 1);
        for (var ala = 1; ala <= 4; ala++)
        {
            var page = new TabPage($"{ala}ª Ala") { BackColor = CorFundo, Padding = new Padding(8) };
            var grid = Grid();
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.CellMouseDown += EscalaCellMouseDown;
            page.Controls.Add(grid);
            _gridsEscala[ala] = grid;
            _tabsEscala.TabPages.Add(page);
        }

        var legendaGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ColumnHeadersVisible = false,
            ScrollBars = ScrollBars.None,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            CellBorderStyle = DataGridViewCellBorderStyle.Single,
            GridColor = CorBorda,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 22 },
            EnableHeadersVisualStyles = false,
        };
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c1", FillWeight = 30 });
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "d1", FillWeight = 170 });
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c2", FillWeight = 30 });
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "d2", FillWeight = 170 });
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "c3", FillWeight = 30 });
        legendaGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "d3", FillWeight = 170 });

        var headerRow = legendaGrid.Rows.Add("LEGENDA:", "", "", "", "", "");
        legendaGrid.Rows[headerRow].DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold | FontStyle.Italic),
            ForeColor = CorPrimaria,
            BackColor = ColorTranslator.FromHtml("#F4F6F9"),
        };
        var cols = 3;
        for (var i = 0; i < LegendaItens.Length; i += cols)
        {
            var vals = new string[cols * 2];
            for (var j = 0; j < cols; j++)
            {
                var idx = i + j;
                if (idx < LegendaItens.Length)
                {
                    vals[j * 2] = LegendaItens[idx].Cod;
                    vals[j * 2 + 1] = LegendaItens[idx].Desc;
                }
                else
                {
                    vals[j * 2] = "";
                    vals[j * 2 + 1] = "";
                }
            }
            var rowIdx = legendaGrid.Rows.Add(vals);
            for (var j = 0; j < cols; j++)
            {
                var idx = i + j;
                if (idx < LegendaItens.Length)
                {
                    var bgColor = ColorTranslator.FromHtml(LegendaItens[idx].Bg);
                    var fgColor = ColorTranslator.FromHtml(LegendaItens[idx].Fg);
                    legendaGrid.Rows[rowIdx].Cells[j * 2].Style = new DataGridViewCellStyle
                    {
                        BackColor = bgColor,
                        ForeColor = fgColor,
                        Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                    };
                    legendaGrid.Rows[rowIdx].Cells[j * 2 + 1].Style = new DataGridViewCellStyle
                    {
                        Font = new Font("Segoe UI", 8F),
                    };
                }
            }
        }
        root.Controls.Add(legendaGrid, 0, 2);
        return tab;
    }

    private TabPage BuildObservacoesTab()
    {
        var tab = NewTab("Observações && 2º Esforço");
        tab.AutoScroll = false;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = CorFundo,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        tab.Controls.Add(root);

        // Obs Gerais card
        var obsCard = CardPanel();
        obsCard.Dock = DockStyle.Fill;
        obsCard.Padding = new Padding(12);
        var obsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        obsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        obsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        obsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        obsCard.Controls.Add(obsLayout);
        obsLayout.Controls.Add(new Label { Text = "Observações Gerais (rodapé do PDF)", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = CorPrimaria }, 0, 0);
        obsLayout.Controls.Add(new Label { Text = "Uma observação por linha. Use *texto* para destacar em negrito.  ·  Ctrl+Z desfaz.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = CorTextoMuted }, 0, 1);
        _txtObsGerais = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 10F) };
        obsLayout.Controls.Add(_txtObsGerais, 0, 2);
        root.Controls.Add(obsCard, 0, 0);

        // Obs por Ala — sub-tabs
        var alaTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(10, 4) };
        for (var ala = 1; ala <= 4; ala++)
        {
            var page = new TabPage($"{ala}ª Ala") { Padding = new Padding(8) };
            var header = new Label
            {
                Text = $"Observações da {ala}ª Ala — aparece no rodapé desta ala. Uma linha = uma observação.",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 9F),
                ForeColor = CorTextoMuted,
            };
            var txt = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 10F) };
            page.Controls.Add(txt);
            page.Controls.Add(header);
            _txtObsAlas[ala] = txt;
            alaTabs.TabPages.Add(page);
        }
        root.Controls.Add(alaTabs, 0, 1);

        // 2º Esforço
        var esforcoCard = CardPanel();
        esforcoCard.Dock = DockStyle.Fill;
        esforcoCard.Padding = new Padding(12);
        var esforcoLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        esforcoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        esforcoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        esforcoLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
        esforcoCard.Controls.Add(esforcoLayout);

        var esforcoHeader = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        esforcoHeader.Controls.Add(new Label
        {
            Text = "Escala de 2º Esforço (ADM / GPV)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = CorPrimaria,
            AutoSize = true,
            Padding = new Padding(0, 6, 20, 0),
        });
        esforcoHeader.Controls.Add(CompactButton(PrimaryButton("Gerar automático", GerarEsforcoAutomaticoAtual, 150)));
        esforcoHeader.Controls.Add(CompactButton(SecondaryButton("Novo manual", NovoEsforco, 120)));
        esforcoHeader.Controls.Add(CompactButton(SecondaryButton("Editar", EditarEsforco, 100)));
        esforcoHeader.Controls.Add(CompactButton(SecondaryButton("Remover", RemoverEsforco, 100)));
        esforcoLayout.Controls.Add(esforcoHeader, 0, 0);

        _gridEsforco = Grid();
        _gridEsforco.ReadOnly = true;
        _gridEsforco.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridEsforco.MultiSelect = false;
        _gridEsforco.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridEsforco.Columns.Add(new DataGridViewTextBoxColumn { Name = "militar", HeaderText = "Militar empenhado", FillWeight = 200 });
        _gridEsforco.Columns.Add(new DataGridViewTextBoxColumn { Name = "de", HeaderText = "DE", FillWeight = 120 });
        _gridEsforco.Columns.Add(new DataGridViewTextBoxColumn { Name = "ate", HeaderText = "ATÉ", FillWeight = 120 });
        _gridEsforco.DoubleClick += (_, _) => EditarEsforco();
        esforcoLayout.Controls.Add(_gridEsforco, 0, 1);
        root.Controls.Add(esforcoCard, 0, 2);

        // Save button
        var savePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 4, 0, 0),
        };
        savePanel.Controls.Add(CompactButton(PrimaryButton("Salvar observações", SalvarObs, 180)));
        root.Controls.Add(savePanel, 0, 3);

        return tab;
    }

    private TabPage BuildUnidadeTab()
    {
        var tab = NewTab("Unidade && Comando");
        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(24), BackColor = CorFundo };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        _txtUnidade = AddTextField(root, "Unidade completa (cabeçalho):");
        _txtCidade = AddTextField(root, "Cidade do quartel:");
        _txtDataHomologacao = AddTextField(root, "Data de homologação:");
        _cmbCmtPel = AddComboField(root, "CMT do Pelotão:");
        _cmbCmtCia = AddComboField(root, "CMT da CIA:");
        var save = PrimaryButton("Salvar Unidade", SalvarUnidade, 160);
        var row = root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.Controls.Add(new Label(), 0, row);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = CorFundo,
            Padding = new Padding(0, 8, 0, 0),
        };
        footer.Controls.Add(save);
        root.Controls.Add(footer, 1, row);
        return tab;
    }

    private static readonly Dictionary<string, (string Label, Color Cor)> _tipoAusenciaInfo = new()
    {
        ["FA"] = ("Férias Anuais", ColorTranslator.FromHtml("#1565C0")),
        ["FP"] = ("Férias Prêmio", ColorTranslator.FromHtml("#0277BD")),
        ["L"]  = ("Licença Médica", ColorTranslator.FromHtml("#757575")),
        ["D"]  = ("Dispensa Médica", ColorTranslator.FromHtml("#E65100")),
        ["FD"] = ("Folga Diurna", ColorTranslator.FromHtml("#2E7D32")),
        ["FN"] = ("Folga Noturna", ColorTranslator.FromHtml("#1B5E20")),
        ["FR"] = ("Folga Obrigatória", ColorTranslator.FromHtml("#4A148C")),
        ["LN"] = ("Licença Núpcias", ColorTranslator.FromHtml("#AD1457")),
        ["T"]  = ("Trânsito", ColorTranslator.FromHtml("#4E342E")),
        ["MO"] = ("Movimentado", ColorTranslator.FromHtml("#37474F")),
        ["O"]  = ("Outro", ColorTranslator.FromHtml("#616161")),
        ["1ª Ala"] = ("Remanejamento 1ª Ala", ColorTranslator.FromHtml("#1F4E79")),
        ["2ª Ala"] = ("Remanejamento 2ª Ala", ColorTranslator.FromHtml("#2E7D32")),
        ["3ª Ala"] = ("Remanejamento 3ª Ala", ColorTranslator.FromHtml("#6A1B9A")),
        ["4ª Ala"] = ("Remanejamento 4ª Ala", ColorTranslator.FromHtml("#BF360C")),
    };

    private TabPage BuildRegistroFolgasTab()
    {
        var tab = NewTab("Registro Folgas/Férias");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tab.Controls.Add(root);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = CorFundo,
            Padding = new Padding(0, 6, 0, 0),
        };
        toolbar.Controls.Add(new Label
        {
            Text = "Filtrar tipo:",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(0, 6, 4, 0),
        });
        _filtroRegistroTipo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        _filtroRegistroTipo.Items.Add("Todos");
        foreach (var kv in _tipoAusenciaInfo)
            _filtroRegistroTipo.Items.Add($"{kv.Key} - {kv.Value.Label}");
        _filtroRegistroTipo.SelectedIndex = 0;
        _filtroRegistroTipo.SelectedIndexChanged += (_, _) => RefreshRegistro();
        toolbar.Controls.Add(_filtroRegistroTipo);
        root.Controls.Add(toolbar, 0, 0);

        _gridRegistro = Grid();
        _gridRegistro.ReadOnly = true;
        _gridRegistro.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _gridRegistro.Columns.AddRange(
            new DataGridViewTextBoxColumn { Name = "Tipo", HeaderText = "Tipo", FillWeight = 60 },
            new DataGridViewTextBoxColumn { Name = "Militar", HeaderText = "Militar", FillWeight = 120 },
            new DataGridViewTextBoxColumn { Name = "Posto", HeaderText = "Posto", FillWeight = 50 },
            new DataGridViewTextBoxColumn { Name = "Ala", HeaderText = "Ala", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "Inicio", HeaderText = "Início", FillWeight = 55 },
            new DataGridViewTextBoxColumn { Name = "Fim", HeaderText = "Fim", FillWeight = 55 },
            new DataGridViewTextBoxColumn { Name = "Obs", HeaderText = "Observação", FillWeight = 100 },
            new DataGridViewTextBoxColumn { Name = "Origem", HeaderText = "Origem", FillWeight = 55 }
        );
        _gridRegistro.CellFormatting += GridRegistro_CellFormatting;
        root.Controls.Add(_gridRegistro, 0, 1);

        _lblRegistroResumo = new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = CorPrimaria,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0),
        };
        root.Controls.Add(_lblRegistroResumo, 0, 2);
        return tab;
    }

    private void GridRegistro_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_gridRegistro.Rows[e.RowIndex].Tag is not string tipo)
            return;
        if (_tipoAusenciaInfo.TryGetValue(tipo, out var info))
        {
            if (e.ColumnIndex == _gridRegistro.Columns["Tipo"]!.Index)
            {
                e.CellStyle!.ForeColor = info.Cor;
                e.CellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
            e.CellStyle!.BackColor = e.RowIndex % 2 == 0 ? Color.White : ColorTranslator.FromHtml("#F7F9FB");
        }
    }

    private static readonly HashSet<string> _tiposRemanejamento = ["1ª Ala", "2ª Ala", "3ª Ala", "4ª Ala"];

    private void RefreshRegistro()
    {
        if (_gridRegistro is null) return;
        _gridRegistro.Rows.Clear();

        var filtro = _filtroRegistroTipo.SelectedItem?.ToString() ?? "Todos";
        var tipoFiltro = filtro == "Todos" ? null : filtro.Split(" - ")[0];

        var registros = new List<(string Tipo, string Militar, string Posto, string Ala, string Inicio, string Fim, string Obs, string Origem)>();

        foreach (var mil in _militares)
        {
            foreach (var aus in mil.Ausencias)
            {
                if (tipoFiltro != null && aus.Tipo != tipoFiltro) continue;
                if (_cmbMes.SelectedItem is int mes && _cmbAno.SelectedItem is int ano && !AusenciaSobrepoePeriodo(aus, mes, ano))
                    continue;

                var origem = _tiposRemanejamento.Contains(aus.Tipo) || aus.Tipo == "FR"
                    ? "Sistema (auto)"
                    : "Manual";

                registros.Add((
                    aus.Tipo,
                    mil.Nome,
                    mil.Posto,
                    mil.Ala > 0 ? $"{mil.Ala}ª" : "—",
                    aus.DataInicio,
                    aus.DataFim,
                    aus.Observacao,
                    origem
                ));
            }
        }

        registros.Sort((a, b) =>
        {
            var da = EscalaLogic.ParseDataBr(a.Inicio);
            var db = EscalaLogic.ParseDataBr(b.Inicio);
            if (da.HasValue && db.HasValue) return da.Value.CompareTo(db.Value);
            if (da.HasValue) return -1;
            if (db.HasValue) return 1;
            return 0;
        });

        foreach (var r in registros)
        {
            var label = _tipoAusenciaInfo.TryGetValue(r.Tipo, out var info) ? info.Label : r.Tipo;
            var idx = _gridRegistro.Rows.Add(r.Tipo, r.Militar, r.Posto, r.Ala, r.Inicio, r.Fim, r.Obs, r.Origem);
            _gridRegistro.Rows[idx].Tag = r.Tipo;
        }

        var total = registros.Count;
        var manuais = registros.Count(r => r.Origem == "Manual");
        var auto = registros.Count(r => r.Origem == "Sistema (auto)");
        _lblRegistroResumo.Text = $"Total no período: {total} registro(s)  |  Manuais: {manuais}  |  Automáticos: {auto}";
    }

    private TabPage BuildDiagnosticoTab()
    {
        var tab = NewTab("Diagnóstico");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        var toolbar = ToolbarPanel();
        toolbar.Dock = DockStyle.Fill;
        toolbar.Controls.Add(PrimaryButton("Executar diagnóstico", ExecutarDiagnostico, 180));
        toolbar.Controls.Add(SecondaryButton("Reequilibrar Motoristas D", RebalancearD, 210));
        root.Controls.Add(toolbar, 0, 0);

        _txtDiagnostico = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10F),
            BackColor = Color.White,
            ForeColor = CorTexto,
        };
        root.Controls.Add(_txtDiagnostico, 0, 1);
        return tab;
    }

    private void LoadEscalaPeriodo()
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        _escalaAtual = Storage.LoadEscala(mes, ano) ?? new EscalaMensal { Mes = mes, Ano = ano };
        Storage.HerdarObservacoesDoMesAnterior(_escalaAtual);
        AtualizarEscala2EsforcoAoCarregar();
        Storage.SaveEscala(_escalaAtual);

        LoadEscalaIntoControls();
        RefreshAll();
        Status($"Período: {EscalaLogic.MesesPt[mes]}/{ano}");
    }

    private void LoadEscalaIntoControls()
    {
        if (_escalaAtual is null)
            return;

        _txtObsGerais.Text = string.Join(Environment.NewLine, _escalaAtual.ObservacoesGerais);
        foreach (var (ala, txt) in _txtObsAlas)
        {
            _escalaAtual.ObservacoesAlas.TryGetValue(ala.ToString(), out var linhas);
            txt.Text = string.Join(Environment.NewLine, linhas ?? []);
        }
        _txtUnidade.Text = _escalaAtual.Unidade;
        _txtCidade.Text = _escalaAtual.Cidade;
        _txtDataHomologacao.Text = _escalaAtual.DataHomologacao;
        RefreshCmtCombos();
        SelectComboMilitar(_cmbCmtPel, _escalaAtual.CmtPelNumero);
        SelectComboMilitar(_cmbCmtCia, _escalaAtual.CmtCiaNumero);
        RefreshEsforco();
    }

    private void RefreshAll()
    {
        RefreshDashboard();
        RefreshMilitares();
        RefreshAlas();
        RefreshEscalaView();
        RefreshEsforco();
        RefreshCmtCombos();
        RefreshRegistro();
    }

    private void RefreshDashboard()
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        var alertas = EscalaLogic.Diagnosticar(_militares, mes, ano);
        _cardTotal.Text = _militares.Count.ToString();
        _cardMotoristas.Text = _militares.Count(m => m.EhMotoristaD).ToString();
        _cardAlertas.Text = alertas.Count(a => a.Severidade == "alta").ToString();
        _cardPeriodo.Text = $"{EscalaLogic.MesesPt[mes][..3]}/{ano}";

        _resumoAlasPanel.Controls.Clear();
        for (var ala = 1; ala <= 4; ala++)
        {
            var militaresAla = _militares.Where(m => m.Ala == ala).ToList();
            var cardWidth = _resumoAlasPanel.ClientSize.Width > 0
                ? Math.Max(250, (_resumoAlasPanel.ClientSize.Width - 56) / 4)
                : 300;
            var panel = CardPanel();
            panel.Width = cardWidth;
            panel.Height = 108;
            panel.Margin = new Padding(6);

            var cardLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                BackColor = CorCartao,
                Padding = new Padding(10, 6, 10, 6),
            };
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            cardLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(cardLayout);
            cardLayout.Controls.Add(new Label
            {
                Text = $"{ala}ª Ala",
                ForeColor = CorPrimaria,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            }, 0, 0);
            cardLayout.Controls.Add(new Label
            {
                Text = $"{militaresAla.Count} militares",
                ForeColor = CorTexto,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            }, 0, 1);
            cardLayout.Controls.Add(new Label
            {
                Text = $"Mot. D: {militaresAla.Count(m => m.EhMotoristaD)}    Sgt: {militaresAla.Count(m => m.GrupoPosto == "SUBTEN/SGT")}",
                ForeColor = CorTextoMuted,
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            }, 0, 2);
            _resumoAlasPanel.Controls.Add(panel);
        }
    }

    private void RefreshMilitares()
    {
        var selected = MilitarSelecionado()?.Numero;
        _gridMilitares.Rows.Clear();
        var secao = _filtroSecao.SelectedItem?.ToString() ?? "TODAS";
        var filtroAla = _filtroAla.SelectedItem?.ToString() ?? "Todas alas";
        var ordenados = EscalaLogic.MilitaresPorAntiguidade(_militares);

        for (var idx = 0; idx < ordenados.Count; idx++)
        {
            var militar = ordenados[idx];
            if (secao != "TODAS" && militar.Secao != secao)
                continue;
            if (filtroAla != "Todas alas")
            {
                if (filtroAla == "Sem ala" && militar.Ala != 0)
                    continue;
                if (filtroAla != "Sem ala" && militar.Ala != int.Parse(filtroAla[0].ToString(), CultureInfo.InvariantCulture))
                    continue;
            }

            var aus = _cmbMes.SelectedItem is int mes && _cmbAno.SelectedItem is int ano
                ? ResumoAusencias(militar, mes, ano)
                : ResumoAusencias(militar);
            if (string.IsNullOrWhiteSpace(aus))
                aus = militar.Observacoes;
            var rowIdx = _gridMilitares.Rows.Add(idx + 1, militar.Numero, militar.Posto, militar.Nome,
                militar.CategoriaCnh, militar.Secao, militar.Ala == 0 ? "-" : militar.Ala,
                militar.Funcao, aus, militar.BancoHorasStr());
            var row = _gridMilitares.Rows[rowIdx];
            row.Tag = militar;
            if (!string.IsNullOrWhiteSpace(aus))
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FFEBEE");
            else if (militar.GrupoPosto == "SUBTEN/SGT")
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#E3F2FD");
            else if (militar.GrupoPosto == "OFICIAIS")
                row.DefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FFF4E5");
            if (selected == militar.Numero)
                row.Selected = true;
        }
    }

    private string ResumoAusencias(Militar militar, int? mes = null, int? ano = null)
    {
        var linhas = militar.Ausencias
            .Where(a => !a.CoberturaAutomatica)
            .Where(a => !mes.HasValue || !ano.HasValue || AusenciaSobrepoePeriodo(a, mes.Value, ano.Value))
            .Select(ResumoAusencia)
            .Where(s => !string.IsNullOrWhiteSpace(s));

        return string.Join("; ", linhas);
    }

    private static string ResumoAusencia(Ausencia ausencia)
    {
        var codigo = string.IsNullOrWhiteSpace(ausencia.Tipo) ? "" : ausencia.Tipo.Trim();
        var data = ausencia.DataInicio;
        if (!string.IsNullOrWhiteSpace(ausencia.DataFim) && ausencia.DataFim != ausencia.DataInicio)
            data = $"{ausencia.DataInicio}-{ausencia.DataFim}";

        var obs = (ausencia.Observacao ?? "").Trim();
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(codigo))
            partes.Add(codigo);
        if (!string.IsNullOrWhiteSpace(data))
            partes.Add(data);

        var resumo = string.Join(" ", partes);
        return string.IsNullOrWhiteSpace(obs)
            ? resumo
            : $"{resumo} - {obs}".Trim(' ', '-');
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

    private void RefreshAlas()
    {
        foreach (var (ala, list) in _listasAlas)
        {
            list.BeginUpdate();
            list.Items.Clear();
            var items = _militares.Where(m => ala == 0 ? m.Ala == 0 : m.Ala == ala)
                .OrderBy(m => m.ChaveAntiguidade.Posto)
                .ThenBy(m => m.ChaveAntiguidade.Ordem)
                .ToList();
            foreach (var militar in items)
                list.Items.Add(militar);
            if (list.Tag is Label stats)
                stats.Text = $"Militares: {items.Count}    Mot. D: {items.Count(m => m.EhMotoristaD)}    Sgt: {items.Count(m => m.GrupoPosto == "SUBTEN/SGT")}";
            list.EndUpdate();
        }
    }

    private void RefreshEscalaView()
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        foreach (var ala in Enumerable.Range(1, 4))
        {
            var grid = _gridsEscala[ala];
            grid.Rows.Clear();
            grid.Columns.Clear();
            grid.Columns.Add("ord", "ORD.");
            grid.Columns.Add("numero", "Nº");
            grid.Columns.Add("posto", "P/G");
            grid.Columns.Add("nome", "NOME");
            grid.Columns.Add("mot", "MOT CAT");
            grid.Columns["nome"]!.Width = 260;

            var militaresAla = _militares.Where(m => m.Ala == ala)
                .OrderBy(m => m.ChaveAntiguidade.Posto)
                .ThenBy(m => m.ChaveAntiguidade.Ordem)
                .ToList();
            var (dias, grade) = EscalaLogic.MontarGradeAla(militaresAla, _militares, ala, mes, ano, _escalaAtual);
            foreach (var dia in dias)
                grid.Columns.Add($"d{dia:ddMMyyyy}", $"{EscalaLogic.NomeDiaSemana(dia)}\n{dia.Day}/{EscalaLogic.MesAbrev[dia.Month].ToLowerInvariant()}.");
            grid.Columns.Add("funcao", "FUNÇÃO");
            grid.Columns.Add("obs", "OBSERVAÇÕES");
            grid.Columns["funcao"]!.Width = 150;
            grid.Columns["obs"]!.Width = 240;

            var mapa = _militares.ToDictionary(m => m.Numero, m => m);
            var titulares = militaresAla.Select(m => m.Numero).ToHashSet();
            var visitantes = grade.Keys.Where(n => !titulares.Contains(n) && mapa.ContainsKey(n)).Select(n => mapa[n]);
            var linhas = militaresAla.Concat(visitantes).ToList();
            var ord = 1;
            foreach (var militar in linhas)
            {
                var titular = titulares.Contains(militar.Numero);
                var cells = grade.GetValueOrDefault(militar.Numero, []);
                var values = new List<object?>
                {
                    titular ? ord : "-",
                    militar.Numero,
                    militar.Posto,
                    militar.Nome,
                    militar.CategoriaCnh,
                };
                values.AddRange(cells.Select(c => c.Valor));
                values.Add(militar.Funcao);
                values.Add(ResumoAusencias(militar, mes, ano));

                var rowIndex = grid.Rows.Add(values.ToArray());
                var row = grid.Rows[rowIndex];
                row.Tag = militar;
                for (var i = 0; i < cells.Count; i++)
                {
                    var cell = row.Cells[5 + i];
                    cell.Tag = new EscalaCellTag(militar, dias[i], ala);
                    StyleEscalaCell(cell, cells[i]);
                }
                if (titular)
                    ord++;
            }
        }
    }

    private void RefreshEsforco()
    {
        if (_escalaAtual is null || _gridEsforco is null)
            return;
        _gridEsforco.Rows.Clear();
        var mapa = _militares.ToDictionary(m => m.Numero, m => m);
        foreach (var item in OrdenarItensEsforco(_escalaAtual.Escala2Esforco))
        {
            item.TryGetValue("militar_numero", out var numero);
            item.TryGetValue("de", out var de);
            item.TryGetValue("ate", out var ate);
            var nome = numero is not null && mapa.TryGetValue(numero, out var militar) ? militar.DisplayNome() : "";
            var row = _gridEsforco.Rows.Add(nome, de ?? "", ate ?? "");
            _gridEsforco.Rows[row].Tag = item;
        }
    }

    private void AtualizarEscala2EsforcoAoCarregar()
    {
        if (_escalaAtual is null)
            return;

        if (_escalaAtual.Escala2Esforco.Count > 0)
        {
            AtualizarReferenciaEsforco(_escalaAtual.Escala2Esforco, _escalaAtual.Mes, _escalaAtual.Ano);
            OrdenarEscala2Esforco(_escalaAtual);
            var geradaAtual = GerarEscala2Esforco(_escalaAtual.Mes, _escalaAtual.Ano);
            if (!EhMesBaseEsforco(_escalaAtual.Mes, _escalaAtual.Ano)
                && PareceEscala2EsforcoAutomatica(_escalaAtual)
                && geradaAtual.Count > 0
                && !ItensEsforcoIguais(_escalaAtual.Escala2Esforco, geradaAtual))
            {
                _escalaAtual.Escala2Esforco = geradaAtual;
                Storage.SaveEscala(_escalaAtual);
            }
            return;
        }

        var gerada = GerarEscala2Esforco(_escalaAtual.Mes, _escalaAtual.Ano);
        if (gerada.Count > 0)
            _escalaAtual.Escala2Esforco = gerada;
    }

    private void GerarEsforcoAutomaticoAtual()
    {
        if (_escalaAtual is null)
            return;

        if (_escalaAtual.Escala2Esforco.Count > 0)
        {
            AtualizarReferenciaEsforco(_escalaAtual.Escala2Esforco, _escalaAtual.Mes, _escalaAtual.Ano);
            if (MessageBox.Show(this,
                    "Substituir a escala de 2º esforço deste mês pela geração automática?",
                    "Gerar 2º esforço",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;
        }

        var gerada = GerarEscala2Esforco(_escalaAtual.Mes, _escalaAtual.Ano);
        if (gerada.Count == 0)
        {
            MessageBox.Show(this,
                "Nenhum militar elegível para a escala de 2º esforço. Confira as seções marcadas em Configurações.",
                "2º esforço",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _escalaAtual.Escala2Esforco = gerada;
        Storage.SaveEscala(_escalaAtual);
        RefreshEsforco();
        Status("Escala de 2º esforço gerada.");
    }

    private List<Dictionary<string, string>> GerarEscala2Esforco(int mes, int ano)
    {
        var referencia = ObterReferenciaEsforco(mes, ano);
        if (referencia.Ordem.Count == 0)
            return [];

        var elegiveis = MilitaresElegiveisEsforco().ToDictionary(m => m.Numero, m => m);
        var diffMeses = MesesEntre(referencia.BaseMes, referencia.BaseAno, mes, ano);
        var inicioFila = Mod(-diffMeses, referencia.Ordem.Count);
        var ponteiro = inicioFila;
        var linhas = new List<Dictionary<string, string>>();

        foreach (var inicio in QuartasDoMes(mes, ano).Select(d => d.Date.AddHours(8)))
        {
            var fim = inicio.AddDays(7);
            var escolhido = ProximoDisponivelEsforco(referencia.Ordem, elegiveis, ponteiro, inicio, fim);
            if (escolhido is null)
                continue;

            linhas.Add(new Dictionary<string, string>
            {
                ["militar_numero"] = escolhido.Value.Militar.Numero,
                ["de"] = EscalaLogic.FmtDataHoraCbmmg(inicio),
                ["ate"] = EscalaLogic.FmtDataHoraCbmmg(fim),
            });

            ponteiro = Mod(escolhido.Value.Index + 1, referencia.Ordem.Count);
        }

        return linhas;
    }

    private (List<string> Ordem, int BaseMes, int BaseAno) ObterReferenciaEsforco(int mes, int ano)
    {
        var cfg = Storage.LoadConfig();
        var elegiveis = MilitaresElegiveisEsforco();
        var elegiveisSet = elegiveis.Select(m => m.Numero).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordem = DistinctValidos(cfg["ordem_2esforco"], elegiveisSet);
        var basePeriodo = ParseBaseEsforco(cfg);
        var semente = EncontrarOrdemEsforcoSalva();
        var alterou = false;

        if (ordem.Count == 0 && semente.Ordem.Count > 0)
        {
            ordem = DistinctValidos(semente.Ordem, elegiveisSet);
            basePeriodo = (semente.Mes, semente.Ano);
            alterou = true;
        }

        foreach (var militar in elegiveis)
        {
            if (!ordem.Contains(militar.Numero, StringComparer.OrdinalIgnoreCase))
            {
                ordem.Add(militar.Numero);
                alterou = true;
            }
        }

        if (basePeriodo is null)
        {
            basePeriodo = semente.Ordem.Count > 0 ? (semente.Mes, semente.Ano) : (mes, ano);
            alterou = true;
        }

        if (!cfg["ordem_2esforco"].SequenceEqual(ordem) || alterou)
        {
            cfg["ordem_2esforco"] = ordem;
            cfg["base_2esforco"] = [$"{basePeriodo.Value.Ano}", $"{basePeriodo.Value.Mes:00}"];
            Storage.SaveConfig(cfg);
        }

        return (ordem, basePeriodo.Value.Mes, basePeriodo.Value.Ano);
    }

    private void AtualizarReferenciaEsforco(IEnumerable<Dictionary<string, string>> itens, int mes, int ano)
    {
        var cfg = Storage.LoadConfig();
        var elegiveis = MilitaresElegiveisEsforco();
        var elegiveisSet = elegiveis.Select(m => m.Numero).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var manuais = itens
            .Select(i => i.TryGetValue("militar_numero", out var numero) ? numero : "")
            .Where(n => !string.IsNullOrWhiteSpace(n) && elegiveisSet.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (manuais.Count == 0)
            return;

        var ordem = DistinctValidos(cfg["ordem_2esforco"], elegiveisSet);
        if (ordem.Count == 0)
            ordem.AddRange(manuais);
        else
            ordem.AddRange(manuais.Where(n => !ordem.Contains(n, StringComparer.OrdinalIgnoreCase)));

        foreach (var militar in elegiveis)
        {
            if (!ordem.Contains(militar.Numero, StringComparer.OrdinalIgnoreCase))
                ordem.Add(militar.Numero);
        }

        cfg["ordem_2esforco"] = ordem;
        if (ParseBaseEsforco(cfg) is null)
            cfg["base_2esforco"] = [$"{ano}", $"{mes:00}"];
        Storage.SaveConfig(cfg);
    }

    private List<Militar> MilitaresElegiveisEsforco()
    {
        var cfg = Storage.LoadConfig();
        var secoes = cfg["secoes_2esforco"].ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (secoes.Count == 0)
            return [];

        return _militares
            .Where(m => !string.IsNullOrWhiteSpace(m.Numero))
            .Where(m => secoes.Contains(m.Secao))
            .Where(m => !ModelConstants.PostosOficiais.Contains(m.Posto))
            .OrderBy(m => cfg["ordem_2esforco"].FindIndex(n => string.Equals(n, m.Numero, StringComparison.OrdinalIgnoreCase)) is var idx && idx >= 0 ? idx : int.MaxValue)
            .ThenBy(m => _militares.IndexOf(m))
            .ToList();
    }

    private (List<string> Ordem, int Mes, int Ano) EncontrarOrdemEsforcoSalva()
    {
        if (_escalaAtual?.Escala2Esforco.Count > 0)
            return (OrdemDosItensEsforco(_escalaAtual.Escala2Esforco), _escalaAtual.Mes, _escalaAtual.Ano);

        foreach (var periodo in Storage.ListEscalas())
        {
            var escala = Storage.LoadEscala(periodo.Mes, periodo.Ano);
            if (escala?.Escala2Esforco.Count > 0)
                return (OrdemDosItensEsforco(escala.Escala2Esforco), periodo.Mes, periodo.Ano);
        }

        return ([], 0, 0);
    }

    private static List<string> OrdemDosItensEsforco(IEnumerable<Dictionary<string, string>> itens) =>
        itens
            .Select(i => i.TryGetValue("militar_numero", out var numero) ? numero : "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool EhMesBaseEsforco(int mes, int ano)
    {
        var cfg = Storage.LoadConfig();
        var basePeriodo = ParseBaseEsforco(cfg);
        return basePeriodo.HasValue && basePeriodo.Value.Mes == mes && basePeriodo.Value.Ano == ano;
    }

    private static bool PareceEscala2EsforcoAutomatica(EscalaMensal escala)
    {
        var itens = OrdenarItensEsforco(escala.Escala2Esforco);
        var quartas = QuartasDoMes(escala.Mes, escala.Ano)
            .Select(d => d.Date.AddHours(8))
            .ToList();
        if (itens.Count != quartas.Count)
            return false;

        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            if (item.ContainsKey("nome_manual"))
                return false;
            if (!item.TryGetValue("de", out var deRaw) || !item.TryGetValue("ate", out var ateRaw))
                return false;

            var de = EscalaLogic.ParseDataHoraCbmmg(deRaw);
            var ate = EscalaLogic.ParseDataHoraCbmmg(ateRaw);
            if (!de.HasValue || !ate.HasValue)
                return false;
            if (de.Value != quartas[i] || ate.Value != quartas[i].AddDays(7))
                return false;
        }

        return true;
    }

    private static bool ItensEsforcoIguais(IEnumerable<Dictionary<string, string>> atuais, IEnumerable<Dictionary<string, string>> gerados)
    {
        var listaAtual = OrdenarItensEsforco(atuais);
        var listaGerada = OrdenarItensEsforco(gerados);
        if (listaAtual.Count != listaGerada.Count)
            return false;

        for (var i = 0; i < listaAtual.Count; i++)
        {
            if (!string.Equals(ValorItemEsforco(listaAtual[i], "militar_numero"), ValorItemEsforco(listaGerada[i], "militar_numero"), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ValorItemEsforco(listaAtual[i], "de"), ValorItemEsforco(listaGerada[i], "de"), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(ValorItemEsforco(listaAtual[i], "ate"), ValorItemEsforco(listaGerada[i], "ate"), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string ValorItemEsforco(Dictionary<string, string> item, string chave) =>
        item.TryGetValue(chave, out var valor) ? valor : "";

    private static List<string> DistinctValidos(IEnumerable<string> numeros, HashSet<string> validos) =>
        numeros
            .Where(n => !string.IsNullOrWhiteSpace(n) && validos.Contains(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static (int Mes, int Ano)? ParseBaseEsforco(Dictionary<string, List<string>> cfg)
    {
        if (!cfg.TryGetValue("base_2esforco", out var baseInfo) || baseInfo.Count < 2)
            return null;
        return int.TryParse(baseInfo[0], out var ano) && int.TryParse(baseInfo[1], out var mes)
            ? (mes, ano)
            : null;
    }

    private static (int Index, Militar Militar)? ProximoDisponivelEsforco(
        IReadOnlyList<string> ordem,
        IReadOnlyDictionary<string, Militar> elegiveis,
        int inicio,
        DateTime de,
        DateTime ate)
    {
        for (var tentativa = 0; tentativa < ordem.Count; tentativa++)
        {
            var idx = Mod(inicio + tentativa, ordem.Count);
            if (!elegiveis.TryGetValue(ordem[idx], out var militar))
                continue;
            if (!MilitarTemAusenciaNoPeriodo(militar, de, ate))
                return (idx, militar);
        }

        return null;
    }

    private static bool MilitarTemAusenciaNoPeriodo(Militar militar, DateTime de, DateTime ate)
    {
        var inicioPeriodo = de.Date;
        var fimPeriodo = ate.Date;
        foreach (var ausencia in militar.Ausencias)
        {
            var inicio = EscalaLogic.ParseDataBr(ausencia.DataInicio);
            var fim = EscalaLogic.ParseDataBr(ausencia.DataFim) ?? inicio;
            if (inicio.HasValue && fim.HasValue && inicio.Value.Date <= fimPeriodo && fim.Value.Date >= inicioPeriodo)
                return true;
        }

        return false;
    }

    private static List<DateTime> QuartasDoMes(int mes, int ano)
    {
        var last = DateTime.DaysInMonth(ano, mes);
        var dias = new List<DateTime>();
        for (var dia = 1; dia <= last; dia++)
        {
            var dt = new DateTime(ano, mes, dia);
            if (dt.DayOfWeek == DayOfWeek.Wednesday)
                dias.Add(dt);
        }
        return dias;
    }

    private static int MesesEntre(int mesBase, int anoBase, int mes, int ano) =>
        (ano - anoBase) * 12 + (mes - mesBase);

    private static int Mod(int value, int divisor) =>
        ((value % divisor) + divisor) % divisor;

    private static void OrdenarEscala2Esforco(EscalaMensal escala) =>
        escala.Escala2Esforco = OrdenarItensEsforco(escala.Escala2Esforco);

    private static List<Dictionary<string, string>> OrdenarItensEsforco(IEnumerable<Dictionary<string, string>> itens) =>
        itens
            .OrderBy(i => i.TryGetValue("de", out var de) ? EscalaLogic.ParseDataHoraCbmmg(de) ?? DateTime.MaxValue : DateTime.MaxValue)
            .ToList();

    private void RefreshCmtCombos()
    {
        if (_cmbCmtPel is null || _cmbCmtCia is null)
            return;
        var pel = (_cmbCmtPel.SelectedItem as MilitarComboItem)?.Numero;
        var cia = (_cmbCmtCia.SelectedItem as MilitarComboItem)?.Numero;
        FillCmtCombo(_cmbCmtPel);
        FillCmtCombo(_cmbCmtCia);
        SelectComboMilitar(_cmbCmtPel, pel ?? _escalaAtual?.CmtPelNumero ?? "");
        SelectComboMilitar(_cmbCmtCia, cia ?? _escalaAtual?.CmtCiaNumero ?? "");
    }

    private void FillCmtCombo(ComboBox combo)
    {
        combo.Items.Clear();
        combo.Items.Add(new MilitarComboItem("", ""));
        foreach (var militar in EscalaLogic.MilitaresPorAntiguidade(_militares))
            combo.Items.Add(new MilitarComboItem(militar.Numero, $"{militar.Numero} - {militar.Posto} {militar.Nome}"));
    }

    private void SelectComboMilitar(ComboBox combo, string numero)
    {
        foreach (var item in combo.Items.OfType<MilitarComboItem>())
        {
            if (item.Numero == numero)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void NovoMilitar()
    {
        using var dlg = new MilitarDialog(null);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Militar is null)
            return;
        if (_militares.Any(m => m.Numero == dlg.Militar.Numero))
        {
            MessageBox.Show(this, $"Já existe militar com número {dlg.Militar.Numero}.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        _militares.Add(dlg.Militar);
        Storage.SaveMilitares(_militares);
        RefreshAll();
        Status("Militar salvo.");
    }

    private void EditarMilitarSelecionado()
    {
        var militar = MilitarSelecionado();
        if (militar is null)
        {
            MessageBox.Show(this, "Selecione um militar na lista.", "Editar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        EditarMilitar(militar);
    }

    private void EditarMilitar(Militar militar)
    {
        using var dlg = new MilitarDialog(militar);
        var result = dlg.ShowDialog(this);
        if (result == DialogResult.Abort)
        {
            _militares = _militares.Where(m => m.Numero != militar.Numero).ToList();
            Storage.SaveMilitares(_militares);
            RefreshAll();
            Status("Militar excluído.");
            return;
        }
        if (result != DialogResult.OK || dlg.Militar is null)
            return;

        var novo = dlg.Militar;
        if (novo.Numero != militar.Numero && _militares.Any(m => m.Numero == novo.Numero))
        {
            MessageBox.Show(this, $"Já existe militar com número {novo.Numero}.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        militar.Numero = novo.Numero;
        militar.Posto = novo.Posto;
        militar.Nome = novo.Nome;
        militar.NomeGuerra = novo.NomeGuerra;
        militar.CategoriaCnh = novo.CategoriaCnh;
        militar.Secao = novo.Secao;
        militar.Ala = novo.Ala;
        militar.Ordem = novo.Ordem;
        militar.Funcao = novo.Funcao;
        militar.Observacoes = novo.Observacoes;
        Storage.SaveMilitares(_militares);
        RefreshAll();
        Status("Militar salvo.");
    }

    private void AusenciasSelecionado()
    {
        var militar = MilitarSelecionado();
        if (militar is null)
        {
            MessageBox.Show(this, "Selecione um militar.", "Ausências", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        GerenciarAusencias(militar);
    }

    private void GerenciarAusencias(Militar militar)
    {
        // Ensure we use the canonical reference from _militares
        var canonical = _militares.FirstOrDefault(m => m.Numero == militar.Numero);
        if (canonical is not null)
            militar = canonical;

        using var dlg = new AusenciasManagerDialog(militar);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Storage.SaveMilitares(_militares);
            RefreshAll();
            Status("Ausências salvas.");
        }
    }

    private void MoverMilitar(int direcao)
    {
        var militar = MilitarSelecionado();
        if (militar is null)
            return;
        var mesmoPosto = _militares.Where(m => m.Posto == militar.Posto).OrderBy(m => m.Ordem).ToList();
        var idx = mesmoPosto.IndexOf(militar);
        var novo = idx + direcao;
        if (idx < 0 || novo < 0 || novo >= mesmoPosto.Count)
            return;
        (mesmoPosto[idx], mesmoPosto[novo]) = (mesmoPosto[novo], mesmoPosto[idx]);
        for (var i = 0; i < mesmoPosto.Count; i++)
            mesmoPosto[i].Ordem = i;
        Storage.SaveMilitares(_militares);
        RefreshAll();
        SelectMilitar(militar.Numero);
    }

    private void EditarAlas()
    {
        using var dlg = new AlasDialog(_alas, _militares);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Storage.SaveAlas(_alas);
            Status("Configuração de alas salva.");
        }
    }

    private void EscalaCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.Button != MouseButtons.Right || e.RowIndex < 0)
            return;

        var alaVisao = AlaDoGrid(grid);
        if (alaVisao == 0)
            return;

        grid.ClearSelection();
        var menu = new ContextMenuStrip();

        if (e.ColumnIndex >= 5 && grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag is EscalaCellTag tag)
        {
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;

            menu.Items.Add("Lançar ausência neste dia", null, (_, _) => AddAusenciaDia(tag.Militar, tag.Dia));
            var mover = new ToolStripMenuItem("Mover para outra ala");
            for (var ala = 1; ala <= 4; ala++)
            {
                var destino = ala;
                if (destino == tag.AlaOrigem)
                    continue;
                mover.DropDownItems.Add($"{destino}ª Ala", null, (_, _) => AplicarRemanejamento(tag.Militar, destino, tag.Dia));
            }
            menu.Items.Add(mover);
            menu.Items.Add("Editar todas as ausências", null, (_, _) => GerenciarAusencias(tag.Militar));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Editar célula manualmente...", null, (_, _) => EditarCelulaManual(tag.Militar, tag.AlaOrigem, tag.Dia));
            menu.Items.Add("Limpar edição manual desta célula", null, (_, _) => LimparCelulaManual(tag.Militar, tag.AlaOrigem, tag.Dia));
            menu.Items.Add(new ToolStripSeparator());
        }
        else
        {
            grid.Rows[e.RowIndex].Selected = true;
        }

        Militar? militarLinha = null;
        if (grid.Rows[e.RowIndex].Tag is Militar m)
            militarLinha = m;

        menu.Items.Add($"Inserir militar avulso na {alaVisao}ª Ala...", null, (_, _) => InserirMilitarNaAla(alaVisao));
        if (militarLinha is not null && militarLinha.Ala != alaVisao)
        {
            menu.Items.Add($"Remover {militarLinha.DisplayNome()} desta ala (visualização)", null,
                (_, _) => OcultarMilitarDaAla(militarLinha, alaVisao));
        }

        menu.Show(grid, grid.PointToClient(Cursor.Position));
    }

    private int AlaDoGrid(DataGridView grid)
    {
        foreach (var (ala, g) in _gridsEscala)
            if (ReferenceEquals(g, grid))
                return ala;
        return 0;
    }

    private void EditarCelulaManual(Militar militar, int ala, DateTime dia)
    {
        if (_escalaAtual is null)
            return;
        var atual = _escalaAtual.CelulasManuais
            .FirstOrDefault(c => c.Ala == ala && c.MilitarNumero == militar.Numero && c.Data == EscalaLogic.FmtDataBr(dia));
        using var dlg = new CelulaManualDialog(militar, ala, dia, atual?.Valor);
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        var valor = dlg.Valor ?? "";
        _escalaAtual.CelulasManuais.RemoveAll(c => c.Ala == ala && c.MilitarNumero == militar.Numero && c.Data == EscalaLogic.FmtDataBr(dia));
        if (valor.Length > 0)
        {
            _escalaAtual.CelulasManuais.Add(new CelulaManual
            {
                Ala = ala,
                MilitarNumero = militar.Numero,
                Data = EscalaLogic.FmtDataBr(dia),
                Valor = valor,
            });
        }
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Célula manual atualizada.");
    }

    private void LimparCelulaManual(Militar militar, int ala, DateTime dia)
    {
        if (_escalaAtual is null)
            return;
        var n = _escalaAtual.CelulasManuais.RemoveAll(c => c.Ala == ala && c.MilitarNumero == militar.Numero && c.Data == EscalaLogic.FmtDataBr(dia));
        if (n == 0)
            return;
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Edição manual removida.");
    }

    private void InserirMilitarNaAla(int ala)
    {
        if (_escalaAtual is null)
            return;
        var grid = _gridsEscala[ala];
        var jaListados = grid.Rows.Cast<DataGridViewRow>()
            .Select(r => r.Tag as Militar)
            .Where(m => m is not null)
            .Select(m => m!.Numero)
            .ToHashSet();
        var disponiveis = EscalaLogic.MilitaresPorAntiguidade(_militares)
            .Where(m => !jaListados.Contains(m.Numero))
            .ToList();
        if (disponiveis.Count == 0)
        {
            MessageBox.Show(this, "Todos os militares já estão visíveis nesta ala.", "Inserir", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new SelecionarMilitarDialog(disponiveis, $"Inserir militar avulso na {ala}ª Ala");
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.MilitarNumero is null)
            return;

        _escalaAtual.OcultacoesAla.RemoveAll(o => o.Ala == ala && o.MilitarNumero == dlg.MilitarNumero);
        if (!_escalaAtual.InsercoesAla.Any(i => i.Ala == ala && i.MilitarNumero == dlg.MilitarNumero))
        {
            _escalaAtual.InsercoesAla.Add(new InsercaoAla
            {
                Ala = ala,
                MilitarNumero = dlg.MilitarNumero,
            });
        }
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Militar inserido na ala. Edite as células com botão direito.");
    }

    private void InserirMilitarNaAlaAtual()
    {
        var ala = _tabsEscala.SelectedIndex + 1;
        if (ala is < 1 or > 4)
            return;
        InserirMilitarNaAla(ala);
    }

    private void LimparEdicoesManuaisAla()
    {
        if (_escalaAtual is null)
            return;
        var ala = _tabsEscala.SelectedIndex + 1;
        if (ala is < 1 or > 4)
            return;
        var ins = _escalaAtual.InsercoesAla.Count(i => i.Ala == ala);
        var oc = _escalaAtual.OcultacoesAla.Count(o => o.Ala == ala);
        var cm = _escalaAtual.CelulasManuais.Count(c => c.Ala == ala);
        if (ins + oc + cm == 0)
        {
            MessageBox.Show(this, $"Não há edições manuais na {ala}ª Ala.", "Limpar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this,
                $"Remover todas as edições manuais da {ala}ª Ala?\n\n" +
                $"  • {ins} inserção(ões) avulsa(s)\n" +
                $"  • {oc} ocultação(ões)\n" +
                $"  • {cm} célula(s) editada(s)",
                "Limpar edições manuais",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _escalaAtual.InsercoesAla.RemoveAll(i => i.Ala == ala);
        _escalaAtual.OcultacoesAla.RemoveAll(o => o.Ala == ala);
        _escalaAtual.CelulasManuais.RemoveAll(c => c.Ala == ala);
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Edições manuais removidas.");
    }

    private void OcultarMilitarDaAla(Militar militar, int ala)
    {
        if (_escalaAtual is null)
            return;
        if (MessageBox.Show(this,
                $"Remover {militar.DisplayNome()} da visualização da {ala}ª Ala?\n\nA ocultação afeta apenas esta tela e o PDF; ausências e remanejamentos ficam preservados.",
                "Remover da visualização",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _escalaAtual.InsercoesAla.RemoveAll(i => i.Ala == ala && i.MilitarNumero == militar.Numero);
        _escalaAtual.CelulasManuais.RemoveAll(c => c.Ala == ala && c.MilitarNumero == militar.Numero);
        if (!_escalaAtual.OcultacoesAla.Any(o => o.Ala == ala && o.MilitarNumero == militar.Numero))
        {
            _escalaAtual.OcultacoesAla.Add(new OcultacaoAla
            {
                Ala = ala,
                MilitarNumero = militar.Numero,
            });
        }
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Militar ocultado da visualização da ala.");
    }

    private void AddAusenciaDia(Militar militar, DateTime dia)
    {
        using var dlg = new AusenciaDialog(null, dia);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Ausencia is not null)
        {
            militar.Ausencias.Add(dlg.Ausencia);
            Storage.SaveMilitares(_militares);
            RefreshAll();
            Status("Ausência lançada.");
        }
    }

    private void AplicarRemanejamento(Militar militar, int alaDestino, DateTime dia)
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano || _escalaAtual is null)
            return;

        var (valido, motivo, folga) = EscalaLogic.ValidarRemanejamento(militar, militar.Ala, alaDestino, dia, mes, ano);
        if (!valido)
        {
            MessageBox.Show(this, motivo, "Remanejamento proibido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (folga == 48)
        {
            var res = MessageBox.Show(this,
                $"{motivo}\n\nRegistrar +24h no banco de horas?",
                "Folga curta",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
            if (res == DialogResult.Cancel)
                return;
            if (res == DialogResult.Yes)
                militar.HorasExtrasMin += 24 * 60;
        }
        else if (MessageBox.Show(this, motivo + "\n\nConfirmar remanejamento?", "Remanejamento", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        militar.Ausencias.Add(new Ausencia
        {
            Tipo = $"{alaDestino}ª Ala",
            DataInicio = EscalaLogic.FmtDataBr(dia),
            DataFim = EscalaLogic.FmtDataBr(dia),
            Observacao = $"Remanejado para {alaDestino}ª Ala",
        });
        _escalaAtual.Remanejamentos.Add(new RemanejamentoLog
        {
            MilitarNumero = militar.Numero,
            Data = EscalaLogic.FmtDataBr(dia),
            DeAla = militar.Ala,
            ParaAla = alaDestino,
            Motivo = motivo,
            FolgaHoras = folga,
        });

        Storage.SaveMilitares(_militares);
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        Status("Remanejamento aplicado.");
    }

    private void NovoEsforco()
    {
        if (_escalaAtual is null)
            return;
        var candidatos = MilitaresElegiveisEsforco();
        if (candidatos.Count == 0)
        {
            MessageBox.Show(this, "Nenhum militar elegível. Marque as seções em Configurações.", "2º esforço", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dlg = new EsforcoDialog(candidatos, null);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Value is not null)
        {
            _escalaAtual.Escala2Esforco.Add(dlg.Value);
            AtualizarReferenciaEsforco(_escalaAtual.Escala2Esforco, _escalaAtual.Mes, _escalaAtual.Ano);
            OrdenarEscala2Esforco(_escalaAtual);
            Storage.SaveEscala(_escalaAtual);
            RefreshEsforco();
        }
    }

    private void EditarEsforco()
    {
        if (_escalaAtual is null || _gridEsforco.CurrentRow?.Tag is not Dictionary<string, string> item)
            return;
        var candidatos = MilitaresElegiveisEsforco();
        if (item.TryGetValue("militar_numero", out var numeroAtual) &&
            _militares.FirstOrDefault(m => m.Numero == numeroAtual) is { } atual &&
            candidatos.All(m => m.Numero != atual.Numero))
        {
            candidatos.Add(atual);
        }
        using var dlg = new EsforcoDialog(candidatos, item);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Value is not null)
        {
            var idx = _escalaAtual.Escala2Esforco.IndexOf(item);
            if (idx >= 0)
                _escalaAtual.Escala2Esforco[idx] = dlg.Value;
            AtualizarReferenciaEsforco(_escalaAtual.Escala2Esforco, _escalaAtual.Mes, _escalaAtual.Ano);
            OrdenarEscala2Esforco(_escalaAtual);
            Storage.SaveEscala(_escalaAtual);
            RefreshEsforco();
        }
    }

    private void RemoverEsforco()
    {
        if (_escalaAtual is null || _gridEsforco.CurrentRow?.Tag is not Dictionary<string, string> item)
            return;
        if (MessageBox.Show(this, "Remover esta linha de 2º esforço?", "Remover", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _escalaAtual.Escala2Esforco.Remove(item);
            OrdenarEscala2Esforco(_escalaAtual);
            Storage.SaveEscala(_escalaAtual);
            RefreshEsforco();
        }
    }

    private void SalvarObs()
    {
        if (_escalaAtual is null)
            return;
        SalvarObsSilencioso();
        MessageBox.Show(this, "Observações salvas com sucesso.", "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SalvarObsSilencioso()
    {
        if (_escalaAtual is null)
            return;
        _escalaAtual.ObservacoesGerais = Lines(_txtObsGerais.Text);
        var obsAlas = new Dictionary<string, List<string>>();
        foreach (var (ala, txt) in _txtObsAlas)
        {
            var lines = Lines(txt.Text);
            if (lines.Count > 0)
                obsAlas[ala.ToString()] = lines;
        }
        _escalaAtual.ObservacoesAlas = obsAlas;
        _escalaAtual.ObservacoesDefinidas = true;
        Storage.SaveEscala(_escalaAtual);
        Status("Observações salvas.");
    }

    private void SalvarUnidade()
    {
        if (_escalaAtual is null)
            return;
        SalvarUnidadeSilencioso();
        MessageBox.Show(this, "Configurações da unidade salvas.", "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SalvarUnidadeSilencioso()
    {
        if (_escalaAtual is null)
            return;
        _escalaAtual.Unidade = string.IsNullOrWhiteSpace(_txtUnidade.Text) ? _escalaAtual.Unidade : _txtUnidade.Text.Trim();
        _escalaAtual.Cidade = string.IsNullOrWhiteSpace(_txtCidade.Text) ? "Formiga" : _txtCidade.Text.Trim();
        _escalaAtual.DataHomologacao = _txtDataHomologacao.Text.Trim();
        _escalaAtual.CmtPelNumero = (_cmbCmtPel.SelectedItem as MilitarComboItem)?.Numero ?? "";
        _escalaAtual.CmtCiaNumero = (_cmbCmtCia.SelectedItem as MilitarComboItem)?.Numero ?? "";
        Storage.SaveEscala(_escalaAtual);
        Status("Unidade salva.");
    }

    private void ExecutarDiagnostico()
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        var alertas = EscalaLogic.Diagnosticar(_militares, mes, ano);
        _txtDiagnostico.Clear();
        if (alertas.Count == 0)
        {
            _txtDiagnostico.AppendText("Nenhum problema crítico detectado." + Environment.NewLine + Environment.NewLine);
        }
        else
        {
            var alta = alertas.Where(a => a.Severidade == "alta").ToList();
            var media = alertas.Where(a => a.Severidade == "media").ToList();
            _txtDiagnostico.AppendText($"{alta.Count} alertas críticos  -  {media.Count} avisos{Environment.NewLine}{Environment.NewLine}");
            if (alta.Count > 0)
            {
                _txtDiagnostico.AppendText("=== CRÍTICOS ===" + Environment.NewLine);
                foreach (var a in alta)
                    _txtDiagnostico.AppendText("  [!] " + a.Mensagem + Environment.NewLine);
                _txtDiagnostico.AppendText(Environment.NewLine);
            }
            if (media.Count > 0)
            {
                _txtDiagnostico.AppendText("=== AVISOS ===" + Environment.NewLine);
                foreach (var a in media)
                    _txtDiagnostico.AppendText("  [i] " + a.Mensagem + Environment.NewLine);
            }
        }

        var sugestoes = EscalaLogic.SugerirRebalanceamentoD(_militares);
        if (sugestoes.Count > 0)
        {
            _txtDiagnostico.AppendText(Environment.NewLine + "Sugestões automáticas:" + Environment.NewLine);
            foreach (var s in sugestoes)
                _txtDiagnostico.AppendText("  - " + s + Environment.NewLine);
        }
        RefreshDashboard();
    }

    private void RebalancearD()
    {
        if (MessageBox.Show(this,
                "Mover automaticamente motoristas categoria D entre alas para igualar a distribuição?\n\nOs mais modernos serão movidos preferencialmente.",
                "Reequilibrar Motoristas D",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var n = EscalaLogic.AplicarRebalanceamentoD(_militares);
        Storage.SaveMilitares(_militares);
        RefreshAll();
        MessageBox.Show(this, $"{n} movimentos aplicados.", "Reequilíbrio concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void EqualizarEfetivo()
    {
        if (MessageBox.Show(this,
                "Mover militares mais modernos entre alas para igualar o número total?\n\nSargentos-chefe e motoristas únicos serão preservados.",
                "Equalizar efetivo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var n = EscalaLogic.AplicarRebalanceamentoEfetivo(_militares);
        Storage.SaveMilitares(_militares);
        RefreshAll();
        MessageBox.Show(this, $"{n} movimentos aplicados.", "Equalização concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CobrirAusenciasAutomaticamente()
    {
        if (_escalaAtual is null || _cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        if (MessageBox.Show(this,
                "Recalcular coberturas automáticas deste mês?\n\nCoberturas automáticas anteriores serão substituídas. Coberturas manuais serão mantidas.",
                "Cobrir ausências automaticamente",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var resultado = EscalaLogic.AplicarCoberturasAutomaticas(_militares, _escalaAtual, mes, ano);
        Storage.SaveMilitares(_militares);
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();

        var msg = resultado.DiasCobertos == 0
            ? "Nenhuma cobertura automática foi necessária."
            : $"{resultado.DiasCobertos} dia(s) coberto(s) em {resultado.Intervalos} intervalo(s).";
        if (resultado.Pendencias > 0)
            msg += $"\n\n{resultado.Pendencias} falta(s) continuaram sem candidato disponível em ala adjacente.";

        MessageBox.Show(this, msg, "Coberturas automáticas", MessageBoxButtons.OK,
            resultado.Pendencias > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void DesfazerCoberturasAutomaticas()
    {
        if (_escalaAtual is null || _cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;

        if (MessageBox.Show(this,
                "Desfazer todas as coberturas automáticas deste mês?\n\nRemanejamentos manuais serão mantidos.",
                "Desfazer coberturas",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var removidas = EscalaLogic.DesfazerCoberturasAutomaticas(_militares, _escalaAtual, mes, ano);
        Storage.SaveMilitares(_militares);
        Storage.SaveEscala(_escalaAtual);
        RefreshAll();
        MessageBox.Show(this, $"{removidas} registro(s) automático(s) removido(s).", "Coberturas desfeitas", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void GerarEscalaAssistente()
    {
        if (_cmbMes.SelectedItem is not int mes || _cmbAno.SelectedItem is not int ano)
            return;
        var alertas = EscalaLogic.Diagnosticar(_militares, mes, ano);
        var alta = alertas.Count(a => a.Severidade == "alta");
        var media = alertas.Count(a => a.Severidade == "media");
        var msg = $"Diagnóstico atual: {alta} problemas críticos e {media} avisos.\n\n" +
                  "Sim: cobrir ausências automaticamente\n" +
                  "Não: gerar PDF agora\n" +
                  "Cancelar: fechar";
        var res = MessageBox.Show(this, msg, "Assistente - Gerar Escala do Mês", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (res == DialogResult.Yes)
            CobrirAusenciasAutomaticamente();
        else if (res == DialogResult.No)
            GerarPdf();
    }

    private void SalvarTudo()
    {
        if (_escalaAtual is not null)
        {
            SalvarObsSilencioso();
            SalvarUnidadeSilencioso();
            Storage.SaveEscala(_escalaAtual);
        }
        Storage.SaveMilitares(_militares);
        Storage.SaveAlas(_alas);
        Status("Tudo salvo.");
        MessageBox.Show(this, "Todos os dados foram salvos com sucesso!", "Salvar tudo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void GerarPdf()
    {
        if (_escalaAtual is null)
            return;
        SalvarObsSilencioso();
        SalvarUnidadeSilencioso();
        var nome = $"ESCALA - {_escalaAtual.Cidade.ToUpperInvariant()} - {CultureTitle(EscalaLogic.MesesPt[_escalaAtual.Mes])} {_escalaAtual.Ano}.pdf";
        using var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            InitialDirectory = Storage.OutputDir,
            FileName = nome,
            Title = "Salvar PDF como...",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var caminho = dlg.FileName;
            if (!PythonPdfExporter.TryGenerate(_escalaAtual, caminho, out var pyError))
            {
                caminho = PdfExport.GerarPdf(_escalaAtual, _militares, _alas, dlg.FileName);
                Status($"PDF gerado pelo fallback C#: {pyError}");
            }
            Status($"PDF gerado: {caminho}");
            if (MessageBox.Show(this, $"Salvo em:\n{caminho}\n\nAbrir agora?", "PDF gerado", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Erro ao gerar PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AbrirPdfsGerados()
    {
        Directory.CreateDirectory(Storage.OutputDir);
        var pdfs = Directory.GetFiles(Storage.OutputDir, "*.pdf", SearchOption.TopDirectoryOnly);
        if (pdfs.Length == 0)
        {
            MessageBox.Show(this, $"Nenhum PDF encontrado em:\n{Storage.OutputDir}", "PDFs gerados", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Storage.OutputDir) { UseShellExecute = true });
    }

    private Militar? MilitarSelecionado()
    {
        if (_gridMilitares.CurrentRow?.Tag is Militar militar)
            return militar;
        return null;
    }

    private void SelectMilitar(string numero)
    {
        foreach (DataGridViewRow row in _gridMilitares.Rows)
        {
            if (row.Tag is Militar militar && militar.Numero == numero)
            {
                row.Selected = true;
                _gridMilitares.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private void StyleEscalaCell(DataGridViewCell cell, CelulaEscala escalaCell)
    {
        cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        var manual = escalaCell.Cor == "manual";

        if (escalaCell.Valor == "S")
        {
            cell.Style.Font = new Font(_gridMilitares.Font, FontStyle.Italic);
            if (manual)
            {
                cell.Style.BackColor = ColorTranslator.FromHtml("#FFF8DC");
                cell.ToolTipText = "Edição manual";
            }
            return;
        }

        if (escalaCell.Valor == "R")
        {
            cell.Style.BackColor = ColorTranslator.FromHtml("#B19CD9");
            cell.Style.ForeColor = Color.White;
            cell.Style.Font = new Font(_gridMilitares.Font, FontStyle.Bold);
            if (manual)
                cell.ToolTipText = "Edição manual";
            return;
        }

        if (escalaCell.Valor == "FR")
        {
            cell.Style.BackColor = ColorTranslator.FromHtml("#9970AB");
            cell.Style.ForeColor = Color.White;
            cell.Style.Font = new Font(_gridMilitares.Font, FontStyle.Bold);
            if (manual)
                cell.ToolTipText = "Edição manual";
            return;
        }

        var (bg, fg, bold) = escalaCell.Valor switch
        {
            "FA" or "FP" => ("#ED7D31", Color.White, true),
            "L" => ("#BFBFBF", Color.Black, true),
            "D" => ("#C00000", Color.White, true),
            "FD" => ("#00B0F0", Color.White, true),
            "FN" => ("#FFFF00", Color.Black, true),
            "O" => ("#FFC000", Color.Black, true),
            "MO" => ("#BF8F00", Color.White, true),
            "T" => ("#7B7B7B", Color.White, true),
            "LN" => ("#F2F2F2", Color.Black, true),
            _ when escalaCell.Valor.EndsWith("ª Ala", StringComparison.Ordinal) && escalaCell.Cor == "ala_origem" => ("#E7E6E6", ColorTranslator.FromHtml("#7B7B7B"), false),
            _ when escalaCell.Valor.EndsWith("ª Ala", StringComparison.Ordinal) => ("#5B9BD5", Color.White, true),
            _ => ("#FFFFFF", Color.Black, false),
        };
        cell.Style.BackColor = ColorTranslator.FromHtml(bg);
        cell.Style.ForeColor = fg;
        if (bold)
            cell.Style.Font = new Font(_gridMilitares.Font, FontStyle.Bold);
        if (manual)
            cell.ToolTipText = "Edição manual";
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(Storage.AssetsDir, "escala_bmc.ico");
            if (File.Exists(iconPath))
                return new Icon(iconPath);

            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private Button TopButton(string text, Action action)
    {
        var btn = new Button
        {
            Text = text,
            Height = 36,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = CorPrimaria,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => action();
        return btn;
    }

    private Button PrimaryButton(string text, Action action, int width = 140)
    {
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            Margin = new Padding(8, 10, 4, 8),
            BackColor = CorPrimaria,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = Padding.Empty,
            Cursor = Cursors.Hand,
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = CorPrimariaHover;
        btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#0A2442");
        btn.Click += (_, _) => action();
        return btn;
    }

    private Button SecondaryButton(string text, Action action, int width = 140)
    {
        var btn = PrimaryButton(text, action, width);
        btn.BackColor = Color.White;
        btn.ForeColor = CorPrimaria;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = CorBorda;
        btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#E8F1FA");
        btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#DCEBFA");
        return btn;
    }

    private static Button CompactButton(Button btn)
    {
        btn.Margin = new Padding(8, 3, 4, 3);
        btn.Padding = Padding.Empty;
        btn.TextAlign = ContentAlignment.MiddleCenter;
        return btn;
    }

    private Panel CardPanel() => new()
    {
        BackColor = CorCartao,
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(8),
    };

    private FlowLayoutPanel ToolbarPanel() => new()
    {
        BackColor = CorCartao,
        BorderStyle = BorderStyle.FixedSingle,
        Padding = new Padding(10, 8, 10, 8),
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        AutoScroll = false,
    };

    private Label SectionLabel(string text, DockStyle dock) => new()
    {
        Text = text,
        Dock = dock,
        Height = 34,
        ForeColor = CorPrimaria,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(12, 0, 0, 0),
        BackColor = CorCartao,
    };

    private TabPage NewTab(string text) => new(text) { BackColor = CorFundo, AutoScroll = true };

    private DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AllowUserToResizeColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        ScrollBars = ScrollBars.Both,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        RowHeadersVisible = false,
        RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = CorPrimaria, ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) },
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = CorTexto, SelectionBackColor = CorPrimaria, SelectionForeColor = Color.White, Padding = new Padding(3) },
        RowTemplate = { Height = 28 },
    };

    private static void SetFill(DataGridView grid, string column, float weight)
    {
        if (grid.Columns[column] is not { } col)
            return;
        col.FillWeight = weight;
        col.MinimumWidth = Math.Min(80, Math.Max(45, (int)weight));
    }

    private TextBox AddTextField(TableLayoutPanel root, string label)
    {
        var row = root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, row);
        var txt = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(4, 10, 4, 4) };
        root.Controls.Add(txt, 1, row);
        return txt;
    }

    private ComboBox AddComboField(TableLayoutPanel root, string label)
    {
        var row = root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }, 0, row);
        var cb = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 10, 4, 4) };
        root.Controls.Add(cb, 1, row);
        return cb;
    }

    private static List<string> Lines(string text) =>
        text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        return char.ToUpper(value[0]) + value[1..].ToLowerInvariant();
    }

    private void Status(string text) => _status.Text = text;

    private void DrawMilitarListItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ListBox list || e.Index < 0 || list.Items[e.Index] is not Militar militar)
            return;
        e.DrawBackground();
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var bg = selected ? CorPrimaria : Color.White;
        using var bgBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);
        var stripe = militar.Ala > 0 && _coresAla.TryGetValue(militar.Ala, out var c) ? c : CorTextoMuted;
        using var stripeBrush = new SolidBrush(stripe);
        var textColor = selected ? Color.White : CorTexto;
        var cardRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 5, e.Bounds.Width - 16, e.Bounds.Height - 10);
        using var borderPen = new Pen(CorBorda);
        using var cardBrush = new SolidBrush(selected ? CorPrimaria : Color.White);
        e.Graphics.FillRectangle(cardBrush, cardRect);
        e.Graphics.DrawRectangle(borderPen, cardRect);
        e.Graphics.FillRectangle(stripeBrush, cardRect.Left, cardRect.Top, 5, cardRect.Height);
        if (militar.EhMotoristaD)
        {
            var badgeRect = new Rectangle(cardRect.Right - 28, cardRect.Top + 7, 20, 20);
            using var badge = new SolidBrush(stripe);
            e.Graphics.FillRectangle(badge, badgeRect);
            TextRenderer.DrawText(e.Graphics, "D", new Font("Segoe UI", 8F, FontStyle.Bold), badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        TextRenderer.DrawText(e.Graphics, $"#{e.Index + 1}    {militar.Posto}", new Font("Segoe UI", 9F, FontStyle.Bold),
            new Rectangle(cardRect.Left + 12, cardRect.Top + 6, cardRect.Width - 45, 18), textColor, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, militar.Nome, new Font("Segoe UI", 9F),
            new Rectangle(cardRect.Left + 12, cardRect.Top + 29, cardRect.Width - 24, 18), textColor, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, $"{militar.Funcao}    CNH: {militar.CategoriaCnh}", new Font("Segoe UI", 8F, FontStyle.Italic),
            new Rectangle(cardRect.Left + 12, cardRect.Top + 51, cardRect.Width - 24, 18), selected ? Color.WhiteSmoke : CorTextoMuted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    private void DrawMainTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs)
            return;
        var selected = e.Index == tabs.SelectedIndex;
        var rect = e.Bounds;
        using var back = new SolidBrush(selected ? CorPrimaria : ColorTranslator.FromHtml("#D9D9D9"));
        using var border = new Pen(ColorTranslator.FromHtml("#9AA7B3"));
        e.Graphics.FillRectangle(back, rect);
        e.Graphics.DrawRectangle(border, rect);
        var text = tabs.TabPages[e.Index].Text;
        TextRenderer.DrawText(e.Graphics, text, new Font("Segoe UI", 9F, FontStyle.Bold), rect,
            selected ? Color.White : CorTexto, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private TabPage BuildConfiguracoesTab()
    {
        var tab = NewTab("Configurações");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(10), BackColor = CorFundo };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tab.Controls.Add(root);

        var header = CardPanel();
        header.Dock = DockStyle.Fill;
        header.Padding = new Padding(14, 10, 14, 10);
        var lblTitle = new Label
        {
            Text = "Configurações da Unidade",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = CorPrimaria,
            AutoSize = true,
            Location = new Point(14, 6),
        };
        var lblSub = new Label
        {
            Text = "Personalize as seções e funções disponíveis ao cadastrar militares. Útil para outras unidades que tenham seções diferentes.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = CorTextoMuted,
            AutoSize = true,
            Location = new Point(14, 32),
        };
        header.Controls.Add(lblTitle);
        header.Controls.Add(lblSub);
        root.Controls.Add(header, 0, 0);

        var cols = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = CorFundo };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(cols, 0, 1);

        cols.Controls.Add(BuildSecoesConfigList(), 0, 0);
        cols.Controls.Add(BuildConfigList("Funções", "Aparece no campo \"Função\" ao cadastrar militar.", out _lbFuncoes, "funcoes"), 1, 0);
        RecarregarListasConfig();
        return tab;
    }

    private Panel BuildSecoesConfigList()
    {
        var card = CardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(4);
        card.Padding = new Padding(14);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.Controls.Add(layout);

        layout.Controls.Add(new Label { Text = "Seções", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = CorPrimaria }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "Aparece no cadastro. Marque as seções que entram na escala de 2º esforço.",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            ForeColor = CorTextoMuted,
        }, 0, 1);

        _lbSecoes = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = CorTexto,
            CheckOnClick = true,
        };
        _lbSecoes.ItemCheck += (_, _) =>
        {
            if (_recarregandoConfig)
                return;
            BeginInvoke(new Action(SalvarSecoesSegundoEsforco));
        };
        layout.Controls.Add(_lbSecoes, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(PrimaryButton("+ Adicionar", () => AdicionarItemConfig("secoes", _lbSecoes), 120));
        buttons.Controls.Add(SecondaryButton("Renomear", () => RenomearItemConfig("secoes", _lbSecoes), 120));
        var btnRem = SecondaryButton("Remover", () => RemoverItemConfig("secoes", _lbSecoes), 120);
        btnRem.BackColor = CorCritico;
        btnRem.ForeColor = Color.White;
        buttons.Controls.Add(btnRem);
        layout.Controls.Add(buttons, 0, 3);
        return card;
    }

    private Panel BuildConfigList(string titulo, string descricao, out ListBox listBox, string chave)
    {
        var card = CardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(4);
        card.Padding = new Padding(14);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        card.Controls.Add(layout);

        layout.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = CorPrimaria }, 0, 0);
        layout.Controls.Add(new Label { Text = descricao, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), ForeColor = CorTextoMuted }, 0, 1);

        listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = CorTexto,
        };
        layout.Controls.Add(listBox, 0, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0) };
        var lb = listBox;
        buttons.Controls.Add(PrimaryButton("+ Adicionar", () => AdicionarItemConfig(chave, lb), 120));
        buttons.Controls.Add(SecondaryButton("Renomear", () => RenomearItemConfig(chave, lb), 120));
        var btnRem = SecondaryButton("Remover", () => RemoverItemConfig(chave, lb), 120);
        btnRem.BackColor = CorCritico;
        btnRem.ForeColor = Color.White;
        buttons.Controls.Add(btnRem);
        layout.Controls.Add(buttons, 0, 3);
        return card;
    }

    private void RecarregarListasConfig()
    {
        var cfg = Storage.LoadConfig();
        _recarregandoConfig = true;
        _lbSecoes.Items.Clear();
        var secoesEsforco = cfg["secoes_2esforco"].ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var s in cfg["secoes"])
        {
            var idx = _lbSecoes.Items.Add(s);
            _lbSecoes.SetItemChecked(idx, secoesEsforco.Contains(s));
        }
        _recarregandoConfig = false;

        _lbFuncoes.Items.Clear();
        foreach (var f in cfg["funcoes"])
            if (!string.IsNullOrEmpty(f))
                _lbFuncoes.Items.Add(f);
    }

    private void SalvarSecoesSegundoEsforco()
    {
        if (_recarregandoConfig || _lbSecoes is null)
            return;

        var cfg = Storage.LoadConfig();
        cfg["secoes_2esforco"] = _lbSecoes.CheckedItems.Cast<string>().ToList();
        Storage.SaveConfig(cfg);
        Status("Seções da escala de 2º esforço atualizadas.");
    }

    private void AdicionarItemConfig(string chave, ListBox lb)
    {
        var novo = PromptTexto("Adicionar", "Digite o novo item:");
        if (string.IsNullOrWhiteSpace(novo)) return;
        var cfg = Storage.LoadConfig();
        if (cfg[chave].Contains(novo))
        {
            MessageBox.Show(this, "Item já existe.", "Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        cfg[chave].Add(novo);
        if (chave == "secoes")
            cfg["secoes_2esforco"].Remove(novo);
        Storage.SaveConfig(cfg);
        RecarregarListasConfig();
    }

    private void RenomearItemConfig(string chave, ListBox lb)
    {
        if (lb.SelectedItem is not string sel)
        {
            MessageBox.Show(this, "Selecione um item.", "Renomear", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var novo = PromptTexto("Renomear", $"Novo nome para \"{sel}\":", sel);
        if (string.IsNullOrWhiteSpace(novo) || novo == sel) return;
        var cfg = Storage.LoadConfig();
        var idx = cfg[chave].IndexOf(sel);
        if (idx >= 0) cfg[chave][idx] = novo;
        if (chave == "secoes")
        {
            var idxEsforco = cfg["secoes_2esforco"].IndexOf(sel);
            if (idxEsforco >= 0)
                cfg["secoes_2esforco"][idxEsforco] = novo;
        }
        Storage.SaveConfig(cfg);
        RecarregarListasConfig();
    }

    private void RemoverItemConfig(string chave, ListBox lb)
    {
        if (lb.SelectedItem is not string sel)
        {
            MessageBox.Show(this, "Selecione um item.", "Remover", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"Remover \"{sel}\"?", "Remover", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        var cfg = Storage.LoadConfig();
        cfg[chave].Remove(sel);
        if (chave == "secoes")
            cfg["secoes_2esforco"].Remove(sel);
        Storage.SaveConfig(cfg);
        RecarregarListasConfig();
    }

    private string? PromptTexto(string titulo, string mensagem, string valorInicial = "")
    {
        using var dlg = new Form
        {
            Text = titulo,
            Width = 400,
            Height = 180,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        var lbl = new Label { Text = mensagem, Left = 20, Top = 20, AutoSize = true };
        var txt = new TextBox { Left = 20, Top = 50, Width = 340, Text = valorInicial };
        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 200, Top = 90, Width = 75 };
        var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 285, Top = 90, Width = 75 };
        dlg.Controls.AddRange([lbl, txt, btnOk, btnCancel]);
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;
        return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text.Trim() : null;
    }

    private sealed record EscalaCellTag(Militar Militar, DateTime Dia, int AlaOrigem);

    private sealed record MilitarComboItem(string Numero, string Label)
    {
        public override string ToString() => Label;
    }
}
