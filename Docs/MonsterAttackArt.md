# 몬스터 공격 이미지 적용 가이드

**12종·24패턴·36개 원본 공격 단계**에 이미지 자리와 음악 시계 기반 동작이 연결되어 있다. 아직 새 몬스터걸 PNG는 생성하지 않았다. 파일이 없는 공격은 종족별 도형으로 표시하므로 지금도 준비 화면의 연습과 전투에서 경로를 확인할 수 있다.

## 넣을 폴더와 최소 파일

기준 폴더: `Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/`

공격 이미지: `<몬스터 ID>/<패턴 ID>/step-N/`. `N`은 해당 패턴의 원본 Response 순서이며 0부터 시작한다. 여러 무기가 같은 Response에 연결되어도 이 폴더와 공격은 하나다. 같은 몬스터가 여러 번/여러 인스턴스로 등장할 때 리소스는 공유하고 재생 상태는 각각 유지한다.

예: `clock-spirit/clock-seven-beat-wait/step-0/travel.png`에 작은 인형 이미지를 넣으면 7박 걷기에 적용된다. `tap-slime/count-four-tap/step-0/spawn.png`는 각 Call의 젤리 폭발에 사용된다. 모든 대상 폴더는 이미 생성되어 있다.

| 파일명 | 사용되는 시점 |
|---|---|
| `spawn.png` | 공격 생성 직후. 젤리 연쇄형은 각 Call에서 다시 사용 |
| `wait.png` | 생성 후 제자리 대기. 여우불·비늘 조각·실의 급발진 전 |
| `travel.png` | 이동·도약·걷기·신체 전개 |
| `contact.png` | Response 위치에 도착. Hold/Dive/Shake의 유지 구간에도 사용 |
| `perfect.png` | 실제 Perfect 후 파괴·흩어짐·되튕김·회수·밀려남 |
| `half-miss.png` | 실제 HalfMiss 후 비껴나가는 모습 |
| `miss.png` | 실제 Miss 후 플레이어를 때리는 모습 |

`travel.png` 한 장부터 작동한다. 누락된 상태는 travel → spawn → contact 순서로 대체하고, 모두 없으면 임시 도형을 쓴다. 판정 이미지가 빠져 있어도 코드의 이동·확대·색·페이드로 결과를 구별한다. 대기 없는 공격의 `wait`, 꿈먹는 맥의 실체화형의 이동 이미지 등은 만들 필요가 없다.

움직이는 그림은 `travel-0.png`, `travel-1.png`처럼 **0부터 빈 번호 없이** 추가한다. 모든 상태에 같은 규칙을 쓸 수 있다. 번호 클립이 단일 PNG보다 우선한다. `travel-0`처럼 이름 붙인 Sprite Editor Multiple 슬라이스도 읽는다. 기본은 한 박에 2프레임, 결과 클립은 초당 12프레임이다. 같은 몬스터 내 별도 패턴의 이미지는 해당 경로에 각각 넣는다.

새 PNG는 Unity 임포트가 끝난 뒤 전투/연습을 다시 시작하면 자동 연결된다. 실행 중 새 파일 탐색을 반복하지 않는다. 새로운 파일은 Sprite/Single, Full Rect, 원본 알파, Bilinear, Clamp, mipmap 없음, 비압축, 최대 1024로 가져온다. 사용자가 이후 수정한 Sprite 설정과 슬라이스는 재임포트 때 유지한다. 배경 제거·색상 키·런타임 아틀라스 분할은 없다. PNG 자체가 투명해야 한다.

## 그림의 방향과 접점

- 모든 공격은 **오른쪽 몬스터 → 왼쪽 플레이어** 방향으로 그린다. 파일에는 배경·UI·플레이어를 합성하지 않는다.
- 일반 탄은 중앙 기준, 일정한 캔버스/여백으로 상태별 그림을 맞춘다. 기본 표시 크기는 그림의 세로 픽셀 비율 대신 아래 표의 플레이어 기준 높이로 정한다. 가로세로 비율은 유지한다.
- 지면 젤리 폭발과 작은 인형은 **아래 중앙을 접지점**으로 쓰며, 이미지 바닥에 발을 맞춘다. 작은 인형은 기본 높이 0.52, 펀치 접점은 플레이어 높이 0.46이므로 `contact`의 공격 손을 캔버스 높이 약 88%에 둔다. 젤리 폭발은 높이 0.48의 꼭대기 근처가 펀치에 닿는다. 떠 있는 젤리 변형은 중앙 기준이다.
- 길게 뻗는 꼬리·방전·장막·실은 **오른쪽 끝이 몬스터 연결부, 왼쪽 끝이 타격 끝**인 수평 띠로 그린다. 코드가 양 끝 사이로 너비를 늘리고 회전한다. 이 형식은 가로 늘어남을 전제로 하므로 원형 장식이나 얼굴을 띠 안에 넣지 않는다.
- 일반 투사체는 생성 시점의 몬스터 위치에서 출발한다. 몬스터가 이동해도 발사된 탄의 출발점은 끌려가지 않는다. 신체에 붙은 띠는 현재 몬스터 위치를 따라간다.

