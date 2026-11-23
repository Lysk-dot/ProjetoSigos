namespace SIGOS.Dados;

/// <summary>
/// Quem está usando o sistema. Enquanto a tela de login não existe, o usuário
/// é sempre o "admin" (cadastrado no banco). Quando o login for criado, basta
/// preencher UsuarioId e Nome depois da autenticação.
/// </summary>
public static class Sessao
{
    private static int _usuarioId;
    private static string _nome = "";

    public static int UsuarioId
    {
        get { Garantir(); return _usuarioId; }
    }

    public static string Nome
    {
        get { Garantir(); return _nome; }
    }

    private static void Garantir()
    {
        if (_usuarioId != 0) return;

        var tabela = Banco.Tabela("SELECT id, nome FROM usuarios WHERE login = 'admin' AND ativo = 1 LIMIT 1");
        if (tabela.Rows.Count == 0)
            throw new RegraNegocioException("O usuário \"admin\" ativo não foi encontrado na tabela usuarios.");

        _usuarioId = Convert.ToInt32(tabela.Rows[0]["id"]);
        _nome = tabela.Rows[0]["nome"]?.ToString() ?? "admin";
    }
}
