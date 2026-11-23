using MySql.Data.MySqlClient;
using SIGOS.Dados;

namespace SIGOS.Telas;

/// <summary>Caixinhas de mensagem e tratamento padrão de erros (mesmo comportamento das telas de cadastro).</summary>
internal static class Caixa
{
    public static void Aviso(string mensagem)
    {
        MessageBox.Show(mensagem, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Erro(string mensagem)
    {
        MessageBox.Show(mensagem, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>Roda a ação e avisa o usuário se algo der errado. Devolve true quando deu certo.</summary>
    public static bool Tentar(Action acao)
    {
        try
        {
            acao();
            return true;
        }
        catch (RegraNegocioException ex)
        {
            Aviso(ex.Message);
            ex.Foco?.Focus();
            return false;
        }
        catch (MySqlException ex) when (ex.Number == 1451)
        {
            Aviso("Não é possível concluir: existem registros vinculados.");
            return false;
        }
        catch (MySqlException ex)
        {
            Erro("Erro no banco de dados: " + ex.Message);
            return false;
        }
    }
}
