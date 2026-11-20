// SIGOS - Cadastros e Interface (tema azul e branco) - ARQUIVO UNICO
// Adicione este arquivo ao projeto SIGOS e substitua o Program.cs. Mantenha o seu Conexao.cs.

using MySql.Data.MySqlClient;
using SIGOS.Dados;
using SIGOS.Visual;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

// ===================== Dados/Banco.cs =====================
namespace SIGOS.Dados
{
    /// <summary>
    /// Atalhos para falar com o banco. Todas as telas usam estes métodos,
    /// sempre com PARÂMETROS (@nome) para proteger contra SQL injection.
    /// Precisa da sua classe Conexao (pasta Dados) com o método Criar().
    /// </summary>
    public static class Banco
    {
        /// <summary>SELECT que devolve uma tabela (usada no DataGridView).</summary>
        public static DataTable Tabela(string sql, params MySqlParameter[] parametros)
        {
            using var conn = Conexao.Criar();
            using var adaptador = new MySqlDataAdapter(sql, conn);
            adaptador.SelectCommand.Parameters.AddRange(parametros);
            var tabela = new DataTable();
            adaptador.Fill(tabela);
            return tabela;
        }

        /// <summary>INSERT, UPDATE ou DELETE. Devolve quantas linhas foram afetadas.</summary>
        public static int Executar(string sql, params MySqlParameter[] parametros)
        {
            using var conn = Conexao.Criar();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddRange(parametros);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>SELECT COUNT(*) ... devolve o número encontrado.</summary>
        public static int Contar(string sql, params MySqlParameter[] parametros)
        {
            using var conn = Conexao.Criar();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddRange(parametros);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Cria um parâmetro. Valor nulo vira NULL no banco.</summary>
        public static MySqlParameter P(string nome, object? valor)
        {
            return new MySqlParameter(nome, valor ?? DBNull.Value);
        }
    }
}

// ===================== Dados/RegraNegocioException.cs =====================
namespace SIGOS.Dados
{
    /// <summary>
    /// Erro "esperado": o usuário digitou algo errado ou quebrou uma regra de negócio.
    /// A mensagem aparece numa caixinha e o cursor volta para o campo indicado.
    /// </summary>
    public class RegraNegocioException : Exception
    {
        public Control? Foco { get; }

        public RegraNegocioException(string mensagem, Control? foco = null) : base(mensagem)
        {
            Foco = foco;
        }
    }
}

// ===================== Dados/Seguranca.cs =====================
namespace SIGOS.Dados
{
    /// <summary>
    /// Hash de senha: "tritura" a senha para que o banco nunca guarde o texto puro.
    /// Gera o mesmo resultado do SQL:  SHA2('senha', 256)
    /// </summary>
    public static class Seguranca
    {
        public static string GerarHash(string senha)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(senha));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

// ===================== Dados/Validador.cs =====================
namespace SIGOS.Dados
{
    /// <summary>Validações e formatações (CPF, CNPJ, telefone, e-mail, dinheiro).</summary>
    public static class Validador
    {
        public static readonly CultureInfo Cultura = new CultureInfo("pt-BR");

        // ---------- Limpeza de texto ----------

        public static string SomenteNumeros(string? texto)
        {
            return new string((texto ?? "").Where(char.IsDigit).ToArray());
        }

        private static bool EhLetraOuNumero(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z');
        }

        /// <summary>Tira pontos, traços e barras. Aceita letras porque o novo CNPJ (2026) pode ter letras.</summary>
        public static string NormalizarDocumento(string? texto)
        {
            return new string((texto ?? "").ToUpperInvariant().Where(EhLetraOuNumero).ToArray());
        }

        // ---------- CPF e CNPJ ----------

        public static bool CpfValido(string? cpf)
        {
            string n = SomenteNumeros(cpf);
            if (n.Length != 11 || n.Distinct().Count() == 1) return false;

            for (int t = 9; t <= 10; t++)
            {
                int soma = 0;
                for (int i = 0; i < t; i++)
                    soma += (n[i] - '0') * (t + 1 - i);

                int resto = soma * 10 % 11;
                if (resto == 10) resto = 0;
                if (resto != n[t] - '0') return false;
            }
            return true;
        }

        public static bool CnpjValido(string? cnpj)
        {
            string n = NormalizarDocumento(cnpj);
            if (n.Length != 14 || n.Distinct().Count() == 1) return false;
            if (!char.IsDigit(n[12]) || !char.IsDigit(n[13])) return false;

            int[] pesos1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] pesos2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            return n[12] - '0' == Digito(n, pesos1) && n[13] - '0' == Digito(n, pesos2);
        }

