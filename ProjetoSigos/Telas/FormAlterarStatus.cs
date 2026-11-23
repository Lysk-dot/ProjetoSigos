using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;

namespace SIGOS.Telas;

/// <summary>Janela para mudar o status da OS seguindo a ordem natural (ou cancelar). Cada mudança vai para o histórico.</summary>
[DesignerCategory("Code")]
public class FormAlterarStatus : Form
{
    private readonly int osId;
    private readonly ComboBox cmbNovo = Estilo.NovoCombo();
    private readonly TextBox txtJustificativa = Estilo.NovoTexto(255);
    private readonly Label lblAtual = new Label();
    private readonly Label lblDica = new Label();
    private readonly Button btnConfirmar = new Button { Text = "Confirmar" };
    private readonly Button btnFechar = new Button { Text = "Fechar" };
    private string statusAtual = "";

    public FormAlterarStatus(int osId, string descricao)
    {
        this.osId = osId;

        Text = "Alterar status da OS";
        Font = Estilo.Fonte;
        BackColor = Color.White;
        ClientSize = new Size(540, 470);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(28, 20, 28, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // título
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // status atual
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // rótulo novo status
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // combo
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // rótulo justificativa
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));   // justificativa
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // dica
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // espaço
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // botões

        var titulo = new Label
        {
            Text = "OS nº " + osId + ": " + descricao,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 12f),
            ForeColor = Estilo.AzulEscuro,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblAtual.Dock = DockStyle.Fill;
        lblAtual.ForeColor = Estilo.CorSuave;
        lblAtual.TextAlign = ContentAlignment.MiddleLeft;

        cmbNovo.Dock = DockStyle.Fill;
        cmbNovo.Margin = new Padding(0, 2, 0, 4);

        txtJustificativa.Multiline = true;
        txtJustificativa.Dock = DockStyle.Fill;
        txtJustificativa.Margin = new Padding(0, 2, 0, 4);

        lblDica.Dock = DockStyle.Fill;
        lblDica.ForeColor = Estilo.CorSuave;
        lblDica.TextAlign = ContentAlignment.TopLeft;

        Estilo.BotaoPrimario(btnConfirmar, Estilo.Azul);
        Estilo.BotaoSecundario(btnFechar);
        var botoes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        botoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        botoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnConfirmar.Dock = DockStyle.Fill;
        btnConfirmar.Margin = new Padding(0, 0, 6, 0);
        btnFechar.Dock = DockStyle.Fill;
        btnFechar.Margin = new Padding(6, 0, 0, 0);
        botoes.Controls.Add(btnConfirmar, 0, 0);
        botoes.Controls.Add(btnFechar, 1, 0);

        raiz.Controls.Add(titulo, 0, 0);
        raiz.Controls.Add(lblAtual, 0, 1);
        raiz.Controls.Add(NovoRotulo("Novo status *"), 0, 2);
        raiz.Controls.Add(cmbNovo, 0, 3);
        raiz.Controls.Add(NovoRotulo("Justificativa (obrigatória para cancelar)"), 0, 4);
        raiz.Controls.Add(txtJustificativa, 0, 5);
        raiz.Controls.Add(lblDica, 0, 6);
        raiz.Controls.Add(botoes, 0, 8);
        Controls.Add(raiz);

        cmbNovo.SelectedIndexChanged += (s, e) => AtualizarDica();
        btnFechar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        btnConfirmar.Click += (s, e) =>
        {
            if (cmbNovo.SelectedItem is not Opcao escolhida)
            {
                Caixa.Aviso("Selecione o novo status.");
                cmbNovo.Focus();
                return;
            }

            if (Caixa.Tentar(() => OrdemRegras.AlterarStatus(this.osId, escolhida.Valor, txtJustificativa.Text)))
            {
                MessageBox.Show("Status alterado para \"" + escolhida.Rotulo + "\".", "SIGOS",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        Load += (s, e) => CarregarStatus();
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

    private void CarregarStatus()
    {
        Caixa.Tentar(() =>
        {
            DataTable os = Banco.Tabela("SELECT status FROM ordens_servico WHERE id = @id", Banco.P("@id", osId));
            if (os.Rows.Count == 0)
                throw new RegraNegocioException("Ordem de serviço não encontrada.");

            statusAtual = os.Rows[0]["status"].ToString() ?? "";
            lblAtual.Text = "Status atual: " + OrdemRegras.RotuloStatus(statusAtual);

            if (OrdemRegras.Finalizada(statusAtual))
            {
                lblDica.Text = "Esta OS já está " + OrdemRegras.RotuloStatus(statusAtual).ToLower() + " e não pode mais mudar de status.";
                cmbNovo.Enabled = false;
                txtJustificativa.Enabled = false;
                btnConfirmar.Enabled = false;
                return;
            }

            string? proximo = OrdemRegras.Proximo(statusAtual);
            if (proximo != null)
                cmbNovo.Items.Add(new Opcao(proximo, OrdemRegras.RotuloStatus(proximo)));
            cmbNovo.Items.Add(new Opcao("CANCELADA", OrdemRegras.RotuloStatus("CANCELADA")));
            cmbNovo.SelectedIndex = 0;
        });
    }

    private void AtualizarDica()
    {
        string codigo = (cmbNovo.SelectedItem as Opcao)?.Valor ?? "";
        lblDica.Text = codigo switch
        {
            "DIAGNOSTICO" => "Exige técnico responsável definido na OS.",
            "AGUARDANDO_APROVACAO" => "Exige diagnóstico preenchido e pelo menos um serviço lançado.",
            "EM_EXECUCAO" => "Use depois que o cliente aprovar o orçamento.",
            "CONCLUIDA" => "Exige a solução aplicada preenchida. Depois disso a OS não poderá mais ser editada.",
            "CANCELADA" => "Justificativa obrigatória. Depois disso a OS não poderá mais ser editada.",
            _ => ""
        };
    }
}
