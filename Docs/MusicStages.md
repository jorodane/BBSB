# 음악 스테이지와 패턴 슬롯

음원 없이 설계를 시험하기 위한 다섯 곡의 설정이다. 오디오를 분석해 채보하는 기능은 없다. 이후 실제 음원을 연결할 때 곡 ID를 기준으로 오디오와 시간 오프셋을 연결할 수 있다. BPM과 배열은 임시 값이다.

## 다섯 샘플

모두 4/4박자, 16마디다. `MusicCatalog.cs`가 기본 설정의 원본이다.

| 곡 ID / 이름 | BPM | 리듬 | Hold / Dive 길이 | 하이라이트 가중치 |
|---|---:|---|---:|---:|
| `steady-pulse` / Steady Pulse | 96 | 매 박 Tap | 2박 | ×3 |
| `offbeat-spark` / Offbeat Spark | 124 | 정박 일부와 엇박 조합 | 1박 | ×3.5 |
| `deep-current` / Deep Current | 80 | 2박 간격 Tap, 긴 유지 안의 Shake | 4박 | ×2.5 |
| `rapid-drive` / Rapid Drive | 168 | 반 박 간격 Tap | 1박 | ×4 |
| `switchback` / Switchback | 112 | 정박 마디와 엇박 마디 교대 | 2박 / 1박 | ×3 |

마디 표시는 1부터, 코드의 마디/틱 인덱스는 0부터 시작한다. 1마디는 INTRO로 Response 슬롯을 만들지 않고 전조 공간으로 쓸 수 있다. 2~7마디는 GROOVE(×1), 8~11마디는 HIGHLIGHT, 12~16마디는 OUTRO(×1.25)다. 이 인트로와 별개로 **각 몬스터 패턴에도 전조 시간이 필요하다.**

템플릿의 한 마디 안 오프셋을 반복해 슬롯을 만든다. 교대 리듬은 두 마디 템플릿을 반복한다. 템플릿에는 입력 종류, 시작 틱, 유지 길이, 기본 가중치가 있다. 슬롯의 실제 가중치는 시작 구간의 배율을 곱한 값이다. 유지가 구간을 넘더라도 시작 구간의 가중치를 사용한다. 곡 끝을 넘거나 Response 금지 구간을 가로지르는 유지 슬롯은 통째로 제외한다.

## 슬롯과 입력 상태

`RhythmTime.TicksPerBeat = 4`다. 정박은 4틱, 반 박은 2틱 간격이다. 패턴 조회는 정수 틱으로만 비교하고, 표시/재생 시간으로 바꿀 때 `RhythmTime.Seconds(ticks, bpm)`를 사용한다. 현재는 고정 BPM만 지원한다.

`MusicSlot`은 실제 노트가 아닌 **배치 가능한 선택지**다. 같은 시점의 Tap/Hold/Dive 등의 슬롯은 동시에 존재할 수 있다. 이를 모두 실제 입력으로 표시하거나 슬롯 개수를 몬스터의 점유 박자 수로 계산하면 안 된다.

| 종류 | 시작 전이 | 유지 조건 | 끝 전이 / 움직임 |
|---|---|---|---|
| Tap | Press | 없음 | 없음 |
| Hold | Press | 지정 구간 누름 | 강제 Release 없음 |
| Dive | Press | 지정 구간 누름 | Release |
| Flick | 새 Press 없음 | 순간 슬롯 | 해당 시점에 튕기며 Release |
| Shake | 새 Press 없음 | 지정 구간 누름 | 흔들기, 강제 Release 없음 |

`TouchRequirement`는 다음 단계의 충돌 판정용 메타데이터다. Shake는 이미 누른 상태에서 시작할 수 있고 Flick의 Release는 Dive의 끝과 공유할 수 있다. 실제 손가락 상태 추적, 움직임 판정, 불가능한 입력 중첩 중재는 아직 구현하지 않았다.

## 몬스터 배치에서 사용할 API

`RunSession.Enter()`에서 전투 노드에 진입하면 `BattleMusic`을 한 번 생성한다. 런 시드·필드·행·열로 곡을 선택하므로 같은 노드는 같은 런 시드에서 재현된다. 맵/보상 난수나 UI 조회 순서는 음악 선택에 영향을 주지 않는다. 다른 전투에서 같은 곡이 다시 선택될 수 있다.