        private static int Digito(string numero, int[] pesos)
        {
            int soma = 0;
            for (int i = 0; i < pesos.Length; i++)
                soma += (numero[i] - '0') * pesos[i];

            int resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        public static bool DocumentoValido(string? documento)
        {
            string n = NormalizarDocumento(documento);
            if (n.Length == 11) return CpfValido(n);
            if (n.Length == 14) return CnpjValido(n);
            return false;
        }

        // ---------- Telefone e e-mail ----------

        public static bool TelefoneValido(string? telefone)
        {
            string d = SomenteNumeros(telefone);
            if (d.Length != 10 && d.Length != 11) return false;
            if (int.Parse(d.Substring(0, 2)) < 11) return false;   // DDD válido começa em 11
            if (d.Length == 11 && d[2] != '9') return false;       // celular começa com 9
            return true;
        }

        /// <summary>E-mail é opcional: vazio é aceito. Se tiver algo, precisa estar no formato certo.</summary>
        public static bool EmailValido(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return true;
            string e = email.Trim();
            try
            {
                var m = new MailAddress(e);
                return m.Address == e && m.Host.Contains('.');
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // ---------- Formatação para mostrar na tela ----------

        public static string FormatarDocumento(string? documento)
        {
            string n = NormalizarDocumento(documento);
            if (n.Length == 11 && n.All(char.IsDigit))
                return $"{n.Substring(0, 3)}.{n.Substring(3, 3)}.{n.Substring(6, 3)}-{n.Substring(9, 2)}";
            if (n.Length == 14)
                return $"{n.Substring(0, 2)}.{n.Substring(2, 3)}.{n.Substring(5, 3)}/{n.Substring(8, 4)}-{n.Substring(12, 2)}";
            return documento ?? "";
        }

        public static string FormatarTelefone(string? telefone)
        {
            string d = SomenteNumeros(telefone);
            if (d.Length == 10)
                return $"({d.Substring(0, 2)}) {d.Substring(2, 4)}-{d.Substring(6, 4)}";
            if (d.Length == 11)
                return $"({d.Substring(0, 2)}) {d.Substring(2, 5)}-{d.Substring(7, 4)}";
            return telefone ?? "";
        }

        // ---------- Dinheiro ----------

        /// <summary>Entende "89,90", "1.250,00" e também "89.90".</summary>
        public static bool TentarMoeda(string? texto, out decimal valor)
        {
            string t = (texto ?? "").Replace("R$", "").Trim();

            // "50.5" ou "12.50": ponto com 1 ou 2 casas depois = ponto decimal
            if (t.Contains('.') && !t.Contains(','))
            {
                int ultimoPonto = t.LastIndexOf('.');
                if (t.Length - ultimoPonto - 1 != 3)
                    t = t.Replace('.', ',');
            }

            return decimal.TryParse(t, NumberStyles.Number, Cultura, out valor);
        }

        // ---------- Para gravar no banco ----------

        /// <summary>Texto vazio vira NULL no banco.</summary>
        public static object NuloSeVazio(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return DBNull.Value;
            return texto.Trim();
        }
    }
}

// ===================== Visual/Estilo.cs =====================
namespace SIGOS.Visual
{
    /// <summary>
    /// Cores, fontes e estilos de TODAS as telas (tema azul e branco).
    /// Mudou uma cor aqui? O sistema inteiro muda junto.
    /// </summary>
    public static class Estilo
    {
        // ---------- CORES ----------
        public static readonly Color AzulEscuro = ColorTranslator.FromHtml("#1F3A5F");  // menu lateral, títulos, cabeçalho da tabela
        public static readonly Color AzulMenu = ColorTranslator.FromHtml("#2A4A73");    // botão do menu com o mouse em cima
        public static readonly Color AzulSelecao = ColorTranslator.FromHtml("#3A6AA5"); // botão do menu da tela aberta
        public static readonly Color Azul = ColorTranslator.FromHtml("#2D7DD2");        // botões principais
        public static readonly Color AzulClaro = ColorTranslator.FromHtml("#DCEBFA");   // linha selecionada na tabela
        public static readonly Color Fundo = Color.White;
        public static readonly Color FundoSuave = ColorTranslator.FromHtml("#F4F7FB");
        public static readonly Color Borda = ColorTranslator.FromHtml("#D6DEE9");
        public static readonly Color CorTexto = ColorTranslator.FromHtml("#1E2A3A");
        public static readonly Color CorSuave = ColorTranslator.FromHtml("#6B7A90");
        public static readonly Color Vermelho = ColorTranslator.FromHtml("#D64545");
        public static readonly Color Cinza = ColorTranslator.FromHtml("#B8C2CF");

        // ---------- FONTES ----------
        public static readonly Font Fonte = new Font("Segoe UI", 10f);
        public static readonly Font FonteCampo = new Font("Segoe UI", 10.5f);
        public static readonly Font FonteNegrito = new Font("Segoe UI Semibold", 10f);
        public static readonly Font FonteRotulo = new Font("Segoe UI Semibold", 9f);
        public static readonly Font FonteTitulo = new Font("Segoe UI Semibold", 20f);

        // ---------- CAMPOS ----------

        public static TextBox NovoTexto(int tamanhoMaximo)
        {
            return new TextBox
            {
                MaxLength = tamanhoMaximo,
                BorderStyle = BorderStyle.FixedSingle,
                Font = FonteCampo,
                ForeColor = CorTexto,
                BackColor = Color.White
            };
        }

        public static ComboBox NovoCombo(bool aceitaDigitar = false)
        {
            return new ComboBox
            {
                DropDownStyle = aceitaDigitar ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                Font = FonteCampo,
                FlatStyle = FlatStyle.Flat,
                ForeColor = CorTexto,
                BackColor = Color.White
            };
        }

        // ---------- BOTÕES ----------

        /// <summary>Botão colorido com texto branco (Cadastrar, Editar, Excluir...).</summary>
        public static void BotaoPrimario(Button b, Color cor)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = cor;
            b.ForeColor = Color.White;
            b.Font = FonteNegrito;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(cor, 0.08f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(cor, 0.2f);
            b.EnabledChanged += (s, e) => b.BackColor = b.Enabled ? cor : Cinza;
        }

        /// <summary>Botão branco com borda azul (Limpar, Ver histórico...).</summary>
        public static void BotaoSecundario(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Azul;
            b.BackColor = Color.White;
            b.ForeColor = Azul;
            b.Font = FonteNegrito;
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.FlatAppearance.MouseOverBackColor = AzulClaro;
            b.FlatAppearance.MouseDownBackColor = AzulClaro;
        }

        // ---------- TABELA ----------

        public static void Grid(DataGridView g)
        {
            g.Font = Fonte;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = Borda;
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 40;

            var cab = g.ColumnHeadersDefaultCellStyle;
            cab.BackColor = AzulEscuro;
            cab.ForeColor = Color.White;
            cab.Font = FonteNegrito;
            cab.SelectionBackColor = AzulEscuro;
            cab.SelectionForeColor = Color.White;
            cab.Alignment = DataGridViewContentAlignment.MiddleLeft;
            cab.Padding = new Padding(8, 0, 8, 0);

            g.RowTemplate.Height = 34;
            var cel = g.DefaultCellStyle;
            cel.BackColor = Color.White;
            cel.ForeColor = CorTexto;
            cel.SelectionBackColor = AzulClaro;
            cel.SelectionForeColor = AzulEscuro;
            cel.Padding = new Padding(8, 0, 8, 0);

            g.AlternatingRowsDefaultCellStyle.BackColor = FundoSuave;
        }
    }
}

// ===================== Visual/Cartao.cs =====================
namespace SIGOS.Visual
{
    /// <summary>Painel com borda fina azul-acinzentada (o "cartão" das telas).</summary>
    public class Cartao : Panel
    {
        public Cartao()
        {
            BackColor = Color.White;
            Padding = new Padding(16);
            Margin = new Padding(0);
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var caneta = new Pen(Estilo.Borda);
            e.Graphics.DrawRectangle(caneta, 0, 0, Width - 1, Height - 1);
        }
    }
}

// ===================== Visual/BotaoMenu.cs =====================
namespace SIGOS.Visual
{
    /// <summary>
    /// Botão do menu lateral: ícone + texto, destaque ao passar o mouse
    /// e uma barrinha branca quando é a tela aberta.
    /// Os ícones são caracteres da fonte "Segoe MDL2 Assets" (já vem no Windows 10 e 11).
    /// </summary>
    public class BotaoMenu : Button
    {
        private bool _ativo;
        private bool _mouseEmCima;

        // Estes atributos dizem ao Visual Studio para NÃO salvar a propriedade no designer
        // (o .NET 10 exige isso em propriedades públicas de controles - aviso WFO1000).
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Icone { get; set; } = "";

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Ativo
        {
            get { return _ativo; }
            set { _ativo = value; Invalidate(); }
        }

        public BotaoMenu()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Dock = DockStyle.Top;
            Height = 54;
            Font = new Font("Segoe UI", 10.5f);
            Cursor = Cursors.Hand;
            TabStop = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _mouseEmCima = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _mouseEmCima = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color fundo = _ativo ? Estilo.AzulSelecao : (_mouseEmCima ? Estilo.AzulMenu : Estilo.AzulEscuro);
            using (var pincel = new SolidBrush(fundo))
                e.Graphics.FillRectangle(pincel, ClientRectangle);

            if (_ativo)
            {
                using var barra = new SolidBrush(Color.White);
                e.Graphics.FillRectangle(barra, 0, 0, 4, Height);
            }

            using var fonteIcone = new Font("Segoe MDL2 Assets", 14f);
            TextRenderer.DrawText(e.Graphics, Icone, fonteIcone,
                new Rectangle(14, 0, 40, Height), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(e.Graphics, Text, Font,
                new Rectangle(62, 0, Width - 62, Height), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}

// ===================== Telas/FormCadastroBase.cs =====================
namespace SIGOS.Telas
{
    /// <summary>
    /// MOLDE das telas de cadastro (clientes, técnicos, equipamentos, serviços, usuários).
    /// Ele monta o visual (título, campos à esquerda, tabela com pesquisa à direita)
    /// e cuida dos botões Cadastrar / Editar / Excluir / Limpar.
    /// Cada tela filha só diz QUAIS campos tem e QUAL SQL usar.
    /// </summary>
    [DesignerCategory("Code")]
    public abstract class FormCadastroBase : Form
    {
        // ---------- Coisas que as telas filhas usam ----------
        protected readonly TextBox txtPesquisa = Estilo.NovoTexto(100);
        protected readonly DataGridView grid = new DataGridView();

        /// <summary>Código (id) do registro clicado na tabela. 0 = nenhum.</summary>
        protected int IdSelecionado { get; private set; }

        /// <summary>Campo onde o cursor fica ao abrir/limpar a tela.</summary>
        protected Control? CampoInicial { get; set; }

        /// <summary>Nomes bonitos das colunas: Cabecalhos["numero_serie"] = "Nº de série".</summary>
        protected Dictionary<string, string> Cabecalhos { get; } = new Dictionary<string, string>();

        /// <summary>Colunas que existem na consulta mas não devem aparecer na tabela.</summary>
        protected List<string> ColunasOcultas { get; } = new List<string>();

        // ---------- Partes internas ----------
        private readonly TableLayoutPanel tblCampos = new TableLayoutPanel();
        private readonly TableLayoutPanel tblBotoes = new TableLayoutPanel();
        private readonly Label lblTotal = new Label();
        private readonly Button btnCadastrar = new Button { Text = "Cadastrar" };
        private readonly Button btnEditar = new Button { Text = "Editar" };
        private readonly Button btnExcluir = new Button { Text = "Excluir" };
        private readonly Button btnLimpar = new Button { Text = "Limpar" };
        private readonly List<Button> botoesExtras = new List<Button>();

        // ---------- O que cada tela filha precisa escrever ----------
        protected abstract DataTable Consultar(string filtro);
        protected abstract void Validar();
        protected abstract void Inserir();
        protected abstract void Atualizar(int id);
        protected abstract void Excluir(int id);
        protected abstract void PreencherCampos(DataGridViewRow linha);
        protected abstract void LimparCampos();

        /// <summary>Roda uma vez ao abrir a tela (ex.: carregar um ComboBox). Opcional.</summary>
        protected virtual void AoAbrir() { }

        protected FormCadastroBase(string titulo, string subtitulo)
        {
            Text = titulo;
            Font = Estilo.Fonte;
            BackColor = Estilo.Fundo;
            ConstruirTela(titulo, subtitulo);

            Load += (s, e) =>
            {
                try
                {
                    AoAbrir();
                    Carregar();
                }
                catch (MySqlException ex)
                {
                    Erro("Não foi possível acessar o banco de dados.\nConfira se o MySQL está ligado (verde) no XAMPP.\n\n" + ex.Message);
                }
                AtualizarBotoes();
                CampoInicial?.Focus();
            };
        }

        // =====================================================================
        //  MONTAGEM DO VISUAL
        // =====================================================================
        private void ConstruirTela(string titulo, string subtitulo)
        {
            // ----- Cabeçalho (título + subtítulo + linha) -----
            var cabecalho = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.White };
            cabecalho.Controls.Add(new Label
            {
                Text = titulo,
                Font = Estilo.FonteTitulo,
                ForeColor = Estilo.AzulEscuro,
                AutoSize = true,
                Location = new Point(28, 12)
            });
            cabecalho.Controls.Add(new Label
            {
                Text = subtitulo,
                ForeColor = Estilo.CorSuave,
                AutoSize = true,
                Location = new Point(31, 56)
            });
            cabecalho.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Estilo.Borda });

            // ----- Cartão da esquerda: campos + botões -----
            var cartaoCampos = new Cartao { Dock = DockStyle.Fill, BackColor = Estilo.FundoSuave };

            tblCampos.Dock = DockStyle.Fill;
            tblCampos.ColumnCount = 1;
            tblCampos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblCampos.AutoScroll = true;

            tblBotoes.Dock = DockStyle.Bottom;
            tblBotoes.Padding = new Padding(0, 10, 0, 0);
            tblBotoes.ColumnCount = 2;
            tblBotoes.RowCount = 2;
            tblBotoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblBotoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblBotoes.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tblBotoes.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tblBotoes.Height = 106;

            Estilo.BotaoPrimario(btnCadastrar, Estilo.Azul);
            Estilo.BotaoPrimario(btnEditar, Estilo.AzulEscuro);
            Estilo.BotaoPrimario(btnExcluir, Estilo.Vermelho);
            Estilo.BotaoSecundario(btnLimpar);
            foreach (var b in new[] { btnCadastrar, btnEditar, btnExcluir, btnLimpar })
            {
                b.Dock = DockStyle.Fill;
                b.Margin = new Padding(4);
            }
            tblBotoes.Controls.Add(btnCadastrar, 0, 0);
            tblBotoes.Controls.Add(btnEditar, 1, 0);
            tblBotoes.Controls.Add(btnExcluir, 0, 1);
            tblBotoes.Controls.Add(btnLimpar, 1, 1);

            // Regra do Dock: o que preenche (Fill) é adicionado PRIMEIRO.
            cartaoCampos.Controls.Add(tblCampos);
            cartaoCampos.Controls.Add(tblBotoes);

            // ----- Cartão da direita: pesquisa + tabela -----
            var cartaoLista = new Cartao { Dock = DockStyle.Fill };

            Estilo.Grid(grid);
            grid.Dock = DockStyle.Fill;

            lblTotal.Dock = DockStyle.Bottom;
            lblTotal.Height = 28;
            lblTotal.ForeColor = Estilo.CorSuave;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;

            txtPesquisa.PlaceholderText = "Pesquisar...";
            txtPesquisa.Dock = DockStyle.Top;
            var barraPesquisa = new Panel { Dock = DockStyle.Top, Height = 48 };
            barraPesquisa.Controls.Add(txtPesquisa);

            cartaoLista.Controls.Add(grid);
            cartaoLista.Controls.Add(lblTotal);
            cartaoLista.Controls.Add(barraPesquisa);

            // ----- Corpo: [cartão campos] [espaço] [cartão lista] -----
            var corpo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(28, 20, 28, 24)
            };
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 380));
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            corpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            corpo.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            corpo.Controls.Add(cartaoCampos, 0, 0);
            corpo.Controls.Add(cartaoLista, 2, 0);

            Controls.Add(corpo);
            Controls.Add(cabecalho);

            // ----- Eventos -----
            btnCadastrar.Click += (s, e) => Processar(() => { Validar(); Inserir(); }, "Cadastro realizado com sucesso!");
            btnEditar.Click += BtnEditar_Click;
            btnExcluir.Click += BtnExcluir_Click;
            btnLimpar.Click += (s, e) => LimparTudo();
            grid.CellClick += Grid_CellClick;
            grid.CellFormatting += Grid_CellFormatting;
            txtPesquisa.TextChanged += (s, e) => Carregar();
        }

