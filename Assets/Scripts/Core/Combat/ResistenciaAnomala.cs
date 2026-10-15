using System;

namespace FavelaAmarela.Core.Combat
{
    /// <summary>
    /// Quanto a <b>Resistência Anômala</b> protege a mente de um <b>dreno contínuo</b> — o grito do
    /// Byakhee, a Pressão Psíquica, o Eco de Carcosa, o olhar do Rei.
    ///
    /// <para><b>Por que não a mesma regra do golpe (2026-10-15).</b> Golpe usa
    /// <see cref="MitigacaoDeDano"/>: <c>max(15% do golpe, golpe − R)</c>. Funciona quando o golpe é
    /// maior que R (o grito pontual de 20 com R 3 passa 17). Dreno não tem golpe, tem taxa: subtrair R
    /// de uma fatia de quadro — ou da taxa por segundo — zera tudo assim que R passa da taxa, e o
    /// dreno despenca ao piso. Foi o defeito real: com o conjunto inicial (R 3) todo dreno valia 15%.</para>
    ///
    /// <para><b>A regra:</b> a parte que passa é <c>1 − R / (R + K)</c>, com a redução limitada a
    /// <see cref="TetoDeReducao"/>. É retorno decrescente: cada ponto vale menos que o anterior, e
    /// <see cref="MeiaEficacia"/> é o R em que o dreno cai pela metade. Com K = 20: R 3 (conjunto
    /// inicial) reduz 13%; R 9 (+ Couraçado no nível 3), 31%; R 19 (Set + Coroa de Ossos), 49%.
    /// O teto garante que a mente nunca deixa de ser ameaça — o horror não zera.</para>
    ///
    /// <para><b>É multiplicativa</b>, então não depende do tamanho do quadro: 60 fatias de 1/60 s
    /// drenam o mesmo que um segundo inteiro.</para>
    /// </summary>
    public static class ResistenciaAnomala
    {
        /// <summary>A Resistência Anômala que corta um dreno pela metade.</summary>
        public const float MeiaEficacia = 20f;

        /// <summary>A maior redução possível de um dreno (75%).</summary>
        public const float TetoDeReducao = 0.75f;

        /// <summary>A fração do dreno que a resistência tira, em [0, <see cref="TetoDeReducao"/>].</summary>
        public static float ReducaoDeDreno(float resistencia,
                                           float meiaEficacia = MeiaEficacia,
                                           float teto = TetoDeReducao)
        {
            if (resistencia <= 0f || meiaEficacia <= 0f) return 0f;
            float r = resistencia / (resistencia + meiaEficacia);
            return Math.Min(Math.Max(teto, 0f), r);
        }

        /// <summary>Quanto de um dreno passa pela resistência.</summary>
        public static float DrenoQuePassa(float dreno, float resistencia)
            => dreno <= 0f ? 0f : dreno * (1f - ReducaoDeDreno(resistencia));
    }
}
