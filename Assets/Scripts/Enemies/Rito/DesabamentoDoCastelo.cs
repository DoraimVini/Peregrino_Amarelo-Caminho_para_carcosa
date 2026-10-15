using System;
using UnityEngine;
using FavelaAmarela.Core.Enemies;

namespace FavelaAmarela.Runtime.Enemies
{
    /// <summary>
    /// Camada Runtime (MonoBehaviour). O <b>fim trágico</b>: selado o Rei, o Castelo de Carcosa
    /// desaba sobre o Damião. O cronograma é do <see cref="DesabamentoDoTrono"/> (Core); aqui as
    /// pedras caem de verdade — com sombra crescendo no chão, tremor de câmera, Nobres tombando —
    /// e, no fim, o arco do Castelo cai em cima do Damião.
    ///
    /// <para><b>Versão jogada, com a arte que o projeto tem</b> (ruínas do pacote Undead, da
    /// CraftPix). A intenção do Vini é uma cinemática no lugar dela quando houver arte: a
    /// <c>SequenciaDeSelamento</c> chama <see cref="Tocar"/> e espera o aviso de fim — trocar este
    /// componente por um vídeo ou uma Timeline com o mesmo contrato basta.</para>
    ///
    /// <para><b>Sem alocação por quadro:</b> as quedas vêm de um conjunto criado uma vez, no
    /// começo da sequência. Pedra que já caiu fica no chão como entulho; se o conjunto esgota, a
    /// mais antiga volta a cair.</para>
    /// </summary>
    [AddComponentMenu("Favela Amarela/Enemies/Rito/Desabamento do Castelo")]
    public sealed class DesabamentoDoCastelo : MonoBehaviour
    {
        [Header("Arte [ASSET]")]
        [Tooltip("As pedras que caem (ruínas pequenas).")]
        [SerializeField] private Sprite[] pedras;

        [Tooltip("O arco que cai sobre o Damião.")]
        [SerializeField] private Sprite arcoFinal;

        [Tooltip("Material de sprite do projeto (o Sprites-Default que o resto da cena usa).")]
        [SerializeField] private Material material;

        [SerializeField] private float escalaDoArco = 3f;
        [SerializeField] private Vector2 escalaDasPedras = new Vector2(0.8f, 1.3f);

        [Header("A sala [CENA]")]
        [Tooltip("Os Nobres que tombam durante o desabamento.")]
        [SerializeField] private CoberturaDoTrono[] nobres;

        [Tooltip("Centro do losango do chão do Trono.")]
        [SerializeField] private Vector2 centroDaSala = new Vector2(0f, 62f);

        [Tooltip("Meia-largura e meia-altura do losango do chão.")]
        [SerializeField] private Vector2 meiaDiagonal = new Vector2(15f, 8f);

        [Header("A queda")]
        [SerializeField] private float alturaDaQueda = 10f;
        [SerializeField] private float duracaoDaQueda = 0.55f;
        [Tooltip("Ordem de desenho das sombras: acima do chão, abaixo dos atores.")]
        [SerializeField] private int ordemDaSombra = -1990;

        [Header("Fala")]
        [SerializeField] private FavelaAmarela.Runtime.UI.TutorialHintUI caixaDeTexto;

        [Tooltip("Dita quando a pedra começa a ceder. PROVISÓRIA — o texto final é do Vini.")]
        [TextArea(1, 3)]
        [SerializeField] private string falaDoTremor = "A pedra de Carcosa não sabe existir sem o Rei.";

        private const int TamanhoDoConjunto = 64;

        private sealed class Queda
        {
            public Transform Corpo;
            public SpriteRenderer Sprite;
            public Transform Sombra;
            public SpriteRenderer SpriteDaSombra;
            public Vector2 Alvo;
            public float T;
            public float Duracao;
            public float Altura;
            public bool Caindo;
            public bool Usada;
        }

        private DesabamentoDoTrono _rito;
        private Queda[] _quedas;
        private Queda _arco;
        private int _proxima;
        private int _pousos;
        private Transform _jogador;
        private FavelaAmarela.CameraSystem.IsometricCameraController _camera;
        private Action _aoSoterrar;
        private bool _comandoTomado;
        private float[] _quedaDosNobres;
        private System.Random _sorte;
        private static Sprite _sombraPadrao;