        // =====================================================================
        //  MÉTODOS PARA AS TELAS FILHAS MONTAREM OS CAMPOS
        // =====================================================================

        /// <summary>Coloca um rótulo + campo no cartão da esquerda.</summary>
        protected void AdicionarCampo(string rotulo, Control campo, int altura = 30)
        {
            var lbl = new Label
            {
                Text = rotulo,
                Dock = DockStyle.Fill,
                Font = Estilo.FonteRotulo,
                ForeColor = Estilo.CorSuave,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0)
            };
            campo.Dock = DockStyle.Fill;
            campo.Margin = new Padding(0, 2, 0, 4);

            int linha = tblCampos.RowCount;
            tblCampos.RowCount = linha + 2;
            tblCampos.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            tblCampos.RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 6));
            tblCampos.Controls.Add(lbl, 0, linha);
            tblCampos.Controls.Add(campo, 0, linha + 1);
        }

        /// <summary>Coloca uma caixinha de marcar (ex.: "Cadastro ativo").</summary>
        protected void AdicionarCheck(CheckBox caixa)
        {
            caixa.AutoSize = true;
            caixa.Anchor = AnchorStyles.Top | AnchorStyles.Left;   // fica no topo da linha, sem esticar
            caixa.Font = Estilo.Fonte;
            caixa.ForeColor = Estilo.CorTexto;
            caixa.Margin = new Padding(0, 10, 0, 0);

            int linha = tblCampos.RowCount;
            tblCampos.RowCount = linha + 1;
            tblCampos.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            tblCampos.Controls.Add(caixa, 0, linha);
        }

        /// <summary>Botão extra embaixo dos 4 principais. Só fica ativo com um registro selecionado.</summary>
        protected Button AdicionarBotaoExtra(string texto, EventHandler aoClicar)
        {
            var botao = new Button { Text = texto, Dock = DockStyle.Fill, Margin = new Padding(4) };
            Estilo.BotaoSecundario(botao);
            botao.Click += aoClicar;

            int linha = tblBotoes.RowCount;
            tblBotoes.RowCount = linha + 1;
            tblBotoes.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tblBotoes.Controls.Add(botao, 0, linha);
            tblBotoes.SetColumnSpan(botao, 2);
            tblBotoes.Height += 48;

            botoesExtras.Add(botao);
            botao.Enabled = IdSelecionado != 0;
            return botao;
        }

        /// <summary>Lê o texto de uma coluna da linha clicada (nunca devolve nulo).</summary>
        protected static string Texto(DataGridViewRow linha, string coluna)
        {
            return linha.Cells[coluna].Value?.ToString() ?? "";
        }

        // =====================================================================
        //  FUNCIONAMENTO
        // =====================================================================

