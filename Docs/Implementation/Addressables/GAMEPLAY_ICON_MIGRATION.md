# Gameplay presentation icon migration

Gameplay configuration now carries stable client addresses, never direct Sprite references. This table records every migrated serialized edge.

| Config asset | Serialized address field | Sprite asset | Address | GUID |
|---|---|---|---|---|
| `Assets/Config/Formal/Abilities/Aatrox/AatroxQ.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxQ1.png` | `ui/icon/e894b675c21081c48870a9a7933c7439` | `e894b675c21081c48870a9a7933c7439` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxQ.asset` | `castModel.firstImpactIconAddressOverride` | `Assets/Art/Icon/Ability/Aatrox/AatroxQ1.png` | `ui/icon/e894b675c21081c48870a9a7933c7439` | `e894b675c21081c48870a9a7933c7439` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxQ.asset` | `castModel.secondImpactIconAddressOverride` | `Assets/Art/Icon/Ability/Aatrox/AatroxQ2.png` | `ui/icon/00e9e3466d875504dacc31d33410c22a` | `00e9e3466d875504dacc31d33410c22a` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxQ.asset` | `castModel.finalImpactIconAddressOverride` | `Assets/Art/Icon/Ability/Aatrox/AatroxQ3.png` | `ui/icon/6b760e2beac14724da8a55d9ff3ecd64` | `6b760e2beac14724da8a55d9ff3ecd64` |
| `Assets/Config/Formal/Abilities/VarusQ.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯Q_未蓄力.png` | `ui/icon/6118bc6bfec0b614594d040f5541d5bd` | `6118bc6bfec0b614594d040f5541d5bd` |
| `Assets/Config/Formal/Abilities/VarusQ.asset` | `castModel.holdIconAddressOverride` | `Assets/Art/Icon/Ability/Varus/韦鲁斯Q_蓄力中.png` | `ui/icon/4fadc3515107f0e4e979a26aeaa8e7b7` | `4fadc3515107f0e4e979a26aeaa8e7b7` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxW.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxW.png` | `ui/icon/804f3e1b015d3c445af822036f4058ba` | `804f3e1b015d3c445af822036f4058ba` |
| `Assets/Config/Formal/Abilities/VarusE.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯E.png` | `ui/icon/0d1ee503e046e064790a584b400c97e2` | `0d1ee503e046e064790a584b400c97e2` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxE.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxE.png` | `ui/icon/237800e8fddf3e54bb1bf43e3e96ff5b` | `237800e8fddf3e54bb1bf43e3e96ff5b` |
| `Assets/Config/Formal/Abilities/VarusR.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯R.png` | `ui/icon/780063d3e4c43d144951a3e1a5cd50f8` | `780063d3e4c43d144951a3e1a5cd50f8` |
| `Assets/Config/Formal/Abilities/VarusW.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯W_未激活.png` | `ui/icon/dbf61c3eedeecfa41adf5571348e67a1` | `dbf61c3eedeecfa41adf5571348e67a1` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxR.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxR.png` | `ui/icon/530bdb7b178dc224e98d3a27461805f2` | `530bdb7b178dc224e98d3a27461805f2` |
| `Assets/Config/Formal/Abilities/VarusFixedPassive.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯被动.png` | `ui/icon/f6b922f5d10cb3741a50fbfd1dc7bddf` | `f6b922f5d10cb3741a50fbfd1dc7bddf` |
| `Assets/Config/Formal/Abilities/Aatrox/AatroxFixedPassive.asset` | `iconAddress` | `Assets/Art/Icon/Ability/Aatrox/Aatrox_Passive.png` | `ui/icon/35a40a65df8df004eb47015ccda2a0c2` | `35a40a65df8df004eb47015ccda2a0c2` |
| `Assets/Config/Formal/Equipment/Pickaxe.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/十字镐.png` | `ui/icon/0e05c618c0a7c024a94ced98ffdc0695` | `0e05c618c0a7c024a94ced98ffdc0695` |
| `Assets/Config/Formal/Equipment/GuinsoosRageblade.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/鬼索的狂暴之刃.png` | `ui/icon/86d765fa57452364f9577c5014480763` | `86d765fa57452364f9577c5014480763` |
| `Assets/Config/Formal/Equipment/RecurveBow.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/反曲之弓.png` | `ui/icon/6b7a039792e48b5468c197a77371f7d1` | `6b7a039792e48b5468c197a77371f7d1` |
| `Assets/Config/Formal/Equipment/LongSword.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/长剑.png` | `ui/icon/10109fb3c0d85f24aaf62c84fa4bde67` | `10109fb3c0d85f24aaf62c84fa4bde67` |
| `Assets/Config/Formal/Equipment/RubyCrystal.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/红水晶.png` | `ui/icon/7ff31451ad4b246479de80aa3261dedc` | `7ff31451ad4b246479de80aa3261dedc` |
| `Assets/Config/Formal/Equipment/GlowingMote.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/荧尘.png` | `ui/icon/be809818752188249a3aab7ad105cfe0` | `be809818752188249a3aab7ad105cfe0` |
| `Assets/Config/Formal/Equipment/Tunneler.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/掘道钻头.png` | `ui/icon/5e44ca630b8853b4eac51877ef530e61` | `5e44ca630b8853b4eac51877ef530e61` |
| `Assets/Config/Formal/Equipment/CaulfieldsWarhammer.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/考尔菲德的战锤.png` | `ui/icon/a7271a60e0865f940a12ad9f97d92d7e` | `a7271a60e0865f940a12ad9f97d92d7e` |
| `Assets/Config/Formal/Equipment/SunderedSky.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/焚天.png` | `ui/icon/22ee2f49eb0f53c4f9007811dd4883cc` | `22ee2f49eb0f53c4f9007811dd4883cc` |
| `Assets/Config/Formal/Equipment/AmplifyingTome.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/增幅典籍.png` | `ui/icon/eb073a169e8d14f41a58c531b824a368` | `eb073a169e8d14f41a58c531b824a368` |
| `Assets/Config/Formal/Equipment/Dagger.asset` | `IconAddress` | `Assets/Art/Icon/Equipment/短剑.png` | `ui/icon/ce093c4d89bfff64999673091f7b1130` | `ce093c4d89bfff64999673091f7b1130` |
| `Assets/Config/Formal/Buffs/Aatrox/AatroxWTether.asset` | `Display.IconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxW.png` | `ui/icon/804f3e1b015d3c445af822036f4058ba` | `804f3e1b015d3c445af822036f4058ba` |
| `Assets/Config/Formal/Buffs/RevengeBuffDefinition.asset` | `Display.IconAddress` | `Assets/Art/Icon/Ability/Varus/韦鲁斯被动.png` | `ui/icon/f6b922f5d10cb3741a50fbfd1dc7bddf` | `f6b922f5d10cb3741a50fbfd1dc7bddf` |
| `Assets/Config/Formal/Buffs/Aatrox/AatroxWorldEnder.asset` | `Display.IconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxR.png` | `ui/icon/530bdb7b178dc224e98d3a27461805f2` | `530bdb7b178dc224e98d3a27461805f2` |
| `Assets/Config/Formal/Buffs/Buff_SeethingStrike.asset` | `Display.IconAddress` | `Assets/Art/Icon/Equipment/鬼索的狂暴之刃.png` | `ui/icon/86d765fa57452364f9577c5014480763` | `86d765fa57452364f9577c5014480763` |
| `Assets/Config/Formal/Buffs/Aatrox/Buff_DeathbringerStance.asset` | `Display.IconAddress` | `Assets/Art/Icon/Ability/Aatrox/Aatrox_Passive.png` | `ui/icon/35a40a65df8df004eb47015ccda2a0c2` | `35a40a65df8df004eb47015ccda2a0c2` |
| `Assets/Config/Formal/Buffs/Buff_SunderedSkyOverheal.asset` | `Display.IconAddress` | `Assets/Art/Icon/Equipment/焚天.png` | `ui/icon/22ee2f49eb0f53c4f9007811dd4883cc` | `22ee2f49eb0f53c4f9007811dd4883cc` |
| `Assets/Config/Formal/Buffs/Aatrox/AatroxWSlow.asset` | `Display.IconAddress` | `Assets/Art/Icon/Ability/Aatrox/AatroxW.png` | `ui/icon/804f3e1b015d3c445af822036f4058ba` | `804f3e1b015d3c445af822036f4058ba` |
| `Assets/Config/Formal/Buffs/BlightBuffDefinition.asset` | `Display.IconAddress` | `Assets/Art/Icon/Buff/韦鲁斯_枯萎.png` | `ui/icon/50137e36b1e92454db3e2534e676b03b` | `50137e36b1e92454db3e2534e676b03b` |
| `Assets/Config/Formal/HeroDisplayTable.asset` | `entries.Array.data[0].AvatarAddress` | `Assets/Art/Icon/Hero/Varus.jpg` | `ui/icon/b54876e9c470b774fa7adc1a21c34d1e` | `b54876e9c470b774fa7adc1a21c34d1e` |
| `Assets/Config/Formal/HeroDisplayTable.asset` | `entries.Array.data[1].AvatarAddress` | `Assets/Art/Icon/Hero/Aatrox.png` | `ui/icon/05cc6a8fbb52ed246b0f3b4720325ef1` | `05cc6a8fbb52ed246b0f3b4720325ef1` |
