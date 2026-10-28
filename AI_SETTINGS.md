> 현재 규칙: 65초마다 난이도가 증가하는 무한 모드입니다. 원본 65초 생성 일정을 반복하고, 각 일정의 생성 수를 1배→2배→3배로 늘립니다(주기당 89/178/267개). 추가 생성은 별도로 위치를 추첨합니다. 기존 적·탄환은 주기가 바뀌어도 유지되며 HP3·탄환 피해1·이동 속도는 변하지 않습니다. 사망 또는 평가 시간 제한으로 종료합니다. 아래의 65.008초 완주/상한 설명은 이전 유한 모드 기록이며 현재 적용되지 않습니다. 이 무한 증가 규칙은 요청에 따라 추가한 것으로 원본 규칙이 아닙니다. 빌드·테스트·평가는 실행하지 않았습니다.
# AI 설정과 사용자 평가

이 프로젝트에는 생존을 목표로 하는 초기 휴리스틱 정책이 들어 있습니다. 성능 평가, 자동 튜닝, 학습은 수행하지 않았습니다. 아래 명령은 사용자가 직접 실행합니다.

## 설정 적용

가장 간단한 방법은 프로젝트의 `Configs/ai-default.json`을 수정하고 실행할 때 해당 파일을 지정하는 것입니다. 평가 스크립트는 기본적으로 이 파일을 읽습니다. JSON 설정만 바꾸면 C# 재빌드가 필요하지 않습니다. 변경한 설정에는 구분 가능한 `version`을 붙이세요.

```powershell
# GroundhogAI 프로젝트 폴더에서 실행
.\Launch.ps1 -AIConfig .\Configs\ai-default.json
```

실행 파일에 직접 전달할 수도 있습니다.

```powershell
& .\Build\Windows\GroundhogAI.exe --ai-config "$PWD\Configs\ai-default.json"
```

실행 파일을 인자 없이 열면 빌드에 포함된 `GroundhogAI_Data/StreamingAssets/ai-default.json`을 사용합니다. 이 파일의 프로젝트 원본은 `Assets/StreamingAssets/ai-default.json`입니다. `Configs/ai-default.json`을 수정한 뒤 실행 파일을 더블클릭하는 방식으로는 그 변경이 적용되지 않으므로, 외부 설정을 지정하는 방식을 권장합니다.

설정은 프로그램 시작 시 읽습니다. 실행 중 파일을 수정했다면 다음 실행부터 반영됩니다. 필드 이름과 대소문자를 그대로 유지하고, JSON의 숫자에는 소수점 `.`을 사용하세요. 누락된 필드는 코드 기본값을 사용하며, 범위를 벗어난 설정은 오류로 처리합니다.

## 설정 항목

거리 단위는 원본의 논리 픽셀이고 시간 단위는 초입니다. 월드 크기는 1280×720입니다.

