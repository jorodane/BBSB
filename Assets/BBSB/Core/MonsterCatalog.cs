using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MonsterCatalog
    {
        // Four ticks per beat. Tresillo uses 0, 6, 12 in a 16-tick phrase.
        // Five of nine species have a Tap theme; the mixed-input bat is deliberately rarer.
        public static IReadOnlyList<MonsterDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new MonsterDefinition("tap-slime", "통통 슬라임", "박자 선생 · Call을 따라 세고 마지막에 한 번 눌러.", GestureKind.Tap, new[]
            {
                Pattern("count-four-tap", "넷에 통!", "1·2·3박 Call, 4박에 Tap.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "하나"), Call(4, "둘"), Call(8, "셋") }, 4, 4, .18),
                Pattern("tresillo-call-tap", "트레실로 예고", "1박·2박 반 Call, 4박에 Tap. 간격은 3+3+2야.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "통"), Call(6, "통") }, 4, 4, .18)
            }, encounterWeight: 1.4),
            new MonsterDefinition("march-slime", "행진 슬라임", "정박 행진 · 일정한 간격으로 또박또박 눌러.", GestureKind.Tap, new[]
            {
                Pattern("march-three", "세 걸음 행진", "Call 다음 세 박자를 한 번씩 눌러.", 4,
                    new[] { Tap(0), Tap(4), Tap(8) }, new[] { Call(0, "출발!") }, 12, 4, .16),
                Pattern("march-spaced", "큰 걸음 행진", "Call 뒤 한 번, 한 박자 쉬고 다시 한 번 눌러.", 4,
                    new[] { Tap(0), Tap(8) }, new[] { Call(0, "크게!") }, 12, 4, .14)
            }, encounterWeight: 1.4, artId: "tap-slime"),
            new MonsterDefinition("tresillo-bat", "춤추는 박쥐", "트레실로 춤 · 세 번의 Tap 사이 간격을 바꿔.", GestureKind.Tap, new[]
            {
                Pattern("tresillo-taps", "세 번 반짝", "대응 구간의 1박·2박 반·4박에 Tap. 간격은 3+3+2야.", 4,
                    new[] { Tap(0), Tap(6), Tap(12) }, new[] { Call(0, "반짝!") }, 16, 4, .16),
                Pattern("rotated-tresillo", "엇갈린 반짝", "대응 구간의 1박·2박 반·3박 반에 Tap. 간격은 3+2+3이야.", 4,
                    new[] { Tap(0), Tap(6), Tap(10) }, new[] { Call(0, "반짝 반짝!") }, 16, 4, .14)
            }, encounterWeight: 1.4, artId: "spark-bat"),
            new MonsterDefinition("offbeat-goblin", "뒷박 도깨비", "뒷박 장난 · 신호에서 반 박을 기다린 뒤 눌러.", GestureKind.Tap, new[]
            {
                Pattern("offbeat-single-tap", "반 박 뒤 톡", "Call에서 반 박 뒤 한 번 눌러.", 2,
                    new[] { Tap(0) }, new[] { Call(0, "톡!") }, 4, 4, .18),
                Pattern("offbeat-pair", "뒷박 두 걸음", "두 번의 Call 뒤 반 박을 기다려. Tap 두 번은 한 박 간격이야.", 6,
                    new[] { Tap(0), Tap(4) }, new[] { Call(0, "하나"), Call(4, "둘") }, 8, 4, .14)
            }, encounterWeight: 1.4, artId: "flick-goblin"),
            new MonsterDefinition("spark-bat", "반짝 박쥐", "변주 장난꾼 · 빠른 Tap과 짧은 Hold를 바꿔 사용해.", GestureKind.Tap, new[]
            {
                Pattern("bat-quick-taps", "짧게 두 번", "찌·릿 Call 뒤 반 박 간격으로 두 번 눌러.", 4,
                    new[] { Tap(0), Tap(2) }, new[] { Call(0, "찌"), Call(2, "릿!") }, 4, 4, .14),
                Pattern("bat-hold", "불빛 붙잡기", "길게 반짝이면 한 박자 눌러 빛을 붙잡아.", 4,
                    new[] { Held(GestureKind.Hold, 0, 4) }, new[] { Call(0, "반자아악") }, 4, 8, .12)
            }, encounterWeight: .35),
            new MonsterDefinition("iron-turtle", "철갑 거북", "버티기 · 신호가 길수록 오래 눌러 방어해.", GestureKind.Hold, new[]
            {
                Pattern("turtle-long-hold", "두 번 쿵, 길게", "두 번의 정박 Call 뒤 두 박자 눌러.", 8,
                    new[] { Held(GestureKind.Hold, 0, 8) }, new[] { Call(0, "쿵"), Call(4, "쿵!") }, 8, 4, .18),
                Pattern("turtle-short-hold", "한 번 쿵, 짧게", "한 번의 Call 뒤 한 박자 눌러.", 4,
                    new[] { Held(GestureKind.Hold, 0, 4) }, new[] { Call(0, "쿵!") }, 4, 4, .16)
            }),
            new MonsterDefinition("diving-ray", "잠수 가오리", "잠수 시간 · 누른 뒤 정해진 끝 박자에 손을 떼어.", GestureKind.Dive, new[]
            {
                Pattern("ray-deep-dive", "깊은 잠수", "슈·욱 Call 뒤 네 박자 잠수하고 끝에 떼어.", 8,
                    new[] { Held(GestureKind.Dive, 0, 16) }, new[] { Call(0, "슈"), Call(4, "욱!") }, 16, 8, .25),
                Pattern("ray-short-dive", "얕은 잠수", "짧은 Call 뒤 두 박자 잠수하고 끝에 떼어.", 4,
                    new[] { Held(GestureKind.Dive, 0, 8) }, new[] { Call(0, "슉!") }, 8, 4, .20)
            }),
            new MonsterDefinition("bubble-spirit", "방울 정령", "왕복 춤 · 방울 하나마다 한 번 흔들었다 돌아와.", GestureKind.Shake, new[]
            {
                Pattern("one-beat-shake", "방울 하나", "한 박자 안에 한 번 왕복하면 완성돼.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4) }, new[] { Call(0, "방울!") }, 4, 4, .18),
                Pattern("two-bubble-shakes", "방울 둘", "한 번 왕복하고 한 박자 쉬어. 다음 방울도 한 번 왕복해.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4), Held(GestureKind.Shake, 8, 4) }, new[] { Call(0, "방울 둘!") }, 12, 4, .14)
            }),
            new MonsterDefinition("flick-goblin", "튕김 도깨비", "튕기는 타이밍 · Call 뒤 기다리는 간격을 듣고 튕겨.", GestureKind.Flick, new[]
            {
                Pattern("counted-flick", "둘 세고 휙", "두 번의 정박 Call 다음 박자에 튕겨 떼어.", 8,
                    new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { Call(0, "하나"), Call(4, "둘") }, 4, 4, .18),
                Pattern("offbeat-flick", "반 박 먼저 휙", "두 번째 Call에서 반 박 뒤에 튕겨 떼어.", 6,
                    new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { Call(0, "하나"), Call(4, "휙!") }, 4, 4, .16)
            })
        });

        private static MonsterPatternDefinition Pattern(string id, string name, string description, int cue,
            PatternStep[] steps, CallSignal[] calls, int response, int rest, double chance)
            => new MonsterPatternDefinition(name, description, new RhythmPattern(id, cue, steps), calls, response, rest, chance,
                cueAlignmentTicks: RhythmTime.TicksPerBeat);
        private static PatternStep Tap(int tick) => new PatternStep(GestureKind.Tap, tick);
        private static PatternStep Held(GestureKind kind, int tick, int duration) => new PatternStep(kind, tick, duration);
        private static CallSignal Call(int tick, string label) => new CallSignal(tick, label);
    }
}
