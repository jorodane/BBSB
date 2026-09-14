# 몬스터 에디터

Unity 메뉴 **BBSB → Monster Editor**에서 몬스터를 추가한다. 프로젝트 창에서는 **Create → BBSB → Monster**로도 만들 수 있다. 유효한 사용자 몬스터는 다음 Play에서 출현 목록과 도감에 함께 등록된다.

## 기존 몬스터 편집

기존 **12종 / 24개 패턴**도 `Assets/BBSB/Resources/BBSB/Monsters/<몬스터 ID>.asset`에 각각 저장되어 있다. Monster Editor의 **등록된 몬스터** 목록에서 이름으로 고르면 같은 탭에서 외형·패턴·출현 가중치·피해를 수정할 수 있다. 새로 생성하거나 데이터를 복사할 필요가 없다.

- Play와 빌드의 출현 목록은 이 폴더의 유효하고 활성화된 에셋만 사용한다. 기존 몬스터도 출현 옵션을 끄거나 에셋을 삭제하면 제외된다. 수정 후 다음 Play 또는 빌드부터 적용된다.
- 기존 종족 ID와 패턴 ID를 유지해야 이미 등록된 전용 이미지 슬롯이 계속 연결된다. 이름과 설명은 자유롭게 변경해도 된다.
- 기존 에셋은 **기존 리소스 몸체 모션 사용**이 켜져 있다. 이미 등록한 Call·Attack·Recover와 전용 공격 연출을 유지한다. 직접 만든 Controller 또는 UI 외형 Prefab을 연결하면 새 외형이 우선한다. 연결을 비우면 기존 모션으로 돌아간다.
- 네코마타(`seesaw-goblin`)는 **Beat Shift** 배치 방식과 전환 전 기본 Call 3회를 유지한다. 다른 종족은 Independent다. 해당 방식의 패턴 조건은 패턴 탭과 저장 검증에서 확인한다.
- 기본 모습 Sprite를 바꾸고 전투에서도 정지 외형으로 사용하려면 기존 리소스 몸체 모션 옵션을 끈다. 옵션이 켜져 있으면 기존 모션 Sprite가 전투 외형을 결정한다.

개발 참고: `MonsterCatalog.BuiltIn`은 Unity 없이 실행하는 시뮬레이션의 기본값과 기존 투사체 연출 키의 기준으로 남는다. Unity 런타임은 에셋 전체 목록으로 교체하며 이 기본값을 추가로 붙이지 않는다. 에셋의 편집 내용을 코드 기본값으로 덮어쓰는 자동 생성 과정은 없다. `MigratedAssetsPreserveEveryPatternPlannerAndPortrait` 테스트는 초기 이전 값의 회귀 기준이므로 몬스터의 밸런스나 외형을 의도적으로 바꿀 때 그 기준도 함께 갱신한다.

## 1. 기본 모습

1. **새 몬스터**를 누른다. 에셋은 `Assets/BBSB/Resources/BBSB/Monsters`에 생성된다.
2. 이름, 설명, 주요 입력, 출현 가중치와 판정 기본 피해를 설정한다. 고유 ID는 소문자·숫자·하이픈을 사용하고 다른 에셋과 겹치지 않게 한다.
3. **기본 모습 / 도감 Sprite**에 투명 PNG에서 임포트한 Sprite를 등록한다. Sprite Editor의 Pivot은 발 접지점에 맞춘다. 도감 썸네일과 애니메이션 미등록 시 외형에도 이 Sprite를 쓴다.
4. 크기와 위치 보정으로 전투 배치를 조정한다. 위치 보정은 몸 높이 기준이다.

`출현 목록에 등록`을 끄면 제작 중인 몬스터를 전투와 도감에서 제외할 수 있다. 테스트 장면에서는 이 옵션과 관계없이 선택한 에셋을 연습할 수 있다. 출현 가중치가 높으면 선택될 가능성이 높아진다. 해당 곡의 리듬 소켓에 패턴을 배치할 수 있어야 출현하므로 모든 곡에 강제로 등장하지는 않는다. 기존 스테이지 계획은 재생 중에 바뀌지 않는다.

