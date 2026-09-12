# clock-spirit / clock-seven-beat-wait

첫 Call 시각을 0박으로 센다. 4틱 = 1박. 모든 위치는 `Display.asset`에서 보정할 수 있다.

| 폴더 | 입력 | 생성 시점 | 접촉 시점 | 유지 | 이동 | 기본 높이 |
|---|---|---|---|---|---|---|
| `step-0/` | Tap | 0박 | 7박 | 0박 | Walk | 플레이어 높이 × 0.52 |

각 단계: `spawn.png`, `wait.png`, `travel.png`, `contact.png`, `perfect.png`, `half-miss.png`, `miss.png`. 필요한 파일부터 넣으면 된다.

본체 교체: `body/call-0.png` (0박), `body/attack.png`, `body/recover.png`.

[공통 규격](../../README.md).
