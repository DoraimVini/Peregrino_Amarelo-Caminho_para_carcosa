using FavelaAmarela.Core.Combat;
using NUnit.Framework;

namespace FavelaAmarela.Tests.EditMode
{
    /// <summary>
    /// A matemática da Resistência Anômala contra drenos (2026-10-15): a parte que passa é
    /// <c>1 − R/(R + 20)</c>, com a redução limitada a 75%.
    ///
    /// <para>Os pontos da tabela são os que o jogo tem: R 3 é o conjunto inicial (Capuz, Colete,
    /// Caneleiras); R 9, ele mais um Couraçado no nível 3; R 19, o Set mais a Coroa de Ossos.</para>
    /// </summary>
    public sealed class ResistenciaAnomalaTests
    {
        [TestCase(0f, 0f)]
        [TestCase(3f, 3f / 23f)]
        [TestCase(9f, 9f / 29f)]
        [TestCase(19f, 19f / 39f)]
        [TestCase(20f, 0.5f)]
        [TestCase(60f, 0.75f)]
        [TestCase(500f, 0.75f)]
        public void Reducao_SegueATabela(float resistencia, float esperado)
            => Assert.AreEqual(esperado, ResistenciaAnomala.ReducaoDeDreno(resistencia), 1e-4f);

        [Test]
        public void MeiaEficacia_CortaODrenoPelaMetade()
            => Assert.AreEqual(1f, ResistenciaAnomala.DrenoQuePassa(2f, ResistenciaAnomala.MeiaEficacia), 1e-4f);

        [Test]
        public void CadaPontoValeMenosQueOAnterior()
        {
            float anterior = ResistenciaAnomala.ReducaoDeDreno(1f) - ResistenciaAnomala.ReducaoDeDreno(0f);
            for (int r = 1; r < 50; r++)
            {
                float ganho = ResistenciaAnomala.ReducaoDeDreno(r + 1) - ResistenciaAnomala.ReducaoDeDreno(r);
                Assert.LessOrEqual(ganho, anterior + 1e-6f, $"O ponto {r + 1} rendeu mais que o {r}.");
                anterior = ganho;
            }
        }

        [Test]
        public void NuncaZeraODreno()
            => Assert.Greater(ResistenciaAnomala.DrenoQuePassa(5f, 10000f), 0f,
                "Com o teto de 75%, a mente nunca deixa de ser ameaça.");

        [Test]
        public void ResistenciaNegativaOuDrenoZero_NaoFazemNada()
        {
            Assert.AreEqual(0f, ResistenciaAnomala.ReducaoDeDreno(-5f));
            Assert.AreEqual(0f, ResistenciaAnomala.DrenoQuePassa(0f, 10f));
        }
    }
}
