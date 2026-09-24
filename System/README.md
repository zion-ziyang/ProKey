# Study System

## Requirements
> The studies were conducted with Meta XR Interaction SDK v85 and the project has since been upgraded to v205.

| | |
|---|---|
| Unity | 2022.3.62f3 (built-in render pipeline), Android build support |
| Headset | Meta Quest 3, hand tracking enabled |
| Packages | Meta XR Interaction SDK 205.0.0, Oculus XR Plugin 4.5.1, TextMesh Pro 3.0.9 |
| Word suggestions | A Fleksy licence of your own, see below |

## Setup

1. Open the project folder with Unity 2022.3 and open `Assets/Scenes/StudyScene.unity`.
2. Add your Fleksy licence. Without it the keyboards work, but no word suggestions appear.
3. Switch the platform to Android and build to the headset (`File > Build Settings`).

> Word completion and next-word prediction come from the [Fleksy](https://www.fleksy.com/) predictive text SDK. Request your own key and secret from Fleksy, then
> 1. Copy `Assets/Scripts/FleksySDK/FleksyLicense.example.json` to `Assets/Resources/FleksyLicense.json`,
> 2. Replace the two placeholder values with your key and secret.

## Keyboards

All keyboards are children of `KeyboardSystem` in the scene. Sizes are in metres and readable directly in the Inspector, and a key's collider has the size in its `BoxCollider`.

| Scene object | Used in | Key | Gap | Key-area width |
|---|---|---|---|---|
| `PokeKey` | Study 1, baseline of Studies 2 and 3 | 20 mm | 8 mm | 300 mm |
| `PokeKey_Size_L` | Study 2, scale+ | 24 mm | 9.6 mm | 360 mm |
| `PokeKey_Gap_L` | Study 2, gap+ | 20 mm | 14 mm | 360 mm |
| `ProKey` | Study 3 | 20 mm | 8 mm | 300 mm |

## Third-party content

Fleksy SDK binaries (Fleksy licence terms apply), Meta XR SDK, TextMesh Pro with the Roboto and Liberation Sans fonts, and the [phrases used in the studies](Assets/Resources/Task/days.txt), selected from the MacKenzie and Soukoreff phrase set.