        /// <summary>O cronograma em curso, ou <c>null</c> antes de <see cref="Tocar"/>.</summary>
        public DesabamentoDoTrono Cronograma => _rito;

        /// <summary>Se o arco já caiu.</summary>
        public bool Soterrado => _rito != null && _rito.Fase == FaseDoDesabamento.Soterrado;

        /// <summary>
        /// Começa o desabamento. <paramref name="aoSoterrar"/> é chamado no instante em que o arco
        /// cai sobre o Damião — quem chama corta a tela ali. Idempotente.
        /// </summary>
        public void Tocar(Action aoSoterrar)
        {
            if (_rito != null) return;
            _aoSoterrar = aoSoterrar;

            var jogador = GameObject.FindGameObjectWithTag("Player");
            _jogador = jogador != null ? jogador.transform : null;
            ProtegerODamiao(jogador);
            var cam = Camera.main;
            _camera = cam != null ? cam.GetComponent<FavelaAmarela.CameraSystem.IsometricCameraController>() : null;
            _sorte = new System.Random(20261015);

            CriarConjunto();
            AgendarOsNobres();

            _rito = new DesabamentoDoTrono();
            _rito.OnFaseMudou += HandleFaseMudou;
            _rito.Comecar();
        }

        private void OnDestroy()
        {
            if (_rito != null) _rito.OnFaseMudou -= HandleFaseMudou;
            DevolverComando();
        }

        // ── Cada quadro ──────────────────────────────────────────────────────

        private void Update()
        {
            if (_rito == null || Soterrado) return;

            float dt = Time.deltaTime;
            var quadro = _rito.Avancar(dt);

            if (_camera != null && quadro.Tremor > 0f) _camera.AcrescentarTrauma(quadro.Tremor * dt * 2.5f);
            for (int i = 0; i < quadro.Pedras; i++) SoltarPedra(Mira(quadro.RaioDaMira));

            TombarNobres();
            AnimarQuedas(dt);

            if (quadro.GolpeFinal) Soterrar();
        }

        private void HandleFaseMudou(FaseDoDesabamento anterior, FaseDoDesabamento nova)
        {
            if (nova == FaseDoDesabamento.Tremor) Dizer(falaDoTremor, 3.5f);
            if (nova == FaseDoDesabamento.Soterramento) PrenderODamiao();
        }

        // ── As pedras ────────────────────────────────────────────────────────

        private Vector2 Mira(float raio)
        {
            Vector2 base_ = _jogador != null ? (Vector2)_jogador.position : centroDaSala;
            double ang = _sorte.NextDouble() * Math.PI * 2.0;
            double dist = Math.Sqrt(_sorte.NextDouble()) * raio;
            var p = base_ + new Vector2((float)(Math.Cos(ang) * dist), (float)(Math.Sin(ang) * dist * 0.5));
            return NoChao(p);
        }

        /// <summary>Traz um ponto para dentro do losango do chão — entulho no vazio lê como erro.</summary>
        private Vector2 NoChao(Vector2 p)
        {
            Vector2 d = p - centroDaSala;
            float n = Mathf.Abs(d.x) / meiaDiagonal.x + Mathf.Abs(d.y) / meiaDiagonal.y;
            return n <= 0.92f ? p : centroDaSala + d * (0.92f / n);
        }

        private void SoltarPedra(Vector2 alvo)
        {
            var q = _quedas[_proxima];
            _proxima = (_proxima + 1) % _quedas.Length;

            q.Sprite.sprite = pedras[_sorte.Next(pedras.Length)];
            float escala = Mathf.Lerp(escalaDasPedras.x, escalaDasPedras.y, (float)_sorte.NextDouble());
            q.Corpo.localScale = new Vector3(_sorte.Next(2) == 0 ? escala : -escala, escala, 1f);
            // Um giro leve por pedra: entulho caído não pousa alinhado. Só no sprite solto, sem
            // corpo físico — a regra de rotação travada é dos Rigidbody2D, e aqui não há nenhum.
            q.Corpo.rotation = Quaternion.Euler(0f, 0f, (float)(_sorte.NextDouble() * 30.0 - 15.0));
            Lancar(q, alvo, duracaoDaQueda, alturaDaQueda);
        }

