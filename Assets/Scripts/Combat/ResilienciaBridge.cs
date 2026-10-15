using UnityEngine;
using FavelaAmarela.Core.Combat;

namespace FavelaAmarela.Runtime.Combat
{
    /// <summary>
    /// Bridge da <b>Resiliência Mental</b> de Damião — a contraparte da
    /// <see cref="VitalidadeBridge"/> para o canal anômalo.
    ///
    /// <para><b>Por que ela não existia, e por que passou a existir (2026-08-18):</b> a
    /// Vitalidade sempre teve bridge, então tudo que fere a carne resolve o alvo com
    /// <c>GetComponentInParent&lt;VitalidadeBridge&gt;()</c>. A Resiliência não tinha nenhuma —
    /// e por isso <b>19 call-sites em 11 arquivos</b> alcançavam
    /// <c>GameManager.Instance.Resiliencia</c>. Não era descuido de quem escreveu: era a única
    /// porta que existia. Criar a bridge remove a necessidade do global de uma vez, em vez de
    /// injetar o POCO em onze tipos diferentes.</para>
    ///
    /// <para><b>O caso que só isto resolve:</b> o <c>ConeDeGelo</c> é instanciado pelo Abdul
    /// <b>em runtime</b> — o bootstrap não tem como injetar num objeto que ainda não existe.
    /// Com a bridge, o cone não precisa ser alcançado: ele pergunta a quem acertou, do mesmo
    /// jeito que já pergunta a Resistência Anômala.</para>
    ///
    /// <para><b>Não é dona do POCO.</b> A <see cref="ResilienciaMental"/> nasce no
    /// <c>GameLoopBootstrap</c>, que a injeta aqui — diferente da <c>VitalidadeBridge</c>, que
    /// cria a sua a partir da ficha. A razão é que a Resiliência do jogador é única na cena e
    /// configurada no bootstrap (<c>maxResiliencia</c>, <c>fracaoPanico</c>), enquanto Vitalidade
    /// é por unidade. Esta bridge é <b>porta de acesso</b>, não fonte.</para>
    /// </summary>
    [AddComponentMenu("Favela Amarela/Resiliência Bridge")]
    public sealed class ResilienciaBridge : MonoBehaviour
    {
        private ResilienciaMental _resiliencia;

        /// <summary>
        /// A Resiliência Mental, ou <c>null</c> antes do bind. Quem consome deve checar — uma
        /// cena sem bootstrap (a Arena, por exemplo) é caso legítimo.
        /// </summary>
        public ResilienciaMental Resiliencia => _resiliencia;

        /// <summary>Se já recebeu a fonte. Útil para o chamador avisar uma vez, em vez de por frame.</summary>
        public bool Ligada => _resiliencia != null;

        /// <summary>
        /// Enquanto verdadeiro, todo Trauma e todo Colapso forçado são ignorados. Espelha
        /// <see cref="VitalidadeBridge.IgnorarDano"/> e é ligado pelo <c>CutsceneController</c>
        /// durante sequências roteirizadas.
        ///
        /// <para><b>Por que a checagem mora aqui:</b> antes, cada fonte de morte instantânea
        /// (<c>ColapsoTrigger</c>, <c>CoisaDoCemiterioAI</c>, <c>ReiEmAmareloAI</c>) consultava
        /// <c>GameManager.JogadorInvulneravel</c> por conta própria — três cópias da mesma regra,
        /// e qualquer fonte nova nascia sem ela. Centralizar aqui faz a proteção valer para
        /// <b>todos</b>, inclusive os que ainda não existem.</para>
        /// </summary>
        public bool IgnorarTrauma { get; set; }

        /// <summary>
        /// Liga a Resiliência criada pelo <c>GameLoopBootstrap</c>. Idempotente.
        /// </summary>
        public void Bind(ResilienciaMental resiliencia)
        {
            if (resiliencia == null)
            {
                Debug.LogError("[ResilienciaBridge] Bind recebeu Resiliência nula — nada que " +
                               "fere a mente de Damião terá efeito, e em silêncio.", this);
                return;
            }

            _resiliencia = resiliencia;
        }