## 2. 패턴

패턴 탭에서 패턴을 추가하고 Call 신호와 Response 입력을 편집한다. 타임라인의 금색 표시는 Call, 청록색 표시는 Response다. 유지 입력은 구간으로 표시된다.

| 설정 | 의미 |
|---|---|
| Call → Response 간격 | 첫 Call부터 첫 Response까지의 박자 수 |
| Response 구간 길이 | 모든 Response와 유지 종료를 포함하는 구간 |
| 이후 휴식 | 해당 구간 뒤의 휴식 |
| Call 시작 정렬 단위 | 1박이면 정박, 0.5박이면 반 박, 0.25박이면 4분의 1박 단위로 배치 가능 |
| 긴 무음 대기 | 보통 0. 설정할 때는 마지막 Call에서 첫 Response까지 4박 이상 |
| 패턴 참여 확률 | 배치 가능한 후보 중 이 패턴을 제출할 확률 |

시각은 **0부터 시작하는 상대 박자**이며 0.25박 단위로 반올림한다. Call 시각은 Call 시작 기준이고 Response 시각은 Response 시작 기준이다. 첫 Call과 첫 Response의 상대 시각은 각각 0이어야 한다. Call은 Response 시작 전에 모두 끝나야 한다.

예: Call → Response 간격 2박, Call 시각 0박·1박, Response Tap 시각 0박·0.5박이면 전체로는 Call 0박·1박, Tap 2박·2.5박이 된다. Response 구간을 1박으로 잡으면 이후 휴식은 전체 3박부터 시작한다.

- Tap/Flick/Shake: 한 시점에 입력한다.
- Hold/Dive: 유지 길이를 지정한다. 시작과 종료를 모두 포함하도록 Response 구간을 잡는다.
- 각 Call은 기존 합성음 종류와 기본 동작을 선택할 수 있다.
- 패턴 사이의 Call 구분, 중복 입력과 병행 불가능한 입력은 기존 게임 규칙으로 검증한다. 긴 무음 대기 패턴은 한 몬스터당 하나만 허용한다.

## 3. Animator로 모션 등록

**Animator 모션 → Animator · Clip · UI Prefab 기본 틀 생성**을 누르면 몬스터 에셋 옆에 새 폴더가 생긴다. Controller, 9개 Clip, UI Prefab을 만들고 자동 연결한다. 재생성할 때 기존 파일을 덮어쓰지 않고 새 폴더를 만든다.

초기 Clip은 모두 기본 Sprite를 유지하는 자리 표시자다. **외형 Prefab 열기**로 Prefab Mode에 들어가 `Visual` 루트를 선택하고 Animation 창에서 각각의 Clip에 Sprite 교체·자식 위치·회전·크기를 기록한다. **Animator 열기**에서는 각 State의 Motion에 직접 만든 Clip을 등록할 수도 있다.

| 상황 | 재생 시점 |
|---|---|
| Idle | 콜이나 공격이 없는 대기 |
| Call | 각 Call 시각. 다음 Call 또는 첫 Response까지 |
| Attack | 각 Response 시작. Hold/Dive는 유지 구간까지 |
| Recover | Attack 구간 뒤 반 박 |
| Hit | 실제 무기 타격이 몬스터에 닿았을 때 |
| Perfect | 플레이어가 해당 몬스터의 Response를 퍼펙트로 처리했을 때 |
| HalfMiss | 플레이어의 반미스 확정 |
| Miss | 플레이어의 미스 확정; 몬스터의 공격 성공 반응으로 활용 |
| Defeated | 적 전체 HP가 0이 된 뒤 마지막 판정 구간을 마무리했을 때 |

상황별 모션에는 `Base Layer.Call`처럼 **레이어를 포함한 전체 상태 경로**와 재생 길이(박), 반복 여부를 지정한다. 같은 상황의 상태를 중복 등록하지 않는다. 패턴별 Attack/Recover 상태, Call별 상태는 패턴 탭에서 덮어쓸 수 있다. 이때 재생 길이와 반복 여부는 해당 상황의 공통 설정을 사용한다. 비워두면 공통 상태를 사용한다.

