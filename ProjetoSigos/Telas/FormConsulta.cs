using MySql.Data.MySqlClient;
using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;

namespace SIGOS.Telas;

/// <summary>Consulta de ordens de serviço com filtros. Somente leitura; dois cliques abrem os detalhes.</summary>
[DesignerCategory("Code")]
public class FormConsulta : Form
{
    private readonly DateTimePicker dtDe = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Font = Estilo.FonteCampo };
    private readonly DateTimePicker dtAte = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Font = Estilo.FonteCampo };
    private readonly ComboBox cmbStatus = Estilo.NovoCombo();
    private readonly ComboBox cmbPrioridade = Estilo.NovoCombo();
    private readonly ComboBox cmbTecnico = Estilo.NovoCombo();
    private readonly TextBox txtBusca = Estilo.NovoTexto(100);
    private readonly Button btnPesquisar = new Button { Text = "Pesquisar" };
    private readonly Button btnLimpar = new Button { Text = "Limpar filtros" };
    private readonly DataGridView grid = new DataGridView();
    private readonly Label lblTotal = new Label();
    private bool carregando = true;

    public FormConsulta()
    {
        Text = "Consulta";
        Font = Estilo.Fonte;
        BackColor = Estilo.Fundo;

        // ----- Cabeçalho (igual ao das telas de cadastro) -----
        var cabecalho = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.White };
        cabecalho.Controls.Add(new Label
        {
            Text = "Consulta de Ordens de Serviço",
            Font = Estilo.FonteTitulo,
            ForeColor = Estilo.AzulEscuro,
            AutoSize = true,
            Location = new Point(28, 12)
        });
        cabecalho.Controls.Add(new Label
        {
            Text = "Filtre por período, status, prioridade ou técnico. Dê dois cliques em uma OS para ver os detalhes.",
            ForeColor = Estilo.CorSuave,
            AutoSize = true,
            Location = new Point(31, 56)
        });
        cabecalho.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Estilo.Borda });

        // ----- Cartão de filtros -----
        var tblFiltros = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2 };
        for (int i = 0; i < 5; i++) tblFiltros.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        tblFiltros.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        tblFiltros.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        txtBusca.PlaceholderText = "Nº da OS, cliente, equipamento, técnico, série...";

        tblFiltros.Controls.Add(Campo("Aberta a partir de", dtDe), 0, 0);
        tblFiltros.Controls.Add(Campo("Aberta até", dtAte), 1, 0);
        tblFiltros.Controls.Add(Campo("Status", cmbStatus), 2, 0);
        tblFiltros.Controls.Add(Campo("Prioridade", cmbPrioridade), 3, 0);
        tblFiltros.Controls.Add(Campo("Técnico", cmbTecnico), 4, 0);

        var campoBusca = Campo("Busca livre", txtBusca);
        tblFiltros.Controls.Add(campoBusca, 0, 1);
        tblFiltros.SetColumnSpan(campoBusca, 3);

        Estilo.BotaoPrimario(btnPesquisar, Estilo.Azul);
        Estilo.BotaoSecundario(btnLimpar);
        btnPesquisar.Size = new Size(130, 32);
        btnLimpar.Size = new Size(130, 32);
        var botoes = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 22, 0, 0)
        };
        botoes.Controls.Add(btnPesquisar);
        botoes.Controls.Add(btnLimpar);
        tblFiltros.Controls.Add(botoes, 3, 1);
        tblFiltros.SetColumnSpan(botoes, 2);

        var cartaoFiltros = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16) };
        cartaoFiltros.Controls.Add(tblFiltros);

        // ----- Cartão da lista -----
        Estilo.Grid(grid);
        grid.Dock = DockStyle.Fill;

        lblTotal.Dock = DockStyle.Fill;
        lblTotal.ForeColor = Estilo.CorSuave;
        lblTotal.TextAlign = ContentAlignment.MiddleRight;
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        rodape.Controls.Add(lblTotal);

        var cartaoLista = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(0) };
        cartaoLista.Controls.Add(grid);
        cartaoLista.Controls.Add(rodape);

        // ----- Corpo -----
        var corpo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(28, 20, 28, 24)
        };
        corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        corpo.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
        corpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        corpo.Controls.Add(cartaoFiltros, 0, 0);
        corpo.Controls.Add(cartaoLista, 0, 1);

        Controls.Add(corpo);
        Controls.Add(cabecalho);

        // ----- Eventos -----
        btnPesquisar.Click += (s, e) => Pesquisar();
        btnLimpar.Click += (s, e) => LimparFiltros();
        txtBusca.TextChanged += (s, e) => Pesquisar();
        txtBusca.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { Pesquisar(); e.SuppressKeyPress = true; }
        };
        cmbStatus.SelectedIndexChanged += (s, e) => Pesquisar();
        cmbPrioridade.SelectedIndexChanged += (s, e) => Pesquisar();
        cmbTecnico.SelectedIndexChanged += (s, e) => Pesquisar();
        dtDe.ValueChanged += (s, e) => Pesquisar();
        dtAte.ValueChanged += (s, e) => Pesquisar();

        grid.DataBindingComplete += (s, e) => FormatarColunas();
        grid.CellFormatting += Grid_CellFormatting;
        grid.DataError += (s, e) => e.ThrowException = false;
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            int id = Convert.ToInt32(grid.Rows[e.RowIndex].Cells["id"].Value);
            using var detalhe = new FormDetalheOS(id);
            detalhe.ShowDialog(this);
        };

        Load += (s, e) =>
        {
            try
            {
                CarregarFiltros();
                carregando = false;
                Pesquisar();
            }
            catch (MySqlException ex)
            {
                Caixa.Erro("Não foi possível acessar o banco de dados.\nConfira se o MySQL está ligado (verde) no XAMPP.\n\n" + ex.Message);
            }
        };
    }

    /// <summary>Rótulo pequeno em cima do campo, igual às telas de cadastro.</summary>
    private static Panel Campo(string rotulo, Control controle)
    {
        var painel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        controle.Dock = DockStyle.Top;
        var texto = new Label
        {
            Text = rotulo,
            Dock = DockStyle.Top,
            Height = 24,
            Font = Estilo.FonteRotulo,
            ForeColor = Estilo.CorSuave,
            TextAlign = ContentAlignment.BottomLeft
        };
        // Regra do Dock = Top: o último adicionado fica no topo.
        painel.Controls.Add(controle);
        painel.Controls.Add(texto);
        return painel;
    }

    private void CarregarFiltros()
    {
        cmbStatus.Items.Add(new Opcao("", "Todos"));
        cmbStatus.Items.AddRange(OrdemRegras.OpcoesStatus());
        cmbStatus.SelectedIndex = 0;

        cmbPrioridade.Items.Add(new Opcao("", "Todas"));
        cmbPrioridade.Items.AddRange(OrdemRegras.OpcoesPrioridade());
        cmbPrioridade.SelectedIndex = 0;

        cmbTecnico.Items.Add(new Opcao("", "Todos"));
        cmbTecnico.Items.Add(new Opcao("0", "(sem técnico)"));
        foreach (DataRow t in Banco.Tabela("SELECT id, nome FROM tecnicos ORDER BY nome").Rows)
            cmbTecnico.Items.Add(new Opcao(t["id"].ToString()!, t["nome"].ToString()!));
        cmbTecnico.SelectedIndex = 0;
    }

    private void LimparFiltros()
    {
        carregando = true;
        dtDe.Checked = false;
        dtAte.Checked = false;
        if (cmbStatus.Items.Count > 0) cmbStatus.SelectedIndex = 0;
        if (cmbPrioridade.Items.Count > 0) cmbPrioridade.SelectedIndex = 0;
        if (cmbTecnico.Items.Count > 0) cmbTecnico.SelectedIndex = 0;
        txtBusca.Clear();
        carregando = false;
        Pesquisar();
    }

    private void Pesquisar()
    {
        if (carregando) return;

        try
        {
            var condicoes = new List<string>();
            var parametros = new List<MySqlParameter>();

            if (dtDe.Checked && dtAte.Checked && dtDe.Value.Date > dtAte.Value.Date)
            {
                grid.DataSource = null;
                lblTotal.Text = "Período inválido: a data inicial é maior que a final.";
                return;
            }

            if (dtDe.Checked)
            {
                condicoes.Add("o.data_abertura >= @de");
                parametros.Add(Banco.P("@de", dtDe.Value.Date));
            }
            if (dtAte.Checked)
            {
                condicoes.Add("o.data_abertura < @ate");
                parametros.Add(Banco.P("@ate", dtAte.Value.Date.AddDays(1)));
            }
            if (cmbStatus.SelectedItem is Opcao status && status.Valor != "")
            {
                condicoes.Add("o.status = @status");
                parametros.Add(Banco.P("@status", status.Valor));
            }
            if (cmbPrioridade.SelectedItem is Opcao prioridade && prioridade.Valor != "")
            {
                condicoes.Add("o.prioridade = @prioridade");
                parametros.Add(Banco.P("@prioridade", prioridade.Valor));
            }
            if (cmbTecnico.SelectedItem is Opcao tecnico && tecnico.Valor != "")
            {
                if (tecnico.Valor == "0")
                {
                    condicoes.Add("o.tecnico_id IS NULL");
                }
                else
                {
                    condicoes.Add("o.tecnico_id = @tecnico");
                    parametros.Add(Banco.P("@tecnico", int.Parse(tecnico.Valor)));
                }
            }

            string busca = txtBusca.Text.Trim();
            if (busca.Length > 0)
            {
                condicoes.Add(@"(c.nome LIKE @busca OR e.tipo LIKE @busca OR e.marca LIKE @busca OR e.modelo LIKE @busca
                                 OR e.numero_serie LIKE @busca OR t.nome LIKE @busca OR CAST(o.id AS CHAR) LIKE @busca
                                 OR o.diagnostico LIKE @busca)");
                parametros.Add(Banco.P("@busca", "%" + busca + "%"));
            }

            string sql =
                @"SELECT o.id, c.nome AS cliente,
                         CONCAT_WS(' ', e.tipo, e.marca, e.modelo) AS equipamento,
                         IFNULL(t.nome, '—') AS tecnico,
                         o.prioridade, o.status, o.data_abertura, o.data_conclusao, o.total
                  FROM ordens_servico o
                  INNER JOIN clientes c ON c.id = o.cliente_id
                  INNER JOIN equipamentos e ON e.id = o.equipamento_id
                  LEFT JOIN tecnicos t ON t.id = o.tecnico_id "
                + (condicoes.Count > 0 ? "WHERE " + string.Join(" AND ", condicoes) : "")
                + " ORDER BY o.data_abertura DESC, o.id DESC";

            DataTable tabela = Banco.Tabela(sql, parametros.ToArray());
            grid.DataSource = tabela;

            grid.Columns["id"].HeaderText = "Nº OS";
            grid.Columns["id"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["id"].Width = 74;
            grid.Columns["cliente"].HeaderText = "Cliente";
            grid.Columns["equipamento"].HeaderText = "Equipamento";
            grid.Columns["tecnico"].HeaderText = "Técnico";
            grid.Columns["prioridade"].HeaderText = "Prioridade";
            grid.Columns["status"].HeaderText = "Status";
            grid.Columns["data_abertura"].HeaderText = "Abertura";
            grid.Columns["data_conclusao"].HeaderText = "Conclusão";
            grid.Columns["total"].HeaderText = "Total";
            grid.ClearSelection();

            decimal soma = 0m;
            foreach (DataRow linha in tabela.Rows)
                soma += Convert.ToDecimal(linha["total"]);

            lblTotal.Text = tabela.Rows.Count + " ordem(ns) de serviço   |   Valor total: " + soma.ToString("C2", Validador.Cultura);
        }
        catch (MySqlException ex)
        {
            Caixa.Erro("Erro ao consultar as ordens de serviço: " + ex.Message);
        }
    }

    private void FormatarColunas()
    {
        foreach (string nome in new[] { "data_abertura", "data_conclusao" })
            if (grid.Columns.Contains(nome))
                grid.Columns[nome].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

        if (grid.Columns.Contains("total"))
        {
            var estilo = grid.Columns["total"].DefaultCellStyle;
            estilo.Format = "C2";
            estilo.FormatProvider = Validador.Cultura;
            estilo.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        switch (grid.Columns[e.ColumnIndex].Name)
        {
            case "status" when e.Value != null && e.Value != DBNull.Value:
                e.Value = OrdemRegras.RotuloStatus(e.Value.ToString());
                e.FormattingApplied = true;
                break;
            case "prioridade" when e.Value != null && e.Value != DBNull.Value:
                e.Value = OrdemRegras.RotuloPrioridade(e.Value.ToString());
                e.FormattingApplied = true;
                break;
            case "data_conclusao" when e.Value == null || e.Value == DBNull.Value:
                e.Value = "—";
                e.FormattingApplied = true;
                break;
        }
    }
}