| 필드 | 기본값 | 의미 |
| --- | ---: | --- |
| `version` | `heuristic-v1` | 평가 결과에 기록하는 설정 식별자. 값을 수정할 때 함께 바꾸면 비교하기 쉽습니다. |
| `riskWeight` | 12 | 예측 경로의 적 접근과 접촉 위험에 부여하는 비용입니다. |
| `wallWeight` | 2 | 벽에 가까워지거나 벽 밖으로 향하는 이동 후보에 부여하는 비용입니다. |
| `escapeSpaceWeight` | 3 | 후보 위치에 도착한 뒤 다른 방향으로 빠져나갈 여유를 선호하는 정도입니다. |
| `movementContinuityWeight` | 0.2 | 직전 이동 방향을 유지하는 선호도입니다. |
| `predictionSeconds` | 1 | 이동 후보를 몇 초 앞까지 예측할지 정합니다. 허용 범위는 0.016~10입니다. |
| `predictionSamples` | 6 | 예측 구간을 나눠 위험을 계산하는 표본 수입니다. 허용 범위는 2~32입니다. |
| `safetyMargin` | 30 | 실제 접촉 반경 바깥에 추가로 확보하려는 거리입니다. 허용 범위는 0~1000입니다. |
| `wallMargin` | 80 | 벽 회피 비용이 생기는 거리입니다. 허용 범위는 1~360입니다. |
| `targetThreatWeight` | 3 | 가까이 있거나 접근 중인 적을 공격 대상으로 우선하는 정도입니다. |
| `targetWoundedWeight` | 1.5 | 이미 피해를 받은 적을 마무리하는 우선순위입니다. |
| `targetDistanceWeight` | 1 | 가까운 적을 조준하는 선호도입니다. |
| `targetPersistenceWeight` | 0.3 | 이전 조준 대상을 유지하는 선호도입니다. |
| `decisionIntervalSeconds` | 0.048 | 이동·조준·사격 결정을 갱신하는 최소 간격입니다. 허용 범위는 0.016~2입니다. |
| `movementCandidates` | 8 | 원 둘레에서 샘플링할 입력 방향 수입니다. 허용 범위는 4~64입니다. 원본은 축별 입력 부호를 사용하므로 실제 이동 방향은 최대 8개이며, 중복을 제거하고 정지를 추가합니다. |

가중치는 모두 0~10000 범위입니다. 값을 높인다고 생존 시간이 반드시 증가하는 것은 아닙니다. 예측 길이와 표본 수를 늘리면 정책 계산량도 증가합니다. 판단 간격은 16ms 시뮬레이션 틱에 맞춰 다음 판단 가능한 시점에 적용됩니다.

정책은 이동 후보의 접촉 위험, 벽과의 거리, 후보 위치에서의 탈출 공간을 비교합니다. 적의 관측 속도로 가까운 미래를 예측하고, 플레이어의 관성은 짧은 전이 구간으로 근사합니다. 이 근사는 AI의 판단용이며 적의 실제 이동이나 생성 규칙을 변경하지 않습니다. 조준은 선택한 적의 관측 속도를 이용해 탄환 도착 위치를 예측합니다.

## 평가 실행

`Evaluate.ps1`은 Windows 실행 빌드를 화면 없이 실행합니다. 빌드를 먼저 준비해야 하며, 스크립트 자체가 Unity를 빌드하지는 않습니다.

```powershell
# GroundhogAI 프로젝트 폴더에서 실행
.\Evaluate.ps1 -Episodes 20 -Seed 1 -MaxSeconds 120 `
  -AIConfig .\Configs\ai-default.json `
  -OutputPath .\Evaluation\baseline
```

| 인자 | 기본값 | 의미 |
| --- | --- | --- |
| `-Episodes` | 20 | 실행할 판 수입니다. |
| `-Seed` | 1 | 첫 판의 시드입니다. 이후 판은 1씩 증가합니다. |
| `-MaxSeconds` | 300 | 판당 시뮬레이션 시간 제한입니다. 벽시계 실행 시간 제한이 아닙니다. |
| `-AIConfig` | `Configs/ai-default.json` | 사용할 AI 설정 파일입니다. |
| `-OutputPath` | `Evaluation/<실행 시각>` | 결과를 저장할 폴더입니다. |
| `-Executable` | `Build/Windows/GroundhogAI.exe` | 사용할 Windows 실행 파일입니다. |

각 판은 게임 상태와 정책 상태를 초기화합니다. 재시도 사이에 적, 탄환, 진행 상태를 이어받지 않습니다. 같은 시드 구간과 같은 게임 빌드를 사용하면 AI 설정을 비교하기 위한 입력 조건을 맞출 수 있습니다.

기본 모드의 원본 레벨은 유한한 65초 스테이지입니다. 현재 이식에서는 16ms 틱과 원본의 엄격한 시간 비교에 따라 **65.008초**에 `stage_completed`로 종료됩니다. `-MaxSeconds 120`을 지정해도 원본 스테이지를 무한히 연장하지 않습니다. 사용자가 지정한 시간 제한도 틱 경계에서 처리되므로 최대 한 틱만큼 초과할 수 있습니다.