## 위치와 크기 조절

[Display.asset](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/Display.asset)을 선택하고 `Attack image slot`에서 몬스터/패턴/단계를 고른다. 리소스 경로도 Inspector에 표시한다.

| 설정 | 기준 |
|---|---|
| Player contact sockets | 플레이어 접지점에서 기준 캐릭터 높이 단위로 계산한 펀치·가드·머리·발·밀기 위치 |
| Scale | 해당 공격 단계의 모든 상태에 공통 적용되는 크기 배율 |
| Source Offset | 몬스터 크기 단위로 생성 위치 보정 |
| Target Offset | 플레이어 크기 단위로 도착 위치 보정 |
| Image Offset | 경로는 유지하고 그림만 플레이어 높이 단위로 이동 |
| Frames Per Beat | 결과 이외의 번호 클립 재생 속도 |
| Poses → Scale / Offset / Rotation | 생성·이동·접촉·각 Grade 이미지의 개별 크기·위치·회전 |
| Poses → Override Pivot / Pivot | 필요할 때 해당 상태 그림의 기준점 보정. 신체 띠는 몬스터 연결부가 우측 중앙으로 고정 |

기본 접점은 `(X, Y)`로 펀치 `(0.24, 0.46)`, 가드 `(0.17, 0.47)`, 머리 `(0.05, 0.78)`, 발 `(0.03, 0.08)`, 밀기 `(0.24, 0.47)`이다. 좌표는 화면 비율이 아니라 플레이어 높이 단위다. 지면형은 Y 접점을 바닥으로 바꾸고 그림 안의 타격 부위를 주먹 높이에 맞춘다. `PlayerMotionDisplay.asset`의 캐릭터 크기와 지면을 바꾸거나 16:9 ↔ 16:10으로 바꾸면 이 기준도 같이 적용된다.

## 몬스터별 공격 자리

표의 시간은 **첫 Call = 0박**이며, 7박 경과 후 대응은 준비 화면에서 Call을 1박으로 셀 때 8박에 해당한다. 생성/도착/유지는 `step-0`, `step-1` 순서로 나열한다. 각 패턴 링크에 정확한 파일 경로와 단계별 표가 있다. 생성 이후의 추가 발사/폭발/발걸음은 연출이며 새 Call이나 입력 노트를 추가하지 않는다.

