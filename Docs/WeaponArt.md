# 등급별 무기 이미지

## 속성 이미지와 13종 무기 확장 팩

`BBSB_AttributeWeapons_13Expansion_20260918.zip`의 `Assets` 폴더를 프로젝트에 합치면 최신 코드가 자동으로 사용한다. 새 경로는 `Assets/BBSB/Resources/BBSB/WeaponAttributeArt/<weapon-id>.png`다. 별도 프리팹 배선이나 배경색 제거가 필요 없다. 신규 13종의 패턴·배치·지원 효과·보상은 [WeaponExpansion.md](WeaponExpansion.md)에 있다.

이번 팩은 **30개 PNG, 111개 무기·속성 조합, 모션을 포함한 135개 스프라이트 영역**이다.

- 일반 24종: 한 PNG 안의 좌상 빛, 우상 어둠, 좌하 이면, 우하 혼돈.
- 활·석궁·마도봉 3종: 가로 빛·어둠·이면·혼돈, 세로 대기·준비·발사. 기존 전투와 5라인 전투 모두 모션을 선택한다.
- 양면 지팡이·일식 거울·혼돈의 진자: 전용 속성의 독립 PNG.

빛은 백색·금색·태양, 어둠은 남색·보라·초승달, 이면은 두 재질과 일식 장식, 혼돈은 비대칭 파편·분홍·청록으로 구분한다. 등급은 이름·테두리·기존 행동 수로 표시하며 속성 그림 자체를 등급마다 반복 제작하지 않는다. 미리 포함해 둔 비반복 무기의 혼돈 그림은 이제 [두 구간 혼돈 무기 21종](LongWeaponPatterns.md)에 사용한다. 이 21종을 재전달하는 `BBSB_Chaos_Weapon_Art_21.zip`은 원본 PNG·기존 `.meta`를 그대로 담으며, 기존 30종 팩이 있으면 다시 설치할 필요가 없다.

속성 팩이 있으면 우선 사용하고, 없으면 기존 등급 그림 → 공통 그림 → 같은 계열 그림/코드 실루엣으로 표시한다. 도감·편성·보상·상점·전투의 `WeaponIconGraphic`이 동일한 선택 경로를 사용한다. 새 패턴은 이미지 파일 유무와 독립적으로 실행된다.

PNG 원본은 편집·리사이즈·알파 변환 없이 전달한다. `WeaponAttributeArtBounds.cs`와 `weapon-attribute-art-manifest.json`은 실제 비어 있는 칸 경계와 그림 범위를 기록하고, Unity가 그 영역을 스프라이트로 선택한다. 표시 범위 측정은 기존 미리보기 도구와 같은 알파 8/255 이상 및 2px 여백 기준이며, 미세한 알파 잔여물을 원본에서 삭제하지 않는다. 배경과 내부 빈 구멍의 검사는 실제 알파 0을 확인한다. 임포터는 최대 2048px, NPOT 원본 크기, RGBA32, 밉맵 없음으로 설정한다.

검증 명령:

```bash
python Tools/validate_attribute_weapon_art.py /path/to/unzipped-pack
```

검사는 30개 원본 해시, 실제 투명 픽셀과 불투명 본체, 135개 영역의 잘림 여부, 미리보기 범위, 내부 구멍 샘플을 확인한다. Unity Test Runner의 `InstalledAttributePackUsesTheCorrectVariantAndPoseInEveryUiIcon`은 팩 설치 후 속성·모션·아이콘 연결을 확인한다. PNG가 없는 환경에서는 이 팩 전용 검사만 생략한다. Unity Editor에서의 실제 임포트·PlayMode 실행은 별도 확인이 필요하다.

## 설치

1. `codex/roguelike-foundation`의 최신 코드를 받는다.
2. 전달된 `BBSB_weapon_art_v1.zip`을 풀고 안의 `Assets` 폴더를 Unity 프로젝트의 `Assets`에 합친다.
3. Unity가 PNG를 가져오면 준비·장비·보상·상점·전투가 같은 등급별 이미지를 사용한다. 폰트 파일도 함께 합쳐 새 등급 이름을 표시한다.

원본은 실제 알파 채널이 있는 투명 PNG다. 이미지·음원은 사용자가 직접 업로드한다. PNG가 없는 동안에는 기존 실루엣을 표시한다.

## 경로와 소켓 수

공통 폴더는 `Assets/BBSB/Resources/BBSB/WeaponArt/`다.

| 무기 폴더 | 일반 `common.png` | 희귀 `rare.png` | 영웅 `epic.png` | 전설 `legendary.png` |
|---|---:|---:|---:|---:|
| `sword` 한손검 | 1 | 2 | 2 | 3 |
| `spear` 창 | 1 | 2 | 2 | 3 |
| `hammer` 해머 | 1 | 2 | 2 | 3 |
| `dagger` 단검 | 1 | 2 | 2 | 3 |
| `greatsword` 대검 | 1 | 2 | 2 | 3 |
| `blade` 비검 | 1 | 2 | 2 | 3 |
| `bell` 진동 방울 | 1 | 2 | 2 | 3 |
| `shield` 방패 | 1 | 1 | 1 | 2 |

