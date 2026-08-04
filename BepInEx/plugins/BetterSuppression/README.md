# 🎙️ BetterSuppression

> **RNNoise AI 딥러닝 기반 실시간 마이크 노이즈 제거 & 노이즈 게이트 모드**
> Real-time AI-powered Microphone Noise Suppression & Volume Noise Gate for Lethal Company.

---

## 💡 주요 기능 (Features)

* **🧠 RNNoise AI 딥러닝 노이즈 제거 Engine**:
  * 키보드 타건음, 팬 소음, 주변 백그라운드 잡음을 AI 딥러닝 모델로 실시간으로 깔끔하게 필터링합니다.
* **🎚️ 음량 기반 노이즈 게이트 (Noise Gate)**:
  * 마이크 입력 데시벨(dBFS)이 설정된 임계값 이하일 때 잡음 입력을 완전히 차단하여 정적을 유지합니다.
* **🎛️ LethalConfig 인게임 실시간 UI 지원**:
  * 게임을 재시작할 필요 없이 인게임 메뉴에서 체크박스 및 슬라이더로 노이즈 억제 활성화, VAD 감도, 게이트 dB 임계값을 즉시 조정할 수 있습니다.
* **⚡ 초저지연 (Zero-Latency) 성능**:
  * 10ms 단위 48kHz PCM 프레임 버퍼링 알고리즘으로 음성 지연이나 프레임 드랍 없이 부드러운 음성을 전달합니다.

---

## ⚙️ 인게임 설정 메뉴 (LethalConfig)

게임 내 `LethalConfig` 메뉴의 **`BetterSuppression`** 탭에서 아래 항목을 실시간 변경할 수 있습니다:

| 카테고리 | 설정 항목명 | 설명 | 기본값 | 범위 |
| :--- | :--- | :--- | :--- | :--- |
| **General** | `Enable Noise Suppression` | RNNoise AI 마이크 노이즈 제거 활성화 | `true` | Checkbox |
| **General** | `VAD Threshold` | 음성 감지 확률 임계값 (0.0 = 비활성화) | `0.0` | `0.0 ~ 1.0` (Slider) |
| **Noise Gate** | `Enable Noise Gate` | 음량 기반 노이즈 게이트 활성화 | `true` | Checkbox |
| **Noise Gate** | `Gate Threshold (dB)` | 게이트 음량 임계값 (설정값 미만 시 음소거) | `-45.0` | `-80.0 ~ 0.0 dB` (Slider) |

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
