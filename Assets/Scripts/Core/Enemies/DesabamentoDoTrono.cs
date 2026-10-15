using System;

namespace FavelaAmarela.Core.Enemies
{
    /// <summary>As fases do desabamento do Trono de Aldebaran, na ordem.</summary>
    public enum FaseDoDesabamento
    {
        /// <summary>Antes de o selo fechar.</summary>
        Parado,
        /// <summary>O selo fechou e, por um fôlego, parece vitória.</summary>
        Silencio,
        /// <summary>A pedra começa a ceder: tremor e as primeiras pedras, longe.</summary>
        Tremor,
        /// <summary>O Castelo vem abaixo: cada vez mais pedras, cada vez mais perto.</summary>
        Desabamento,
        /// <summary>Damião já não se move; a sombra do arco cresce sobre ele.</summary>
        Soterramento,
        /// <summary>O arco caiu. Fim.</summary>
        Soterrado,
    }

    /// <summary>O que o desabamento pede ao mundo neste quadro.</summary>
    public readonly struct QuadroDoDesabamento
    {
        /// <summary>Quantas pedras soltar agora.</summary>
        public readonly int Pedras;

        /// <summary>Raio, em unidades, em volta do Damião onde as pedras caem.</summary>
        public readonly float RaioDaMira;

        /// <summary>Força do tremor, de 0 a 1.</summary>
        public readonly float Tremor;

        /// <summary>Verdadeiro só no quadro em que o arco cai sobre o Damião.</summary>
        public readonly bool GolpeFinal;

        /// <summary>Cria o quadro.</summary>
        public QuadroDoDesabamento(int pedras, float raioDaMira, float tremor, bool golpeFinal)
        {
            Pedras = pedras;
            RaioDaMira = raioDaMira;
            Tremor = tremor;
            GolpeFinal = golpeFinal;
        }
    }

    /// <summary>Os números do desabamento — tempos em segundos, taxas em pedras por segundo.</summary>
    public sealed class ParametrosDoDesabamento
    {
        /// <summary>O fôlego de falsa vitória.</summary>
        public float Silencio { get; set; } = 1.5f;

        /// <summary>Duração do tremor inicial.</summary>
        public float Tremor { get; set; } = 2.5f;

        /// <summary>Duração do desabamento propriamente dito.</summary>
        public float Desabamento { get; set; } = 6f;

        /// <summary>Do controle travado até o arco cair — o aviso da sombra.</summary>
        public float Soterramento { get; set; } = 1.2f;

        /// <summary>Pedras por segundo no começo do tremor.</summary>
        public float TaxaInicial { get; set; } = 0.8f;

        /// <summary>Pedras por segundo no fim do desabamento.</summary>
        public float TaxaFinal { get; set; } = 7f;

        /// <summary>Raio da mira no começo: as pedras caem longe.</summary>
        public float RaioInicial { get; set; } = 9f;

        /// <summary>Raio da mira no fim: as pedras caem em volta do Damião.</summary>
        public float RaioFinal { get; set; } = 2f;

        /// <summary>Duração total, do selo fechado ao arco caído.</summary>
        public float Total => Silencio + Tremor + Desabamento + Soterramento;
    }

    /// <summary>
    /// O <b>fim trágico</b> do Vertical Slice (pedido do Vini, 2026-10-15): selado o Rei, o
    /// Castelo de Carcosa desaba sobre o Damião. Este POCO é só o <b>cronograma</b> — em que fase
    /// se está, quantas pedras soltar, quão perto do Damião, quanto tremer, e o instante do golpe
    /// final. Quem faz as pedras caírem é o adaptador (<c>DesabamentoDoCastelo</c>).
    ///
    /// <para><b>Por que o jogador mantém o controle até o Soterramento:</b> correr é a reação certa
    /// a um teto caindo, e ela não pode adiantar. A pedra mira cada vez mais perto de onde ele
    /// está, não de onde ele estava — fugir só muda onde o arco cai.</para>
    ///
    /// <para>É a versão jogada, com a arte que o projeto tem. Quando houver arte, a ideia é uma
    /// cinemática no lugar dela (decisão do Vini) — a <c>SequenciaDeSelamento</c> só precisa
    /// trocar quem ela chama antes do texto final.</para>
    /// </summary>
    public sealed class DesabamentoDoTrono
    {
        private readonly ParametrosDoDesabamento _p;
        private float _t;
        private float _acumulado;
        private bool _caiu;