        private void Lancar(Queda q, Vector2 alvo, float duracao, float altura)
        {
            q.Alvo = alvo;
            q.Altura = altura;
            q.T = 0f;
            q.Duracao = duracao;
            q.Caindo = true;
            q.Usada = true;
            q.Corpo.gameObject.SetActive(true);
            q.Sombra.gameObject.SetActive(true);
            q.Sprite.sortingOrder = Mathf.RoundToInt(-alvo.y * 10f) + 2;
            Posicionar(q, 0f);
        }

        private void AnimarQuedas(float dt)
        {
            for (int i = 0; i < _quedas.Length; i++) Animar(_quedas[i], dt, pequena: true);
            Animar(_arco, dt, pequena: false);
        }

        private void Animar(Queda q, float dt, bool pequena)
        {
            if (!q.Caindo) return;
            q.T += dt;
            float p = Mathf.Clamp01(q.T / q.Duracao);
            Posicionar(q, p);
            if (p < 1f) return;

            q.Caindo = false;
            q.Sombra.gameObject.SetActive(false);
            if (!pequena) return;

            if (_camera != null) _camera.AcrescentarTrauma(0.08f);
            if (++_pousos % 3 == 0)
                FavelaAmarela.Runtime.Audio.MixerDeAudio.Instancia?.Tocar(
                    FavelaAmarela.Runtime.Audio.SomDoJogo.EntidadeAbatida, q.Alvo);
        }

        /// <summary>Queda com gravidade: devagar no alto, depressa no fim; a sombra cresce e escurece.</summary>
        private static void Posicionar(Queda q, float p)
        {
            q.Corpo.position = q.Alvo + new Vector2(0f, (1f - p * p) * q.Altura);
            q.Sombra.position = q.Alvo;
            float s = Mathf.Lerp(0.35f, 1f, p) * Mathf.Abs(q.Corpo.localScale.y);
            q.Sombra.localScale = new Vector3(s * 1.6f, s * 0.8f, 1f);
            var c = q.SpriteDaSombra.color;
            c.a = Mathf.Lerp(0.15f, 0.55f, p);
            q.SpriteDaSombra.color = c;
        }

        // ── Os Nobres ────────────────────────────────────────────────────────

        private void AgendarOsNobres()
        {
            int n = nobres != null ? nobres.Length : 0;
            _quedaDosNobres = new float[n];
            var p = new ParametrosDoDesabamento();
            float inicio = p.Silencio + p.Tremor;
            for (int i = 0; i < n; i++)
                _quedaDosNobres[i] = inicio + (p.Desabamento * 0.85f) * (i + 0.5f) / n;
        }

        private void TombarNobres()
        {
            if (nobres == null) return;
            for (int i = 0; i < nobres.Length; i++)
            {
                if (_quedaDosNobres[i] < 0f || _rito.Tempo < _quedaDosNobres[i]) continue;
                _quedaDosNobres[i] = -1f;
                if (nobres[i] == null || !nobres[i].isActiveAndEnabled) continue;

                nobres[i].Desfazer();
                SoltarPedra(NoChao((Vector2)nobres[i].transform.position + new Vector2(0f, 0.6f)));
            }
        }

        // ── O fim ────────────────────────────────────────────────────────────

        /// <summary>
        /// O controle trava e o arco começa a cair, mirado no Damião. A sombra cresce durante o
        /// <see cref="ParametrosDoDesabamento.Soterramento"/> inteiro — é o aviso, e não há saída.
        /// </summary>
        private void PrenderODamiao()
        {
            FavelaAmarela.Runtime.Entrada.ArbitroDeFoco.Tomar(FavelaAmarela.Core.Entrada.CamadaDeEntrada.PainelModal);
            _comandoTomado = true;

            var rb = _jogador != null ? _jogador.GetComponent<Rigidbody2D>() : null;
            if (rb != null) rb.linearVelocity = Vector2.zero;

            Vector2 alvo = _jogador != null ? (Vector2)_jogador.position : centroDaSala;
            _arco.Sprite.sprite = arcoFinal;
            _arco.Corpo.localScale = new Vector3(escalaDoArco, escalaDoArco, 1f);
            // O arco é desenhado na FRENTE do Damião: é ele quem fica por baixo.
            Lancar(_arco, alvo + new Vector2(0f, -0.4f), _rito.Parametros.Soterramento, alturaDaQueda * 1.4f);
            _arco.Sprite.sortingOrder = Mathf.RoundToInt(-(alvo.y - 1f) * 10f);
        }