        protected void Carregar()
        {
            try
            {
                DataTable tabela = Consultar(txtPesquisa.Text.Trim());
                grid.DataSource = tabela;

                foreach (string nome in ColunasOcultas)
                    if (grid.Columns.Contains(nome))
                        grid.Columns[nome].Visible = false;

                if (grid.Columns.Contains("id"))
                {
                    grid.Columns["id"].HeaderText = "Cód.";
                    grid.Columns["id"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    grid.Columns["id"].Width = 64;
                }
                foreach (var par in Cabecalhos)
                    if (grid.Columns.Contains(par.Key))
                        grid.Columns[par.Key].HeaderText = par.Value;

                grid.ClearSelection();
                lblTotal.Text = tabela.Rows.Count + " registro(s)";
            }
            catch (MySqlException ex)
            {
                Erro("Erro ao carregar os dados: " + ex.Message);
            }
        }

        protected void LimparTudo()
        {
            IdSelecionado = 0;
            LimparCampos();
            grid.ClearSelection();
            AtualizarBotoes();
            CampoInicial?.Focus();
        }

        private void AtualizarBotoes()
        {
            bool selecionado = IdSelecionado != 0;
            btnCadastrar.Enabled = !selecionado;   // com um registro escolhido, só Editar/Excluir
            btnEditar.Enabled = selecionado;
            btnExcluir.Enabled = selecionado;
            foreach (var b in botoesExtras) b.Enabled = selecionado;
        }

        /// <summary>Roda uma ação (salvar, excluir...) e trata todos os tipos de erro.</summary>
        private void Processar(Action acao, string mensagemSucesso)
        {
            try
            {
                acao();
                MessageBox.Show(mensagemSucesso, "SIGOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LimparTudo();
                Carregar();
            }
            catch (RegraNegocioException ex)
            {
                Aviso(ex.Message);
                ex.Foco?.Focus();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                Aviso("Já existe um cadastro com esse valor (campo que não pode repetir, como CPF/CNPJ ou login).");
            }
            catch (MySqlException ex) when (ex.Number == 1451)
            {
                Aviso("Não é possível excluir: existem registros vinculados a este cadastro.");
            }
            catch (MySqlException ex)
            {
                Erro("Erro no banco de dados: " + ex.Message);
            }
        }

        private void BtnEditar_Click(object? sender, EventArgs e)
        {
            if (IdSelecionado == 0)
            {
                Aviso("Clique em um registro da lista para editar.");
                return;
            }
            Processar(() => { Validar(); Atualizar(IdSelecionado); }, "Cadastro atualizado com sucesso!");
        }

        private void BtnExcluir_Click(object? sender, EventArgs e)
        {
            if (IdSelecionado == 0)
            {
                Aviso("Clique em um registro da lista para excluir.");
                return;
            }
            var resposta = MessageBox.Show("Deseja realmente excluir este cadastro?\nEsta ação não pode ser desfeita.",
                "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (resposta != DialogResult.Yes) return;

            Processar(() => Excluir(IdSelecionado), "Cadastro excluído com sucesso!");
        }

        private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            DataGridViewRow linha = grid.Rows[e.RowIndex];
            IdSelecionado = Convert.ToInt32(linha.Cells["id"].Value);
            PreencherCampos(linha);
            AtualizarBotoes();
        }

        /// <summary>Mostra CPF/telefone formatados, preço em R$ e ativo como Sim/Não.</summary>
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Value == null || e.Value == DBNull.Value) return;

            switch (grid.Columns[e.ColumnIndex].Name)
            {
                case "documento":
                    e.Value = Validador.FormatarDocumento(e.Value.ToString());
                    e.FormattingApplied = true;
                    break;
                case "telefone":
                    e.Value = Validador.FormatarTelefone(e.Value.ToString());
                    e.FormattingApplied = true;
                    break;
                case "preco":
                    if (e.Value is decimal preco)
                    {
                        e.Value = preco.ToString("C2", Validador.Cultura);
                        e.CellStyle!.Alignment = DataGridViewContentAlignment.MiddleRight;
                        e.FormattingApplied = true;
                    }
                    break;
                case "ativo":
                    e.Value = Convert.ToBoolean(e.Value) ? "Sim" : "Não";
                    e.FormattingApplied = true;
                    break;
            }
        }

