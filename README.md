# BetterSuppression

> English description is provided below.

주의 : 원인을 모르겠으나 'https://thunderstore.io/c/lethal-company/p/Fusition/BetterSpectate/' 해당 모드와 같이 적용해야 정상 작동합니다.

BetterSuppression은 클라이언트 사이드 소음 억제 모드입니다.

마이크 입력에 **RNNoise**와 **노이즈 게이트**를 적용하여 주변의 잡음과 불필요한 소리를 줄여줍니다.

## 기능

### 소음 억제

**RNNoise**를 사용하여 마이크에 섞여 들어오는 주변 소음을 실시간으로 억제합니다.

키보드 소리, 컴퓨터 팬 소리, 생활 소음 등 지속적으로 발생하는 배경 소음을 줄이는 데 효과적입니다.

### 노이즈 게이트

설정한 기준보다 작은 소리를 자동으로 차단합니다.

노이즈 게이트의 값을 **직접 수정하여 자신의 환경에 맞게 조절**할 수 있습니다.

- 작은 배경 소음 차단
- 말하지 않을 때 발생하는 잡음 감소
- Threshold 등의 값을 직접 조절 가능

> **참고:** 기본 설정이 강하게 적용되어 있어 `음...`과 같은 작은 소리도 차단될 수 있습니다.  
> 본인의 발음 및 말하는 패턴에 맞게 값을 조절하는 것을 권장합니다.

### 다른 사람의 마이크 잡음 제거

음성 채팅에서 **다른 사람의 마이크에서 발생하는 잡음도 내 컴퓨터에서 제거**할 수 있습니다.

단, 이 기능은 **본인에게 들리는 소리에만 적용됩니다.**

즉, 상대방의 마이크 입력 자체가 변경되는 것은 아니며, 다른 사람에게 들리는 상대방의 음성에는 영향을 주지 않습니다.

Note: I’m not sure what caused it, but it works properly only when applied like the mod at https://thunderstore.io/c/lethal-company/p/Fusition/BetterSpectate/' .

BetterSuppression is a client-side noise suppression mod.

It applies **RNNoise** and a **Noise Gate** to microphone input to reduce background noise and unwanted sounds.

## Features

### Noise Suppression

Uses **RNNoise** to suppress background noise from your microphone in real time.

It is effective at reducing continuous background sounds such as keyboard noise, computer fans, and other ambient noise.

### Noise Gate

Automatically blocks sounds below a specified volume threshold.

The Noise Gate settings can be **manually adjusted** to suit your environment.

- Reduces quiet background noise
- Reduces noise when you are not speaking
- Allows manual adjustment of the Threshold and other settings

> **Note:** The default settings are relatively aggressive, so quiet sounds such as "um..." may also be blocked.  
> Adjust the settings according to your voice and speaking style.

### Suppressing Other People's Microphone Noise

You can also reduce **noise from other people's microphones** in voice chat.

However, this processing is **only applied to what you hear**.

In other words, it does not modify the other person's actual microphone input and does not affect what other people hear from them.
