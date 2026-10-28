> 현재 규칙: 65초마다 난이도가 증가하는 무한 모드입니다. 원본 65초 생성 일정을 반복하고, 각 일정의 생성 수를 1배→2배→3배로 늘립니다(주기당 89/178/267개). 추가 생성은 별도로 위치를 추첨합니다. 기존 적·탄환은 주기가 바뀌어도 유지되며 HP3·탄환 피해1·이동 속도는 변하지 않습니다. 사망 또는 평가 시간 제한으로 종료합니다. 아래의 65.008초 완주/상한 설명은 이전 유한 모드 기록이며 현재 적용되지 않습니다. 이 무한 증가 규칙은 요청에 따라 추가한 것으로 원본 규칙이 아닙니다. 빌드·테스트·평가는 실행하지 않았습니다.
# GroundhogAI

Unity **6000.5.5f1 / Universal 2D**를 대상으로 하는, Pipeline **0.9.1-exp.1** 기반의 Groundhog Release 5 정적 분석 이식과 생존 AI입니다. 제공된 원본 실행 파일을 디스어셈블하고 동봉 레벨 데이터를 읽었습니다. 플레이 검증, 테스트 작성·실행, AI 평가와 자동 튜닝은 수행하지 않았습니다.

**현재 전달 상태:** 소스와 씬, 평가 스크립트까지 준비했습니다. 사용자 요청으로 진행 중이던 Windows 빌드를 중단했으므로 실행 빌드는 제공하지 않습니다. 프로젝트 버전과 빌드 스크립트를 6000.5.5f1로 변경했습니다. 현재 컴퓨터에는 6000.3.10f1만 설치되어 있어 6000.5.5f1에서의 최초 임포트·패키지 갱신·컴파일은 수행하지 않았습니다. 앞서 성공한 컴파일과 MCP 씬 저장은 6000.3.10f1에서 수행한 것입니다. Unity Hub에서 6000.5.5f1을 설치하고 이 프로젝트를 해당 버전으로 열면 됩니다.

## 실행

Unity 6000.5.5f1에서 사용자가 Windows 빌드를 생성한 다음, PowerShell에서 이 프로젝트 폴더로 이동해 실행합니다. 지금 바로 확인하려면 Unity에서 `Assets/Scenes/GroundhogAI.unity`를 열어 Play를 사용하세요.

```powershell
.\Launch.ps1
.\Launch.ps1 -Seed 42 -AIConfig .\Configs\ai-default.json
```

실행 파일은 `Build\Windows\GroundhogAI.exe`입니다. EXE만 옮기지 말고 `Windows` 폴더 전체를 유지하세요. AI가 기본으로 플레이합니다. 사망 또는 레벨 완료 후 새 시드로 다음 판을 시작하고 개체·탄환·시간·정책 상태를 초기화합니다. 화면 아래의 마지막 결과는 표시용이며 게임 상태에 영향을 주지 않습니다.

- `Tab`: AI / 수동 전환
- `Space`: 일시 정지
- `R` 또는 Restart seed: 현재 시드로 새 판 시작, AI 설정 다시 읽기
- 수동 조작: WASD/방향키, 마우스 조준, 왼쪽 클릭 사격

Unity에서는 `Assets/Scenes/GroundhogAI.unity`를 열어 Play하면 됩니다. Editor의 기본 설정 파일은 `Assets/StreamingAssets/ai-default.json`입니다. EXE 직접 실행의 기본 파일은 `GroundhogAI_Data/StreamingAssets/ai-default.json`이며, `Launch.ps1`과 `Evaluate.ps1`은 수정하기 쉬운 `Configs/ai-default.json`을 명시적으로 전달합니다.

## 사용자가 실행하는 평가

```powershell
.\Evaluate.ps1 -Episodes 20 -Seed 1 -MaxSeconds 120 `
  -AIConfig .\Configs\ai-default.json `
  -OutputPath .\Evaluation\baseline
```

연속 시드 1~20으로 실행합니다. 기존 보고서가 있는 폴더는 덮어쓰지 않습니다. 결과는 `episodes.json`, `episodes.csv`, `summary.json`, 실제 적용된 `ai-config.json`, `player.log`입니다. 판별 생존 시간·처치 수·종료 원인·AI 버전/SHA256·규칙 버전을 저장하고 평균·중앙값·최장 시간을 출력합니다. 종료 원인은 `enemy_contact`, `stage_completed`, `time_limit`입니다. 시간 제한은 16ms tick 단위로 도달하므로 요청값보다 최대 한 tick 미만 길 수 있습니다.

