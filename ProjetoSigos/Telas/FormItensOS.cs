using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;

namespace SIGOS.Telas;

/// <summary>Janela para lançar e remover os serviços de uma ordem de serviço. O total da OS é recalculado a cada mudança.</summary>
[DesignerCategory("Code")]
public class FormItensOS : Form
{
    private readonly int osId;
    private readonly ComboBox cmbServico = Estilo.NovoCombo();
    private readonly TextBox txtQtd = Estilo.NovoTexto(10);
    private readonly TextBox txtPreco = Estilo.NovoTexto(14);
    private readonly Button btnAdicionar = new Button { Text = "Adicionar" };
    private readonly Button btnRemover = new Button { Text = "Remover selecionado" };
    private readonly Button btnFechar = new Button { Text = "Fechar" };
    private readonly DataGridView grade = new DataGridView();
    private readonly Label lblTotais = new Label();
    private readonly Label lblAviso = new Label();
    private readonly Cartao cartaoAdicionar = new Cartao { Dock = DockStyle.Top, Height = 106, Margin = new Padding(0) };

    public FormItensOS(int osId, string descricao)
    {
        this.osId = osId;

        Text = "Serviços da ordem de serviço";
        Font = Estilo.Fonte;
        BackColor = Color.White;
        Size = new Size(940, 620);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterParent;
        Padding = new Padding(24, 0, 24, 16);

        var titulo = new Label
        {
            Text = "Serviços da OS nº " + osId + ": " + descricao,
            Dock = DockStyle.Top,
            Height = 64,
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = Estilo.AzulEscuro,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        // ----- Linha de adicionar: serviço, quantidade, preço, botão -----
        var tblAdd = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
        tblAdd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tblAdd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        tblAdd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        tblAdd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        tblAdd.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        tblAdd.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        tblAdd.Controls.Add(NovoRotulo("Serviço"), 0, 0);
        tblAdd.Controls.Add(NovoRotulo("Quantidade"), 1, 0);
        tblAdd.Controls.Add(NovoRotulo("Preço unitário (R$)"), 2, 0);

        txtQtd.Text = "1";
        txtQtd.TextAlign = HorizontalAlignment.Right;
        txtPreco.TextAlign = HorizontalAlignment.Right;
        txtPreco.PlaceholderText = "0,00";
        Estilo.BotaoPrimario(btnAdicionar, Estilo.Azul);

        cmbServico.Dock = DockStyle.Fill;
        cmbServico.Margin = new Padding(0, 2, 12, 4);
        txtQtd.Dock = DockStyle.Fill;
        txtQtd.Margin = new Padding(0, 2, 12, 4);
        txtPreco.Dock = DockStyle.Fill;
        txtPreco.Margin = new Padding(0, 2, 12, 4);
        btnAdicionar.Dock = DockStyle.Fill;
        btnAdicionar.Margin = new Padding(0, 2, 0, 4);

        tblAdd.Controls.Add(cmbServico, 0, 1);
        tblAdd.Controls.Add(txtQtd, 1, 1);
        tblAdd.Controls.Add(txtPreco, 2, 1);
        tblAdd.Controls.Add(btnAdicionar, 3, 1);
        cartaoAdicionar.Controls.Add(tblAdd);

        // ----- Rodapé: remover, totais, fechar -----
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 64 };
        Estilo.BotaoPrimario(btnRemover, Estilo.Vermelho);
        Estilo.BotaoSecundario(btnFechar);
        btnRemover.Dock = DockStyle.Left;
        btnRemover.Width = 190;
        btnFechar.Dock = DockStyle.Right;
        btnFechar.Width = 120;
        lblTotais.Dock = DockStyle.Fill;
        lblTotais.TextAlign = ContentAlignment.MiddleCenter;
        lblTotais.Font = Estilo.FonteNegrito;
        lblTotais.ForeColor = Estilo.AzulEscuro;
        rodape.Controls.Add(lblTotais);
        rodape.Controls.Add(btnRemover);
        rodape.Controls.Add(btnFechar);
        rodape.Padding = new Padding(0, 12, 0, 12);

        lblAviso.Dock = DockStyle.Top;
        lblAviso.Height = 0;
        lblAviso.ForeColor = Estilo.Vermelho;
        lblAviso.TextAlign = ContentAlignment.MiddleLeft;

        Estilo.Grid(grade);
        grade.Dock = DockStyle.Fill;

        // Regra do Dock: o que preenche (Fill) entra primeiro; o último adicionado fica no topo.
        Controls.Add(grade);
        Controls.Add(rodape);
        Controls.Add(lblAviso);
        Controls.Add(cartaoAdicionar);
        Controls.Add(titulo);

        // ----- Eventos -----
        cmbServico.SelectedIndexChanged += (s, e) =>
        {
            if (cmbServico.SelectedItem is DataRowView linha)
                txtPreco.Text = Convert.ToDecimal(linha["preco"]).ToString("N2", Validador.Cultura);
        };
        txtPreco.Leave += (s, e) =>
        {
            if (Validador.TentarMoeda(txtPreco.Text, out decimal valor))
                txtPreco.Text = valor.ToString("N2", Validador.Cultura);
        };
        btnAdicionar.Click += (s, e) =>
        {
            if (Caixa.Tentar(Adicionar))
            {
                cmbServico.SelectedIndex = -1;
                txtPreco.Clear();
                txtQtd.Text = "1";
                Recarregar();
                cmbServico.Focus();
            }
        };
        btnRemover.Click += (s, e) =>
        {
            if (Caixa.Tentar(Remover)) Recarregar();
        };
        btnFechar.Click += (s, e) => Close();

        Load += (s, e) =>
        {
            Caixa.Tentar(() =>
            {
                cmbServico.DisplayMember = "descricao";
                cmbServico.ValueMember = "id";
                cmbServico.DataSource = Banco.Tabela("SELECT id, descricao, preco FROM servicos WHERE ativo = 1 ORDER BY descricao");
                cmbServico.SelectedIndex = -1;
                Recarregar();
            });
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
            TextAlign = ContentAlignment.BottomLeft
        };
    }