소켓은 위에서 아래, 같은 줄에서는 왼쪽에서 오른쪽 순서로 `WeaponDefinition.Actions`에 대응한다. 같은 등급에서 강화하면 피해·방어 수치만 바뀌고 이미지·소켓 수는 유지한다. 소켓별 행동과 효과는 [Weapons.md](Weapons.md)에 있다.

## 가져오기와 발광

`WeaponArtImporter`는 실제 알파를 유지하는 Single Sprite·Full Rect·중앙 피벗·Bilinear·Clamp를 설정한다. 게임에서 보이는 크기에 맞춰 근접·방어 무기는 최대 512px, 원거리 아틀라스는 최대 1024px로 가져오며 ZIP의 원본 해상도는 유지한다. 불투명 파일은 오류를 표시한다. 배경색 제거는 수행하지 않는다.

소켓 중심과 반지름은 `WeaponArtLayout.cs`에 이미지별로 기록한다. 원점은 이미지 왼쪽 아래이고 모든 좌표는 0~1 범위다. 원형 소켓의 약한 원근 차이를 맞추도록 가로·세로 반지름을 각각 보관한다. 무기와 발광은 같은 변환을 사용하며, 빛은 실제 성공 판정에 따른 해당 행동의 발동 시간에 반응한다.

## 검증

Pillow 설치 후 다음 명령으로 실제 투명 픽셀과 소켓 위치를 검사한다. 검사는 원본 파일을 변경하지 않는다.

```bash
python Tools/validate_weapon_art.py
python Tools/validate_weapon_art.py /path/to/unzipped-pack
```

`weapon-art-manifest.json`은 32개 파일과 61개 소켓의 대응표다. 이미지를 수정해 소켓 위치가 바뀌면 이 파일과 `WeaponArtLayout.cs`를 함께 갱신한다. 프로젝트의 모든 새 이미지는 `AGENTS.md`의 투명 PNG 제작·검사 규칙을 따른다.

CoreChecks는 등급·강화 조합, 보상·구매·교체의 등급 보존, 소켓 수와 좌표, 발광 시간 함수를 검사한다. Unity Test Runner의 PlayMode `WeaponArtIntegrationTests`는 이미지 44개의 실제 임포트·알파·등급별 연결과 소켓 부착을 검사한다. 이 작업 환경에는 Unity Editor가 없어 PlayMode 실행과 실제 화면 확인은 수행하지 못했다.

임포터 API는 Unity 6.3의 [OnPostprocessTexture](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetPostprocessor.OnPostprocessTexture.html)와 [플랫폼 설정](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/TextureImporter.GetDefaultPlatformTextureSettings.html)을 기준으로 확인했다.

## 원거리 확장 팩

`BBSB_effects_ranged_v1.zip`을 추가로 합치면 활·석궁·마도봉의 4등급과 대기·준비·발사 동작이 연결된다. `bow`, `crossbow`, `wand` 폴더의 각 PNG는 세 칸짜리 아틀라스다. 원거리의 소켓 수는 등급순 1·2·2·3이며 프레임별 좌표는 `RangedWeaponArtLayout`에 있다. 기존 32개 파일 명세와 별도로 `battle-effects-manifest.json`이 새 12개 아틀라스와 이펙트 10개를 관리한다. 설치·검증은 [BattleEffects.md](BattleEffects.md)를 참고한다.

## 행동 기호 확장

`BBSB_gesture_icons_v1.zip`을 추가하면 작은 소켓의 상시 기호와 큰 Response 안내를 같은 문양으로 표시한다. 기호와 발광은 무기 변환을 그대로 상속하며, 발광 중에도 기호가 사라지지 않는다. [GestureIcons.md](GestureIcons.md)를 참고한다.

## 봉·신령 방울 추가 팩

`BBSB_Staff_SpiritBell_20260917_v1.zip`의 `Assets` 폴더를 프로젝트에 합친다. 봉 `staff/common.png`와 신령 방울 `spirit-bell/common.png`는 `Assets/BBSB/Resources/BBSB/WeaponArt/` 아래에 설치된다. 두 이미지 모두 실제 알파가 있는 1024×1536 PNG다. 새로운 이름의 `령` 등이 누락되지 않도록 최신 UI 글꼴과 라이선스도 함께 포함한다.

두 무기는 준비·편성·보상·상점·전투의 기존 `WeaponIconGraphic`에 연결되어 있다. 등급별 전용 파일이 없으면 이 공통 이미지를 사용하며 등급 이름·테두리·강화 수치는 유지한다. 현재 팩은 등급마다 다른 그림을 제공하지 않는다. PNG를 아직 설치하지 않은 환경에는 봉 또는 세 방울의 코드 실루엣을 표시하므로 코드만 받아도 동작한다.

완성 PNG는 생성 원본의 알파를 그대로 보존했다. 검증은 배경·내부 빈 공간의 투명 픽셀, 본체 불투명도, 소켓 위치를 검사하며 배경색 제거를 하지 않는다. 신규 이미지·글꼴은 ZIP으로 전달하고 Git에는 로직·연결 코드·문서를 반영한다.