애니메이션은 현재 곡 시각으로 정규화 시간을 계산해 샘플링한다. 일시정지 중에는 같은 자세를 유지하고 연습 반복은 처음 상태로 돌아간다. 비반복 모션은 설정한 길이 후 마지막 프레임을 유지한다. Attack이 길게 유지돼야 하면 반복 모션을 쓸 수 있다. 새 콜과 최근 사건이 이전 반응을 교체하며 같은 시각의 실제 타격은 판정 반응보다 우선한다.

### 외형 Prefab 규칙

- 현재 전투는 Canvas 기반이므로 **UI Image**를 쓴다. SpriteRenderer나 3D 모델 프리팹을 그대로 넣는 방식은 지원하지 않는다.
- 루트는 RectTransform, 기준 높이는 512, 기준 발 위치는 아래 중앙이다. 루트에 Animator 하나를 두고 자식 `Portrait` 또는 직접 만든 자식 Image 계층을 움직인다.
- 전투 위치와 피격 흔들림은 바깥 래퍼가 맡는다. 외형 애니메이션은 그 안의 자식 계층에 작성한다.
- Controller는 Base Layer 하나와 상태별 Clip으로 구성한다. 자동 Transition은 만들지 않는다. 코드가 상황별 상태와 재생 위치를 지정한다.
- 루트 모션과 Animation Event는 재생하지 않는다. 입력 판정·피해·투사체 생성은 기존 전투 로직이 담당한다. StateMachineBehaviour에도 전투 로직을 넣지 않는다.
- 지정한 상태를 찾지 못하면 경고를 한 번 남기고 Idle로 돌아간다. Animator가 없으면 등록한 Sprite 또는 UI Prefab을 기본 동작과 함께 표시한다.

커스텀 몬스터의 공격 궤적은 기본 투사체 표시를 사용한다. 이번 에디터의 Animator 연결 대상은 몬스터 몸체다. 개별 투사체 이미지 제작은 기존 `MonsterAttacks/<몬스터 ID>/<패턴 ID>/step-N` 슬롯 규칙을 사용할 수 있다. 기본 12종의 전용 공격 연출은 그대로 유지된다.

Animator API 참고: [Animator.Play](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.Play.html), [Animator.Update](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator.Update.html).

## 4. 검증과 테스트

**설정 검증 및 저장**으로 패턴·ID·Prefab·Animator 경로를 확인한다. 에셋이 `Resources/BBSB/Monsters` 밖에 있으면 전투에 자동 등록되지 않는다. 유효하지 않은 에셋과 중복 ID는 플레이 시작 시 Console에 이유를 표시하고 등록에서 제외한다.

**선택한 패턴의 테스트 장면 열기**는 현재 장면의 저장 여부를 확인한 뒤 별도의 빈 테스트 장면을 연다. 선택한 몬스터·패턴·BPM이 연결된 `Monster rehearsal` 오브젝트가 생성된다. Play를 누르면 같은 패턴을 반복 연습할 수 있다.

- 상단: Call/Response 타임라인과 현재 시각.
- 전투: 실제 행동 아이콘, 몬스터 외형·Animator, 기본 공격 효과와 플레이어 대응 모션.
- 하단: 몬스터·패턴 이름과 콤보.
- 우측: 일시정지·처음부터·소리 켜기/끄기.

테스트는 별도의 RhythmRound를 사용하므로 탐험 HP·보상·저장 데이터에 영향을 주지 않는다. 유지 입력 중 일시정지하면 화면을 다시 눌러 이어간다. 테스트를 마친 뒤 원래 장면은 Project 창에서 다시 연다. 테스트 장면은 필요하면 직접 저장할 수 있다.

자동 검증에는 커스텀 목록의 중복 거부·원자적 갱신, 새 패턴의 기본 공격 연출, Call/Attack/Recover/판정 상태 선택과 반복을 포함한다. Unity 테스트에는 에셋 변환·등록/제외·Animator 상태 샘플링과 입력 통과를 추가했다. 작업 환경에는 Unity Editor가 없어 Unity 테스트 실행과 실제 에디터/게임 화면 검증은 별도로 필요하다.
