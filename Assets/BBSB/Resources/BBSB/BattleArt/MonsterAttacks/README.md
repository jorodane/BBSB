# 몬스터 공격 이미지 넣기

전체 파일명·동작·위치 표: [MonsterAttackArt.md](../../../../../../Docs/MonsterAttackArt.md).

1. 아래 몬스터 ID → 패턴 ID → `step-0` 등의 폴더에 투명 PNG를 넣는다.
2. `travel.png` 한 장부터 적용할 수 있다. 생성 `spawn.png`, 대기 `wait.png`, 접촉 `contact.png`, 판정 결과 `perfect.png` / `half-miss.png` / `miss.png`는 선택해서 추가한다.
3. 여러 프레임은 `travel-0.png`, `travel-1.png`처럼 0부터 연속 번호를 쓴다. 단일 이미지와 번호 프레임이 함께 있으면 번호 프레임을 우선한다.
4. 몬스터 본체는 종족 폴더의 `idle.png`, 각 패턴의 `body/call-0.png`, `body/attack.png`, `body/recover.png`로 연결한다.
5. `Display.asset` Inspector에서 공격 슬롯을 선택해 크기·시작 위치·접촉 위치·상태별 이미지 보정을 조절한다.

이미지를 넣고 Unity의 임포트가 끝난 후 전투 또는 준비 화면의 연습을 새로 시작하면 적용된다. 아직 파일이 없는 단계는 종족별 임시 도형으로 표시한다. 원본 알파를 그대로 쓰며 배경 제거는 수행하지 않는다.
