# Flya - A easy flying system for VRChat worlds -
## 概要

Udon#製の、2種類の飛行ギミックを含みます。
下記のprefabをご参照ください。

- `galaxy-sixth-sensey/Flya/ExamplePrefabs/FlyerWithUsingDown`
    - Sphereをピックアップしたままトリガーを引くと、一回分の飛行をします
- `galaxy-sixth-sensey/Flya/ExamplePrefabs/FlyerWithToggling`
    - Sphereをピックアップしたままトリガーを引くと、飛行し続けます。
      そのままもう一度トリガーを引くと、飛行をやめます

`Udon Behavior`の`Flapping Strength`を上げ下げすることで、飛行時の上がり方の強さを変更できます。

当Udon#スクリプトは、他のモデルにも設定していただけます。
モデルのコンポーネントに`VRC_Pickup`と`UdonBehavior`を追加して、`galaxy-sixth-sensey/Flya/Scripts`にあるスクリプトを`UdonBehavior`に設定してください。

## おまけ

- `galaxy-sixth-sensey/Flya/ExamplePrefabs/RespawingItems`

Interact時に、設定した1つ以上のGameObjectを、初期位置に戻します。