        // ── Atalhos de uso comum ─────────────────────────────────────────────
        //
        // Existem para que o chamador não precise escrever `bridge.Resiliencia?.X()` em toda
        // parte — e, principalmente, para que "não há Resiliência ligada" seja tratado num lugar
        // só, em vez de replicar o null-check em 19 pontos como acontecia com o global.

        /// <summary>
        /// Aplica Trauma. Ignorado se a fonte não estiver ligada <b>ou</b> se
        /// <see cref="IgnorarTrauma"/> estiver ativo (cutscene).
        /// </summary>
        public void SofrerTrauma(float quantidade)
        {
            if (IgnorarTrauma) return;

            _resiliencia?.SofrerTrauma(MitigacaoDeDano.Aplicar(quantidade, _defesaAnomala()));
        }

        /// <summary>
        /// Drena Resiliência <b>sem mitigação</b> — para desgaste contínuo, cobrado em fatias por
        /// quadro. Respeita <see cref="IgnorarTrauma"/>, como todo dano à mente.
        ///
        /// <para><b>Por que não <see cref="SofrerTrauma"/>.</b> A <c>MitigacaoDeDano</c> subtrai a
        /// defesa de <b>cada golpe</b> e guarda um piso proporcional. Aplicada a 6 RM/s em fatias de
        /// 0,1, qualquer Defesa Anômala esmaga a fatia até o piso — e o dreno do olhar do Rei, que é
        /// a regra da luta, viraria outro número conforme o equipamento, sem ninguém ter decidido
        /// isso. Desgaste contínuo não é golpe; a relíquia que filtra o olhar (o Anel) é quem mexe
        /// nele, e de propósito.</para>
        ///
        /// <para><b>Quem passa por aqui (2026-09-28):</b> o olhar do Rei, o grito infrassônico do
        /// Byakhee, a <c>PressaoPsiquicaZone</c>, o Eco de Carcosa e o <c>DrenoRM</c> do
        /// equipamento. As quatro últimas iam por <see cref="SofrerTrauma"/> — e o defeito
        /// <b>não estava inerte</b>: as três peças do conjunto inicial dão +1 de Defesa Anômala
        /// cada desde 2026-09-09, e com qualquer uma vestida cada fatia caía no piso de 15%.
        /// O grito do Byakhee andava a 0,3 RM/s em vez dos 2 que as simulações calibraram.</para>
        ///
        /// <para><b>Desde 2026-10-15 é o dreno CRU</b> — para o que não é ataque: o <c>DrenoRM</c>
        /// que o próprio equipamento cobra. Dreno de anomalia vai por
        /// <see cref="SofrerDrenoAnomalo"/>, que a Resistência Anômala reduz.</para>
        /// </summary>
        public void SofrerDrenoContinuo(float quantidade)
        {
            if (IgnorarTrauma || quantidade <= 0f) return;
            _resiliencia?.SofrerTrauma(quantidade);
        }

        /// <summary>
        /// Drena Resiliência por uma <b>anomalia</b> — o grito do Byakhee, a Pressão Psíquica, o Eco
        /// de Carcosa, o olhar do Rei — com a <b>Resistência Anômala</b> reduzindo em porcentagem
        /// (<see cref="ResistenciaAnomala.DrenoQuePassa"/>: retorno decrescente, metade em R = 20,
        /// teto de 75%). Multiplicativa, então não depende do tamanho do quadro.
        ///
        /// <para>Pedido do Vini (2026-10-15): a resistência tem de proteger dos drenos — só não pela
        /// regra do golpe, que por fatia zera o dreno inteiro (o defeito antigo).</para>
        /// </summary>
        public void SofrerDrenoAnomalo(float quantidade)
        {
            if (quantidade <= 0f) return;
            SofrerDrenoContinuo(ResistenciaAnomala.DrenoQuePassa(quantidade, _defesaAnomala()));
        }

