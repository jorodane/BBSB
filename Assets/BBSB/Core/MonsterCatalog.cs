using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MonsterCatalog
    {
        // Four ticks per beat. Tresillo uses 0, 6, 12 in a 16-tick phrase.
        // Eight of twelve species have a Tap theme; each specialist uses its own pattern placement strategy.
        public static IReadOnlyList<MonsterDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new MonsterDefinition("tap-slime", "통통 슬라임", "박자 선생 · Call을 따라 세고 마지막에 한 번 눌러.", GestureKind.Tap, new[]
            {
                Pattern("count-four-tap", "넷에 통!", "딱딱한 나무 소리와 제자리걸음. 1·2·3박을 세고 4박에 Tap.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "하나", CallSound.Wood, CallMotion.Step), Call(4, "둘", CallSound.Wood, CallMotion.Step), Call(8, "셋", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("tresillo-call-tap", "트레실로 예고", "낮은 북소리에 몸을 좌우로 흔들어. 1박·2박 반 Call, 4박 Tap: 3+3+2.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "둠", CallSound.Drum, CallMotion.Sway), Call(6, "둠", CallSound.Drum, CallMotion.Sway) }, 4, 4, .18)
            }, encounterWeight: 1.4),
            new MonsterDefinition("march-slime", "행진 슬라임", "정박 행진 · 일정한 간격으로 또박또박 눌러.", GestureKind.Tap, new[]
            {
                Pattern("march-three", "세 걸음 행진", "나무 소리와 제자리걸음 뒤, 세 박자를 한 번씩 눌러.", 4,
                    new[] { Tap(0), Tap(4), Tap(8) }, new[] { Call(0, "출발!", CallSound.Wood, CallMotion.Step) }, 12, 4, .16),
                Pattern("march-spaced", "큰 걸음 행진", "올라가는 휘파람과 큰 점프 뒤 한 번, 한 박자 쉬고 한 번 눌러.", 4,
                    new[] { Tap(0), Tap(8) }, new[] { Call(0, "크게!", CallSound.RisingWhistle, CallMotion.Hop) }, 12, 4, .14)
            }, encounterWeight: 1.4, artId: "tap-slime"),
            new MonsterDefinition("tresillo-bat", "춤추는 박쥐", "트레실로 춤 · 세 번의 Tap 사이 간격을 바꿔.", GestureKind.Tap, new[]
            {
                Pattern("tresillo-taps", "세 번 반짝", "맑은 종소리와 빛 번쩍. 대응 1박·2박 반·4박 Tap: 3+3+2.", 4,
                    new[] { Tap(0), Tap(6), Tap(12) }, new[] { Call(0, "반짝!", CallSound.Bell, CallMotion.Flash) }, 16, 4, .16),
                Pattern("rotated-tresillo", "엇갈린 반짝", "자르르 소리와 좌우 회전. 대응 1박·2박 반·3박 반 Tap: 3+2+3.", 4,
                    new[] { Tap(0), Tap(6), Tap(10) }, new[] { Call(0, "빙글!", CallSound.Rattle, CallMotion.Sway) }, 16, 4, .14)
            }, encounterWeight: 1.4, artId: "spark-bat"),
            new MonsterDefinition("offbeat-goblin", "뒷박 도깨비", "뒷박 장난 · 신호에서 반 박을 기다린 뒤 눌러.", GestureKind.Tap, new[]
            {
                Pattern("offbeat-single-tap", "반 박 뒤 톡", "딱 소리와 옆걸음에서 반 박 뒤 한 번 눌러.", 2,
                    new[] { Tap(0) }, new[] { Call(0, "톡!", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("offbeat-pair", "뒷박 두 걸음", "맑은 종소리와 두 번의 점프. 마지막 Call 반 박 뒤부터 한 박 간격 Tap 두 번.", 6,
                    new[] { Tap(0), Tap(4) }, new[] { Call(0, "뿅", CallSound.Bell, CallMotion.Hop), Call(4, "뿅!", CallSound.Bell, CallMotion.Hop) }, 8, 4, .14)
            }, encounterWeight: 1.4, artId: "flick-goblin"),
            new MonsterDefinition("drowsy-slime", "졸음 슬라임", "꾸벅 졸음 · 짧게 통통 뛰다가 가끔 네 박 동안 잠들어.", GestureKind.Tap, new[]
            {
                Pattern("drowsy-quick-tap", "깜짝 통!", "나무 소리와 점프 뒤 한 박을 기다려 Tap. 평소에 자주 나와.", 4,
                    new[] { Tap(0) }, new[] { Call(0, "통!", CallSound.Wood, CallMotion.Hop) }, 4, 4, .32),
                Pattern("drowsy-four-beat-wait", "네 박 낮잠", "내려가는 휘파람에 몸을 낮추면 네 박을 기다려 Tap. Call을 1박으로 세면 5박에 눌러.", 16,
                    new[] { Tap(0) }, new[] { Call(0, "스르르", CallSound.FallingWhistle, CallMotion.Dip) }, 4, 4, .18,
                    silentWaitTicks: 16)
            }, encounterWeight: 1.2, artId: "tap-slime"),
            new MonsterDefinition("clock-spirit", "시계 정령", "긴 종소리 · 째깍에는 한 박 뒤 대응하고 종이 울리면 일곱 박을 기억해.", GestureKind.Tap, new[]
            {
                Pattern("clock-quick-tap", "째깍 톡!", "나무 소리와 제자리걸음 뒤 한 박을 기다려 Tap. 평소에 자주 나와.", 4,
                    new[] { Tap(0) }, new[] { Call(0, "째깍!", CallSound.Wood, CallMotion.Step) }, 4, 4, .32),
                Pattern("clock-seven-beat-wait", "일곱 박 종소리", "맑은 종과 섬광 뒤 일곱 박을 기다려 Tap. Call을 1박으로 세면 8박에 눌러.", 28,
                    new[] { Tap(0) }, new[] { Call(0, "땡!", CallSound.Bell, CallMotion.Flash) }, 4, 4, .18,
                    silentWaitTicks: 28)
            }, encounterWeight: 1.2, artId: "bubble-spirit"),
            new MonsterDefinition("seesaw-goblin", "시소 도깨비", "박자 지휘자 · 매 박 따라 치다가 당겨! 소리에 마지막 Tap을 반 박 당겨.", GestureKind.Tap, new[]
            {
                new MonsterPatternDefinition("한 박씩 또각", "또각! 한 박 뒤 Tap. 그 Tap과 함께 다음 Call이 이어져.",
                    new RhythmPattern("seesaw-steady-tap", 4, new[] { Tap(0) }),
                    new[] { Call(0, "또각!", CallSound.Wood, CallMotion.Step) }, 4, 0, .24, cueAlignmentTicks: 2),
                new MonsterPatternDefinition("반 박 당겨!", "올라가는 울림과 몸 비틀기 뒤 Tap, 반 박 뒤 한 번 더 Tap. 그 마지막 Tap부터 다음 Call도 반 박 앞당겨져.",
                    new RhythmPattern("seesaw-early-finish", 4, new[] { Tap(0), Tap(2) }),
                    new[] { Call(0, "당겨!", CallSound.RisingChime, CallMotion.Sway) }, 4, 0, .24, cueAlignmentTicks: 2)
            }, encounterWeight: .6, artId: "flick-goblin", patternPlanner: new BeatShiftPlanner()),
            new MonsterDefinition("spark-bat", "반짝 박쥐", "변주 장난꾼 · 빠른 Tap과 짧은 Hold를 바꿔 사용해.", GestureKind.Tap, new[]
            {
                Pattern("bat-quick-taps", "짧게 두 번", "찌·릿 떨리는 소리와 두 번의 섬광 뒤 반 박 간격 Tap 두 번.", 4,
                    new[] { Tap(0), Tap(2) }, new[] { Call(0, "찌", CallSound.Rattle, CallMotion.Flash), Call(2, "릿!", CallSound.Rattle, CallMotion.Flash) }, 4, 4, .14),
                Pattern("bat-hold", "불빛 붙잡기", "울림이 올라가며 몸이 솟으면, 한 박자 눌러 빛을 붙잡아.", 4,
                    new[] { Held(GestureKind.Hold, 0, 4) }, new[] { Call(0, "반자아악", CallSound.RisingChime, CallMotion.Rise) }, 4, 8, .12)
            }, encounterWeight: .35),
            new MonsterDefinition("iron-turtle", "철갑 거북", "발과 꼬리 · 쿵 뒤 한 박 동안 꼬리를 확인한 다음 방어해.", GestureKind.Hold, new[]
            {
                Pattern("turtle-long-hold", "발 구르고 버티기", "쿵 뒤 꼬리가 없으면, 쿵에서 두 박 뒤 두 박 Hold 한 번.", 8,
                    new[] { Held(GestureKind.Hold, 0, 8) },
                    new[] { Call(0, "쿵", CallSound.Drum, CallMotion.Stomp) }, 8, 4, .18),
                Pattern("turtle-hold-tap", "꼬리까지 막기", "쿵 한 박 뒤 꼬리가 휙! 쿵에서 두 박 뒤 두 박 Hold, 한 박 쉬고 Tap.", 8,
                    new[] { Held(GestureKind.Hold, 0, 8), Tap(12) },
                    new[] { Call(0, "쿵", CallSound.Drum, CallMotion.Stomp), Call(4, "휙!", CallSound.Sweep, CallMotion.TailSweep) }, 16, 4, .16)
            }),
            new MonsterDefinition("diving-ray", "잠수 가오리", "잠수 시간 · 누른 뒤 정해진 끝 박자에 손을 떼어.", GestureKind.Dive, new[]
            {
                Pattern("ray-deep-dive", "깊은 잠수", "낮아지는 휘파람과 두 번의 몸 낮추기 뒤 네 박 잠수하고 떼어.", 8,
                    new[] { Held(GestureKind.Dive, 0, 16) }, new[] { Call(0, "부우", CallSound.FallingWhistle, CallMotion.Dip), Call(4, "욱!", CallSound.FallingChime, CallMotion.Dip) }, 16, 8, .25),
                Pattern("ray-short-dive", "얕은 잠수", "올라가는 휘파람과 솟구치기 뒤 두 박 잠수하고 떼어.", 4,
                    new[] { Held(GestureKind.Dive, 0, 8) }, new[] { Call(0, "슉!", CallSound.RisingWhistle, CallMotion.Rise) }, 8, 4, .20)
            }),
            new MonsterDefinition("bubble-spirit", "방울 정령", "왕복 춤 · 방울 하나마다 한 번 흔들었다 돌아와.", GestureKind.Shake, new[]
            {
                Pattern("one-beat-shake", "방울 하나", "맑은 종소리와 점프 뒤 한 박 안에 한 번 왕복해.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4) }, new[] { Call(0, "뽕!", CallSound.Bell, CallMotion.Hop) }, 4, 4, .18),
                Pattern("two-bubble-shakes", "방울 둘", "자르르 소리와 좌우 흔들기 뒤 한 번 왕복. 한 박 쉬고 다시 왕복해.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4), Held(GestureKind.Shake, 8, 4) }, new[] { Call(0, "또르르!", CallSound.Rattle, CallMotion.Sway) }, 12, 4, .14)
            }),
            new MonsterDefinition("flick-goblin", "튕김 도깨비", "튕기는 타이밍 · Call 뒤 기다리는 간격을 듣고 튕겨.", GestureKind.Flick, new[]
            {
                Pattern("counted-flick", "둘 세고 휙", "나무 소리에 두 걸음을 세고, 다음 박자에 튕겨 떼어.", 8,
                    new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { Call(0, "하나", CallSound.Wood, CallMotion.Step), Call(4, "둘", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("offbeat-flick", "반 박 먼저 휙", "바람 소리와 몸 비틀기로 시작해. 두 번째 Call 반 박 뒤에 튕겨 떼어.", 6,
                    new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { Call(0, "스윽", CallSound.Sweep, CallMotion.Sway), Call(4, "휙!", CallSound.RisingWhistle, CallMotion.Rise) }, 4, 4, .16)
            })
        });

        private static MonsterPatternDefinition Pattern(string id, string name, string description, int cue,
            PatternStep[] steps, CallSignal[] calls, int response, int rest, double chance, int silentWaitTicks = 0)
            => new MonsterPatternDefinition(name, description, new RhythmPattern(id, cue, steps), calls, response, rest, chance,
                cueAlignmentTicks: RhythmTime.TicksPerBeat, silentWaitTicks: silentWaitTicks);
        private static PatternStep Tap(int tick) => new PatternStep(GestureKind.Tap, tick);
        private static PatternStep Held(GestureKind kind, int tick, int duration) => new PatternStep(kind, tick, duration);
        private static CallSignal Call(int tick, string label, CallSound sound, CallMotion motion) => new CallSignal(tick, label, sound, motion);
    }
}