        // ---------- Caixinhas de mensagem ----------
        protected static void Aviso(string mensagem)
        {
            MessageBox.Show(mensagem, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected static void Erro(string mensagem)
        {
            MessageBox.Show(mensagem, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

// ===================== Telas/FormClientes.cs =====================
namespace SIGOS.Telas
{
    [DesignerCategory("Code")]
    public class FormClientes : FormCadastroBase
    {
        private readonly TextBox txtNome = Estilo.NovoTexto(120);
        private readonly TextBox txtDocumento = Estilo.NovoTexto(20);
        private readonly TextBox txtTelefone = Estilo.NovoTexto(20);
        private readonly TextBox txtEmail = Estilo.NovoTexto(120);
        private readonly TextBox txtEndereco = Estilo.NovoTexto(200);
        private readonly CheckBox chkAtivo = new CheckBox { Text = "Cadastro ativo", Checked = true };

        public FormClientes() : base("Cadastro de Clientes", "Cadastre, edite, pesquise e exclua os clientes da assistência técnica.")
        {
            txtDocumento.PlaceholderText = "000.000.000-00 ou CNPJ";
            txtTelefone.PlaceholderText = "(11) 91234-5678";

            AdicionarCampo("Nome *", txtNome);
            AdicionarCampo("CPF / CNPJ *", txtDocumento);
            AdicionarCampo("Telefone *", txtTelefone);
            AdicionarCampo("E-mail", txtEmail);
            AdicionarCampo("Endereço", txtEndereco);
            AdicionarCheck(chkAtivo);
            CampoInicial = txtNome;

            // Ao sair do campo, arruma a formatação (pontos, traços, parênteses)
            txtDocumento.Leave += (s, e) => txtDocumento.Text = Validador.FormatarDocumento(txtDocumento.Text);
            txtTelefone.Leave += (s, e) => txtTelefone.Text = Validador.FormatarTelefone(txtTelefone.Text);

            Cabecalhos["nome"] = "Nome";
            Cabecalhos["documento"] = "CPF / CNPJ";
            Cabecalhos["telefone"] = "Telefone";
            Cabecalhos["email"] = "E-mail";
            Cabecalhos["endereco"] = "Endereço";
            Cabecalhos["ativo"] = "Ativo";
        }

        protected override DataTable Consultar(string filtro)
        {
            string doc = Validador.NormalizarDocumento(filtro);
            string num = Validador.SomenteNumeros(filtro);

            // "\u0001" é um valor que nunca existe no banco: usado quando não há números para buscar
            return Banco.Tabela(
                @"SELECT id, nome, documento, telefone, email, endereco, ativo
                  FROM clientes
                  WHERE nome LIKE @f OR email LIKE @f OR documento LIKE @d OR telefone LIKE @n
                  ORDER BY nome",
                Banco.P("@f", "%" + filtro + "%"),
                Banco.P("@d", doc.Length > 0 ? "%" + doc + "%" : "\u0001"),
                Banco.P("@n", num.Length > 0 ? "%" + num + "%" : "\u0001"));
        }

        protected override void Validar()
        {
            if (string.IsNullOrWhiteSpace(txtNome.Text) || txtNome.Text.Trim().Length < 3)
                throw new RegraNegocioException("Informe o nome do cliente (mínimo 3 letras).", txtNome);

            if (!Validador.DocumentoValido(txtDocumento.Text))
                throw new RegraNegocioException("CPF ou CNPJ inválido. Confira os números digitados.", txtDocumento);

            if (!Validador.TelefoneValido(txtTelefone.Text))
                throw new RegraNegocioException("Telefone inválido. Digite o DDD + número, por exemplo (11) 91234-5678.", txtTelefone);

            if (!Validador.EmailValido(txtEmail.Text))
                throw new RegraNegocioException("E-mail inválido. Exemplo: nome@email.com", txtEmail);
        }

        protected override void Inserir()
        {
            Banco.Executar(
                @"INSERT INTO clientes (nome, documento, telefone, email, endereco, ativo)
                  VALUES (@nome, @doc, @tel, @email, @end, @ativo)",
                Banco.P("@nome", txtNome.Text.Trim()),
                Banco.P("@doc", Validador.NormalizarDocumento(txtDocumento.Text)),
                Banco.P("@tel", Validador.SomenteNumeros(txtTelefone.Text)),
                Banco.P("@email", Validador.NuloSeVazio(txtEmail.Text)),
                Banco.P("@end", Validador.NuloSeVazio(txtEndereco.Text)),
                Banco.P("@ativo", chkAtivo.Checked));
        }

        protected override void Atualizar(int id)
        {
            Banco.Executar(
                @"UPDATE clientes
                  SET nome=@nome, documento=@doc, telefone=@tel, email=@email, endereco=@end, ativo=@ativo
                  WHERE id=@id",
                Banco.P("@nome", txtNome.Text.Trim()),
                Banco.P("@doc", Validador.NormalizarDocumento(txtDocumento.Text)),
                Banco.P("@tel", Validador.SomenteNumeros(txtTelefone.Text)),
                Banco.P("@email", Validador.NuloSeVazio(txtEmail.Text)),
                Banco.P("@end", Validador.NuloSeVazio(txtEndereco.Text)),
                Banco.P("@ativo", chkAtivo.Checked),
                Banco.P("@id", id));
        }

        protected override void Excluir(int id)
        {
            // Regra do professor: não excluir cliente com ordens de serviço
            if (Banco.Contar("SELECT COUNT(*) FROM ordens_servico WHERE cliente_id=@id", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException(
                    "Não é possível excluir: este cliente possui ordens de serviço.\n" +
                    "Para deixá-lo fora de uso, desmarque \"Cadastro ativo\" e clique em Editar.");

            if (Banco.Contar("SELECT COUNT(*) FROM equipamentos WHERE cliente_id=@id", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException(
                    "Não é possível excluir: este cliente possui equipamentos cadastrados.\n" +
                    "Para deixá-lo fora de uso, desmarque \"Cadastro ativo\" e clique em Editar.");

            Banco.Executar("DELETE FROM clientes WHERE id=@id", Banco.P("@id", id));
        }

        protected override void PreencherCampos(DataGridViewRow linha)
        {
            txtNome.Text = Texto(linha, "nome");
            txtDocumento.Text = Validador.FormatarDocumento(Texto(linha, "documento"));
            txtTelefone.Text = Validador.FormatarTelefone(Texto(linha, "telefone"));
            txtEmail.Text = Texto(linha, "email");
            txtEndereco.Text = Texto(linha, "endereco");
            chkAtivo.Checked = Convert.ToBoolean(linha.Cells["ativo"].Value);
        }

        protected override void LimparCampos()
        {
            txtNome.Clear();
            txtDocumento.Clear();
            txtTelefone.Clear();
            txtEmail.Clear();
            txtEndereco.Clear();
            chkAtivo.Checked = true;
        }
    }
}

// ===================== Telas/FormTecnicos.cs =====================
namespace SIGOS.Telas
{
    [DesignerCategory("Code")]
    public class FormTecnicos : FormCadastroBase
    {
        private readonly TextBox txtNome = Estilo.NovoTexto(120);
        private readonly TextBox txtEspecialidade = Estilo.NovoTexto(100);
        private readonly TextBox txtTelefone = Estilo.NovoTexto(20);
        private readonly CheckBox chkAtivo = new CheckBox { Text = "Técnico ativo", Checked = true };

        public FormTecnicos() : base("Cadastro de Técnicos", "Cadastre os técnicos responsáveis pelos atendimentos.")
        {
            txtEspecialidade.PlaceholderText = "Ex.: Notebooks, Impressoras, Redes";
            txtTelefone.PlaceholderText = "(11) 91234-5678";

            AdicionarCampo("Nome *", txtNome);
            AdicionarCampo("Especialidade", txtEspecialidade);
            AdicionarCampo("Telefone", txtTelefone);
            AdicionarCheck(chkAtivo);
            CampoInicial = txtNome;

            txtTelefone.Leave += (s, e) => txtTelefone.Text = Validador.FormatarTelefone(txtTelefone.Text);

            Cabecalhos["nome"] = "Nome";
            Cabecalhos["especialidade"] = "Especialidade";
            Cabecalhos["telefone"] = "Telefone";
            Cabecalhos["ativo"] = "Ativo";
        }

        protected override DataTable Consultar(string filtro)
        {
            string num = Validador.SomenteNumeros(filtro);
            return Banco.Tabela(
                @"SELECT id, nome, especialidade, telefone, ativo
                  FROM tecnicos
                  WHERE nome LIKE @f OR especialidade LIKE @f OR telefone LIKE @n
                  ORDER BY nome",
                Banco.P("@f", "%" + filtro + "%"),
                Banco.P("@n", num.Length > 0 ? "%" + num + "%" : "\u0001"));
        }

        protected override void Validar()
        {
            if (string.IsNullOrWhiteSpace(txtNome.Text) || txtNome.Text.Trim().Length < 3)
                throw new RegraNegocioException("Informe o nome do técnico (mínimo 3 letras).", txtNome);

            // Telefone é opcional, mas se foi digitado precisa estar certo
            if (!string.IsNullOrWhiteSpace(txtTelefone.Text) && !Validador.TelefoneValido(txtTelefone.Text))
                throw new RegraNegocioException("Telefone inválido. Digite o DDD + número, por exemplo (11) 91234-5678.", txtTelefone);
        }

        protected override void Inserir()
        {
            Banco.Executar(
                @"INSERT INTO tecnicos (nome, especialidade, telefone, ativo)
                  VALUES (@nome, @esp, @tel, @ativo)",
                Banco.P("@nome", txtNome.Text.Trim()),
                Banco.P("@esp", Validador.NuloSeVazio(txtEspecialidade.Text)),
                Banco.P("@tel", Validador.NuloSeVazio(Validador.SomenteNumeros(txtTelefone.Text))),
                Banco.P("@ativo", chkAtivo.Checked));
        }

        protected override void Atualizar(int id)
        {
            Banco.Executar(
                @"UPDATE tecnicos
                  SET nome=@nome, especialidade=@esp, telefone=@tel, ativo=@ativo
                  WHERE id=@id",
                Banco.P("@nome", txtNome.Text.Trim()),
                Banco.P("@esp", Validador.NuloSeVazio(txtEspecialidade.Text)),
                Banco.P("@tel", Validador.NuloSeVazio(Validador.SomenteNumeros(txtTelefone.Text))),
                Banco.P("@ativo", chkAtivo.Checked),
                Banco.P("@id", id));
        }

        protected override void Excluir(int id)
        {
            if (Banco.Contar("SELECT COUNT(*) FROM ordens_servico WHERE tecnico_id=@id", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException(
                    "Não é possível excluir: este técnico está vinculado a ordens de serviço.\n" +
                    "Para deixá-lo fora de uso, desmarque \"Técnico ativo\" e clique em Editar.");

            Banco.Executar("DELETE FROM tecnicos WHERE id=@id", Banco.P("@id", id));
        }

        protected override void PreencherCampos(DataGridViewRow linha)
        {
            txtNome.Text = Texto(linha, "nome");
            txtEspecialidade.Text = Texto(linha, "especialidade");
            txtTelefone.Text = Validador.FormatarTelefone(Texto(linha, "telefone"));
            chkAtivo.Checked = Convert.ToBoolean(linha.Cells["ativo"].Value);
        }

        protected override void LimparCampos()
        {
            txtNome.Clear();
            txtEspecialidade.Clear();
            txtTelefone.Clear();
            chkAtivo.Checked = true;
        }
    }
}

// ===================== Telas/FormServicos.cs =====================
namespace SIGOS.Telas
{
    [DesignerCategory("Code")]
    public class FormServicos : FormCadastroBase
    {
        private readonly TextBox txtDescricao = Estilo.NovoTexto(120);
        private readonly TextBox txtPreco = Estilo.NovoTexto(14);
        private readonly CheckBox chkAtivo = new CheckBox { Text = "Serviço ativo", Checked = true };

        public FormServicos() : base("Cadastro de Serviços", "Cadastre os serviços oferecidos e o preço de cada um.")
        {
            txtDescricao.PlaceholderText = "Ex.: Formatação e instalação do Windows";
            txtPreco.PlaceholderText = "0,00";
            txtPreco.TextAlign = HorizontalAlignment.Right;

            AdicionarCampo("Descrição do serviço *", txtDescricao);
            AdicionarCampo("Preço (R$) *", txtPreco);
            AdicionarCheck(chkAtivo);
            CampoInicial = txtDescricao;

            // Ao sair do campo, mostra o preço com 2 casas: 89,9 vira 89,90
            txtPreco.Leave += (s, e) =>
            {
                if (Validador.TentarMoeda(txtPreco.Text, out decimal valor))
                    txtPreco.Text = valor.ToString("N2", Validador.Cultura);
            };

            Cabecalhos["descricao"] = "Descrição";
            Cabecalhos["preco"] = "Preço";
            Cabecalhos["ativo"] = "Ativo";
        }

        protected override DataTable Consultar(string filtro)
        {
            return Banco.Tabela(
                @"SELECT id, descricao, preco, ativo
                  FROM servicos
                  WHERE descricao LIKE @f
                  ORDER BY descricao",
                Banco.P("@f", "%" + filtro + "%"));
        }

        /// <summary>Lê o preço digitado. Se estiver errado, avisa e para tudo.</summary>
        private decimal LerPreco()
        {
            if (!Validador.TentarMoeda(txtPreco.Text, out decimal preco))
                throw new RegraNegocioException("Preço inválido. Digite apenas números, por exemplo 89,90.", txtPreco);

            if (preco < 0)
                throw new RegraNegocioException("O preço não pode ser negativo.", txtPreco);

            if (preco > 99999999.99m)
                throw new RegraNegocioException("Preço muito alto. O máximo é R$ 99.999.999,99.", txtPreco);

            return preco;
        }

        protected override void Validar()
        {
            if (string.IsNullOrWhiteSpace(txtDescricao.Text) || txtDescricao.Text.Trim().Length < 3)
                throw new RegraNegocioException("Informe a descrição do serviço (mínimo 3 letras).", txtDescricao);

            LerPreco();

            // Evita dois serviços com a mesma descrição
            int repetidos = Banco.Contar(
                "SELECT COUNT(*) FROM servicos WHERE descricao=@d AND id<>@id",
                Banco.P("@d", txtDescricao.Text.Trim()),
                Banco.P("@id", IdSelecionado));
            if (repetidos > 0)
                throw new RegraNegocioException("Já existe um serviço com essa descrição.", txtDescricao);
        }

        protected override void Inserir()
        {
            Banco.Executar(
                "INSERT INTO servicos (descricao, preco, ativo) VALUES (@d, @p, @ativo)",
                Banco.P("@d", txtDescricao.Text.Trim()),
                Banco.P("@p", LerPreco()),
                Banco.P("@ativo", chkAtivo.Checked));
        }

        protected override void Atualizar(int id)
        {
            Banco.Executar(
                "UPDATE servicos SET descricao=@d, preco=@p, ativo=@ativo WHERE id=@id",
                Banco.P("@d", txtDescricao.Text.Trim()),
                Banco.P("@p", LerPreco()),
                Banco.P("@ativo", chkAtivo.Checked),
                Banco.P("@id", id));
        }

        protected override void Excluir(int id)
        {
            if (Banco.Contar("SELECT COUNT(*) FROM itens_os WHERE servico_id=@id", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException(
                    "Não é possível excluir: este serviço já foi usado em ordens de serviço.\n" +
                    "Para tirá-lo de uso, desmarque \"Serviço ativo\" e clique em Editar.");

            Banco.Executar("DELETE FROM servicos WHERE id=@id", Banco.P("@id", id));
        }

        protected override void PreencherCampos(DataGridViewRow linha)
        {
            txtDescricao.Text = Texto(linha, "descricao");
            txtPreco.Text = Convert.ToDecimal(linha.Cells["preco"].Value).ToString("N2", Validador.Cultura);
            chkAtivo.Checked = Convert.ToBoolean(linha.Cells["ativo"].Value);
        }

        protected override void LimparCampos()
        {
            txtDescricao.Clear();
            txtPreco.Clear();
            chkAtivo.Checked = true;
        }
    }
}

// ===================== Telas/FormEquipamentos.cs =====================
namespace SIGOS.Telas
{
    [DesignerCategory("Code")]
    public class FormEquipamentos : FormCadastroBase
    {
        private readonly ComboBox cmbCliente = Estilo.NovoCombo();
        private readonly ComboBox cmbTipo = Estilo.NovoCombo(aceitaDigitar: true);
        private readonly TextBox txtMarca = Estilo.NovoTexto(60);
        private readonly TextBox txtModelo = Estilo.NovoTexto(80);
        private readonly TextBox txtSerie = Estilo.NovoTexto(100);
        private readonly TextBox txtDefeito = Estilo.NovoTexto(1000);

        public FormEquipamentos() : base("Cadastro de Equipamentos", "Registre os equipamentos recebidos e consulte o histórico de cada um.")
        {
            cmbTipo.Items.AddRange(new object[]
            {
                "Notebook", "Computador", "Impressora", "Monitor", "Smartphone",
                "Tablet", "Console", "Roteador", "Outro"
            });

            txtDefeito.Multiline = true;
            txtDefeito.ScrollBars = ScrollBars.Vertical;
            txtDefeito.AcceptsReturn = true;

            AdicionarCampo("Cliente proprietário *", cmbCliente);
            AdicionarCampo("Tipo *", cmbTipo);
            AdicionarCampo("Marca *", txtMarca);
            AdicionarCampo("Modelo *", txtModelo);
            AdicionarCampo("Número de série", txtSerie);
            AdicionarCampo("Defeito informado", txtDefeito, 90);
            CampoInicial = cmbCliente;

            AdicionarBotaoExtra("Ver histórico do equipamento", AbrirHistorico);

            ColunasOcultas.Add("cliente_id");
            Cabecalhos["cliente"] = "Cliente";
            Cabecalhos["tipo"] = "Tipo";
            Cabecalhos["marca"] = "Marca";
            Cabecalhos["modelo"] = "Modelo";
            Cabecalhos["numero_serie"] = "Nº de série";
            Cabecalhos["defeito_informado"] = "Defeito informado";
        }

        /// <summary>Preenche o ComboBox de clientes com os dados do banco.</summary>
        protected override void AoAbrir()
        {
            DataTable clientes = Banco.Tabela(
                "SELECT id, CONCAT(nome, IF(ativo = 1, '', ' (inativo)')) AS rotulo FROM clientes ORDER BY nome");

            cmbCliente.DisplayMember = "rotulo";
            cmbCliente.ValueMember = "id";
            cmbCliente.DataSource = clientes;
            cmbCliente.SelectedIndex = -1;
        }

        protected override DataTable Consultar(string filtro)
        {
            return Banco.Tabela(
                @"SELECT e.id, e.cliente_id, c.nome AS cliente, e.tipo, e.marca, e.modelo,
                         e.numero_serie, e.defeito_informado
                  FROM equipamentos e
                  INNER JOIN clientes c ON c.id = e.cliente_id
                  WHERE c.nome LIKE @f OR e.tipo LIKE @f OR e.marca LIKE @f
                     OR e.modelo LIKE @f OR e.numero_serie LIKE @f
                  ORDER BY c.nome, e.tipo",
                Banco.P("@f", "%" + filtro + "%"));
        }

        private int ClienteSelecionado()
        {
            return cmbCliente.SelectedValue == null ? 0 : Convert.ToInt32(cmbCliente.SelectedValue);
        }

        protected override void Validar()
        {
            if (ClienteSelecionado() == 0)
                throw new RegraNegocioException("Selecione o cliente proprietário do equipamento.", cmbCliente);

            if (string.IsNullOrWhiteSpace(cmbTipo.Text))
                throw new RegraNegocioException("Informe o tipo do equipamento.", cmbTipo);

            if (string.IsNullOrWhiteSpace(txtMarca.Text))
                throw new RegraNegocioException("Informe a marca.", txtMarca);

            if (string.IsNullOrWhiteSpace(txtModelo.Text))
                throw new RegraNegocioException("Informe o modelo.", txtModelo);

            // Número de série não pode repetir
            if (!string.IsNullOrWhiteSpace(txtSerie.Text))
            {
                int repetidos = Banco.Contar(
                    "SELECT COUNT(*) FROM equipamentos WHERE numero_serie=@s AND id<>@id",
                    Banco.P("@s", txtSerie.Text.Trim()),
                    Banco.P("@id", IdSelecionado));
                if (repetidos > 0)
                    throw new RegraNegocioException("Já existe um equipamento cadastrado com esse número de série.", txtSerie);
            }
        }

        protected override void Inserir()
        {
            // Equipamento novo só para cliente ativo
            int ativo = Banco.Contar("SELECT COUNT(*) FROM clientes WHERE id=@id AND ativo=1",
                Banco.P("@id", ClienteSelecionado()));
            if (ativo == 0)
                throw new RegraNegocioException("Este cliente está inativo. Reative o cadastro do cliente antes de registrar um equipamento.", cmbCliente);

            Banco.Executar(
                @"INSERT INTO equipamentos (cliente_id, tipo, marca, modelo, numero_serie, defeito_informado)
                  VALUES (@cli, @tipo, @marca, @modelo, @serie, @defeito)",
                Banco.P("@cli", ClienteSelecionado()),
                Banco.P("@tipo", cmbTipo.Text.Trim()),
                Banco.P("@marca", txtMarca.Text.Trim()),
                Banco.P("@modelo", txtModelo.Text.Trim()),
                Banco.P("@serie", Validador.NuloSeVazio(txtSerie.Text)),
                Banco.P("@defeito", Validador.NuloSeVazio(txtDefeito.Text)));
        }

        protected override void Atualizar(int id)
        {
            Banco.Executar(
                @"UPDATE equipamentos
                  SET cliente_id=@cli, tipo=@tipo, marca=@marca, modelo=@modelo,
                      numero_serie=@serie, defeito_informado=@defeito
                  WHERE id=@id",
                Banco.P("@cli", ClienteSelecionado()),
                Banco.P("@tipo", cmbTipo.Text.Trim()),
                Banco.P("@marca", txtMarca.Text.Trim()),
                Banco.P("@modelo", txtModelo.Text.Trim()),
                Banco.P("@serie", Validador.NuloSeVazio(txtSerie.Text)),
                Banco.P("@defeito", Validador.NuloSeVazio(txtDefeito.Text)),
                Banco.P("@id", id));
        }

        protected override void Excluir(int id)
        {
            if (Banco.Contar("SELECT COUNT(*) FROM ordens_servico WHERE equipamento_id=@id", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException("Não é possível excluir: este equipamento possui ordens de serviço no histórico.");

            Banco.Executar("DELETE FROM equipamentos WHERE id=@id", Banco.P("@id", id));
        }

        protected override void PreencherCampos(DataGridViewRow linha)
        {
            cmbCliente.SelectedValue = Convert.ToInt32(linha.Cells["cliente_id"].Value);
            cmbTipo.Text = Texto(linha, "tipo");
            txtMarca.Text = Texto(linha, "marca");
            txtModelo.Text = Texto(linha, "modelo");
            txtSerie.Text = Texto(linha, "numero_serie");
            txtDefeito.Text = Texto(linha, "defeito_informado");
        }

        protected override void LimparCampos()
        {
            cmbCliente.SelectedIndex = -1;
            cmbTipo.SelectedIndex = -1;
            cmbTipo.Text = "";
            txtMarca.Clear();
            txtModelo.Clear();
            txtSerie.Clear();
            txtDefeito.Clear();
        }

        private void AbrirHistorico(object? sender, EventArgs e)
        {
            if (IdSelecionado == 0) return;
            string descricao = (cmbTipo.Text + " " + txtMarca.Text + " " + txtModelo.Text).Trim();
            using var janela = new FormHistoricoEquipamento(IdSelecionado, descricao);
            janela.ShowDialog(this);
        }
    }

    /// <summary>Janelinha com todas as ordens de serviço de um equipamento.</summary>
    [DesignerCategory("Code")]
    public class FormHistoricoEquipamento : Form
    {
        public FormHistoricoEquipamento(int equipamentoId, string descricao)
        {
            Text = "Histórico do equipamento";
            Font = Estilo.Fonte;
            BackColor = Color.White;
            Size = new Size(980, 480);
            MinimumSize = new Size(720, 360);
            StartPosition = FormStartPosition.CenterParent;

            var titulo = new Label
            {
                Text = "Histórico de ordens de serviço: " + descricao,
                Dock = DockStyle.Top,
                Height = 60,
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = Estilo.AzulEscuro,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };
            var grade = new DataGridView { Dock = DockStyle.Fill };
            Estilo.Grid(grade);

            Controls.Add(grade);
            Controls.Add(titulo);

            Load += (s, e) =>
            {
                try
                {
                    DataTable tabela = Banco.Tabela(
                        @"SELECT o.id AS numero, o.data_abertura, o.status, o.prioridade,
                                 t.nome AS tecnico, o.diagnostico, o.total
                          FROM ordens_servico o
                          LEFT JOIN tecnicos t ON t.id = o.tecnico_id
                          WHERE o.equipamento_id = @id
                          ORDER BY o.data_abertura DESC",
                        Banco.P("@id", equipamentoId));

                    grade.DataSource = tabela;
                    if (tabela.Rows.Count == 0)
                    {
                        titulo.Text += "\nNenhuma ordem de serviço registrada para este equipamento.";
                        titulo.Height = 80;
                        return;
                    }

                    grade.Columns["numero"].HeaderText = "Nº OS";
                    grade.Columns["data_abertura"].HeaderText = "Abertura";
                    grade.Columns["data_abertura"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
                    grade.Columns["status"].HeaderText = "Status";
                    grade.Columns["prioridade"].HeaderText = "Prioridade";
                    grade.Columns["tecnico"].HeaderText = "Técnico";
                    grade.Columns["diagnostico"].HeaderText = "Diagnóstico";
                    grade.Columns["total"].HeaderText = "Total";
                    grade.Columns["total"].DefaultCellStyle.Format = "C2";
                    grade.Columns["total"].DefaultCellStyle.FormatProvider = Validador.Cultura;
                    grade.ClearSelection();
                }
                catch (MySqlException ex)
                {
                    MessageBox.Show("Erro ao carregar o histórico: " + ex.Message, "Erro",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
        }
    }
}

// ===================== Telas/FormUsuarios.cs =====================
namespace SIGOS.Telas
{
    [DesignerCategory("Code")]
    public class FormUsuarios : FormCadastroBase
    {
        private readonly TextBox txtNome = Estilo.NovoTexto(100);
        private readonly TextBox txtLogin = Estilo.NovoTexto(50);
        private readonly TextBox txtSenha = Estilo.NovoTexto(60);
        private readonly ComboBox cmbPerfil = Estilo.NovoCombo();
        private readonly CheckBox chkAtivo = new CheckBox { Text = "Usuário ativo", Checked = true };

        public FormUsuarios() : base("Cadastro de Usuários", "Quem pode entrar no sistema e com qual perfil de acesso.")
        {
            txtSenha.UseSystemPasswordChar = true;
            txtSenha.PlaceholderText = "Mínimo 6 caracteres";
            cmbPerfil.Items.AddRange(new object[] { "ADMIN", "ATENDENTE", "TECNICO" });

            AdicionarCampo("Nome *", txtNome);
            AdicionarCampo("Login *", txtLogin);
            AdicionarCampo("Senha (deixe em branco para manter a atual)", txtSenha);
            AdicionarCampo("Perfil de acesso *", cmbPerfil);
            AdicionarCheck(chkAtivo);
            CampoInicial = txtNome;

            Cabecalhos["nome"] = "Nome";
            Cabecalhos["login"] = "Login";
            Cabecalhos["perfil"] = "Perfil";
            Cabecalhos["ativo"] = "Ativo";
        }

        protected override DataTable Consultar(string filtro)
        {
            // A senha (hash) nunca é carregada na tela
            return Banco.Tabela(
                @"SELECT id, nome, login, perfil, ativo
                  FROM usuarios
                  WHERE nome LIKE @f OR login LIKE @f OR perfil LIKE @f
                  ORDER BY nome",
                Banco.P("@f", "%" + filtro + "%"));
        }

        protected override void Validar()
        {
            if (string.IsNullOrWhiteSpace(txtNome.Text) || txtNome.Text.Trim().Length < 3)
                throw new RegraNegocioException("Informe o nome do usuário (mínimo 3 letras).", txtNome);

            string login = txtLogin.Text.Trim();
            if (login.Length < 3 || login.Contains(' '))
                throw new RegraNegocioException("O login precisa ter no mínimo 3 caracteres e não pode ter espaços.", txtLogin);

            if (cmbPerfil.SelectedItem == null)
                throw new RegraNegocioException("Selecione o perfil de acesso.", cmbPerfil);

            bool novo = IdSelecionado == 0;
            if (novo && txtSenha.Text.Length == 0)
                throw new RegraNegocioException("Informe uma senha para o novo usuário.", txtSenha);

            if (txtSenha.Text.Length > 0 && txtSenha.Text.Length < 6)
                throw new RegraNegocioException("A senha precisa ter no mínimo 6 caracteres.", txtSenha);
        }

        protected override void Inserir()
        {
            Banco.Executar(
                @"INSERT INTO usuarios (nome, login, senha_hash, perfil, ativo)
                  VALUES (@nome, @login, @hash, @perfil, @ativo)",
                Banco.P("@nome", txtNome.Text.Trim()),
                Banco.P("@login", txtLogin.Text.Trim()),
                Banco.P("@hash", Seguranca.GerarHash(txtSenha.Text)),
                Banco.P("@perfil", cmbPerfil.SelectedItem!.ToString()),
                Banco.P("@ativo", chkAtivo.Checked));
        }

        protected override void Atualizar(int id)
        {
            if (txtSenha.Text.Length == 0)
            {
                // Senha em branco = mantém a senha atual
                Banco.Executar(
                    "UPDATE usuarios SET nome=@nome, login=@login, perfil=@perfil, ativo=@ativo WHERE id=@id",
                    Banco.P("@nome", txtNome.Text.Trim()),
                    Banco.P("@login", txtLogin.Text.Trim()),
                    Banco.P("@perfil", cmbPerfil.SelectedItem!.ToString()),
                    Banco.P("@ativo", chkAtivo.Checked),
                    Banco.P("@id", id));
            }
            else
            {
                Banco.Executar(
                    "UPDATE usuarios SET nome=@nome, login=@login, senha_hash=@hash, perfil=@perfil, ativo=@ativo WHERE id=@id",
                    Banco.P("@nome", txtNome.Text.Trim()),
                    Banco.P("@login", txtLogin.Text.Trim()),
                    Banco.P("@hash", Seguranca.GerarHash(txtSenha.Text)),
                    Banco.P("@perfil", cmbPerfil.SelectedItem!.ToString()),
                    Banco.P("@ativo", chkAtivo.Checked),
                    Banco.P("@id", id));
            }
        }

        protected override void Excluir(int id)
        {
            if (Banco.Contar("SELECT COUNT(*) FROM usuarios WHERE id=@id AND login='admin'", Banco.P("@id", id)) > 0)
                throw new RegraNegocioException("O usuário administrador padrão (admin) não pode ser excluído.");

            int usos = Banco.Contar("SELECT COUNT(*) FROM ordens_servico WHERE usuario_abertura_id=@id", Banco.P("@id", id))
                     + Banco.Contar("SELECT COUNT(*) FROM historico_os WHERE usuario_id=@id", Banco.P("@id", id));
            if (usos > 0)
                throw new RegraNegocioException(
                    "Não é possível excluir: este usuário já registrou ordens de serviço.\n" +
                    "Para bloquear o acesso, desmarque \"Usuário ativo\" e clique em Editar.");

            Banco.Executar("DELETE FROM usuarios WHERE id=@id", Banco.P("@id", id));
        }

        protected override void PreencherCampos(DataGridViewRow linha)
        {
            txtNome.Text = Texto(linha, "nome");
            txtLogin.Text = Texto(linha, "login");
            txtSenha.Clear();
            cmbPerfil.SelectedItem = Texto(linha, "perfil");
            chkAtivo.Checked = Convert.ToBoolean(linha.Cells["ativo"].Value);
        }

        protected override void LimparCampos()
        {
            txtNome.Clear();
            txtLogin.Clear();
            txtSenha.Clear();
            cmbPerfil.SelectedIndex = -1;
            chkAtivo.Checked = true;
        }
    }
}

// ===================== Telas/FormInicio.cs =====================
namespace SIGOS.Telas
{
    /// <summary>Tela de boas-vindas com os números principais do sistema.</summary>
    [DesignerCategory("Code")]
    public class FormInicio : Form
    {
        private readonly (string Rotulo, string Sql)[] indicadores =
        {
            ("Clientes ativos",   "SELECT COUNT(*) FROM clientes WHERE ativo = 1"),
            ("Equipamentos",      "SELECT COUNT(*) FROM equipamentos"),
            ("Técnicos ativos",   "SELECT COUNT(*) FROM tecnicos WHERE ativo = 1"),
            ("Serviços ativos",   "SELECT COUNT(*) FROM servicos WHERE ativo = 1"),
            ("OS em andamento",   "SELECT COUNT(*) FROM ordens_servico WHERE status NOT IN ('CONCLUIDA','CANCELADA')")
        };

        private readonly List<Label> numeros = new List<Label>();

        public FormInicio()
        {
            Text = "Início";
            Font = Estilo.Fonte;
            BackColor = Color.White;

            // ----- Texto de boas-vindas (centralizado) -----
            var boasVindas = new Panel { Dock = DockStyle.Fill };
            boasVindas.Controls.Add(new Label
            {
                Text = "Bem-vindo ao SIGOS",
                Dock = DockStyle.Bottom,
                Height = 70,
                Font = new Font("Segoe UI Light", 30f),
                ForeColor = Estilo.AzulEscuro,
                TextAlign = ContentAlignment.BottomCenter
            });
            boasVindas.Controls.Add(new Label
            {
                Text = "Sistema de Ordem de Serviço",
                Dock = DockStyle.Bottom,
                Height = 38,
                Font = new Font("Segoe UI", 13f),
                ForeColor = Estilo.CorSuave,
                TextAlign = ContentAlignment.TopCenter
            });

            // ----- Cartões com os números -----
            var cartoes = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = indicadores.Length,
                RowCount = 1
            };
            cartoes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            for (int i = 0; i < indicadores.Length; i++)
            {
                cartoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / indicadores.Length));

                var numero = new Label
                {
                    Text = "–",
                    Dock = DockStyle.Top,
                    Height = 64,
                    Font = new Font("Segoe UI Semibold", 28f),
                    ForeColor = Estilo.Azul,
                    TextAlign = ContentAlignment.BottomLeft
                };
                var rotulo = new Label
                {
                    Text = indicadores[i].Rotulo,
                    Dock = DockStyle.Fill,
                    ForeColor = Estilo.CorSuave,
                    TextAlign = ContentAlignment.TopLeft
                };
                var cartao = new Cartao { Dock = DockStyle.Fill, Margin = new Padding(8) };
                cartao.Controls.Add(rotulo);
                cartao.Controls.Add(numero);

                cartoes.Controls.Add(cartao, i, 0);
                numeros.Add(numero);
            }

            // ----- Estrutura: [boas-vindas] [cartões] [espaço] -----
            var raiz = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(40, 0, 40, 0)
            };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            raiz.Controls.Add(boasVindas, 0, 0);
            raiz.Controls.Add(cartoes, 0, 1);
            Controls.Add(raiz);

            Load += (s, e) => CarregarNumeros();
        }

        private void CarregarNumeros()
        {
            for (int i = 0; i < indicadores.Length; i++)
            {
                try
                {
                    numeros[i].Text = Banco.Contar(indicadores[i].Sql).ToString();
                }
                catch (MySqlException)
                {
                    numeros[i].Text = "–";   // banco desligado: mostra traço em vez de travar
                }
            }
        }
    }
}

// ===================== Telas/FormEmBreve.cs =====================
namespace SIGOS.Telas
{
    /// <summary>
    /// Tela provisória para módulos que ainda não foram feitos (Ordem de Serviço, Consulta).
    /// Quando o módulo ficar pronto, troque no FormPrincipal: () => new FormEmBreve(...) por () => new FormOrdemServico().
    /// </summary>
    [DesignerCategory("Code")]
    public class FormEmBreve : Form
    {
        public FormEmBreve(string titulo, string descricao)
        {
            Text = titulo;
            Font = Estilo.Fonte;
            BackColor = Color.White;

            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            raiz.Controls.Add(new Label
            {
                Text = titulo,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Light", 26f),
                ForeColor = Estilo.AzulEscuro,
                TextAlign = ContentAlignment.BottomCenter
            }, 0, 0);
            raiz.Controls.Add(new Label
            {
                Text = descricao,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Estilo.CorSuave,
                TextAlign = ContentAlignment.TopCenter,
                Padding = new Padding(0, 10, 0, 0)
            }, 0, 1);

            Controls.Add(raiz);
        }
    }
}

// ===================== Telas/FormPrincipal.cs =====================
namespace SIGOS.Telas
{
    /// <summary>
    /// Tela principal: menu lateral azul à esquerda e, à direita, a tela escolhida.
    /// Para ligar um módulo novo (ex.: Ordem de Serviço), troque a linha dele na lista "itens".
    /// </summary>
    [DesignerCategory("Code")]
    public class FormPrincipal : Form
    {
        private readonly Panel pnlMenu = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Estilo.AzulEscuro };
        private readonly Panel pnlConteudo = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        private readonly List<BotaoMenu> botoes = new List<BotaoMenu>();
        private Form? telaAtual;

        public FormPrincipal()
        {
            Text = "SIGOS - Sistema de Ordem de Serviço";
            Font = Estilo.Fonte;
            BackColor = Color.White;
            MinimumSize = new Size(1000, 650);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;

            // ===== Menu: texto, ícone (código da fonte Segoe MDL2 Assets) e a tela que abre =====
            var itens = new (string Texto, string Icone, Func<Form> Criar)[]
            {
                ("Início",           "", () => new FormInicio()),
                ("Clientes",         "", () => new FormClientes()),
                ("Equipamentos",     "", () => new FormEquipamentos()),
                ("Técnicos",         "", () => new FormTecnicos()),
                ("Serviços",         "", () => new FormServicos()),
                ("Ordem de Serviço", "", () => new FormEmBreve("Ordem de Serviço", "Módulo em desenvolvimento.")),
                ("Consulta",         "", () => new FormEmBreve("Consulta", "Módulo em desenvolvimento.")),
                ("Usuários",         "", () => new FormUsuarios())
            };

            foreach (var item in itens)
            {
                var botao = new BotaoMenu { Text = item.Texto, Icone = item.Icone };
                var criar = item.Criar;
                botao.Click += (s, e) => AbrirTela(botao, criar);
                botoes.Add(botao);
            }

            var btnSair = new BotaoMenu { Text = "Sair", Icone = "", Dock = DockStyle.Bottom };
            btnSair.Click += (s, e) =>
            {
                var resposta = MessageBox.Show("Deseja sair do sistema?", "SIGOS",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (resposta == DialogResult.Yes) Close();
            };

            var logo = new Label
            {
                Text = "SIGOS",
                Dock = DockStyle.Top,
                Height = 96,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // REGRA DO DOCK = TOP: o último controle adicionado fica no TOPO.
            // Por isso adicionamos de trás para frente, e o logo por último.
            pnlMenu.Controls.Add(btnSair);
            for (int i = botoes.Count - 1; i >= 0; i--)
                pnlMenu.Controls.Add(botoes[i]);
            pnlMenu.Controls.Add(logo);

            // O painel que preenche (Fill) é adicionado primeiro.
            Controls.Add(pnlConteudo);
            Controls.Add(pnlMenu);

            Load += (s, e) => AbrirTela(botoes[0], itens[0].Criar);
        }

        /// <summary>Mostra uma tela dentro do painel branco e destaca o botão correspondente.</summary>
        private void AbrirTela(BotaoMenu botao, Func<Form> criar)
        {
            foreach (var b in botoes) b.Ativo = false;
            botao.Ativo = true;

            pnlConteudo.Controls.Clear();
            telaAtual?.Dispose();

            Form tela = criar();
            tela.TopLevel = false;
            tela.FormBorderStyle = FormBorderStyle.None;
            tela.Dock = DockStyle.Fill;
            pnlConteudo.Controls.Add(tela);
            telaAtual = tela;
            tela.Show();
        }
    }
}
