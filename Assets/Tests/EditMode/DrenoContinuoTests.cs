using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using FavelaAmarela.Core.Combat;
using FavelaAmarela.Runtime.Combat;

namespace FavelaAmarela.Tests.EditMode
{
    /// <summary>
    /// Guarda a regra "<b>desgaste contínuo não é golpe</b>": uma fonte que drena Resiliência por
    /// segundo tem de drenar o mesmo por segundo qualquer que seja o tamanho do quadro e qualquer
    /// que seja a Defesa Anômala vestida.
    ///
    /// <para><b>O defeito (2026-09-28).</b> <c>ResilienciaBridge.SofrerTrauma</c> aplica a
    /// <c>MitigacaoDeDano</c> a cada chamada — subtrai a defesa do golpe e guarda um piso de 15%.
    /// O grito do Byakhee, a <c>PressaoPsiquicaZone</c>, o Eco de Carcosa e o <c>DrenoRM</c> do
    /// equipamento chamavam <c>SofrerTrauma(taxa * Time.deltaTime)</c> a cada quadro: cada fatia
    /// de ~0,03 era tratada como um golpe inteiro e caía no piso. Era tido como inerte, e não era —
    /// as três peças do conjunto inicial dão +1 de <c>DefesaAnomalia</c> cada desde 2026-09-09.</para>
    ///
    /// <para><b>A defesa é injetada</b> por <c>ResilienciaBridge.DefinirDefesaAnomalaParaTeste</c>. Por isso
    /// <see cref="ComDefesaAnomala_OGolpeDiscretoContinuaMitigado"/> existe: sem ele, uma costura
    /// quebrada devolveria defesa zero e o teste principal passaria no vazio.</para>
    /// </summary>
    public sealed class DrenoContinuoTests
    {
        private const float Maximo = 100f;
        private const float Tolerancia = 1e-3f;

        /// <summary>Capuz de Farrapos + Colete de Sucata + Caneleiras de Ferro, +1 cada.</summary>
        private const float DefesaDoConjuntoInicial = 3f;

        private GameObject _damiao;
        private ResilienciaBridge _mente;

        [SetUp]
        public void SetUp()
        {
            _damiao = new GameObject("Damiao_DrenoContinuoTests");
            _mente = _damiao.AddComponent<ResilienciaBridge>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_damiao);

        // ── A regra ──────────────────────────────────────────────────────────

        /// <summary>
        /// O pedido literal: 60 quadros de 1/60 s drenam o mesmo que 1 quadro de 1 s — e ambos
        /// drenam a <b>taxa autorada</b>. Só a igualdade entre quadros não bastaria: com taxa 2 e
        /// defesa 3, o caminho antigo também dava 0,3 nos dois casos (piso nos dois).
        /// </summary>
        [TestCase(2f, DefesaDoConjuntoInicial, TestName = "Byakhee passivo, conjunto inicial")]
        [TestCase(5f, DefesaDoConjuntoInicial, TestName = "Byakhee em frenesi, conjunto inicial")]
        [TestCase(3f, 14f, TestName = "Eco de Carcosa, conjunto de Set")]
        [TestCase(6f, 19f, TestName = "Olhar do Rei, Set e Coroa de Ossos")]
        public void DrenoContinuo_NaoMudaComOTamanhoDoQuadro(float taxaPorSegundo, float defesa)
        {
            float emUmQuadro = DrenadoEmUmSegundo(1, taxaPorSegundo, defesa, comoGolpe: false);
            float emSessenta = DrenadoEmUmSegundo(60, taxaPorSegundo, defesa, comoGolpe: false);

            Assert.AreEqual(emUmQuadro, emSessenta, Tolerancia,
                $"60 quadros de 1/60 s drenaram {emSessenta:0.000} e 1 quadro de 1 s drenou " +
                $"{emUmQuadro:0.000}. O dreno por segundo virou função do quadro.");

            foreach (int quadros in new[] { 1, 30, 60, 144 })
            {
                float drenado = DrenadoEmUmSegundo(quadros, taxaPorSegundo, defesa, comoGolpe: false);
                Assert.AreEqual(taxaPorSegundo, drenado, Tolerancia,
                    $"A {quadros} quadros/s, com Defesa Anômala {defesa}, o dreno de " +
                    $"{taxaPorSegundo} RM/s entregou {drenado:0.000}. Desgaste contínuo não passa " +
                    "pela mitigação por golpe; quem mexe nele é a fonte, sobre a taxa.");
            }
        }

        /// <summary>
        /// Prova que a defesa injetada chega de fato à mitigação — e que o golpe discreto (o cone
        /// do Byakhee, o Cone de Gelo) continua sendo mitigado, como deve.
        /// </summary>
        [Test]
        public void ComDefesaAnomala_OGolpeDiscretoContinuaMitigado()
        {
            var resiliencia = Ligar(DefesaDoConjuntoInicial);

            _mente.SofrerTrauma(20f);

            Assert.AreEqual(Maximo - (20f - DefesaDoConjuntoInicial), resiliencia.Atual, Tolerancia,
                "Um golpe de 20 com Defesa Anômala 3 deveria passar 17. Se passou 20, a defesa " +
                "injetada não chegou à mitigação — e o teste do dreno contínuo acima está vazio.");
        }