        /// <summary>A fase agora.</summary>
        public FaseDoDesabamento Fase { get; private set; } = FaseDoDesabamento.Parado;

        /// <summary>Segundos desde o selo.</summary>
        public float Tempo => _t;

        /// <summary>Os números em uso.</summary>
        public ParametrosDoDesabamento Parametros => _p;

        /// <summary>Se o Damião ainda pode se mover.</summary>
        public bool DamiaoNoComando => Fase == FaseDoDesabamento.Silencio
                                       || Fase == FaseDoDesabamento.Tremor
                                       || Fase == FaseDoDesabamento.Desabamento;

        /// <summary>Disparado a cada troca de fase (anterior, nova).</summary>
        public event Action<FaseDoDesabamento, FaseDoDesabamento> OnFaseMudou;

        /// <summary>Cria o cronograma; <c>null</c> usa os padrões.</summary>
        public DesabamentoDoTrono(ParametrosDoDesabamento parametros = null)
            => _p = parametros ?? new ParametrosDoDesabamento();

        /// <summary>O selo fechou: começa. Idempotente.</summary>
        public void Comecar()
        {
            if (Fase != FaseDoDesabamento.Parado) return;
            Mudar(FaseDoDesabamento.Silencio);
        }

        /// <summary>Avança um quadro e diz o que o mundo deve fazer nele.</summary>
        public QuadroDoDesabamento Avancar(float dt)
        {
            if (dt <= 0f || Fase == FaseDoDesabamento.Parado || Fase == FaseDoDesabamento.Soterrado)
                return default;

            _t += dt;
            AtualizarFase();

            if (Fase == FaseDoDesabamento.Soterrado && !_caiu)
            {
                _caiu = true;
                return new QuadroDoDesabamento(0, _p.RaioFinal, 1f, true);
            }

            if (Fase == FaseDoDesabamento.Silencio) return default;

            float progresso = Progresso();
            float taxa = Lerp(_p.TaxaInicial, _p.TaxaFinal, progresso);
            _acumulado += taxa * dt;
            int pedras = (int)Math.Floor(_acumulado);
            _acumulado -= pedras;

            float raio = Lerp(_p.RaioInicial, _p.RaioFinal, progresso);
            float tremor = Fase == FaseDoDesabamento.Tremor ? 0.25f : Lerp(0.3f, 0.85f, progresso);
            return new QuadroDoDesabamento(pedras, raio, tremor, false);
        }

        /// <summary>De 0 (começo do tremor) a 1 (o arco caindo).</summary>
        private float Progresso()
        {
            float inicio = _p.Silencio;
            float duracao = _p.Tremor + _p.Desabamento + _p.Soterramento;
            return Math.Min(1f, Math.Max(0f, (_t - inicio) / duracao));
        }

        private void AtualizarFase()
        {
            float fimSilencio = _p.Silencio;
            float fimTremor = fimSilencio + _p.Tremor;
            float fimDesabamento = fimTremor + _p.Desabamento;
            float fim = fimDesabamento + _p.Soterramento;

            var alvo = _t < fimSilencio ? FaseDoDesabamento.Silencio
                     : _t < fimTremor ? FaseDoDesabamento.Tremor
                     : _t < fimDesabamento ? FaseDoDesabamento.Desabamento
                     : _t < fim ? FaseDoDesabamento.Soterramento
                     : FaseDoDesabamento.Soterrado;

            // Um quadro longo pode pular uma fase; cada transição ainda dispara, em ordem.
            while (Fase < alvo) Mudar(Fase + 1);
        }

        private void Mudar(FaseDoDesabamento nova)
        {
            var anterior = Fase;
            Fase = nova;
            OnFaseMudou?.Invoke(anterior, nova);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
