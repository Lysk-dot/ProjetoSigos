using MySql.Data.MySqlClient;
using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;

namespace SIGOS.Telas;

/// <summary>Abrir, editar e acompanhar ordens de serviço. Serviços e status ficam nos botões de baixo.</summary>
[DesignerCategory("Code")]
public class FormOrdemServico : FormCadastroBase
{
    private readonly ComboBox cmbCliente = Estilo.NovoCombo();
    private readonly ComboBox cmbEquipamento = Estilo.NovoCombo();
    private readonly ComboBox cmbTecnico = Estilo.NovoCombo();
    private readonly ComboBox cmbPrioridade = Estilo.NovoCombo();
    private readonly TextBox txtDiagnostico = Estilo.NovoTexto(2000);
    private readonly TextBox txtSolucao = Estilo.NovoTexto(2000);
    private readonly TextBox txtDesconto = Estilo.NovoTexto(14);
    private readonly TextBox txtStatus = Estilo.NovoTexto(40);

    public FormOrdemServico() : base("Ordens de Serviço", "Abra ordens, lance os serviços, acompanhe o andamento e altere o status.")
    {
        foreach (Opcao p in OrdemRegras.OpcoesPrioridade())
            cmbPrioridade.Items.Add(p);

        txtDiagnostico.Multiline = true;
        txtDiagnostico.ScrollBars = ScrollBars.Vertical;
        txtDiagnostico.AcceptsReturn = true;
        txtSolucao.Multiline = true;
        txtSolucao.ScrollBars = ScrollBars.Vertical;
        txtSolucao.AcceptsReturn = true;

        txtDesconto.TextAlign = HorizontalAlignment.Right;
        txtDesconto.PlaceholderText = "0,00";
        txtStatus.ReadOnly = true;
        txtStatus.TabStop = false;
        txtStatus.BackColor = Estilo.FundoSuave;

        AdicionarCampo("Cliente *", cmbCliente);
        AdicionarCampo("Equipamento do cliente *", cmbEquipamento);
        AdicionarCampo("Técnico responsável", cmbTecnico);
        AdicionarCampo("Prioridade *", cmbPrioridade);
        AdicionarCampo("Diagnóstico", txtDiagnostico, 70);
        AdicionarCampo("Solução aplicada", txtSolucao, 70);
        AdicionarCampo("Desconto (R$)", txtDesconto);
        AdicionarCampo("Status (use o botão Alterar status)", txtStatus);
        CampoInicial = cmbCliente;

        AdicionarBotaoExtra("Serviços da OS", AbrirItens);
        AdicionarBotaoExtra("Alterar status", AbrirAlterarStatus);

        ColunasOcultas.AddRange(new[] { "cliente_id", "equipamento_id", "tecnico_id", "diagnostico", "solucao", "desconto" });
        Cabecalhos["id"] = "Nº OS";
        Cabecalhos["cliente"] = "Cliente";
        Cabecalhos["equipamento"] = "Equipamento";
        Cabecalhos["tecnico"] = "Técnico";
        Cabecalhos["prioridade"] = "Prioridade";
        Cabecalhos["status"] = "Status";
        Cabecalhos["data_abertura"] = "Abertura";
        Cabecalhos["total"] = "Total";

        cmbCliente.SelectedIndexChanged += (s, e) => CarregarEquipamentos();

        txtDesconto.Leave += (s, e) =>
        {
            if (Validador.TentarMoeda(txtDesconto.Text, out decimal valor))
                txtDesconto.Text = valor.ToString("N2", Validador.Cultura);
        };

        grid.DataBindingComplete += (s, e) => FormatarColunas();
        grid.CellFormatting += Grid_CellFormatting;
    }

    // ---------- Carregar combos ----------

    protected override void AoAbrir()
    {
        cmbCliente.DisplayMember = "rotulo";
        cmbCliente.ValueMember = "id";
        cmbCliente.DataSource = Banco.Tabela(
            "SELECT id, CONCAT(nome, IF(ativo = 1, '', ' (inativo)')) AS rotulo FROM clientes ORDER BY nome");
        cmbCliente.SelectedIndex = -1;

        DataTable tecnicos = Banco.Tabela(
            "SELECT id, CONCAT(nome, IF(ativo = 1, '', ' (inativo)')) AS rotulo FROM tecnicos ORDER BY nome");
        DataRow semTecnico = tecnicos.NewRow();
        semTecnico["id"] = 0;
        semTecnico["rotulo"] = "(sem técnico definido)";
        tecnicos.Rows.InsertAt(semTecnico, 0);

        cmbTecnico.DisplayMember = "rotulo";
        cmbTecnico.ValueMember = "id";
        cmbTecnico.DataSource = tecnicos;

        cmbPrioridade.SelectedIndex = 1;   // Média
        txtStatus.Text = "Aberta (ao cadastrar)";
    }

