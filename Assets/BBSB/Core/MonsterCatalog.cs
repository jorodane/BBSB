using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public static class MonsterCatalog
    {
        public static IReadOnlyList<MonsterDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new MonsterDefinition("tap-slime", "통통 슬라임", "세 번 두드리고 한 박자 쉬어.",
                new RhythmPattern("triple-tap", 4, new[] { Tap(0), Tap(4), Tap(8) }),
                new[] { new CallSignal(0, "통!") }, 12, 4, .28),
            new MonsterDefinition("spark-bat", "반짝 박쥐", "반 박 간격으로 두 번 누르고, 튕겨서 마무리해.",
                new RhythmPattern("quick-taps-flick", 4, new[] { Tap(0), Tap(2), new PatternStep(GestureKind.Flick, 6) }),
                new[] { new CallSignal(0, "찌"), new CallSignal(2, "릿!") }, 8, 4, .24),
            new MonsterDefinition("iron-turtle", "철갑 거북", "두 박자 누르면서 뒤 한 박자를 흔들어.",
                new RhythmPattern("hold-shake", 4, new[] { new PatternStep(GestureKind.Hold, 0, 8), new PatternStep(GestureKind.Shake, 4, 4) }),
                new[] { new CallSignal(0, "쿵!") }, 8, 4, .25),
            new MonsterDefinition("diving-ray", "잠수 가오리", "네 박자 잠수하며 두 번 흔들고, 끝에서 튕겨 떼어.",
                new RhythmPattern("deep-dive-shake-flick", 8, new[]
                {
                    new PatternStep(GestureKind.Dive, 0, 16), new PatternStep(GestureKind.Shake, 4, 4),
                    new PatternStep(GestureKind.Shake, 12, 4), new PatternStep(GestureKind.Flick, 16)
                }), new[] { new CallSignal(0, "슈"), new CallSignal(4, "욱!") }, 16, 8, .4),
            new MonsterDefinition("bubble-spirit", "방울 정령", "한 박자 누른 상태로 흔들었다 돌아와.",
                new RhythmPattern("one-beat-shake", 4, new[] { new PatternStep(GestureKind.Shake, 0, 4) }),
                new[] { new CallSignal(0, "방울!") }, 4, 4, .2),
            new MonsterDefinition("flick-goblin", "튕김 도깨비", "미리 누르고 있다가 신호 다음 박자에 튕겨 떼어.",
                new RhythmPattern("single-flick", 4, new[] { new PatternStep(GestureKind.Flick, 0) }),
                new[] { new CallSignal(0, "휙!") }, 4, 4, .2)
        });

        private static PatternStep Tap(int tick) => new PatternStep(GestureKind.Tap, tick);
    }
}