        /// <summary>
        /// O defeito, documentado: o mesmo segundo de dreno, cobrado por <c>SofrerTrauma</c>, dá
        /// um número em 1 quadro e outro em 60. É por isso que fonte contínua não pode usá-lo.
        /// </summary>
        [Test]
        public void SofrerTrauma_EmFatiasDeQuadro_EsmagaODreno()
        {
            const float taxa = 5f;

            float emUmQuadro = DrenadoEmUmSegundo(1, taxa, DefesaDoConjuntoInicial, comoGolpe: true);
            float emSessenta = DrenadoEmUmSegundo(60, taxa, DefesaDoConjuntoInicial, comoGolpe: true);

            Assert.AreEqual(taxa - DefesaDoConjuntoInicial, emUmQuadro, Tolerancia);
            Assert.AreEqual(taxa * MitigacaoDeDano.PisoFracaoPadrao, emSessenta, Tolerancia,
                "Em fatias, cada uma cai no piso: 15% da taxa, qualquer que seja a defesa.");
        }

        // ── As fontes ────────────────────────────────────────────────────────

        /// <summary>
        /// Cada fonte de desgaste contínuo chama <c>SofrerDrenoContinuo</c> no método que cobra a
        /// fatia do quadro. Decisão por fonte, com a justificativa no próprio código.
        /// </summary>
        [TestCase("ByakheeAI.cs", "AplicarGritoInfrassonico")]
        [TestCase("PressaoPsiquicaZone.cs", "AplicarDreno")]
        [TestCase("EcoDeCarcosa.cs", "DrenarResiliencia")]
        [TestCase("GerenciadorEfeitosPassivos.cs", "Update")]
        [TestCase("ReiEmAmareloAI.cs", "AplicarNaMente")]
        public void FonteContinua_DrenaPorSofrerDrenoContinuo(string arquivo, string metodo)
        {
            string corpo = SemComentarios(CorpoDoMetodo(Ler(arquivo), metodo));

            StringAssert.Contains("SofrerDrenoContinuo(", corpo,
                $"{arquivo}.{metodo} cobra desgaste por quadro e não chama SofrerDrenoContinuo.");
            StringAssert.DoesNotContain("SofrerTrauma(", corpo,
                $"{arquivo}.{metodo} ainda passa a fatia do quadro por SofrerTrauma, que a mitiga " +
                "como um golpe inteiro.");
        }

        /// <summary>
        /// A rede para a próxima fonte: ninguém em produção passa uma fatia de quadro
        /// (<c>Time.deltaTime</c>) direto para <c>SofrerTrauma</c>.
        /// </summary>
        [Test]
        public void NenhumaFonte_PassaFatiaDeQuadroPorSofrerTrauma()
        {
            var padrao = new Regex(@"SofrerTrauma\s*\([^;]*Time\s*\.\s*(deltaTime|fixedDeltaTime)");
            var infratores = new List<string>();

            foreach (var caminho in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                if (padrao.IsMatch(SemComentarios(File.ReadAllText(caminho))))
                    infratores.Add(Path.GetFileName(caminho));

            Assert.IsEmpty(infratores,
                "Fatia de quadro passando por SofrerTrauma (mitigada como golpe):\n  " +
                string.Join("\n  ", infratores) + "\nUse ResilienciaBridge.SofrerDrenoContinuo.");
        }

        // ── Apoio ────────────────────────────────────────────────────────────

        private ResilienciaMental Ligar(float defesa)
        {
            var resiliencia = new ResilienciaMental(Maximo, 0f);
            _mente.Bind(resiliencia);
            _mente.DefinirDefesaAnomalaParaTeste(() => defesa);
            return resiliencia;
        }

        /// <summary>Um segundo de dreno, cobrado em <paramref name="quadros"/> fatias iguais.</summary>
        private float DrenadoEmUmSegundo(int quadros, float taxaPorSegundo, float defesa, bool comoGolpe)
        {
            var resiliencia = Ligar(defesa);
            float fatia = taxaPorSegundo / quadros;

            for (int i = 0; i < quadros; i++)
            {
                if (comoGolpe) _mente.SofrerTrauma(fatia);
                else _mente.SofrerDrenoContinuo(fatia);
            }

            return Maximo - resiliencia.Atual;
        }

        private static string Ler(string arquivo)
        {
            var achados = Directory.GetFiles("Assets/Scripts", arquivo, SearchOption.AllDirectories);
            Assert.AreEqual(1, achados.Length, $"Esperava exatamente um {arquivo} em Assets/Scripts.");
            return File.ReadAllText(achados[0]);
        }

        /// <summary>Do <c>void Metodo(</c> até fechar as chaves, contando aninhamento.</summary>
        private static string CorpoDoMetodo(string fonte, string metodo)
        {
            var assinatura = Regex.Match(fonte, $@"void\s+{metodo}\s*\(");
            Assert.IsTrue(assinatura.Success, $"Método '{metodo}' não encontrado.");

            int abre = fonte.IndexOf('{', assinatura.Index);
            int nivel = 0;
            for (int i = abre; i < fonte.Length; i++)
            {
                if (fonte[i] == '{') nivel++;
                else if (fonte[i] == '}' && --nivel == 0)
                    return fonte.Substring(assinatura.Index, i - assinatura.Index + 1);
            }

            return fonte.Substring(assinatura.Index);
        }

        /// <summary>
        /// Tira as linhas de comentário — senão a doc que explica o defeito antigo reprova o
        /// código já corrigido (lição de <c>BarraDeItensInjetadaTests</c>, 2026-08-18).
        /// </summary>
        private static string SemComentarios(string fonte)
        {
            var sb = new System.Text.StringBuilder(fonte.Length);
            foreach (var linha in fonte.Split('\n'))
            {
                string t = linha.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                sb.Append(linha).Append('\n');
            }
            return sb.ToString();
        }
    }
}
