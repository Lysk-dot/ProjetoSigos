using MySql.Data.MySqlClient;
using System.Data;

namespace SIGOS.Dados;

/// <summary>
/// Vários comandos no banco "tudo ou nada": se um falhar, nenhum é gravado.
/// Uso:  Transacao.Rodar(tx => { tx.Executar("INSERT ..."); tx.Executar("UPDATE ..."); });
/// </summary>
public sealed class Transacao
{
    private readonly MySqlConnection _conn;
    private readonly MySqlTransaction _tx;

    private Transacao(MySqlConnection conn, MySqlTransaction tx)
    {
        _conn = conn;
        _tx = tx;
    }

    /// <summary>Código (id) gerado pelo último INSERT.</summary>
    public long UltimoId { get; private set; }

    public static void Rodar(Action<Transacao> acao)
    {
        using var conn = Conexao.Criar();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            acao(new Transacao(conn, tx));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public int Executar(string sql, params MySqlParameter[] parametros)
    {
        using var cmd = new MySqlCommand(sql, _conn, _tx);
        cmd.Parameters.AddRange(parametros);
        int linhas = cmd.ExecuteNonQuery();
        UltimoId = cmd.LastInsertedId;
        return linhas;
    }

    public object? Escalar(string sql, params MySqlParameter[] parametros)
    {
        using var cmd = new MySqlCommand(sql, _conn, _tx);
        cmd.Parameters.AddRange(parametros);
        object? valor = cmd.ExecuteScalar();
        return valor == DBNull.Value ? null : valor;
    }

    public DataTable Tabela(string sql, params MySqlParameter[] parametros)
    {
        using var cmd = new MySqlCommand(sql, _conn, _tx);
        cmd.Parameters.AddRange(parametros);
        using var adaptador = new MySqlDataAdapter(cmd);
        var tabela = new DataTable();
        adaptador.Fill(tabela);
        return tabela;
    }
}