    /// <summary>Mostra só os equipamentos do cliente escolhido.</summary>
    private void CarregarEquipamentos()
    {
        try
        {
            cmbEquipamento.DisplayMember = "rotulo";
            cmbEquipamento.ValueMember = "id";
            cmbEquipamento.DataSource = Banco.Tabela(
                @"SELECT e.id,
                         CONCAT(CONCAT_WS(' ', e.tipo, e.marca, e.modelo),
                                IF(e.numero_serie IS NULL OR e.numero_serie = '', '', CONCAT(' - ', e.numero_serie))) AS rotulo
                  FROM equipamentos e
                  WHERE e.cliente_id = @cli
                  ORDER BY e.tipo, e.marca, e.modelo",
                Banco.P("@cli", ValorCombo(cmbCliente)));
            cmbEquipamento.SelectedIndex = -1;
        }
        catch (MySqlException ex)
        {
            Erro("Erro ao carregar os equipamentos: " + ex.Message);
        }
    }

    private static int ValorCombo(ComboBox combo)
    {
        return combo.SelectedValue is IConvertible valor ? Convert.ToInt32(valor) : 0;
    }

    // ---------- Lista ----------

    protected override DataTable Consultar(string filtro)
    {
        return Banco.Tabela(
            @"SELECT o.id, c.nome AS cliente,
                     CONCAT_WS(' ', e.tipo, e.marca, e.modelo) AS equipamento,
                     IFNULL(t.nome, '—') AS tecnico,
                     o.prioridade, o.status, o.data_abertura, o.total,
                     o.cliente_id, o.equipamento_id, o.tecnico_id, o.diagnostico, o.solucao, o.desconto
              FROM ordens_servico o
              INNER JOIN clientes c ON c.id = o.cliente_id
              INNER JOIN equipamentos e ON e.id = o.equipamento_id
              LEFT JOIN tecnicos t ON t.id = o.tecnico_id
              WHERE c.nome LIKE @f OR e.tipo LIKE @f OR e.marca LIKE @f OR e.modelo LIKE @f
                 OR e.numero_serie LIKE @f OR t.nome LIKE @f OR CAST(o.id AS CHAR) LIKE @f
                 OR REPLACE(o.status, '_', ' ') LIKE @f
              ORDER BY o.id DESC",
            Banco.P("@f", "%" + filtro + "%"));
    }

