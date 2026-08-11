# 🎙️ BetterSuppression

> **RNNoise AI 딥러닝 기반 실시간 마이크 노이즈 제거 & 프로급 노이즈 게이트 모드**
> Real-time AI-powered Microphone & Remote Player Voice Noise Suppression & DSP Noise Gate for Lethal Company.

---

## 💡 주요 기능 (Features)

* **🧠 RNNoise AI 딥러닝 노이즈 제거 Engine**:
  * 키보드 타건음, 팬 소음, 주변 백그라운드 잡음을 AI 딥러닝 모델로 실시간 필터링합니다.
* **👥 다른 플레이어 음성 잡음 제거 (Remote Player Voice Filtering)**:
  * 내 마이크뿐만 아니라 **다른 플레이어가 전달하는 음성 수신 데이터에도 RNNoise AI 노이즈 제거 및 게이트를 실시간 적용**하여 팀원의 노이즈 있는 마이크 소리도 깨끗하게 필터링하여 들려줍니다.
* **🎚️ 프로 스튜디오급 DSP 노이즈 게이트 (Noise Gate)**:
  * **Hysteresis (히스테리시스 Dual Threshold)**: 개방(-26dB)과 폐쇄(-32dB) 임계값을 분리하여 음성이 끊기는 현상(Chatter)을 차단합니다.
  * **Attack / Hold / Release 스무딩**: 25ms 어택, 200ms 홀드, 150ms 릴리스 타임으로 음성 시작과 끝이 끊김 없이 부드럽게 전달됩니다.
* **🎛️ LethalConfig 인게임 실시간 UI 지원**:
  * 게임 재시작 없이 인게임 메뉴에서 내 마이크, 다른 플레이어 마이크, 노이즈 게이트 슬라이더를 독립적으로 즉시 조절할 수 있습니다.
* **🔬 마이크 테스트 오버레이 (Mic Test Mode)**:
  * **F7 키**를 눌러 실시간 마이크 진단 화면을 표시합니다.
  * FFT 주파수 스펙트럼, dB 레벨 미터, 노이즈 게이트 상태(Open/Hold/Closing/Closed), RNNoise 상태를 한눈에 확인할 수 있습니다.
  * 노이즈 게이트와 소음 억제가 실제로 작동하는지 시각적으로 검증할 수 있습니다.

---

## ⚙️ 인게임 설정 메뉴 (LethalConfig)

| 카테고리 | 설정 항목명 | 설명 | 기본값 |
| :--- | :--- | :--- | :--- |
| **Local Player** | `Enable Noise Suppression` | 내 마이크에 RNNoise AI 노이즈 제거 활성화/비활성화 | `true` |
| **Local Player** | `Enable Noise Gate` | 내 마이크에 노이즈 게이트 활성화 | `true` |
| **Remote Players** | `Enable Remote Noise Suppression` |다른 플레이어 마이크 음성에도 AI 노이즈 제거 적용 | `true` |
| **Remote Players** | `Enable Remote Noise Gate` | 다른 플레이어 마이크 음성에도 노이즈 게이트 적용 | `true` |
| **Noise Gate DSP** | `Gate Close Threshold (dB)` | 폐쇄 임계값 (신호가 설정값 미만 시 릴리스 시작) | `-32.0 dB` |
| **Noise Gate DSP** | `Gate Open Threshold (dB)` | 개방 임계값 (노이즈 게이트가 열리는 신호 기준) | `-26.0 dB` |
| **Noise Gate DSP** | `Gate Attack Time (ms)` | 어택 타임 (게이트가 열릴 때 페이드인 시간) | `25 ms` |
| **Noise Gate DSP** | `Gate Hold Time (ms)` | 홀드 타임 (신호 감소 후 게이트를 유지하는 시간) | `200 ms` |
| **Noise Gate DSP** | `Gate Release Time (ms)` | 릴리스 타임 (게이트가 닫힐 때 페이드아웃 시간) | `150 ms` |
| **Test Mode** | `Test Mode Key` | 마이크 테스트 오버레이를 켜고 끄는 키 | `F7` |

---

## 📥 설치 방법 (Installation)

### Option A. 모드 매니저 사용 (r2modman / Gale)
1. `BetterSuppression-1.1.0.zip` 파일 다운로드
2. r2modman / Gale에서 **`Import local mod`** 버튼을 눌러 ZIP 파일 선택

### Option B. 수동 설치 (Manual Installation)
1. BepInEx가 설치된 Lethal Company 게임 폴더 준비
2. `BetterSuppression-1.1.0.zip` 압축 해제 후 `BepInEx` 폴더를 게임 설치 경로에 덮어씌우기:
   ```text
   Lethal Company/
   └── BepInEx/
       ├── config/
       │   └── com.lethalcompany.bettersuppression.cfg
       └── plugins/
           └── BetterSuppression/
               ├── BetterSuppression.dll
               ├── manifest.json
               ├── icon.png
               └── README.md
   ```

---

## 📄 라이선스 (License)
This project is licensed under the MIT License.
Powered by RNNoise AI Engine & HarmonyLib.