    private void Recarregar()
    {
        Caixa.Tentar(() =>
        {
            DataTable os = Banco.Tabela(
                @"SELECT status, desconto, total,
                         (SELECT IFNULL(SUM(quantidade * preco_unitario), 0) FROM itens_os WHERE ordem_servico_id = @id) AS subtotal
                  FROM ordens_servico WHERE id = @id",
                Banco.P("@id", osId));

            if (os.Rows.Count == 0)
                throw new RegraNegocioException("Ordem de serviço não encontrada.");

            string status = os.Rows[0]["status"].ToString() ?? "";
            bool encerrada = OrdemRegras.Finalizada(status);

            cartaoAdicionar.Visible = !encerrada;
            btnRemover.Visible = !encerrada;
            lblAviso.Text = encerrada
                ? "Esta OS está " + OrdemRegras.RotuloStatus(status).ToLower() + ": os serviços não podem mais ser alterados."
                : "";
            lblAviso.Height = encerrada ? 30 : 0;

            DataTable itens = Banco.Tabela(
                @"SELECT i.id, s.descricao AS servico, i.quantidade, i.preco_unitario,
                         (i.quantidade * i.preco_unitario) AS subtotal
                  FROM itens_os i
                  INNER JOIN servicos s ON s.id = i.servico_id
                  WHERE i.ordem_servico_id = @id
                  ORDER BY i.id",
                Banco.P("@id", osId));

            grade.DataSource = itens;
            grade.Columns["id"].Visible = false;
            grade.Columns["servico"].HeaderText = "Serviço";
            grade.Columns["quantidade"].HeaderText = "Qtd.";
            grade.Columns["preco_unitario"].HeaderText = "Preço unitário";
            grade.Columns["subtotal"].HeaderText = "Subtotal";
            foreach (string nome in new[] { "quantidade", "preco_unitario", "subtotal" })
            {
                var estilo = grade.Columns[nome].DefaultCellStyle;
                estilo.Format = nome == "quantidade" ? "N2" : "C2";
                estilo.FormatProvider = Validador.Cultura;
                estilo.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            grade.ClearSelection();

            decimal subtotal = Convert.ToDecimal(os.Rows[0]["subtotal"]);
            decimal desconto = Convert.ToDecimal(os.Rows[0]["desconto"]);
            decimal total = Convert.ToDecimal(os.Rows[0]["total"]);
            lblTotais.Text = "Subtotal: " + subtotal.ToString("C2", Validador.Cultura)
                           + "      Desconto: " + desconto.ToString("C2", Validador.Cultura)
                           + "      Total: " + total.ToString("C2", Validador.Cultura);
        });
    }

    private void Adicionar()
    {
        int servico = cmbServico.SelectedValue is IConvertible v ? Convert.ToInt32(v) : 0;
        if (servico == 0)
            throw new RegraNegocioException("Selecione o serviço.", cmbServico);

        if (!Validador.TentarMoeda(txtQtd.Text, out decimal quantidade) || quantidade <= 0 || quantidade > 99999999.99m)
            throw new RegraNegocioException("Quantidade inválida. Use um número maior que zero, por exemplo 1 ou 2,5.", txtQtd);

        if (!Validador.TentarMoeda(txtPreco.Text, out decimal preco) || preco < 0 || preco > 99999999.99m)
            throw new RegraNegocioException("Preço inválido. Digite apenas números, por exemplo 89,90.", txtPreco);

        Transacao.Rodar(tx =>
        {
            string? status = tx.Escalar("SELECT status FROM ordens_servico WHERE id = @id FOR UPDATE", Banco.P("@id", osId))?.ToString();
            if (status == null)
                throw new RegraNegocioException("Ordem de serviço não encontrada.");
            if (OrdemRegras.Finalizada(status))
                throw new RegraNegocioException("Esta OS está " + OrdemRegras.RotuloStatus(status).ToLower() + " e não pode mais ser alterada.");

            tx.Executar(
                @"INSERT INTO itens_os (ordem_servico_id, servico_id, quantidade, preco_unitario)
                  VALUES (@os, @serv, @qtd, @preco)",
                Banco.P("@os", osId),
                Banco.P("@serv", servico),
                Banco.P("@qtd", quantidade),
                Banco.P("@preco", preco));

            OrdemRegras.RecalcularTotal(tx, osId);
        });
    }

    private void Remover()
    {
        if (grade.SelectedRows.Count == 0)
            throw new RegraNegocioException("Clique em um serviço da lista para remover.");

        int itemId = Convert.ToInt32(grade.SelectedRows[0].Cells["id"].Value);
        string nome = grade.SelectedRows[0].Cells["servico"].Value?.ToString() ?? "";

        var resposta = MessageBox.Show("Remover \"" + nome + "\" desta ordem de serviço?",
            "Confirmar remoção", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (resposta != DialogResult.Yes) return;

        Transacao.Rodar(tx =>
        {
            string? status = tx.Escalar("SELECT status FROM ordens_servico WHERE id = @id FOR UPDATE", Banco.P("@id", osId))?.ToString();
            if (status == null || OrdemRegras.Finalizada(status))
                throw new RegraNegocioException("Esta OS não pode mais ser alterada.");

            tx.Executar("DELETE FROM itens_os WHERE id = @item AND ordem_servico_id = @os",
                Banco.P("@item", itemId),
                Banco.P("@os", osId));

            OrdemRegras.RecalcularTotal(tx, osId);
        });
    }
}