    private void FormatarColunas()
    {
        if (grid.Columns.Contains("data_abertura"))
            grid.Columns["data_abertura"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";

        if (grid.Columns.Contains("total"))
        {
            var estilo = grid.Columns["total"].DefaultCellStyle;
            estilo.Format = "C2";
            estilo.FormatProvider = Validador.Cultura;
            estilo.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    /// <summary>Mostra "Em execução" em vez de EM_EXECUCAO.</summary>
    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

        switch (grid.Columns[e.ColumnIndex].Name)
        {
            case "status":
                e.Value = OrdemRegras.RotuloStatus(e.Value.ToString());
                e.FormattingApplied = true;
                break;
            case "prioridade":
                e.Value = OrdemRegras.RotuloPrioridade(e.Value.ToString());
                e.FormattingApplied = true;
                break;
        }
    }

    // ---------- Validação e gravação ----------

    private decimal LerDesconto()
    {
        if (string.IsNullOrWhiteSpace(txtDesconto.Text)) return 0m;

        if (!Validador.TentarMoeda(txtDesconto.Text, out decimal desconto))
            throw new RegraNegocioException("Desconto inválido. Digite apenas números, por exemplo 25,00.", txtDesconto);

        if (desconto < 0)
            throw new RegraNegocioException("O desconto não pode ser negativo.", txtDesconto);

        if (desconto > 99999999.99m)
            throw new RegraNegocioException("Desconto muito alto.", txtDesconto);

        return desconto;
    }

    protected override void Validar()
    {
        int cliente = ValorCombo(cmbCliente);
        if (cliente == 0)
            throw new RegraNegocioException("Selecione o cliente.", cmbCliente);

        int equipamento = ValorCombo(cmbEquipamento);
        if (equipamento == 0)
            throw new RegraNegocioException("Selecione o equipamento do cliente. Se ele não aparece na lista, cadastre-o em Equipamentos.", cmbEquipamento);

        if (Banco.Contar("SELECT COUNT(*) FROM equipamentos WHERE id=@e AND cliente_id=@c",
                Banco.P("@e", equipamento), Banco.P("@c", cliente)) == 0)
            throw new RegraNegocioException("O equipamento selecionado não pertence a este cliente.", cmbEquipamento);

        if (cmbPrioridade.SelectedItem == null)
            throw new RegraNegocioException("Selecione a prioridade.", cmbPrioridade);

        bool nova = IdSelecionado == 0;
        int tecnicoAtual = 0;
        decimal subtotal = 0m;

        if (!nova)
        {
            DataTable os = Banco.Tabela("SELECT status, tecnico_id FROM ordens_servico WHERE id=@id", Banco.P("@id", IdSelecionado));
            if (os.Rows.Count == 0)
                throw new RegraNegocioException("Esta ordem de serviço não existe mais. Clique em Limpar e atualize a lista.");

            string status = os.Rows[0]["status"].ToString() ?? "";
            if (OrdemRegras.Finalizada(status))
                throw new RegraNegocioException("Esta OS está " + OrdemRegras.RotuloStatus(status).ToLower() + " e não pode mais ser editada.");

            if (os.Rows[0]["tecnico_id"] != DBNull.Value)
                tecnicoAtual = Convert.ToInt32(os.Rows[0]["tecnico_id"]);

            subtotal = Convert.ToDecimal(Banco.Tabela(
                "SELECT IFNULL(SUM(quantidade * preco_unitario), 0) AS s FROM itens_os WHERE ordem_servico_id=@id",
                Banco.P("@id", IdSelecionado)).Rows[0]["s"]);
        }
        else if (Banco.Contar("SELECT COUNT(*) FROM clientes WHERE id=@id AND ativo=1", Banco.P("@id", cliente)) == 0)
        {
            throw new RegraNegocioException("Este cliente está inativo. Reative o cadastro do cliente antes de abrir uma OS.", cmbCliente);
        }

        int tecnico = ValorCombo(cmbTecnico);
        if (tecnico != 0 && tecnico != tecnicoAtual &&
            Banco.Contar("SELECT COUNT(*) FROM tecnicos WHERE id=@id AND ativo=1", Banco.P("@id", tecnico)) == 0)
            throw new RegraNegocioException("Este técnico está inativo. Escolha outro técnico.", cmbTecnico);

        decimal desconto = LerDesconto();
        if (desconto > subtotal)
            throw new RegraNegocioException(
                "O desconto não pode ser maior que o subtotal dos serviços (" + subtotal.ToString("C2", Validador.Cultura) + ")." +
                (nova ? "\nLance os serviços depois de cadastrar a OS e então aplique o desconto." : ""),
                txtDesconto);
    }

    private object ValorTecnico()
    {
        int tecnico = ValorCombo(cmbTecnico);
        return tecnico == 0 ? DBNull.Value : tecnico;
    }

    private string ValorPrioridade()
    {
        return ((Opcao)cmbPrioridade.SelectedItem!).Valor;
    }

    protected override void Inserir()
    {
        int usuarioId = Sessao.UsuarioId;

        Transacao.Rodar(tx =>
        {
            tx.Executar(
                @"INSERT INTO ordens_servico
                    (cliente_id, equipamento_id, tecnico_id, usuario_abertura_id, prioridade, status, diagnostico, solucao, desconto, total)
                  VALUES (@cli, @eq, @tec, @usu, @prio, 'ABERTA', @diag, @sol, 0, 0)",
                Banco.P("@cli", ValorCombo(cmbCliente)),
                Banco.P("@eq", ValorCombo(cmbEquipamento)),
                Banco.P("@tec", ValorTecnico()),
                Banco.P("@usu", usuarioId),
                Banco.P("@prio", ValorPrioridade()),
                Banco.P("@diag", Validador.NuloSeVazio(txtDiagnostico.Text)),
                Banco.P("@sol", Validador.NuloSeVazio(txtSolucao.Text)));

            int novaOs = (int)tx.UltimoId;

            tx.Executar(
                @"INSERT INTO historico_os (ordem_servico_id, usuario_id, status_anterior, status_novo, justificativa)
                  VALUES (@os, @usu, NULL, 'ABERTA', NULL)",
                Banco.P("@os", novaOs),
                Banco.P("@usu", usuarioId));
        });
    }

    protected override void Atualizar(int id)
    {
        Transacao.Rodar(tx =>
        {
            tx.Executar(
                @"UPDATE ordens_servico
                  SET cliente_id=@cli, equipamento_id=@eq, tecnico_id=@tec, prioridade=@prio,
                      diagnostico=@diag, solucao=@sol, desconto=@desc
                  WHERE id=@id",
                Banco.P("@cli", ValorCombo(cmbCliente)),
                Banco.P("@eq", ValorCombo(cmbEquipamento)),
                Banco.P("@tec", ValorTecnico()),
                Banco.P("@prio", ValorPrioridade()),
                Banco.P("@diag", Validador.NuloSeVazio(txtDiagnostico.Text)),
                Banco.P("@sol", Validador.NuloSeVazio(txtSolucao.Text)),
                Banco.P("@desc", LerDesconto()),
                Banco.P("@id", id));

            OrdemRegras.RecalcularTotal(tx, id);
        });
    }

    protected override void Excluir(int id)
    {
        DataTable os = Banco.Tabela("SELECT status FROM ordens_servico WHERE id=@id", Banco.P("@id", id));
        if (os.Rows.Count == 0) return;

        if (os.Rows[0]["status"].ToString() != "ABERTA")
            throw new RegraNegocioException(
                "Só é possível excluir uma OS que ainda está Aberta.\n" +
                "Para encerrar uma OS em andamento, use Alterar status > Cancelada (o histórico fica guardado).");

        if (Banco.Contar("SELECT COUNT(*) FROM itens_os WHERE ordem_servico_id=@id", Banco.P("@id", id)) > 0)
            throw new RegraNegocioException(
                "Esta OS já tem serviços lançados.\n" +
                "Remova os serviços (botão Serviços da OS) ou cancele a OS pelo botão Alterar status.");

        Transacao.Rodar(tx =>
        {
            tx.Executar("DELETE FROM historico_os WHERE ordem_servico_id=@id", Banco.P("@id", id));
            tx.Executar("DELETE FROM ordens_servico WHERE id=@id", Banco.P("@id", id));
        });
    }

    // ---------- Campos <-> linha selecionada ----------

    protected override void PreencherCampos(DataGridViewRow linha)
    {
        cmbCliente.SelectedValue = Convert.ToInt32(linha.Cells["cliente_id"].Value);   // recarrega os equipamentos
        cmbEquipamento.SelectedValue = Convert.ToInt32(linha.Cells["equipamento_id"].Value);

        object tecnico = linha.Cells["tecnico_id"].Value;
        cmbTecnico.SelectedValue = tecnico == DBNull.Value ? 0 : Convert.ToInt32(tecnico);

        string prioridade = Texto(linha, "prioridade");
        cmbPrioridade.SelectedIndex = Array.IndexOf(OrdemRegras.Prioridades, prioridade);

        txtDiagnostico.Text = Texto(linha, "diagnostico");
        txtSolucao.Text = Texto(linha, "solucao");
        txtDesconto.Text = Convert.ToDecimal(linha.Cells["desconto"].Value).ToString("N2", Validador.Cultura);
        txtStatus.Text = OrdemRegras.RotuloStatus(Texto(linha, "status"));
    }

    protected override void LimparCampos()
    {
        cmbCliente.SelectedIndex = -1;
        cmbEquipamento.SelectedIndex = -1;
        if (cmbTecnico.Items.Count > 0) cmbTecnico.SelectedIndex = 0;
        cmbPrioridade.SelectedIndex = 1;   // Média
        txtDiagnostico.Clear();
        txtSolucao.Clear();
        txtDesconto.Clear();
        txtStatus.Text = "Aberta (ao cadastrar)";
    }

    // ---------- Botões extras ----------

    private string DescricaoSelecionada()
    {
        return (cmbCliente.Text + " - " + cmbEquipamento.Text).Trim(' ', '-');
    }

    /// <summary>Recarrega a lista e mantém a mesma OS selecionada (para mostrar o novo total/status).</summary>
    private void ReselecionarAtual()
    {
        int id = IdSelecionado;
        Carregar();

        foreach (DataGridViewRow linha in grid.Rows)
        {
            if (Convert.ToInt32(linha.Cells["id"].Value) == id)
            {
                linha.Selected = true;
                PreencherCampos(linha);
                return;
            }
        }
        LimparTudo();
    }

    private void AbrirItens(object? sender, EventArgs e)
    {
        if (IdSelecionado == 0) return;

        using var janela = new FormItensOS(IdSelecionado, DescricaoSelecionada());
        janela.ShowDialog(this);
        ReselecionarAtual();
    }

    private void AbrirAlterarStatus(object? sender, EventArgs e)
    {
        if (IdSelecionado == 0) return;

        using var janela = new FormAlterarStatus(IdSelecionado, DescricaoSelecionada());
        janela.ShowDialog(this);
        ReselecionarAtual();
    }
}