종료 원인은 다음과 같습니다.

| 값 | 의미 |
| --- | --- |
| `enemy_contact` | 적과 접촉해 사망했습니다. |
| `stage_completed` | 사망하지 않고 원본 스테이지 종료 시점에 도달했습니다. |
| `time_limit` | 사용자가 정한 판당 시간 제한에 도달했습니다. |

스테이지 완료나 시간 제한으로 끝난 판의 생존 시간은 해당 종료 시점까지의 관측값입니다. 그 이후 생존 가능 시간을 측정한 값은 아닙니다.

## 결과 파일

실행이 정상 종료되면 평균·중앙값·최장 생존 시간, 스테이지 완료 횟수와 시간 제한 종료 횟수를 터미널에 출력합니다.

| 파일 | 내용 |
| --- | --- |
| `episodes.json` | 판 번호, 시드, 생존 시간, 처치 수, 종료 원인, 설정 버전, 설정 SHA256, 게임 규칙 버전입니다. 최상위 `episodes` 배열에 각 판이 들어 있습니다. |
| `episodes.csv` | 같은 판별 정보를 CSV로 저장합니다. |
| `summary.json` | 평균·중앙값·최장 생존 시간, 판 수, 시간 제한, 시드 시작값, 완료 횟수, 설정과 규칙 식별자입니다. |
| `ai-config.json` | 기본값 적용 후 실제 사용한 전체 설정의 사본입니다. |
| `player.log` | Unity 실행 로그와 판별 진행 메시지입니다. |

같은 결과 폴더에 위 보고서가 이미 있으면 덮어쓰지 않고 중단합니다. 비교 실험마다 새 `-OutputPath`를 사용하세요. 비정상 종료 시에는 완료된 보고서가 없을 수 있으며 `player.log`에서 오류를 확인할 수 있습니다.

## 학습 정책으로 교체

게임 상태와 이동 규칙은 `Assets/Scripts/Simulation/`에 있고, 초기 정책은 `Assets/Scripts/AI/HeuristicPolicy.cs`에 있습니다. `IAgentPolicy`를 구현하는 새 클래스를 작성하고, 게임 시작 코드와 `Assets/Scripts/Evaluation/EvaluationRunner.cs`에서 정책을 생성하는 부분을 바꾸면 됩니다. 정책을 교체할 때 게임 규칙 파일을 수정할 필요는 없습니다.

```csharp
public sealed class LearnedPolicy : IAgentPolicy
{
    public AgentAction Decide(Observation observation)
    {
        // 학습 모델의 출력을 이동, 조준 방향, 사격 여부로 변환합니다.
        return new AgentAction(moveDirection, aimDirection, shouldFire);
    }
}
```

이 예시는 연결 형태를 설명하는 코드이며, `moveDirection`, `aimDirection`, `shouldFire`를 실제 모델 출력으로 구현해야 합니다.

- `Simulation.Reset(seed)`: 새 판을 시작합니다.
- `Simulation.GetObservation()`: 플레이어와 적의 위치·속도, 적 HP, 탄환, 시간, 처치 수, 생존·종료 상태를 가져옵니다.
- `IAgentPolicy.Decide(observation)`: 행동을 반환합니다.
- `Simulation.Step(action)`: 16ms 한 틱을 진행합니다.

관측 위치는 논리 픽셀, 속도는 픽셀/초입니다. 이동은 X/Y 각 축의 입력 부호를 사용하며, 조준은 월드의 방향 벡터입니다. 에피소드마다 새 정책 인스턴스를 만들거나 해당 정책의 내부 상태를 초기화해야 합니다. 현재 `HeuristicPolicy`는 `Reset()`을 제공합니다. 평가기의 해당 초기화 호출도 새 정책에 맞춰 교체하세요.
