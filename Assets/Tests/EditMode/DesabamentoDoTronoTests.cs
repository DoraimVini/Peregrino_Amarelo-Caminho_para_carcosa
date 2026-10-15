using System.Collections.Generic;
using FavelaAmarela.Core.Enemies;
using NUnit.Framework;

namespace FavelaAmarela.Tests.EditMode
{
    /// <summary>
    /// O cronograma do fim trágico (2026-10-15): selado o Rei, o Castelo desaba sobre o Damião.
    /// Guarda a forma da sequência — o silêncio antes, a escalada, o controle até o fim e o
    /// golpe final uma vez só, na hora certa.
    /// </summary>
    public sealed class DesabamentoDoTronoTests
    {
        private const float Dt = 1f / 60f;

        private static (DesabamentoDoTrono rito, List<QuadroDoDesabamento> quadros) Rodar(float segundos)
        {
            var rito = new DesabamentoDoTrono();
            rito.Comecar();
            var quadros = new List<QuadroDoDesabamento>();
            for (float t = 0f; t < segundos; t += Dt) quadros.Add(rito.Avancar(Dt));
            return (rito, quadros);
        }

        [Test]
        public void AntesDeComecar_NadaAcontece()
        {
            var rito = new DesabamentoDoTrono();
            var q = rito.Avancar(1f);
            Assert.AreEqual(FaseDoDesabamento.Parado, rito.Fase);
            Assert.AreEqual(0, q.Pedras);
        }

        [Test]
        public void OSilencio_NaoTemPedraNemTremor()
        {
            var p = new ParametrosDoDesabamento();
            var (_, quadros) = Rodar(p.Silencio - 0.1f);
            foreach (var q in quadros)
            {
                Assert.AreEqual(0, q.Pedras, "Uma pedra no silêncio estraga o fôlego de falsa vitória.");
                Assert.AreEqual(0f, q.Tremor);
            }
        }

        [Test]
        public void AsFases_VemNaOrdem_EOGolpeFinalCaiUmaVez_NoFim()
        {
            var rito = new DesabamentoDoTrono();
            var fases = new List<FaseDoDesabamento>();
            rito.OnFaseMudou += (_, nova) => fases.Add(nova);
            rito.Comecar();

            int golpes = 0;
            float instante = -1f;
            for (float t = 0f; t < rito.Parametros.Total + 2f; t += Dt)
            {
                if (rito.Avancar(Dt).GolpeFinal) { golpes++; instante = rito.Tempo; }
            }

            CollectionAssert.AreEqual(new[]
            {
                FaseDoDesabamento.Silencio, FaseDoDesabamento.Tremor, FaseDoDesabamento.Desabamento,
                FaseDoDesabamento.Soterramento, FaseDoDesabamento.Soterrado,
            }, fases);
            Assert.AreEqual(1, golpes, "O arco cai uma vez só.");
            Assert.AreEqual(rito.Parametros.Total, instante, 2f * Dt);
        }

        [Test]
        public void ODamiaoTemControle_AteOSoterramento()
        {
            var rito = new DesabamentoDoTrono();
            rito.Comecar();
            var p = rito.Parametros;
            float fimDoControle = p.Silencio + p.Tremor + p.Desabamento;

            for (float t = 0f; t < p.Total; t += Dt)
            {
                rito.Avancar(Dt);
                if (rito.Tempo < fimDoControle - Dt) Assert.IsTrue(rito.DamiaoNoComando, $"Sem controle em {rito.Tempo:0.00} s.");
                if (rito.Tempo > fimDoControle + Dt) Assert.IsFalse(rito.DamiaoNoComando, $"Com controle em {rito.Tempo:0.00} s.");
            }
        }

        [Test]
        public void ODesabamento_Escala_MaisPedrasEMaisPerto()
        {
            var p = new ParametrosDoDesabamento();
            var (_, quadros) = Rodar(p.Total - 0.05f);

            int Contar(float de, float ate)
            {
                int n = 0;
                for (int i = 0; i < quadros.Count; i++)
                {
                    float t = (i + 1) * Dt;
                    if (t >= de && t < ate) n += quadros[i].Pedras;
                }
                return n;
            }

            float inicio = p.Silencio;
            int primeiroSegundo = Contar(inicio, inicio + 1f);
            int ultimoSegundo = Contar(p.Total - 1.1f, p.Total - 0.1f);
            Assert.Greater(ultimoSegundo, primeiroSegundo * 3, $"Pedras: {primeiroSegundo}/s no começo, {ultimoSegundo}/s no fim.");

            int total = Contar(0f, p.Total);
            Assert.That(total, Is.InRange(25, 60), "O conjunto de pedras do adaptador (64) precisa dar conta.");

            float raioNoComeco = quadros[(int)((inicio + 0.5f) / Dt)].RaioDaMira;
            float raioNoFim = quadros[quadros.Count - 2].RaioDaMira;
            Assert.Greater(raioNoComeco, raioNoFim * 3f, "As pedras têm de ir fechando sobre o Damião.");
        }

        [Test]
        public void UmQuadroLongo_NaoPulaFaseNemOGolpe()
        {
            var rito = new DesabamentoDoTrono();
            var fases = new List<FaseDoDesabamento>();
            rito.OnFaseMudou += (_, nova) => fases.Add(nova);
            rito.Comecar();

            var q = rito.Avancar(rito.Parametros.Total + 1f);

            Assert.IsTrue(q.GolpeFinal, "Um engasgo de quadro não pode engolir o golpe final.");
            Assert.AreEqual(5, fases.Count, "Cada transição dispara, mesmo num quadro só.");
        }
    }
}
