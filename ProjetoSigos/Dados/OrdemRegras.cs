using System.Data;

namespace SIGOS.Dados;

/// <summary>Item de ComboBox com um valor (o que vai para o banco) e um rótulo (o que aparece na tela).</summary>
public sealed class Opcao
{
    public string Valor { get; }
    public string Rotulo { get; }

    public Opcao(string valor, string rotulo)
    {
        Valor = valor;
        Rotulo = rotulo;
    }

    public override string ToString() => Rotulo;
}

/// <summary>Regras da ordem de serviço: ordem dos status, total e mudança de status com histórico.</summary>
public static class OrdemRegras
{
    /// <summary>Ordem natural de execução. CANCELADA pode acontecer a qualquer momento (antes de encerrar).</summary>
    public static readonly string[] Fluxo =
    {
        "ABERTA", "DIAGNOSTICO", "AGUARDANDO_APROVACAO", "EM_EXECUCAO", "CONCLUIDA"
    };

    public static readonly string[] Prioridades = { "BAIXA", "MEDIA", "ALTA", "URGENTE" };

    public static bool Finalizada(string? status)
    {
        return status == "CONCLUIDA" || status == "CANCELADA";
    }

    /// <summary>Próximo passo natural do status. Nulo quando já terminou.</summary>
    public static string? Proximo(string? status)
    {
        int i = Array.IndexOf(Fluxo, status);
        return i >= 0 && i < Fluxo.Length - 1 ? Fluxo[i + 1] : null;
    }

    public static string RotuloStatus(string? status)
    {
        return status switch
        {
            "ABERTA" => "Aberta",
            "DIAGNOSTICO" => "Em diagnóstico",
            "AGUARDANDO_APROVACAO" => "Aguardando aprovação",
            "EM_EXECUCAO" => "Em execução",
            "CONCLUIDA" => "Concluída",
            "CANCELADA" => "Cancelada",
            _ => status ?? ""
        };
    }

    public static string RotuloPrioridade(string? prioridade)
    {
        return prioridade switch
        {
            "BAIXA" => "Baixa",
            "MEDIA" => "Média",
            "ALTA" => "Alta",
            "URGENTE" => "Urgente",
            _ => prioridade ?? ""
        };
    }

    public static Opcao[] OpcoesStatus()
    {
        return Fluxo.Append("CANCELADA").Select(s => new Opcao(s, RotuloStatus(s))).ToArray();
    }

    public static Opcao[] OpcoesPrioridade()
    {
        return Prioridades.Select(p => new Opcao(p, RotuloPrioridade(p))).ToArray();
    }

    /// <summary>total = soma(quantidade x preço dos itens) - desconto. O desconto nunca passa do subtotal.</summary>
    public static void RecalcularTotal(Transacao tx, int osId)
    {
        tx.Executar(
            @"UPDATE ordens_servico
              SET desconto = LEAST(desconto,
                  (SELECT IFNULL(SUM(quantidade * preco_unitario), 0) FROM itens_os WHERE ordem_servico_id = @id))
              WHERE id = @id",
            Banco.P("@id", osId));

        tx.Executar(
            @"UPDATE ordens_servico
              SET total = (SELECT IFNULL(SUM(quantidade * preco_unitario), 0) FROM itens_os WHERE ordem_servico_id = @id) - desconto
              WHERE id = @id",
            Banco.P("@id", osId));
    }

    /// <summary>
    /// Muda o status seguindo a ordem natural e grava no histórico (quem, quando e por quê).
    /// Tudo na mesma transação: ou muda e registra, ou não muda nada.
    /// </summary>
    public static void AlterarStatus(int osId, string novo, string? justificativa)
    {
        int usuarioId = Sessao.UsuarioId;
        string? just = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim();

        Transacao.Rodar(tx =>
        {
            DataTable os = tx.Tabela(
                "SELECT status, tecnico_id, diagnostico, solucao FROM ordens_servico WHERE id = @id FOR UPDATE",
                Banco.P("@id", osId));

            if (os.Rows.Count == 0)
                throw new RegraNegocioException("Ordem de serviço não encontrada. Ela pode ter sido excluída.");

            DataRow linha = os.Rows[0];
            string atual = linha["status"].ToString() ?? "";

            if (Finalizada(atual))
                throw new RegraNegocioException($"Esta OS já está {RotuloStatus(atual).ToLower()} e não pode mais mudar de status.");

            string? proximo = Proximo(atual);
            if (novo != "CANCELADA" && novo != proximo)
                throw new RegraNegocioException(
                    $"De \"{RotuloStatus(atual)}\" a OS só pode ir para \"{RotuloStatus(proximo)}\" ou ser cancelada.");

            switch (novo)
            {
                case "CANCELADA":
                    if (just == null)
                        throw new RegraNegocioException("Informe a justificativa do cancelamento.");
                    break;

                case "DIAGNOSTICO":
                    if (linha["tecnico_id"] == DBNull.Value)
                        throw new RegraNegocioException("Defina o técnico responsável (edite a OS) antes de iniciar o diagnóstico.");
                    break;

                case "AGUARDANDO_APROVACAO":
                    if (string.IsNullOrWhiteSpace(linha["diagnostico"] as string))
                        throw new RegraNegocioException("Preencha o diagnóstico (edite a OS) antes de enviar para aprovação.");
                    if (Convert.ToInt32(tx.Escalar("SELECT COUNT(*) FROM itens_os WHERE ordem_servico_id = @id", Banco.P("@id", osId))) == 0)
                        throw new RegraNegocioException("Lance pelo menos um serviço na OS antes de enviar para aprovação.");
                    break;

                case "CONCLUIDA":
                    if (string.IsNullOrWhiteSpace(linha["solucao"] as string))
                        throw new RegraNegocioException("Preencha a solução aplicada (edite a OS) antes de concluir.");
                    break;
            }

            tx.Executar(
                @"UPDATE ordens_servico
                  SET status = @novo,
                      data_conclusao = IF(@novo = 'CONCLUIDA', NOW(), data_conclusao)
                  WHERE id = @id",
                Banco.P("@novo", novo),
                Banco.P("@id", osId));

            tx.Executar(
                @"INSERT INTO historico_os (ordem_servico_id, usuario_id, status_anterior, status_novo, justificativa)
                  VALUES (@os, @usu, @ant, @novo, @just)",
                Banco.P("@os", osId),
                Banco.P("@usu", usuarioId),
                Banco.P("@ant", atual),
                Banco.P("@novo", novo),
                Banco.P("@just", just));
        });
    }
}
