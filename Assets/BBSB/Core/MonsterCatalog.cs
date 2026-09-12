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
            new MonsterDefinition("tap-slime", "젤리 슬라임 소녀", "둥글고 탄력 있는 실루엣을 가진 명랑한 종족이다.", GestureKind.Tap, new[]
            {
                Pattern("count-four-tap", "셋 세고 통!", "첫 쿵에 발을 찍어 지면의 젤리 폭발을 시작하고, 이어지는 쿵마다 폭발이 팍팍 플레이어 쪽으로 번진다.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "하나", CallSound.Wood, CallMotion.Step), Call(4, "둘", CallSound.Wood, CallMotion.Step), Call(8, "셋", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("tresillo-call-tap", "출렁, 출렁, 통!", "첫 낮은 울림에서 젤리를 만들고, 두 번째 울림까지 크게 도약한 뒤 플레이어 쪽으로 다시 튀긴다.", 12,
                    new[] { Tap(0) }, new[] { Call(0, "둠", CallSound.Drum, CallMotion.Sway), Call(6, "둠", CallSound.Drum, CallMotion.Sway) }, 4, 4, .18)
            }, encounterWeight: 1.4),
            new MonsterDefinition("march-slime", "도자기 골렘 소녀", "커다란 손발과 분절된 관절을 가진 성실한 골렘이다.", GestureKind.Tap, new[]
            {
                Pattern("march-three", "세 걸음 권격", "발을 딱 구르는 Call 뒤 마력으로 분리되어 움직이는 장갑 주먹을 세 번 보낸다.", 4,
                    new[] { Tap(0), Tap(4), Tap(8) }, new[] { Call(0, "출발!", CallSound.Wood, CallMotion.Step) }, 12, 4, .16),
                Pattern("march-spaced", "큰 보폭 권격", "높게 발을 들어 크게 뛰는 Call 뒤 장갑을 더 깊게 당겨 두 번 보낸다.", 4,
                    new[] { Tap(0), Tap(8) }, new[] { Call(0, "크게!", CallSound.RisingWhistle, CallMotion.Hop) }, 12, 4, .14)
            }, encounterWeight: 1.4, artId: "tap-slime"),
            new MonsterDefinition("tresillo-bat", "깃춤 하피", "팔에서 날개로 이어지는 넓은 깃과 부채꼴 꼬리를 가진 하피다.", GestureKind.Tap, new[]
            {
                Pattern("tresillo-taps", "세 갈래 깃춤", "깃털을 한 번 번쩍 펼치는 맑은 Call 뒤, 깃털 세 묶음이 Response 1박·2박 반·4박에 도착한다.", 4,
                    new[] { Tap(0), Tap(6), Tap(12) }, new[] { Call(0, "반짝!", CallSound.Bell, CallMotion.Flash) }, 16, 4, .16),
                Pattern("rotated-tresillo", "돌아서는 깃춤", "날개를 비비며 자르르 울리고 몸을 틀어, Response 1박·2박 반·3박 반에 깃털을 보낸다.", 4,
                    new[] { Tap(0), Tap(6), Tap(10) }, new[] { Call(0, "빙글!", CallSound.Rattle, CallMotion.Sway) }, 16, 4, .14)
            }, encounterWeight: 1.4, artId: "spark-bat"),
            new MonsterDefinition("offbeat-goblin", "여우불 여우요괴", "풍성한 꼬리 끝에서 여우불을 피우는 장난스러운 여우요괴다.", GestureKind.Tap, new[]
            {
                Pattern("offbeat-single-tap", "뒤따르는 불씨", "옆걸음으로 탁 소리를 내고 반 박 뒤 작은 여우불이 도착한다.", 2,
                    new[] { Tap(0) }, new[] { Call(0, "톡!", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("offbeat-pair", "두 꼬리불 장난", "방울 소리와 두 번의 가벼운 점프 뒤, 마지막 Call에서 반 박 뒤부터 한 박 간격으로 불씨 두 개가 도착한다..", 6,
                    new[] { Tap(0), Tap(4) }, new[] { Call(0, "뿅", CallSound.Bell, CallMotion.Hop), Call(4, "뿅!", CallSound.Bell, CallMotion.Hop) }, 8, 4, .14)
            }, encounterWeight: 1.4, artId: "flick-goblin"),
            new MonsterDefinition("drowsy-slime", "꿈먹는 맥 소녀", "꿈을 먹고 잠에 빠지는 맥 소녀다.", GestureKind.Tap, new[]
            {
                Pattern("drowsy-quick-tap", "깜짝 꿈방울", "발을 통 구르며 놀란 뒤 한 박 후 작은 꿈방울이 도착한다..", 4,
                    new[] { Tap(0) }, new[] { Call(0, "통!", CallSound.Wood, CallMotion.Hop) }, 4, 4, .32),
                Pattern("drowsy-four-beat-wait", "네 박 낮잠", "내려가는 하품 소리와 함께 몸을 낮춘다.", 16,
                    new[] { Tap(0) }, new[] { Call(0, "스르르", CallSound.FallingWhistle, CallMotion.Dip) }, 4, 4, .18,
                    silentWaitTicks: 16)
            }, encounterWeight: 1.2, artId: "tap-slime"),
            new MonsterDefinition("clock-spirit", "태엽 인형 소녀", "등에 태엽, 가슴에 작은 종을 품은 정교한 인형이다.", GestureKind.Tap, new[]
            {
                Pattern("clock-quick-tap", "째깍 인형 던지기", "Call에 작은 인형을 던진다.", 4,
                    new[] { Tap(0) }, new[] { Call(0, "째깍!", CallSound.Wood, CallMotion.Step) }, 4, 4, .32),
                Pattern("clock-seven-beat-wait", "뚜방뚜방 일곱 걸음", "Call에 작은 인형을 자기 앞에 내려놓는다.", 28,
                    new[] { Tap(0) }, new[] { Call(0, "땡!", CallSound.Bell, CallMotion.Flash) }, 4, 4, .18,
                    silentWaitTicks: 28)
            }, encounterWeight: 1.2, artId: "bubble-spirit"),
            new MonsterDefinition("seesaw-goblin", "쌍꼬리 네코마타", "긴 두 꼬리가 서로 먼저 움직이려 하는 네코마타다.", GestureKind.Tap, new[]
            {
                new MonsterPatternDefinition("꼬리 또각", "꼬리 끝을 또각 내려놓고 한 박 뒤 반대 꼬리가 길게 뻗어 타격한다.",
                    new RhythmPattern("seesaw-steady-tap", 4, new[] { Tap(0) }),
                    new[] { Call(0, "또각!", CallSound.Wood, CallMotion.Step) }, 4, 0, .24, cueAlignmentTicks: 2),
                new MonsterPatternDefinition("내가 먼저!", "몸을 비틀며 두 꼬리를 교차해 올리는 Call 뒤, 한 박 뒤 첫 타격과 반 박 뒤 두 번째 타격이 나온다.",
                    new RhythmPattern("seesaw-early-finish", 4, new[] { Tap(0), Tap(2) }),
                    new[] { Call(0, "당겨!", CallSound.RisingChime, CallMotion.Sway) }, 4, 0, .24, cueAlignmentTicks: 2)
            }, encounterWeight: .6, artId: "flick-goblin", patternPlanner: new BeatShiftPlanner()),
            new MonsterDefinition("spark-bat", "뇌수 소녀", "이 프로젝트의 뇌수는 뿔과 뾰족한 털, 전기를 모으는 톱니 모양 꼬리를 가진 수인이다.", GestureKind.Tap, new[]
            {
                Pattern("bat-quick-taps", "찌릿 두 번", "양손에서 불꽃을 두 번 튀기는 Call 뒤, 응집된 전기 구슬 두 개를 반 박 간격으로 보낸다.", 4,
                    new[] { Tap(0), Tap(2) }, new[] { Call(0, "찌", CallSound.Rattle, CallMotion.Flash), Call(2, "릿!", CallSound.Rattle, CallMotion.Flash) }, 4, 4, .14),
                Pattern("bat-hold", "꼬리 축전", "꼬리를 세우고 올라가는 울림을 내는 Call 뒤 1박 동안 굵은 방전을 내보낸다.", 4,
                    new[] { Held(GestureKind.Hold, 0, 4) }, new[] { Call(0, "반자아악", CallSound.RisingChime, CallMotion.Rise) }, 4, 8, .12)
            }, encounterWeight: .35),
            new MonsterDefinition("iron-turtle", "갑각 용인 소녀", "넓은 어깨 갑각과 큰 팔갑, 끝이 두꺼운 꼬리를 가진 용인이다.", GestureKind.Hold, new[]
            {
                Pattern("turtle-long-hold", "비늘 방패 밀기", "낮게 발을 쿵 구른 뒤 두 박 후, 등갑을 닮은 무거운 비늘 방패를 밀어 보내 2박 동안 압박한다.", 8,
                    new[] { Held(GestureKind.Hold, 0, 8) },
                    new[] { Call(0, "쿵", CallSound.Drum, CallMotion.Stomp) }, 8, 4, .18),
                Pattern("turtle-hold-tap", "꼬리까지 조심!", "같은 쿵 뒤 한 박 후 꼬리를 크게 휘두르는 Call이 붙는다.", 8,
                    new[] { Held(GestureKind.Hold, 0, 8), Tap(12) },
                    new[] { Call(0, "쿵", CallSound.Drum, CallMotion.Stomp), Call(4, "휙!", CallSound.Sweep, CallMotion.TailSweep) }, 16, 4, .16)
            }),
            new MonsterDefinition("diving-ray", "장막 라미아", "긴 뱀 하반신과 옆으로 넓게 펼쳐지는 꼬리 막을 가진 라미아다.", GestureKind.Dive, new[]
            {
                Pattern("ray-deep-dive", "긴 장막", "두 번의 낮아지는 울림과 몸 낮추기 뒤 넓은 꼬리 막을 펼친다.", 8,
                    new[] { Held(GestureKind.Dive, 0, 16) }, new[] { Call(0, "부우", CallSound.FallingWhistle, CallMotion.Dip), Call(4, "욱!", CallSound.FallingChime, CallMotion.Dip) }, 16, 8, .25),
                Pattern("ray-short-dive", "짧은 장막", "올라가는 휘파람과 몸을 드는 Call 뒤 짧게 꼬리를 펼쳤다 거둔다.", 4,
                    new[] { Held(GestureKind.Dive, 0, 8) }, new[] { Call(0, "슉!", CallSound.RisingWhistle, CallMotion.Rise) }, 8, 4, .20)
            }),
            new MonsterDefinition("bubble-spirit", "해파리 소녀", "머리 위의 투명한 우산막과 떠 있는 몸, 가느다란 촉수를 가진 해파리 소녀다.", GestureKind.Shake, new[]
            {
                Pattern("one-beat-shake", "우산막 하나", "맑게 뽕 울리며 떠오른 뒤 큰 젤 막 하나를 보낸다.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4) }, new[] { Call(0, "뽕!", CallSound.Bell, CallMotion.Hop) }, 4, 4, .18),
                Pattern("two-bubble-shakes", "우산막 둘", "촉수를 비벼 또르르 울리는 Call 뒤 젤 막을 두 번 보낸다.", 4,
                    new[] { Held(GestureKind.Shake, 0, 4), Held(GestureKind.Shake, 8, 4) }, new[] { Call(0, "또르르!", CallSound.Rattle, CallMotion.Sway) }, 12, 4, .14)
            }),
            new MonsterDefinition("flick-goblin", "실 잣는 아라크네", "인간형 상체 아래로 여러 거미 다리가 펼쳐지고, 앞다리와 손으로 실을 잡는 아라크네다.", GestureKind.Flick, new[]
            {
                Pattern("counted-flick", "둘 세고 당기기", "앞다리로 바닥을 두 번 찍어 하나·둘 Call을 보낸 뒤, 다음 박자에 팽팽한 실을 발목 높이로 당긴다.", 8,
                    new[] { new PatternStep(GestureKind.Flick, 0) }, new[] { Call(0, "하나", CallSound.Wood, CallMotion.Step), Call(4, "둘", CallSound.Wood, CallMotion.Step) }, 4, 4, .18),
                Pattern("offbeat-flick", "반 박 먼저 걷기", "몸을 틀며 스윽, 앞다리를 들며 휙 하는 Call 뒤, 두 번째 신호에서 반 박 뒤 실이 지나간다..", 6,
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
