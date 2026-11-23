using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;

namespace SIGOS.Telas;

/// <summary>Tudo sobre uma OS em modo leitura: dados, serviços lançados e histórico de status.</summary>
[DesignerCategory("Code")]
public class FormDetalheOS : Form
{
    private readonly int osId;
    private readonly Label lblTitulo = new Label();
    private readonly Label lblCliente = NovoValor();
    private readonly Label lblEquipamento = NovoValor();
    private readonly Label lblTecnico = NovoValor();
    private readonly Label lblPrioridade = NovoValor();
    private readonly Label lblStatus = NovoValor();
    private readonly Label lblAbertoPor = NovoValor();
    private readonly Label lblAbertura = NovoValor();
    private readonly Label lblConclusao = NovoValor();
    private readonly TextBox txtDiagnostico = new TextBox();
    private readonly TextBox txtSolucao = new TextBox();
    private readonly DataGridView gradeItens = new DataGridView();
    private readonly DataGridView gradeHistorico = new DataGridView();
    private readonly Label lblTotais = new Label();

    public FormDetalheOS(int osId)
    {
        this.osId = osId;

        Text = "Detalhes da ordem de serviço";
        Font = Estilo.Fonte;
        BackColor = Color.White;
        Size = new Size(1060, 740);
        MinimumSize = new Size(880, 620);
        StartPosition = FormStartPosition.CenterParent;

        lblTitulo.Text = "Ordem de Serviço nº " + osId;
        lblTitulo.Dock = DockStyle.Fill;
        lblTitulo.Font = new Font("Segoe UI Semibold", 16f);
        lblTitulo.ForeColor = Estilo.AzulEscuro;
        lblTitulo.TextAlign = ContentAlignment.MiddleLeft;

        // ----- Cartão com os dados da OS -----
        var info = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 6 };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) info.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        info.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        AdicionarPar(info, 0, 0, "Cliente", lblCliente);
        AdicionarPar(info, 2, 0, "Equipamento", lblEquipamento);
        AdicionarPar(info, 0, 1, "Técnico", lblTecnico);
        AdicionarPar(info, 2, 1, "Prioridade", lblPrioridade);
        AdicionarPar(info, 0, 2, "Status", lblStatus);
        AdicionarPar(info, 2, 2, "Aberta por", lblAbertoPor);
        AdicionarPar(info, 0, 3, "Abertura", lblAbertura);
        AdicionarPar(info, 2, 3, "Conclusão", lblConclusao);

        PrepararTextoLongo(txtDiagnostico);
        PrepararTextoLongo(txtSolucao);
        info.Controls.Add(NovoRotulo("Diagnóstico"), 0, 4);
        info.Controls.Add(txtDiagnostico, 1, 4);
        info.SetColumnSpan(txtDiagnostico, 3);
        info.Controls.Add(NovoRotulo("Solução"), 0, 5);
        info.Controls.Add(txtSolucao, 1, 5);
        info.SetColumnSpan(txtSolucao, 3);

        var cartaoInfo = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        cartaoInfo.Controls.Add(info);

        // ----- Cartão dos serviços -----
        Estilo.Grid(gradeItens);
        gradeItens.Dock = DockStyle.Fill;
        lblTotais.Dock = DockStyle.Bottom;
        lblTotais.Height = 30;
        lblTotais.Font = Estilo.FonteNegrito;
        lblTotais.ForeColor = Estilo.AzulEscuro;
        lblTotais.TextAlign = ContentAlignment.MiddleRight;

        var cartaoItens = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        cartaoItens.Controls.Add(gradeItens);
        cartaoItens.Controls.Add(lblTotais);
        cartaoItens.Controls.Add(NovoSubtitulo("Serviços lançados"));

        // ----- Cartão do histórico -----
        Estilo.Grid(gradeHistorico);
        gradeHistorico.Dock = DockStyle.Fill;
        gradeHistorico.CellFormatting += FormatarHistorico;

        var cartaoHistorico = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        cartaoHistorico.Controls.Add(gradeHistorico);
        cartaoHistorico.Controls.Add(NovoSubtitulo("Histórico de status"));

        // ----- Estrutura geral -----
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(24, 0, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 292));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(lblTitulo, 0, 0);
        raiz.SetColumnSpan(lblTitulo, 2);
        raiz.Controls.Add(cartaoInfo, 0, 1);
        raiz.SetColumnSpan(cartaoInfo, 2);
        raiz.Controls.Add(cartaoItens, 0, 2);
        raiz.Controls.Add(cartaoHistorico, 1, 2);
        Controls.Add(raiz);

        Load += (s, e) => Caixa.Tentar(Carregar);
    }

    private static Label NovoValor()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Estilo.CorTexto,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static Label NovoRotulo(string texto)
    {
        return new Label
        {
            Text = texto,
            Dock = DockStyle.Fill,
            Font = Estilo.FonteRotulo,
            ForeColor = Estilo.CorSuave,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Label NovoSubtitulo(string texto)
    {
        return new Label
        {
            Text = texto,
            Dock = DockStyle.Top,
            Height = 32,
            Font = Estilo.FonteNegrito,
            ForeColor = Estilo.AzulEscuro,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void AdicionarPar(TableLayoutPanel tabela, int coluna, int linha, string rotulo, Label valor)
    {
        tabela.Controls.Add(NovoRotulo(rotulo), coluna, linha);
        tabela.Controls.Add(valor, coluna + 1, linha);
    }

    private static void PrepararTextoLongo(TextBox caixa)
    {
        caixa.Multiline = true;
        caixa.ReadOnly = true;
        caixa.ScrollBars = ScrollBars.Vertical;
        caixa.BorderStyle = BorderStyle.FixedSingle;
        caixa.BackColor = Estilo.FundoSuave;
        caixa.ForeColor = Estilo.CorTexto;
        caixa.Dock = DockStyle.Fill;
        caixa.Margin = new Padding(0, 4, 0, 4);
        caixa.TabStop = false;
    }

    private static string Data(object valor)
    {
        return valor is DateTime d ? d.ToString("dd/MM/yyyy HH:mm") : "—";
    }

    private void Carregar()
    {
        DataTable os = Banco.Tabela(
            @"SELECT c.nome AS cliente,
                     CONCAT_WS(' ', e.tipo, e.marca, e.modelo) AS equipamento,
                     t.nome AS tecnico, o.prioridade, o.status, u.nome AS aberto_por,
                     o.data_abertura, o.data_conclusao, o.diagnostico, o.solucao, o.desconto, o.total,
                     (SELECT IFNULL(SUM(quantidade * preco_unitario), 0) FROM itens_os WHERE ordem_servico_id = o.id) AS subtotal
              FROM ordens_servico o
              INNER JOIN clientes c ON c.id = o.cliente_id
              INNER JOIN equipamentos e ON e.id = o.equipamento_id
              INNER JOIN usuarios u ON u.id = o.usuario_abertura_id
              LEFT JOIN tecnicos t ON t.id = o.tecnico_id
              WHERE o.id = @id",
            Banco.P("@id", osId));

        if (os.Rows.Count == 0)
            throw new RegraNegocioException("Ordem de serviço não encontrada.");

        DataRow r = os.Rows[0];
        lblCliente.Text = r["cliente"].ToString();
        lblEquipamento.Text = r["equipamento"].ToString();
        lblTecnico.Text = r["tecnico"] == DBNull.Value ? "— (sem técnico definido)" : r["tecnico"].ToString();
        lblPrioridade.Text = OrdemRegras.RotuloPrioridade(r["prioridade"].ToString());
        lblStatus.Text = OrdemRegras.RotuloStatus(r["status"].ToString());
        lblStatus.Font = Estilo.FonteNegrito;
        lblAbertoPor.Text = r["aberto_por"].ToString();
        lblAbertura.Text = Data(r["data_abertura"]);
        lblConclusao.Text = Data(r["data_conclusao"]);
        txtDiagnostico.Text = r["diagnostico"] == DBNull.Value ? "" : r["diagnostico"].ToString();
        txtSolucao.Text = r["solucao"] == DBNull.Value ? "" : r["solucao"].ToString();

        lblTotais.Text = "Subtotal: " + Convert.ToDecimal(r["subtotal"]).ToString("C2", Validador.Cultura)
                       + "    Desconto: " + Convert.ToDecimal(r["desconto"]).ToString("C2", Validador.Cultura)
                       + "    Total: " + Convert.ToDecimal(r["total"]).ToString("C2", Validador.Cultura);

        gradeItens.DataSource = Banco.Tabela(
            @"SELECT s.descricao AS servico, i.quantidade, i.preco_unitario, (i.quantidade * i.preco_unitario) AS subtotal
              FROM itens_os i
              INNER JOIN servicos s ON s.id = i.servico_id
              WHERE i.ordem_servico_id = @id
              ORDER BY i.id",
            Banco.P("@id", osId));
        gradeItens.Columns["servico"].HeaderText = "Serviço";
        gradeItens.Columns["quantidade"].HeaderText = "Qtd.";
        gradeItens.Columns["preco_unitario"].HeaderText = "Preço unit.";
        gradeItens.Columns["subtotal"].HeaderText = "Subtotal";
        foreach (string nome in new[] { "quantidade", "preco_unitario", "subtotal" })
        {
            var estilo = gradeItens.Columns[nome].DefaultCellStyle;
            estilo.Format = nome == "quantidade" ? "N2" : "C2";
            estilo.FormatProvider = Validador.Cultura;
            estilo.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        gradeItens.ClearSelection();

        gradeHistorico.DataSource = Banco.Tabela(
            @"SELECT h.data_alteracao, h.status_anterior, h.status_novo, u.nome AS usuario, h.justificativa
              FROM historico_os h
              INNER JOIN usuarios u ON u.id = h.usuario_id
              WHERE h.ordem_servico_id = @id
              ORDER BY h.data_alteracao, h.id",
            Banco.P("@id", osId));
        gradeHistorico.Columns["data_alteracao"].HeaderText = "Data";
        gradeHistorico.Columns["data_alteracao"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
        gradeHistorico.Columns["status_anterior"].HeaderText = "De";
        gradeHistorico.Columns["status_novo"].HeaderText = "Para";
        gradeHistorico.Columns["usuario"].HeaderText = "Usuário";
        gradeHistorico.Columns["justificativa"].HeaderText = "Justificativa";
        gradeHistorico.ClearSelection();
    }

    private void FormatarHistorico(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        string coluna = gradeHistorico.Columns[e.ColumnIndex].Name;
        if (coluna != "status_anterior" && coluna != "status_novo") return;

        e.Value = e.Value == null || e.Value == DBNull.Value ? "—" : OrdemRegras.RotuloStatus(e.Value.ToString());
        e.FormattingApplied = true;
    }
}