        private void Soterrar()
        {
            Posicionar(_arco, 1f);
            _arco.Caindo = false;
            _arco.Sombra.gameObject.SetActive(false);
            if (_camera != null) _camera.AcrescentarTrauma(1f);
            FavelaAmarela.Runtime.Audio.MixerDeAudio.Instancia?.Tocar(
                FavelaAmarela.Runtime.Audio.SomDoJogo.Colapso, _arco.Alvo);

            var fim = _aoSoterrar;
            _aoSoterrar = null;
            fim?.Invoke();
        }

        /// <summary>
        /// Do selo fechado ao Menu, o Damião não pode morrer de outra coisa. Medido em Play
        /// (2026-10-15): com a mente baixa ao fim do rito, o dreno do próprio equipamento a levou a
        /// zero durante a tela preta, e o jogo mostrou o Colapso — "Você se tornou mais um Cultista
        /// Amarelo" — por cima do fim. A morte deste fim é o arco, e só ele.
        /// </summary>
        private static void ProtegerODamiao(GameObject jogador)
        {
            if (jogador == null) return;
            var mente = jogador.GetComponentInChildren<FavelaAmarela.Runtime.Combat.ResilienciaBridge>();
            if (mente != null) mente.IgnorarTrauma = true;
            var corpo = jogador.GetComponentInChildren<FavelaAmarela.Runtime.Combat.VitalidadeBridge>();
            if (corpo != null) corpo.IgnorarDano = true;
        }

        private void DevolverComando()
        {
            if (!_comandoTomado) return;
            _comandoTomado = false;
            FavelaAmarela.Runtime.Entrada.ArbitroDeFoco.Devolver(FavelaAmarela.Core.Entrada.CamadaDeEntrada.PainelModal);
        }

        // ── Montagem ─────────────────────────────────────────────────────────

        private void CriarConjunto()
        {
            if (pedras == null || pedras.Length == 0)
                Debug.LogError("[DesabamentoDoCastelo] Sem sprites de pedra — o Castelo desaba invisível.", this);

            _quedas = new Queda[TamanhoDoConjunto];
            for (int i = 0; i < _quedas.Length; i++) _quedas[i] = CriarQueda($"Pedra_{i}");
            _arco = CriarQueda("Arco_Final");
        }

        private Queda CriarQueda(string nome)
        {
            var corpo = new GameObject(nome).transform;
            corpo.SetParent(transform, false);
            var sr = corpo.gameObject.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;

            var sombra = new GameObject(nome + "_Sombra").transform;
            sombra.SetParent(transform, false);
            var ss = sombra.gameObject.AddComponent<SpriteRenderer>();
            ss.sprite = SombraPadrao();
            if (material != null) ss.sharedMaterial = material;
            ss.color = new Color(0f, 0f, 0f, 0f);
            ss.sortingOrder = ordemDaSombra;

            corpo.gameObject.SetActive(false);
            sombra.gameObject.SetActive(false);
            return new Queda { Corpo = corpo, Sprite = sr, Sombra = sombra, SpriteDaSombra = ss };
        }

        /// <summary>Elipse macia gerada uma vez (32 × 32, PPU 32): a sombra da pedra no chão.</summary>
        private static Sprite SombraPadrao()
        {
            if (_sombraPadrao != null) return _sombraPadrao;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _sombraPadrao = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 32f);
            return _sombraPadrao;
        }

        // ── Fala ─────────────────────────────────────────────────────────────

        private void Dizer(string texto, float duracao)
        {
            var caixa = caixaDeTexto != null ? caixaDeTexto : FavelaAmarela.Runtime.UI.TutorialHintUI.Instancia;
            if (caixa != null && !string.IsNullOrEmpty(texto)) caixa.Mostrar(texto, duracao);
        }
    }
}