| 종족 / 몬스터 ID | 패턴·폴더 | 공격 / 이동 | 생성 → 도착 (박) | 유지 (박) |
|---|---|---|---|---|
| 젤리 슬라임 소녀 / `tap-slime` | [count-four-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/tap-slime/count-four-tap/README.md) | 젤리 폭발/젤리탄 · Call 간격에 따른 연쇄 도약 | 0 → 3 | 0 |
| 젤리 슬라임 소녀 / `tap-slime` | [tresillo-call-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/tap-slime/tresillo-call-tap/README.md) | 젤리 폭발/젤리탄 · Call 간격에 따른 연쇄 도약 | 0 → 3 | 0 |
| 도자기 골렘 소녀 / `march-slime` | [march-three](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/march-slime/march-three/README.md) | 분리된 장갑 주먹 · 등속 이동 | 0 → 1, 1 → 2, 2 → 3 | 0, 0, 0 |
| 도자기 골렘 소녀 / `march-slime` | [march-spaced](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/march-slime/march-spaced/README.md) | 분리된 장갑 주먹 · 포물선 | 0 → 1, 2 → 3 | 0, 0 |
| 깃춤 하피 / `tresillo-bat` | [tresillo-taps](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/tresillo-bat/tresillo-taps/README.md) | 깃털 묶음 · 포물선 | 0 → 1, 0 → 2.5, 0 → 4 | 0, 0, 0 |
| 깃춤 하피 / `tresillo-bat` | [rotated-tresillo](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/tresillo-bat/rotated-tresillo/README.md) | 깃털 묶음 · 포물선 | 0 → 1, 0 → 2.5, 0 → 3.5 | 0, 0, 0 |
| 여우불 여우요괴 / `offbeat-goblin` | [offbeat-single-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/offbeat-goblin/offbeat-single-tap/README.md) | 여우불 · 대기 후 급발진 | 0 → 0.5 | 0 |
| 여우불 여우요괴 / `offbeat-goblin` | [offbeat-pair](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/offbeat-goblin/offbeat-pair/README.md) | 여우불 · 대기 후 급발진 | 0 → 1.5, 1 → 2.5 | 0, 0 |
| 꿈먹는 맥 소녀 / `drowsy-slime` | [drowsy-quick-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/drowsy-slime/drowsy-quick-tap/README.md) | 꿈방울 · 포물선 | 0 → 1 | 0 |
| 꿈먹는 맥 소녀 / `drowsy-slime` | [drowsy-four-beat-wait](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/drowsy-slime/drowsy-four-beat-wait/README.md) | 꿈방울 · Response에 실체화 | 0 → 4 | 0 |
| 태엽 인형 소녀 / `clock-spirit` | [clock-quick-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/clock-spirit/clock-quick-tap/README.md) | 작은 인형 · 포물선 | 0 → 1 | 0 |
| 태엽 인형 소녀 / `clock-spirit` | [clock-seven-beat-wait](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/clock-spirit/clock-seven-beat-wait/README.md) | 작은 인형 · 한 박씩 걷기 | 0 → 7 | 0 |
| 쌍꼬리 네코마타 / `seesaw-goblin` | [seesaw-steady-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/seesaw-goblin/seesaw-steady-tap/README.md) | 긴 꼬리 · 연속 전개 | 0 → 1 | 0 |
| 쌍꼬리 네코마타 / `seesaw-goblin` | [seesaw-early-finish](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/seesaw-goblin/seesaw-early-finish/README.md) | 긴 꼬리 · 연속 전개 | 0 → 1, 0.5 → 1.5 | 0, 0 |
| 뇌수 소녀 / `spark-bat` | [bat-quick-taps](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/spark-bat/bat-quick-taps/README.md) | 전기탄/방전 · 등속 이동 | 0 → 1, 0.5 → 1.5 | 0, 0 |
| 뇌수 소녀 / `spark-bat` | [bat-hold](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/spark-bat/bat-hold/README.md) | 전기탄/방전 · 연속 전개 | 0 → 1 | 1 |
| 갑각 용인 소녀 / `iron-turtle` | [turtle-long-hold](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/iron-turtle/turtle-long-hold/README.md) | 비늘 방패/조각 · 포물선 | 0 → 2 | 2 |
| 갑각 용인 소녀 / `iron-turtle` | [turtle-hold-tap](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/iron-turtle/turtle-hold-tap/README.md) | 비늘 방패/조각 · 포물선 / 비늘 방패/조각 · 대기 후 급발진 | 0 → 2, 1 → 5 | 2, 0 |
| 장막 라미아 / `diving-ray` | [ray-deep-dive](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/diving-ray/ray-deep-dive/README.md) | 꼬리 장막 · 연속 전개 | 1 → 2 | 4 |
| 장막 라미아 / `diving-ray` | [ray-short-dive](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/diving-ray/ray-short-dive/README.md) | 꼬리 장막 · 연속 전개 | 0 → 1 | 2 |
| 해파리 소녀 / `bubble-spirit` | [one-beat-shake](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/bubble-spirit/one-beat-shake/README.md) | 우산막 · 포물선 | 0 → 1 | 1 |
| 해파리 소녀 / `bubble-spirit` | [two-bubble-shakes](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/bubble-spirit/two-bubble-shakes/README.md) | 우산막 · 포물선 | 0 → 1, 2 → 3 | 1, 1 |
| 실 잣는 아라크네 / `flick-goblin` | [counted-flick](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/flick-goblin/counted-flick/README.md) | 발목 실 · 대기 후 급발진 | 0 → 2 | 0 |
| 실 잣는 아라크네 / `flick-goblin` | [offbeat-flick](../Assets/BBSB/Resources/BBSB/BattleArt/MonsterAttacks/flick-goblin/offbeat-flick/README.md) | 발목 실 · 대기 후 급발진 | 0 → 1.5 | 0 |

젤리 연쇄형은 각 Call 위치에서 spawn 이미지를 사용하고 다음 위치로 도약하며, 마지막 Response 한 번만 받아친다. 하피는 첫 Call에 서로 다른 경로/소요 시간의 깃털 묶음을 함께 발사한다. 골렘·해파리의 후속 발사는 첫 Call에서 결정된 일정에 따른다. 여우불과 비늘 조각은 생성 후 기다렸다가 정해진 짧은 구간에 급발진한다.

