using MySql.Data.MySqlClient;

namespace SIGOS.Dados;

public static class Conexao
{
    private const string StringConexao =
        "Server=localhost;Port=3306;Database=sigosdb;Uid=root;Pwd=;";

    public static MySqlConnection Criar()
    {
        return new MySqlConnection(StringConexao);
    }
}