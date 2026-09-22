namespace BBSB.Core
{
    // Relative integer heads retain the activation's beat side: Light favors downbeats,
    // Dark favors offbeats. Half-beat accents never outnumber the main pulse.
    internal static class LongWeaponPatterns
    {
        private static WeaponPhraseNote T(double at, decimal damage, int lane = 0) =>
            new WeaponPhraseNote(at, damage, laneOffset: lane);
        private static WeaponPhraseNote H(double at, double length, decimal damage, int lane = 0) =>
            new WeaponPhraseNote(at, damage, length, laneOffset: lane);
        private static WeaponPhraseNote E(double at, decimal amount, PhraseEffect effect, int lane = 0,
            double hold = 0, double duration = 2) =>
            new WeaponPhraseNote(at, amount, hold, effect, laneOffset: lane, effectDurationBeats: duration);
        private static WeaponPhraseNote Shot(double at, decimal damage, int draw) =>
            new WeaponPhraseNote(at, damage, prerequisite: draw, condition: PhraseNoteCondition.Hit);
        private static WeaponPhrase P(string id, string name, string hint, double length, double cooldown,
            params WeaponPhraseNote[] notes) => new WeaponPhrase(id, name,
                hint + " · " + length + "박 연주 / 쿨타임 " + cooldown + "박", length, notes,
                missCooldownBeats: cooldown, completionCooldownBeats: cooldown);

        internal static WeaponPhrase Pattern(string id, int offset, WeaponBeatSide side, bool transition = false,
            WeaponAttribute? attribute = null)
        {
            if (transition) return Bridge(id);
            var phrase = Base(id, offset, side);
            if (phrase == null || attribute != WeaponAttribute.Chaos || !WeaponAttributes.SupportsChaos(id)) return phrase;
            return new WeaponPhrase(id, phrase.Name, phrase.Hint + (WeaponAttributes.UsesChaosTransitions(id) ? " · 혼돈: 무한 반복·박자 전환" : " · 혼돈: 무작위 박자 두 구간"), phrase.LengthBeats,
                phrase.Notes, phrase.MissCooldownBeats, repeat: true,
                finisherEvery: phrase.FinisherEvery, finisherDamage: phrase.FinisherDamage, groggyBeats: phrase.GroggyBeats,
                completionCooldownBeats: phrase.CompletionCooldownBeats,
                maximumCycles: WeaponAttributes.UsesChaosTransitions(id) ? 0 : 2);
        }

        private static WeaponPhrase Base(string id, int offset, WeaponBeatSide side)
        {
            bool light = side == WeaponBeatSide.Light;
            switch (id)
            {
                case "sword": return P(id, "검", "연속 베기 → 쉼 → 마무리", 8, 6,
                    T(0, 8), T(1, 8), T(2.5, 10), T(3, 10), T(4, 12), T(6, 12), T(7, 18));
                case "hammer": return new WeaponPhrase(id, "트레실로 해머", "트레실로 세 묶음 → 강타·그로기 · 12박 / 쿨타임 8박", 12,
                    new[] { T(0, 6), T(1.5, 8), T(3, 12), T(4, 6), T(5.5, 8), T(7, 12), T(8, 6), T(9.5, 8), T(11, 12) },
                    8, finisherEvery: 1, finisherDamage: 38, groggyBeats: 4, completionCooldownBeats: 8);
                case "bow": return P(id, "활", "당기고 발사 두 번 → 길게 당겨 강사격", 10, 8,
                    H(0, 1, 0), Shot(2, 24, 0), H(3, 1, 0), Shot(5, 28, 2), H(6, 2, 0), Shot(9, 44, 4));
                // These starter pulse weapons teach the main beat. Keep their single
                // taps and unlimited cadence when expanding the other weapons.
                case "dagger": return new WeaponPhrase(id, "단검", "2박마다 Tap 1회 · 성공하면 무한 반복 · 미스 대기 2박", 2,
                    new[] { T(0, 6) }, repeat: true);
                case "dual-swords": return new WeaponPhrase(id, "쌍검", "매 박자 Tap 1회 · 성공하면 무한 반복 · 미스 대기 2박", 1,
                    new[] { T(0, 6) }, repeat: true);
                case "staff": return P(id, "봉", "두 번 두드리기 → 반대쪽 Hold → 되돌려 마무리 · 2라인", 8, 8,
                    T(0, 8), T(.5, 8), H(1, 1, 18, 1), T(3, 10, 1), T(4, 10), H(5, 1, 22, 1), T(7, 16));
                case "spirit-bell": return P(id, "신령 방울", "양옆 호출 → 쿨타임 감소 → 다시 호출", 12, 10,
                    E(0, 0, PhraseEffect.StartAdjacent), T(3, 6), E(4, 2, PhraseEffect.ReduceAdjacentCooldown), T(6, 6),
                    E(8, 2, PhraseEffect.ReduceAdjacentCooldown), E(10, 0, PhraseEffect.StartAdjacent), T(11, 12));
                case "spear": return P(id, "창", "간격 찌르기 → 모아 찌르기 → 두 번 관통", 10, 8,
                    T(0, 12), T(2, 14), T(3.5, 8), T(4, 16), H(6, 1, 22), T(8, 24), T(9, 30));
                case "greatsword": return P(id, "대검", "긴 모아베기 두 번 → 마지막 Hold 강타", 12, 10,
                    H(0, 2, 24), T(3, 14), T(4, 18), H(6, 2, 36), T(9, 20), H(10, 1, 50));
                case "bell": return P(id, "종", "울리기와 유지 두 묶음 → 마지막 공명", 8, 8,
                    T(0, 8), H(1, 1, 16), T(3, 10), T(4.5, 8), H(5, 1, 20), T(7, 24));
                case "blade": return P(id, "쌍날검", "반박 회전 두 묶음 → 마무리 베기", 8, 8,
                    T(0, 6), T(1, 6), T(1.5, 8), T(2, 10), T(4, 6), T(5, 8), T(5.5, 10), T(6, 12), T(7, 18));
                case "crossbow": return P(id, "석궁", "짧게 장전 후 세 발 → 길게 장전 후 두 발", 8, 8,
                    H(0, .5, 0), Shot(1, 18, 0), Shot(2, 18, 0), Shot(3, 22, 0), H(4, 1, 0), Shot(6, 26, 4), Shot(7, 32, 4));
                case "wand": return P(id, "마도봉", "충전과 연사 두 묶음 → 마력 폭발", 12, 10,
                    H(0, 2, 10), T(3, 16), T(4, 18), T(5.5, 10), H(6, 2, 16), T(9, 28), T(10, 30), T(11, 38));
                case "rapier": return P(id, "레이피어", "재빠른 이중 찌르기 두 번 → 깊게 찌르기", 8, 6,
                    T(0, 6), T(.5, 4), T(1, 8), T(2, 10), T(3, 14), T(4, 6), T(4.5, 4), T(5, 8), T(6, 12), T(7, 24));
                case "battle-axe": return P(id, "전투 도끼", "양쪽 번갈아 찍기 → 긴 Hold 내려찍기 · 2라인", 10, 8,
                    T(0, 10), H(1, 1, 26, 1), T(3, 12), T(4, 16, 1), H(5, 2, 42), T(8, 18, 1), T(9, 28));
                case "chain-sickle": return P(id, "사슬낫", "반대쪽 던지기 → Hold 당기기 두 번 · 2라인", 10, 8,
                    T(0, 8), T(.5, 10, 1), H(1, 1, 18), T(3, 12, 1), T(4, 12), T(5.5, 10, 1), H(6, 2, 32), T(9, 30, 1));
                case "war-fan": return P(id, "철선", "펼치고 접는 반박 베기 두 묶음", 8, 6,
                    T(0, 6), T(1, 8), T(1.5, 6), T(2, 10), T(3, 12), T(4, 6), T(5, 10), T(5.5, 8), T(6, 14), T(7, 22));
                case "halberd":
                    int next = offset == 0 ? 1 : 2, end = offset == 0 ? 2 : 1;
                    return P(id, "미늘창", "세 라인 횡단 → 되돌려 모아치기 → 끝에서 강타", 12, 10,
                        T(0, 10), T(1, 14, next), H(2, 1, 26, end), T(4, 12, end), T(5, 16, next), H(6, 2, 36), T(9, 18, next), H(10, 1, 42, end));
                case "war-drum": return P(id, "전쟁 북", "세 라인 행진 · 두 번의 양옆 무기 호출", 8, 8,
                    T(0, 6), T(1, 6, 1), E(2, 0, PhraseEffect.StartAdjacent, 2), T(3, 8, 1), T(4, 8), T(5, 10, 1), E(6, 0, PhraseEffect.StartAdjacent, 2), T(7, 16));
                case "tuning-fork": return P(id, "소리쇠", "번갈아 공명 → 긴 울림 · 양옆 쿨타임 감소", 8, 8,
                    T(0, 6), E(1, 2, PhraseEffect.ReduceAdjacentCooldown, 1), T(2, 8), T(3.5, 8, 1), E(4, 2, PhraseEffect.ReduceAdjacentCooldown), T(5, 10, 1), E(6, 3, PhraseEffect.ReduceAdjacentCooldown, hold: 1));
                case "war-banner": return P(id, "군기", "깃발 유지 → 양옆 강화 두 번 · 6박 안의 다음 타격", 10, 10,
                    H(0, 2, 0), E(3, .5m, PhraseEffect.EmpowerAdjacent, 1, duration: 6), T(4, 8), T(5, 8, 1), H(6, 1, 0), E(8, .75m, PhraseEffect.EmpowerAdjacent, 1, duration: 6), T(9, 16));
                case "censer": return P(id, "향로", "긴 Hold 회복 세 번 · 사이마다 향불 타격", 12, 10,
                    E(0, 3, PhraseEffect.Heal, hold: 2), T(3, 4), E(4, 4, PhraseEffect.Heal, hold: 2), T(7, 6), E(8, 5, PhraseEffect.Heal, hold: 2), T(11, 8));
                case "chakram": return P(id, "차크람", "두 라인 왕복 · 돌아오는 순간 반박 가속", 8, 8,
                    T(0, 8), T(1, 8, 1), T(2, 10), T(2.5, 8, 1), T(3, 12), T(4, 8, 1), T(5, 10), T(6, 12, 1), T(6.5, 10), T(7, 22, 1));
                case "twilight-staff":
                    var effect = light ? PhraseEffect.Heal : PhraseEffect.Strike;
                    return P(id, "양면 지팡이", light ? "양쪽으로 회복을 이어가는 긴 의식" : "양쪽 마력 타격 → 긴 충전 → 폭발", 10, 8,
                        E(0, light ? 1 : 8, effect), E(1, light ? 2 : 18, effect, 1, 1), E(3, light ? 1 : 10, effect), E(4.5, light ? 1 : 8, effect, 1),
                        E(5, light ? 3 : 30, effect, hold: 2), E(8, light ? 2 : 18, effect, 1), E(9, light ? 3 : 28, effect));
                case "eclipse-mirror":
                    var pulse = light ? PhraseEffect.Heal : PhraseEffect.Strike;
                    var support = light ? PhraseEffect.ReduceAdjacentCooldown : PhraseEffect.EmpowerAdjacent;
                    return P(id, "일식 거울", light ? "두 번의 쿨타임 감소 · 지속 회복" : "두 번의 공격 강화 · 연속 마력 타격", 8, 8,
                        E(0, light ? 2 : .5m, support, duration: 4), E(1, light ? 1 : 8, pulse), E(2, light ? 2 : 10, pulse), E(3.5, light ? 1 : 6, pulse),
                        E(4, light ? 2 : .75m, support, duration: 4), E(5, light ? 2 : 12, pulse), E(6, light ? 2 : 14, pulse), E(7, light ? 3 : 20, pulse));
                case "chaos-pendulum": return new WeaponPhrase(id, "혼돈의 진자", "세 라인 왕복 · 8박 무한 반복·박자 전환 · 미스 대기 8박", 8,
                    new[] { T(0, 8), T(1, 8, 1), T(2, 12, 2), T(2.5, 8, 1), T(3, 10), T(4, 10, 1), T(5, 14, 2), T(6, 10, 1), T(7, 20) },
                    8, repeat: true, completionCooldownBeats: 8);
                default: return null; // Shields deliberately keep their short reactive patterns.
            }
        }

        private static WeaponPhrase Bridge(string id)
        {
            WeaponPhraseNote[] notes;
            switch (id)
            {
                case "dagger": notes = new[] { T(0, 8), T(1, 8), T(2.5, 10), T(4, 14) }; break;
                case "dual-swords": notes = new[] { T(0, 8), T(.5, 8), T(1, 8), T(2, 8), T(2.5, 8), T(3, 8), T(4, 16) }; break;
                case "chaos-pendulum": notes = new[] { T(0, 7), T(.5, 7, 1), T(1, 7, 2), T(2, 7, 1), T(2.5, 7), T(3, 7, 1), T(4, 18, 2) }; break;
                default: return null;
            }
            var source = Base(id, 0, WeaponBeatSide.Light);
            return new WeaponPhrase(id, source.Name, "반박 연결 → 반대 박자로 마지막 구간", 4.5, notes,
                source.MissCooldownBeats, repeat: true, completionCooldownBeats: source.CompletionCooldownBeats);
        }
    }
}