**원본 기본 레벨은 유한합니다.** `levels.xml`의 최초 진입 경로가 선택하는 `level_cluster_small.txt`는 65초, 53개 생성 이벤트, 전개 후 총 89개입니다. 마지막 생성 행은 51.2초이고, 65.008초 tick에서 생존했다면 완료됩니다. 따라서 65.008초는 이번 모드의 완주 상한이며 AI의 무한 생존 성능을 뜻하지 않습니다. 임의의 시간별 생성 간격 감소나 무한 반복은 추가하지 않았습니다.

설정 필드의 의미와 정책 교체 방법은 [AI_SETTINGS.md](AI_SETTINGS.md)를 보세요. 설정 수정은 재빌드 없이 다음 실행 또는 Restart seed에 적용됩니다.

## 보존한 규칙과 요청에 따른 변경

- 1280×720 논리 좌표, 16ms tick, 원본 축별 가속·감속, 사격 중 속도 감소, 112ms 연속 발사 간격을 옮겼습니다.
- 일반 직진형 41개는 원본 직진 계산을 유지합니다. 강화형·군집형 슬롯 48개는 원본 일반 추적형의 속도 2.5px/tick, 회전 한도 0.035rad/tick을 사용하는 독립 개체로 대체합니다. 군집의 초기 생성 시점·개수·위치는 유지하지만 집단 이동 제어는 없습니다.
- 적 HP 3, 탄환 피해 1. 탄환은 첫 명중에 소멸하고 정확히 세 번 명중한 적이 사라집니다. 플레이어는 접촉 한 번에 사망합니다. 적의 사격은 없습니다.
- 원본 생성 일정, 한 tick당 한 이벤트, 시점의 엄격한 `>` 비교, 화면 밖 75px에서 생성하는 여섯 난수 계산 순서와 Park–Miller 난수 발생기를 사용합니다.
- 시간 되감기, 과거 플레이어 잔상과 이전 판의 전투 상태는 없습니다. 기본 레벨 완료 후 다음 원본 레벨로 넘어가지 않고 새 에피소드를 시작합니다.

분석 근거는 상위 폴더의 `Analysis/original-spawning.md`, `Analysis/PLAYER_RULES.md`, `Analysis/ENEMY_RULES.md`에 있습니다. 원본 전체 프로그램은 효과에도 전역 난수를 사용합니다. 효과와 일부 적 유형을 제거했으므로 원본 EXE와 동일 시드의 전체 궤적이 일치한다고 주장하지 않습니다. x87 부동소수점과 C# 연산 차이, 이벤트/충돌 처리 순서도 원본 전체 실행의 비트 단위 재현 범위에는 포함하지 않습니다. 게임을 실행해 비교한 결과가 아니라 정적 분석에서 확인한 규칙을 옮긴 구현입니다.

## 코드와 빌드

- `Assets/Scripts/Simulation`: `Simulation.Reset(seed)`, `Step(action)`, `GetObservation()`, 원본 규칙 및 생성 일정
- `Assets/Scripts/AI`: `IAgentPolicy.Decide(observation)`, `HeuristicPolicy`, 외부 JSON 설정
- `Assets/Scripts/Presentation`: 시계·에피소드 관리와 읽기 전용 화면 표시
- `Assets/Scripts/Evaluation`: 화면 없이 실행하는 평가 및 보고서
- `Assets/Editor/ProjectBuilder.cs`: 씬 구성 및 Windows 빌드

게임 로직은 Unity GameObject, 렌더링이나 AI 가중치에 의존하지 않습니다. 좌표는 픽셀, Y축은 아래 방향, 관측 속도는 px/s입니다. 이동 입력은 원본 키보드처럼 축별 부호를 사용하고 대각선 정규화는 하지 않습니다. 조준은 방향 벡터입니다. 정책의 미래 예측은 의사결정을 위한 근사이며 실제 게임 규칙을 수정하지 않습니다.

```powershell
# 이 프로젝트의 Unity Editor를 닫은 뒤 실행
.\Build.ps1

# 편집 명령을 받을 백그라운드 Editor 시작 (Play 실행 아님)
unity open . --batchmode --until-ready
unity command --project-path . --query script --format json

# Unity 내장 MCP stdio 서버 (MCP 클라이언트에서 연결)
unity mcp --project-path .
```

상위 폴더의 `Tools/unity-mcp.mjs`는 Unity 내장 MCP에 직접 연결하는 작은 stdio 클라이언트입니다. Codex 전역 설정이나 네트워크 보안 설정은 변경하지 않습니다.

원본 이미지 일부는 사용자가 제공한 `groundhog/data/gfx`에서 가져왔습니다. 이 프로젝트는 새로운 원본 에셋 배포 권한을 부여하지 않습니다.