`RunPresenter.BattleRequested`에서 `Session.BattleMusic`을 읽는다. 곡만 끝나 준비 단계로 돌아갈 때는 그대로 재사용하고, 스테이지 전체의 승패가 결정된 후에만 `SubmitBattleResult`를 호출한다. 클리어/게임오버/재시작에서는 `BattleMusic`을 해제한다.

```csharp
// 1박 전조 → Tap, Tap, Tap. 이후 1박 휴식은 다음 단계의 몬스터 배치 로직이 결정한다.
var pattern = new RhythmPattern("three-taps", cueLeadTicks: 4, new[]
{
    new PatternStep(GestureKind.Tap, offsetTick: 0),
    new PatternStep(GestureKind.Tap, offsetTick: 4),
    new PatternStep(GestureKind.Tap, offsetTick: 8)
});

IEnumerable<PatternPlacement> candidates = session.BattleMusic.FindPlacements(pattern);
foreach (var candidate in candidates)
{
    // CueStartTick ~ StartTick: 전조 / StartTick ~ EndTick: 전체 패턴
    // Slots: 패턴의 모든 입력에 대응하는 슬롯 / Weight: 후보의 상대 가중치
    // 실제 설치 확률, 스킵/연속/휴식, 다른 몬스터와 충돌은 배치 로직에서 결정한다.
}
```

패턴은 최소 하나의 입력을 포함하고 첫 입력 오프셋은 0이어야 한다. `CueLeadTicks`는 양수다. 유지 동작은 양수 길이, Tap/Flick은 0 길이다. 같은 종류·오프셋·길이의 중복 입력은 거부하고, 서로 다른 동작의 같은 시점 입력은 보존한다.

`FindPlacements`는 시간 순서대로 **완전한 패턴 후보**만 반환한다. 단순히 다음 빈 슬롯을 모으지 않는다. 입력 종류/상대 간격/유지 길이가 전부 맞아야 하고, 전조가 곡 시작 전으로 나가거나 패턴이 곡 끝을 넘으면 제외한다. Dive의 끝에 Flick을 겹치려면 그 정확한 틱의 Flick 슬롯도 있어야 한다. 각 후보의 가중치는 대응 슬롯 가중치의 산술 평균이다. 이를 바로 확률로 해석하거나 하이라이트 출현 보장으로 해석하지 않는다.

조회는 슬롯을 점유하지 않으며 같은 결과를 반복 조회할 수 있다. 전조 구간 `[CueStartTick, StartTick)`의 선행 시간은 확보하지만 전조 신호 자체와 다른 패턴 전조와의 연출 중재는 아직 없다. 몬스터별 공격 계획 확정, 점유 박자 수 비교, 충돌된 묶음 철회는 다음 구현 단계다.

## Unity에서 확인

`RunMap` 실행 → 탐험 시작 → 전투 노드 → **개발용 슬롯 미리보기 / 슬롯 펼치기**. 이전/다음 곡으로 다섯 샘플을, 이전/다음 마디로 인트로와 하이라이트를 확인한다. 세로선은 정박/엇박, 가로막대는 유지 구간, 붉은 표시는 Release다. 3연 Tap에 1박 전조를 붙인 예시의 전체/현재 마디 배치 후보 수도 표시한다.

미리보기에서 곡을 변경해도 상단의 이번 전투 곡은 유지된다. 이 화면은 개발용이며, 실제 플레이어의 준비 화면은 합의대로 몬스터 패턴과 무기 편성을 보여줄 예정이다. `Show Battle Test Controls`를 끄면 미리보기와 테스트 결과 버튼 모두 숨겨진다.

독립 실행 핵심 테스트 28개 중 음악 테스트는 11개다. Unity PlayMode에는 전투 이벤트 시점의 음악 준비, 미리보기의 CanvasRenderer/도형 생성, 샘플 탐색 중 전투 곡 유지, 클리어 후 해제 검증을 추가했다. 작업 환경에 Unity Editor가 없어 PlayMode 실행과 실제 기기 확인은 아직 하지 못했다.