태엽 인형은 일반 패턴에서 작은 인형을 던져 한 박 뒤 플레이어 앞에 착지·공격한다. 긴 패턴에서는 바로 내려놓고 매 박 한 걸음씩 걷는다. 일곱 번째 걸음의 착지와 공격이 Response이며, 추가로 한 박 더 기다리지 않는다. 발걸음이 카운트를 돕는 것은 이 공격의 특징이다. 현재 발소리 음원은 추가하지 않았고 기존 Call 음향을 유지한다. 꿈먹는 맥의 긴 낮잠은 해당 종족의 선택으로 중간 접근을 감추고 Response에만 실체화한다. 긴 쉼 전체에 카운트 보조 금지를 적용하지 않는다.

## 몬스터 본체의 교체 자리

| 위치 | 표시 |
|---|---|
| `<몬스터 ID>/idle.png` | 새 종족의 기본 모습. 없으면 기존 BattleArt 초상화 |
| `<몬스터 ID>/<패턴 ID>/body/call-0.png` | 첫 Call 자세 |
| 같은 body 폴더의 `call-1.png`, `call-2.png` | 실제로 존재하는 두 번째·세 번째 Call 자세 |
| 같은 body 폴더의 `attack.png` | 각 원본 Response의 공격/유지 자세 |
| 같은 body 폴더의 `recover.png` | 공격 후 복귀 자세 |

본체도 번호 클립을 사용할 수 있다. 예를 들어 첫 Call의 클립은 `call-0-0.png`, `call-0-1.png`다. 본체 단일 포즈를 우선 제작해도 된다. 본체 캔버스의 하단 중앙에 발을 맞추고, 여백이 있으면 Sprite Editor에서 실제 발 피벗으로 조정한다. 새 본체에는 기존 변종 색조를 덧씌우지 않는다. Call/공격 포즈가 제공되면 합성 동작을 중복 적용하지 않고, idle만 먼저 넣었을 때는 기존 합성 Call 동작으로 보완한다. 현재 몬스터 이름과 패턴 ID는 기존 값을 유지한다.

## 판정·재생 연결

`MonsterAttackCatalog`가 각 단계의 생성 Call, 발사 지연, 경로와 기본 크기를 정한다. `MonsterAttackTimeline`은 라운드의 음악 시각에서 현재 상태·진행률을 계산한다. `MonsterAttackView`가 그 결과를 위치·이미지에 적용하며, `MonsterAttackSprites`와 임포터가 파일을 연결한다.

- Hold/Dive/Shake는 하나의 이미지 슬롯이 요구 구간 전체를 맡는다. 시작·종료를 별도 투사체나 피해로 복제하지 않는다. Shake는 실제 왕복 진행도에 따라 대상이 밀려난다.
- Tap의 성공·반미스 이미지는 기존 짧은 펀치 준비 이후로 연결하며, 그 사이 접촉 이미지를 유지한다. 판정·무기 발동 자체를 지연하지 않는다. 허용된 이른 입력에도 정상 도착 박자까지 접근을 유지한다.
- 결과 이미지는 실제 `RhythmResult`의 Grade로 선택한다. 너무 이른 Miss로 접근 중인 공격을 미리 지우지 않으며, 유지 입력 실패에도 공격의 원래 유지 시간이 남는다. 공미스는 공격 결과나 무기 효과를 추가하지 않는다.
- 결과 연출은 0.32초 후 사라진다. Perfect 반응은 재질에 맞춰 확대/흩어짐, 되튕김, 회수, 밀려남 등으로 구분한다. 방어막 흡수는 기존 방어 효과와 함께 표시한다.
- 모든 재생은 `round.ElapsedSeconds`를 따른다. 일시정지 중 시각은 고정하고 화면 크기/설정 변경에 따른 배치만 다시 계산한다. 긴 프레임 뒤에는 지난 그림을 다시 생성하지 않는다.
- 새 적 공격 표시는 기존 승리 규칙과 함께 중지한다. 원본 판정 수·입력 박자·무기 동시 발동·공유 적 HP와 연습의 세션 격리는 기존 로직을 사용한다.

## 검증 범위

순수 C# 검사에서 전체 슬롯/Call 연결, 여러 BPM의 정확한 도착, 연쇄 폭발, 7박 걷기, 등속·급발진·포물선·실체화, 다중 발사, Grade/공미스, 유지 입력, 일시정지·시간 건너뛰기를 확인한다. Unity PlayMode 검사에는 번호 클립 읽기, 실제 렌더 슬롯의 접점·크기 변경·정지·동시 공격과 긴 쉼의 차이를 추가했다. **현재 작업 환경에는 Unity Editor가 없어 실제 임포트·PlayMode·화면 렌더는 실행하지 못했다.**

임포터의 처음 가져오기 판별은 Unity 6.3의 [AssetImporter.importSettingsMissing](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetImporter-importSettingsMissing.html)을 사용한다. 판정·종족 설계는 [MonsterAttacks.md](MonsterAttacks.md), [MonsterGirls.md](MonsterGirls.md)를 참고한다.
