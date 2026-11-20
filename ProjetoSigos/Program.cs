using SIGOS.Telas;

namespace SIGOS;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Quando o login estiver pronto, abra o FormLogin aqui antes do FormPrincipal.
        Application.Run(new FormPrincipal());
    }
}