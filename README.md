# 🎙️ BetterSuppression

> **RNNoise AI 딥러닝 기반 실시간 마이크 노이즈 제거 & 프로급 노이즈 게이트 모드**
> Real-time AI-powered Microphone Noise Suppression & DSP Noise Gate for Lethal Company.

---

## 💡 주요 기능 (Features)

* **🧠 RNNoise AI 딥러닝 노이즈 제거 Engine**:
  * 키보드 타건음, 팬 소음, 주변 백그라운드 잡음을 AI 딥러닝 모델로 실시간 필터링합니다.
* **🎚️ 프로 스튜디오급 DSP 노이즈 게이트 (Noise Gate)**:
  * **Hysteresis (히스테리시스 Dual Threshold)**: 개방(-26dB)과 폐쇄(-32dB) 임계값을 분리하여 음성이 끊기는 현상(Chatter)을 차단합니다.
  * **Attack / Hold / Release 스무딩**: 25ms 어택, 200ms 홀드, 150ms 릴리스 타임으로 음성 시작과 끝이 끊김 없이 부드럽게 전달됩니다.
* **🎛️ LethalConfig 인게임 실시간 UI 지원**:
  * 게임 재시작 없이 인게임 메뉴에서 체크박스 및 5가지 슬라이더로 모든 노이즈 게이트 파라미터를 즉시 조절할 수 있습니다.

---

## ⚙️ 인게임 설정 메뉴 (LethalConfig)

게임 내 `LethalConfig` 메뉴의 **`BetterSuppression`** 탭에서 아래 항목을 실시간 변경할 수 있습니다:

| 카테고리 | 설정 항목명 | 설명 | 기본값 | 범위 |
| :--- | :--- | :--- | :--- | :--- |
| **General** | `Enable Noise Suppression` | RNNoise AI 마이크 노이즈 제거 활성화 | `true` | Checkbox |
| **General** | `VAD Threshold` | 음성 감지 확률 임계값 (0.0 = 비활성화) | `0.0` | `0.0 ~ 1.0` (Slider) |
| **Noise Gate** | `Enable Noise Gate` | 음량 기반 노이즈 게이트 활성화 | `true` | Checkbox |
| **Noise Gate** | `Gate Close Threshold (dB)` | 폐쇄 임계값 (신호가 설정값 미만 시 릴리스 시작) | `-32.0 dB` | `-80.0 ~ 0.0 dB` (Slider) |
| **Noise Gate** | `Gate Open Threshold (dB)` | 개방 임계값 (노이즈 게이트가 열리는 신호 기준) | `-26.0 dB` | `-80.0 ~ 0.0 dB` (Slider) |
| **Noise Gate** | `Gate Attack Time (ms)` | 어택 타임 (게이트가 열릴 때 페이드인 시간) | `25 ms` | `0.0 ~ 200.0 ms` (Slider) |
| **Noise Gate** | `Gate Hold Time (ms)` | 홀드 타임 (신호 감소 후 게이트를 유지하는 시간) | `200 ms` | `0.0 ~ 1000.0 ms` (Slider) |
| **Noise Gate** | `Gate Release Time (ms)` | 릴리스 타임 (게이트가 닫힐 때 페이드아웃 시간) | `150 ms` | `0.0 ~ 1000.0 ms` (Slider) |

---

## 📥 설치 방법 (Installation)

### Option A. 모드 매니저 사용 (r2modman / Gale)
1. `BetterSuppression-1.0.0.zip` 파일 다운로드
2. r2modman / Gale에서 **`Import local mod`** 버튼을 눌러 ZIP 파일 선택

### Option B. 수동 설치 (Manual Installation)
1. BepInEx가 설치된 Lethal Company 게임 폴더 준비
2. `BetterSuppression-1.0.0.zip` 압축 해제 후 `BepInEx` 폴더를 게임 설치 경로에 덮어씌우기:
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