        /// <summary>
        /// Paga um <b>custo</b> em Resiliência — ativar um Artefato. Sem mitigação e sem a proteção
        /// de cutscene: custo é preço, não ataque.
        ///
        /// <para><b>O defeito que isto fecha (2026-10-15):</b> o custo do Artefato ia por
        /// <see cref="SofrerTrauma"/>, e a armadura o barateava — com o conjunto inicial (R 3) um
        /// custo de 10 saía por 7, e um de 3 caía ao piso de 0,45.</para>
        /// </summary>
        public void PagarResiliencia(float custo)
        {
            if (custo <= 0f) return;
            _resiliencia?.SofrerTrauma(custo);
        }

        /// <summary>A Resistência Anômala vestida agora — para a interface mostrar o que ela faz.</summary>
        public float ResistenciaAnomalaAtual => _defesaAnomala();

        /// <summary>
        /// Defesa anômala agregada do equipamento — a contraparte mental da
        /// <c>DefesaFisica</c> que a <c>VitalidadeBridge</c> já consulta.
        ///
        /// <para><b>A assimetria que isto fecha (2026-08-28).</b> Todo inimigo mitiga o canal
        /// anômalo pela <c>ResistenciaAnomala</c> da ficha (<c>EnemyBase:111</c>), e o Damião
        /// <b>não mitigava nada</b>: o Trauma chegava cru à Resiliência Mental. Ao mesmo tempo,
        /// <c>StatType.DefesaAnomalia</c> existia no enum, era rolado pela Coroa de Ossos, e
        /// <b>ninguém lia</b> — o artefato prometia proteção que não existia.</para>
        ///
        /// <para>Fica inerte até alguém equipar algo que role o atributo, então não muda
        /// dificuldade nenhuma hoje: fecha o buraco sem mexer no balanceamento.</para>
        ///
        /// <para><b>Desatualizado desde 2026-09-09:</b> as armaduras passaram a dar
        /// <c>DefesaAnomalia</c> (o conjunto inicial soma 3), então isto está vivo em jogo.</para>
        /// </summary>
        private static float DefesaDoEquipamento() =>
            FavelaAmarela.Player.GerenciadorEfeitosPassivos.Instance
                ?.GetBonus(FavelaAmarela.Inventario.StatType.DefesaAnomalia) ?? 0f;

        private System.Func<float> _defesaAnomala = DefesaDoEquipamento;

        /// <summary>
        /// <b>Costura de teste — o jogo não chama.</b> Troca a fonte da Defesa Anômala que
        /// <see cref="SofrerTrauma"/> subtrai; <c>null</c> volta ao equipamento
        /// (<c>GerenciadorEfeitosPassivos</c>), que é a fonte de jogo.
        ///
        /// <para><b>Por que existe.</b> A regra "dreno contínuo não é golpe" só se prova com
        /// defesa <b>maior que zero</b>. Montar inventário e equipamento num teste EditMode para
        /// obter esse número testaria o inventário, não a mente.</para>
        /// </summary>
        public void DefinirDefesaAnomalaParaTeste(System.Func<float> fonte) =>
            _defesaAnomala = fonte ?? DefesaDoEquipamento;

        /// <summary>
        /// Restaura Resiliência (Ancoragem). <b>Não</b> respeita <see cref="IgnorarTrauma"/> —
        /// cutscene protege de dano, não impede cura.
        /// </summary>
        public void Ancorar(float quantidade) => _resiliencia?.Ancorar(quantidade);

        /// <summary>
        /// Força o Colapso imediato — morte instantânea por toque. Ignorado durante cutscene:
        /// a queda roteirizada Z4→Z5 passa perto da Coisa do Cemitério de propósito, e morrer
        /// ali viraria derrota por acidente de encenação.
        /// </summary>
        public void ForcarColapso()
        {
            if (IgnorarTrauma) return;
            _resiliencia?.ForcarColapso();
        }

        /// <summary>Resiliência corrente, ou 0 se não houver fonte.</summary>
        public float Atual => _resiliencia?.Atual ?? 0f;
    }
}
