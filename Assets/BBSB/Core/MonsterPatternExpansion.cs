using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // Three short bases (including the original two) and two longer signature hooks per species.
    public static class MonsterPatternExpansion
    {
        public static string ArtPatternId(string monsterId, string patternId)
        {
            foreach (var monster in MonsterCatalog.BuiltIn)
                if (monster.Id == monsterId)
                    for (int i = 2; i < monster.Patterns.Count; i++)
                        if (monster.Patterns[i].Id == patternId) return monster.Patterns[0].Id;
            return patternId;
        }
        public static IReadOnlyList<MonsterDefinition> Build(IEnumerable<MonsterDefinition> originals)
        {
            var result = new List<MonsterDefinition>();
            foreach (var monster in originals)
            {
                var patterns = new List<MonsterPatternDefinition>(monster.Patterns);
                patterns.AddRange(For(monster.Id));
                result.Add(new MonsterDefinition(monster.Id, monster.Name, monster.Description, monster.MainGesture,
                    patterns, monster.EncounterWeight, monster.DamagePerNote, monster.ArtId, monster.PatternPlanner));
            }
            return result.AsReadOnly();
        }
        public static IReadOnlyList<MonsterPatternDefinition> For(string id)
        {
            switch (id)
            {
                case "tap-slime": return new[]
                {
                    P("jelly-sticky-bounce", "끈적한 되튐", "젤리 두 번 사이에 끈적한 압박을 끼워 넣는다.", 4, 16, false,
                        CallSound.Bell, CallMotion.Hop, new[] { T(0), H(4, 4), T(12) }),
                    P("jelly-tresillo-wave", "출렁이는 파도", "트레실로로 튀긴 젤리가 긴 파도로 합쳐지고 마지막 방울이 터진다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.Stomp, new[] { T(0), T(6), T(12), H(16, 8), T(28) }),
                    P("jelly-inflate-burst", "부풀어 팡팡", "크게 부풀어 네 박 동안 밀어낸 뒤 작은 방울을 연달아 터뜨린다.", 16, 32, true,
                        CallSound.FallingChime, CallMotion.TailSweep, new[] { H(0, 16), T(20), T(22), T(28) }),
                };
                case "march-slime": return new[]
                {
                    P("golem-braced-step", "버티는 한 걸음", "장갑 주먹 뒤 관절을 고정해 한 박 동안 민다.", 4, 12, false,
                        CallSound.Drum, CallMotion.Stomp, new[] { T(0), H(8, 4) }),
                    P("golem-parade", "장갑 행진곡", "정박의 주먹 행진 사이에 두 박짜리 양팔 밀기를 넣는다.", 16, 32, true,
                        CallSound.Bell, CallMotion.TailSweep, new[] { T(0), T(4), H(8, 8), T(20), T(24), T(28) }),
                    P("golem-double-press", "두 번의 관절 잠금", "묵직한 밀기와 주먹을 번갈아 보내 마지막 정박에 마무리한다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.Rise, new[] { H(0, 8), T(12), H(16, 8), T(28) }),
                };
                case "tresillo-bat": return new[]
                {
                    P("harpy-feather-draft", "깃과 바람", "깃털 두 발 뒤 한 박짜리 돌풍을 보낸다.", 4, 16, false,
                        CallSound.Wood, CallMotion.Hop, new[] { T(0), T(6), H(12, 4) }),
                    P("harpy-tresillo-gale", "세 갈래 폭풍", "트레실로 깃털이 모여 지속 돌풍을 만들고 마지막 깃이 날아든다.", 16, 32, true,
                        CallSound.Drum, CallMotion.Step, new[] { T(0), T(6), T(12), H(16, 8), T(28) }),
                    P("harpy-wing-refrain", "날갯짓 후렴", "두 번의 긴 날갯바람 사이에 간격이 다른 깃털을 흩뿌린다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.Stomp, new[] { H(0, 8), T(10), T(16), H(22, 8) }),
                };
                case "offbeat-goblin": return new[]
                {
                    P("foxfire-lingering", "남아 있는 불씨", "엇박의 불씨가 잠시 머무른 뒤 꼬리불이 뒤따른다.", 6, 16, false,
                        CallSound.Drum, CallMotion.Stomp, new[] { T(0), H(4, 4), T(10) }),
                    P("foxfire-procession", "여우불 행렬", "엇박 불씨 세 발 뒤 긴 불길을 끌고 마지막 꼬리를 튕긴다.", 10, 32, true,
                        CallSound.RisingChime, CallMotion.TailSweep, new[] { T(0), T(4), T(10), H(14, 8), T(26), T(30) }),
                    P("foxfire-coiling", "꼬리를 감는 불길", "두 차례의 지속 불길 사이에서 짧은 불씨가 박자를 비튼다.", 10, 32, true,
                        CallSound.FallingChime, CallMotion.Rise, new[] { H(0, 8), T(10), T(14), H(18, 8), T(30) }),
                };
                case "drowsy-slime": return new[]
                {
                    P("dream-yawn-pop", "하품 끝의 방울", "짧은 졸음 장막 뒤 꿈방울 하나가 튀어나온다.", 4, 12, false,
                        CallSound.Drum, CallMotion.Step, new[] { H(0, 4), T(8) }),
                    P("dream-long-exhale", "긴 꿈의 숨결", "네 박짜리 졸음 숨결 뒤 꿈방울 세 개가 잠을 깨운다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Stomp, new[] { H(0, 16), T(20), T(22), T(28) }),
                    P("dream-startle-lullaby", "깜짝 자장가", "느린 두 방울 뒤 긴 낮잠에 잠겼다가 마지막 반 박에 깨어난다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.TailSweep, new[] { T(0), T(8), H(12, 16), T(30) }),
                };
                case "clock-spirit": return new[]
                {
                    P("clock-wind-tick", "째깍 태엽 감기", "정박 두 번 뒤 태엽을 한 박 동안 감는다.", 4, 12, false,
                        CallSound.Drum, CallMotion.Hop, new[] { T(0), T(4), H(8, 4) }),
                    P("clock-parade-refrain", "인형 행진 후렴", "세 걸음 뒤 긴 태엽 압박을 넣고 두 인형이 다시 행진한다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.Stomp, new[] { T(0), T(4), T(8), H(12, 8), T(24), T(28) }),
                    P("clock-double-winding", "두 번 감는 태엽", "짧게 감기와 길게 감기를 번갈아 사용한다.", 16, 32, true,
                        CallSound.FallingChime, CallMotion.TailSweep, new[] { T(0), H(4, 4), T(12), H(16, 8), T(28) }),
                };
                case "seesaw-goblin": return new[]
                {
                    P("neko-tail-embrace", "붙잡는 두 꼬리", "첫 꼬리 반 박 뒤 다른 꼬리가 한 박 동안 감싼다.", 4, 12, false,
                        CallSound.Drum, CallMotion.Hop, new[] { T(0), H(2, 4), T(8) }),
                    P("neko-crossed-ribbon", "엇갈린 꼬리춤", "반 박 꼬리와 긴 감싸기를 번갈아 정박과 엇박으로 보낸다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Stomp, new[] { T(0), T(2), H(4, 8), T(14), T(18), H(20, 8), T(30) }),
                    P("neko-rush-and-wrap", "먼저 달려 감싸기", "정박 달리기에서 반 박을 당긴 뒤 긴 감싸기와 엇박 꼬리로 마친다.", 16, 32, true,
                        CallSound.FallingChime, CallMotion.TailSweep, new[] { T(0), T(4), T(8), T(10), H(12, 8), T(22), T(26), T(30) }),
                };
                case "spark-bat": return new[]
                {
                    P("thunder-spark-stream", "불꽃과 방전", "짧은 불꽃 사이에 한 박짜리 방전을 끼워 넣는다.", 4, 12, false,
                        CallSound.Wood, CallMotion.Hop, new[] { T(0), H(2, 4), T(8) }),
                    P("thunder-capacitor", "축전 폭주", "두 불꽃 뒤 네 박 동안 방전하고 잔류 전기를 세 번 튀긴다.", 16, 32, true,
                        CallSound.Drum, CallMotion.Step, new[] { T(0), T(2), H(4, 16), T(22), T(24), T(26) }),
                    P("thunder-double-current", "쌍둥이 전류", "두 번의 긴 방전 사이에 빠른 불꽃을 보낸다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Stomp, new[] { H(0, 8), T(10), T(12), H(16, 8), T(26), T(28) }),
                };
                case "iron-turtle": return new[]
                {
                    P("turtle-fist-pressure", "주먹 뒤 압박", "짧은 권격 다음 두 박 동안 비늘 방패를 민다.", 4, 12, false,
                        CallSound.Wood, CallMotion.Hop, new[] { T(0), H(4, 8) }),
                    P("turtle-fortress-march", "움직이는 성벽", "네 박짜리 성벽 밀기와 두 꼬리 타격 뒤 다시 두 박을 압박한다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Step, new[] { H(0, 16), T(18), T(22), H(24, 8) }),
                    P("turtle-tail-siege", "꼬리와 포위", "짧은 꼬리와 긴 방패 압박을 번갈아 몰아친다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.TailSweep, new[] { T(0), H(4, 8), T(14), H(16, 8), T(28) }),
                };
                case "diving-ray": return new[]
                {
                    P("lamia-veil-snap", "장막과 꼬리 끝", "꼬리 끝을 튕긴 뒤 장막으로 압박하고 다시 짧게 친다.", 4, 16, false,
                        CallSound.Wood, CallMotion.Hop, new[] { T(0), H(4, 8), T(14) }),
                    P("lamia-curtain-refrain", "긴 장막 후렴", "긴 장막 두 번 사이에 꼬리 끝을 두 차례 튕긴다.", 16, 32, true,
                        CallSound.Drum, CallMotion.Step, new[] { H(0, 16), T(18), T(22), H(24, 8) }),
                    P("lamia-coiling-dance", "감아 도는 춤", "두 번의 지속 장막을 짧고 긴 꼬리 박자로 잇는다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Stomp, new[] { T(0), H(4, 8), T(14), T(18), H(20, 8), T(30) }),
                };
                case "bubble-spirit": return new[]
                {
                    P("jellyfish-pulse", "우산막 맥동", "작은 막 사이에 한 박짜리 촉수 압박이 이어진다.", 4, 16, false,
                        CallSound.Wood, CallMotion.Step, new[] { T(0), H(4, 4), T(10) }),
                    P("jellyfish-tide", "촉수의 조수", "두 번의 긴 막 압박 사이로 작은 기포가 반 박 어긋나 도착한다.", 16, 32, true,
                        CallSound.Drum, CallMotion.Stomp, new[] { H(0, 8), T(10), T(14), H(16, 8), T(26), T(30) }),
                    P("jellyfish-bloom", "해파리 개화", "작은 기포 둘 뒤 네 박 동안 우산막을 펼치고 기포 셋을 남긴다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.TailSweep, new[] { T(0), T(2), H(4, 16), T(22), T(24), T(28) }),
                };
                case "flick-goblin": return new[]
                {
                    P("arachne-thread-knot", "매듭 당기기", "실을 튕기고 팽팽히 유지한 뒤 마지막 매듭을 당긴다.", 4, 16, false,
                        CallSound.Drum, CallMotion.Hop, new[] { T(0), H(4, 4), T(12) }),
                    P("arachne-loom", "거미의 베틀", "짧은 실 두 번과 긴 당기기 두 번을 번갈아 엮는다.", 16, 32, true,
                        CallSound.Bell, CallMotion.Stomp, new[] { T(0), T(2), H(4, 8), T(14), H(16, 8), T(26), T(30) }),
                    P("arachne-web-finale", "긴 그물 마무리", "네 박 동안 그물을 당긴 뒤 짧은 매듭과 마지막 실로 마무리한다.", 16, 32, true,
                        CallSound.RisingChime, CallMotion.TailSweep, new[] { H(0, 16), T(18), T(20), H(24, 4), T(30) }),
                };
                default: return Array.Empty<MonsterPatternDefinition>();
            }
        }
        private static PatternStep T(int tick) => new PatternStep(GestureKind.Tap, tick);
        private static PatternStep H(int tick, int length) => new PatternStep(GestureKind.Hold, tick, length);
        private static MonsterPatternDefinition P(string id, string name, string description, int cue, int response,
            bool hook, CallSound sound, CallMotion motion, PatternStep[] steps)
        {
            var calls = new List<CallSignal> { new CallSignal(0, name, sound, motion) };
            if (hook) calls.Add(new CallSignal(cue - 4, "이어서!", sound, motion));
            return new MonsterPatternDefinition(name, description, new RhythmPattern(id, cue, steps), calls,
                response, 4, hook ? .13 : .24, cueAlignmentTicks: id.StartsWith("neko-", StringComparison.Ordinal) ? 2 : 4, isHook: hook);
        }
    }
}
