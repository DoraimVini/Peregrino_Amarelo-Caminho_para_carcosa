using System.Collections;
using System.Reflection;
using FavelaAmarela.Core.Enemies;
using FavelaAmarela.Runtime.Enemies;
using FavelaAmarela.Runtime.GameLoop;
using FavelaAmarela.Runtime.Persistencia;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FavelaAmarela.Tests.PlayMode
{
    /// <summary>
    /// O fim trágico na cena real do Castelo (2026-10-15): selado o Rei, o Castelo desaba sobre o
    /// Damião — as pedras caem, o controle trava no fim, e a tela corta seco para o preto com a
    /// frase final. O tempo corre acelerado; a volta ao Menu é desligada para o teste ficar na cena.
    /// </summary>
    public sealed class OFimTragicoTests
    {
        private const string Cena = "Castelo_Carcosa";
        private float _escala;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            _escala = Time.timeScale;
            GerenciadorDeSave.Instancia?.LimparRegistro();
            FavelaAmarela.Runtime.UI.HUDController.GarantirInstancia();
            yield return SceneManager.LoadSceneAsync(Cena, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Desmontar()
        {
            Time.timeScale = _escala;
            GerenciadorDeSave.Instancia?.LimparRegistro();

            var castelo = SceneManager.GetSceneByName(Cena);
            if (!castelo.IsValid() || !castelo.isLoaded) yield break;

            var vazia = SceneManager.CreateScene("Vazia_DepoisDoFimTragico");
            SceneManager.SetActiveScene(vazia);
            yield return SceneManager.UnloadSceneAsync(castelo);
            foreach (var go in GameObject.FindGameObjectsWithTag("Player")) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelarORei_DerrubaOCastelo_ECortaParaOFim()
        {
            var rei = Object.FindAnyObjectByType<ReiEmAmareloAI>();
            var desfecho = Object.FindAnyObjectByType<SequenciaDeSelamento>();
            var desabamento = Object.FindAnyObjectByType<DesabamentoDoCastelo>();
            Assert.NotNull(rei, "Sem Rei no Castelo.");
            Assert.NotNull(desfecho, "Sem SequenciaDeSelamento no Castelo.");
            Assert.NotNull(desabamento, "Sem DesabamentoDoCastelo — rode 'Trono: montar o Rito do Olhar'.");

            typeof(SequenciaDeSelamento).GetField("voltarAoMenu", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(desfecho, false);

            var jogador = GameObject.FindGameObjectWithTag("Player");
            var rb = jogador.GetComponent<Rigidbody2D>();
            Vector2 noAltar = rei.Altar.transform.position;
            if (rb != null) rb.position = noAltar;
            jogador.transform.position = noAltar;
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.NotNull(rei.Rito, "Entrar no Trono não começou o rito.");
            rei.Rito.PularPara(FaseDoRito.Selado);
            yield return null;

            Assert.IsTrue(desfecho.Tocado, "O selo fechou e o desfecho não tocou.");

            // Medido em Play: com a mente baixa, o Colapso aparecia por cima do fim.
            var mente = jogador.GetComponentInChildren<FavelaAmarela.Runtime.Combat.ResilienciaBridge>();
            Assert.IsTrue(mente == null || mente.IgnorarTrauma, "No fim o Damião ainda pode morrer de Colapso.");
            Assert.NotNull(desabamento.Cronograma, "O selo fechou e o Castelo não começou a desabar.");
            Assert.IsFalse(desfecho.PainelCobrindo, "A tela escureceu antes do desabamento.");

            Time.timeScale = 4f;
            bool travouNoFim = false;
            float limite = Time.realtimeSinceStartup + 10f;
            while (!desfecho.PainelCobrindo && Time.realtimeSinceStartup < limite)
            {
                if (desabamento.Cronograma.Fase == FaseDoDesabamento.Soterramento)
                    travouNoFim |= !FavelaAmarela.Runtime.Entrada.ArbitroDeFoco.JogoNoComando;
                yield return null;
            }
            Time.timeScale = _escala;

            Assert.IsTrue(desabamento.Soterrado, "O arco não caiu sobre o Damião.");
            Assert.IsTrue(desfecho.PainelCobrindo, "O arco caiu e a tela não cortou para o preto.");
            Assert.IsTrue(travouNoFim, "No soterramento o Damião ainda tinha controle.");

            var painel = (CanvasGroup)typeof(SequenciaDeSelamento)
                .GetField("painel", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(desfecho);
            var canvas = painel != null ? painel.GetComponentInParent<Canvas>() : null;
            Assert.IsTrue(canvas != null && canvas.sortingOrder > 100,
                "O preto do fim fica por baixo do HUD (ordem 100): as barras aparecem no luto.");

            int pedrasNoChao = 0;
            foreach (Transform filho in desabamento.transform)
                if (filho.gameObject.activeSelf && filho.name.StartsWith("Pedra_")) pedrasNoChao++;
            Assert.Greater(pedrasNoChao, 15, "Quase nenhuma pedra caiu — o Castelo 'desabou' sem desabar.");

            yield return new WaitForSecondsRealtime(2f);
            StringAssert.Contains("desabou", desfecho.TextoNaTela, "A frase final não apareceu depois do silêncio.");
        }
    }
}
