# 撤离训练场 · 类塔科夫移动端原型

一个人 + AI 在手机上做的 Unity 移动端撤离射击原型。没有美术资源、没有手工场景：
地图、音效、UI 全部在运行时用代码生成，仓库里只有 .cs 和 5 个 JSON 数据表。

## 一局的完整循环

```
主菜单（选装备、看战绩）
      ↓ 扣装备钱
程序化地图（16 栋建筑 / 掩体 / 12 个可搜容器 / 8 个 Scav / 2 个撤离点）
      ↓
搜刮 → 交战 → 撤离
      ↓
撤离成功：战利品折算成卢布，进账户，能买更好的装备
阵亡：装备钱和背包里的东西全丢
      ↓
结算界面 → 再来一局 / 回大厅
```

## 操作

| 位置 | 操作 |
| --- | --- |
| 左半屏拖动 | 动态摇杆移动 |
| 右半屏滑动 | 转视角（左右各一指，双指不会叠加） |
| 右下 大红圆 | 开火（按住连发） |
| 右下 黄圆「镜」 | 开镜（后坐力更小、散布更小） |
| 右下 蓝圆「搜」 | 搜刮最近的容器（站定 1 秒） |
| 右下 灰圆「换」 | 换弹 |
| 左下 绿圆「跑」 | 冲刺（消耗体力） |
| 左下 粉圆「医」 | 用绷带（止血 + 回血） |

## 系统一览

- **装备套**：轻装（免费）/ 标准 15k / 重装 45k，进场先扣钱，死了就没了
- **战斗**：命中扫描 + 弹丸散布 + 逐发后坐力（停火自动回正）+ 2.5° 轻度辅助瞄准
- **伤害**：护甲吸收 55% 并掉耐久；28% 概率打出出血，需要绷带
- **Scav AI**：巡逻 → 听到枪声去查看 → 看见玩家进入交战（0.55s 反应时间）→ 丢失视野 3.5s 后搜索；距离越远越不准
- **搜刮**：掉落表驱动（`loot_jacket` / `loot_weapon_crate` / `loot_scav_body`），背包 18 格，满了会提示
- **撤离**：站进绿圈 5 秒，离开会缓慢回退
- **存档**：卢布 / 突袭次数 / 撤离 / 阵亡 / 击杀 / 最高战利品 / 灵敏度，PlayerPrefs
- **音效**：全部程序化合成（枪声 3 变体、命中、脚步、受伤、治疗、撤离提示），无音频文件

## 构建

推送 `main` 即触发 GitHub Actions（`.github/workflows/build-android.yml`），
用 `game-ci/unity-builder` 在云端跑 Unity 6000.0.32f1 + IL2CPP 出 APK，
产物在 Actions 的 `android-apk` artifact 里。

需要的 Secrets：`UNITY_EMAIL`、`UNITY_PASSWORD`、`UNITY_LICENSE`。

## 数据

`Assets/StreamingAssets/GameData/` 下的 5 张表在运行时被复制到 persistentDataPath
再解析，真机上改表不用重新打包（清数据会重置）。

| 文件 | 内容 |
| --- | --- |
| `items.json` | 18 件物品（名称 / 价值 / 重量 / 堆叠） |
| `weapons.json` | 5 把武器（口径 / RPM / 弹匣 / 后坐力曲线） |
| `ammo.json` | 9 种弹药（伤害 / 穿透 / 弹丸数） |
| `armor.json` | 3 件护甲（防护等级 / 耐久 / 覆盖部位） |
| `loot_tables.json` | 3 张掉落表 |

## 代码结构

```
Assets/Scripts/
  Boot/      M0Bootstrap（全局设置）· GameDataBootstrap（JSON 加载）
  Game/      GameApp（状态机）· RaidMapBuilder（地图）· EnemyScav（AI）
             PlayerRig 在 Gameplay/ · LootContainer · ExtractionZone
             PlayerInventory · Loadouts · MetaProfile · Fx（音效/曳光）
  Gameplay/  PlayerRig（移动/射击/生命/背包）
  UI/        TouchControls（触屏）· Hud（IMGUI）· MenuUi + UiKit（uGUI）
  Data/      Game.Data 程序集：纯 C# 数据层 + 掉落算法，可脱离引擎单测
```

## 已知取舍（原型阶段）

- 地图是白盒，只有方块和灰泥材质，没有光照烘焙
- Scav 没有寻路网格，用「直线 + 卡住换向」，不会绕过大建筑
- 没有背包界面、没有物品管理，战利品只用于结算价值
- 没有脚步声区分（玩家和 Scav 用同一个合成脚步）