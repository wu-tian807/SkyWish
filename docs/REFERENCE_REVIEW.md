# 参考源码初审

检查日期：2026-11-07。只面向 Cities: Skylines II。
本地共有 82 个参考仓库，含首批 17 个和扩展收录的 65 个。已初步核查仓库来源、主要入口、用途和根目录许可文本，游戏内兼容性待验收。
全部使用浅克隆。完整提交号、远程地址和许可证文件哈希见 [固定清单](../references/sources.lock.json)。

## 首批收录

| 仓库 | 研究用途 | 许可证文件观察 |
|---|---|---|
| [LancerComet/cities-skylines-2-mcp](https://github.com/LancerComet/cities-skylines-2-mcp) | 主线程桥接、原生建造、公交线路 | Apache-2.0 |
| [nachocheesedog/cs2-mcp](https://github.com/nachocheesedog/cs2-mcp) | 地图观察、建造预览、撤销、Blender 导入 | MIT |
| [Mimonsi/CS2-VehicleController](https://github.com/Mimonsi/CS2-VehicleController) | 车辆模板、概率和行驶参数 | 未找到完整许可文件 |
| [northernst11-bot/BetaTestDrivingMod](https://github.com/northernst11-bot/BetaTestDrivingMod) | 车辆接管与归还、车道状态 | MIT |
| [yenyang/CS2-MoveIt](https://github.com/yenyang/CS2-MoveIt) | 移动、旋转、对象更新和撤销 | MIT |
| [Quboid/CS2-MoveIt](https://github.com/Quboid/CS2-MoveIt) | Move It 原实现对比 | MIT |
| [1Chrakv/Unsquare](https://github.com/1Chrakv/Unsquare) | 建筑变形、实例资产变体、存档恢复 | GPL v3 文件 |
| [yenyang/Anarchy](https://github.com/yenyang/Anarchy) | 放置限制和更新流程 | MIT |
| [yenyang/Recolor](https://github.com/yenyang/Recolor) | 实例、变体和模板颜色编辑 | MIT |
| [algernon-A/LineTool-CS2](https://github.com/algernon-A/LineTool-CS2) | 沿线或曲线批量摆放 | Apache-2.0 |
| [Cgameworld/FirstPersonCameraContinued](https://github.com/Cgameworld/FirstPersonCameraContinued) | 居民、车辆跟随镜头 | GPL v2 文件 |
| [lemmy101/Skylines2_CinematicRendererMod](https://github.com/lemmy101/Skylines2_CinematicRendererMod) | 逐帧电影渲染和模拟同步 | GPL v2 文件 |
| [algernon-A/PlopTheGrowables](https://github.com/algernon-A/PlopTheGrowables) | 手动放置分区建筑、等级和生命周期 | Apache-2.0 |
| [JadHajjar/FindIt-CSII](https://github.com/JadHajjar/FindIt-CSII) | 资产搜索、分类和选择 | csproj 写有 MIT，完整许可文件待确认 |
| [JadHajjar/RoadBuilder-CSII](https://github.com/JadHajjar/RoadBuilder-CSII) | 动态道路模板与保存 | MIT |
| [krzychu124/Traffic](https://github.com/krzychu124/Traffic) | 车道连接和优先级 | 未找到完整许可文件 |
| [viktorsx/Copaste](https://github.com/viktorsx/Copaste) | 组合复制、蓝图与重复摆放 | MIT |

首批有 14 个仓库带根目录许可文件，另有 3 个许可待确认。
暂未将任何上游源码复制、链接或打包进 CityDirector。GPL 项目也只作为独立参考保存。
将来采用具体实现时，再根据对应文件的完整许可处理归属和发布方式；不能用本项目的许可覆盖上游许可。

## 扩展收录

本轮新增 65 个仓库。通过 GitHub 主题、模组名和作者搜索建立候选列表，再沿上游手册、依赖和发布页追踪关联项目。下表按开发用途分组，完整提交、许可证文件与哈希见 [固定清单](../references/sources.lock.json)。

源码库按用途分为游戏模组、外部工具、开发工具、模板、文档和资产工程。许可栏的“待确认”表示本轮未确认完整许可文本。已记录非商业条款、自定义许可和资产单独授权等差异。

### 资产与道具

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [qstar-inc/cities2-objects](https://github.com/qstar-inc/cities2-objects) | 工具 | 可用游戏对象索引和网页浏览 | CC-BY-SA-4.0 |
| [CitiesSkylinesModding/CS2-AssetPacksManager](https://github.com/CitiesSkylinesModding/CS2-AssetPacksManager) | 模组 | 资产包缩略图、本地化和兼容处理 | 待确认 |
| [AlphaGaming7780/ExtraAssetsImporter](https://github.com/AlphaGaming7780/ExtraAssetsImporter) | 模组 | 自定义地表材质和贴花导入 | 待确认 |
| [AlphaGaming7780/ExtraDetailingTools](https://github.com/AlphaGaming7780/ExtraDetailingTools) | 模组 | 对象变换、布景与细节摆放 | 待确认 |
| [AlphaGaming7780/ExtraLib](https://github.com/AlphaGaming7780/ExtraLib) | 库 | Extra 系列的 UI、实体与资产支持代码 | 待确认 |
| [klyte45/CS2-WriteEverywhere](https://github.com/klyte45/CS2-WriteEverywhere) | 模组 | 游戏内标牌、字体、图集与文本布局 | 待确认 |
| [klyte45/CS2-WEModuleTemplate](https://github.com/klyte45/CS2-WEModuleTemplate) | 模板 | 标牌布局、图集和字体模块模板 | 待确认 |
| [suller-hub/cs2-asset-pack-exporter](https://github.com/suller-hub/cs2-asset-pack-exporter) | 工具 | Blender 资产包批量导出、比例和纹理处理 | MIT |
| [DanOkami/CS2-Exporter-for-Blender](https://github.com/DanOkami/CS2-Exporter-for-Blender) | 工具 | Blender 网格拆分、UV、材质和 FBX 导出 | MIT |
| [dvovo/BuildingIconGenerator](https://github.com/dvovo/BuildingIconGenerator) | 模组 | 从游戏包读取建筑并批量渲染缩略图 | MIT |
| [Mimonsi/CS2-IntegratedTramStopAssetPack](https://github.com/Mimonsi/CS2-IntegratedTramStopAssetPack) | 资产工程 | 集成电车站资产包结构和打包示例 | 待确认 |

### 路网与场景建造

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [ST-Apps/CS2-ExtendedRoadUpgrades](https://github.com/ST-Apps/CS2-ExtendedRoadUpgrades) | 模组 | 既有道路的附加属性和升级工具 | CC-BY-NC-SA-4.0 |
| [Mimonsi/CS2-ExtraNetworksAndAreas](https://github.com/Mimonsi/CS2-ExtraNetworksAndAreas) | 模组 | 隐形道路、区域和可用于组合站点的标记 | 待确认 |
| [AlphaGaming7780/ExtraLandscapingTools](https://github.com/AlphaGaming7780/ExtraLandscapingTools) | 模组 | 地形笔刷、资源和地表编辑 | 待确认 |
| [yenyang/BetterBulldozer](https://github.com/yenyang/BetterBulldozer) | 模组 | 细分对象删除、归属与清理 | MIT |
| [lucarager/CS2-Platter](https://github.com/lucarager/CS2-Platter) | 模组 | 手工放置地块并接入原生分区生长 | 待确认 |
| [dvovo/CustomZones](https://github.com/dvovo/CustomZones) | 模组 | 自定义分区与可生成建筑筛选 | MIT |
| [LuxStice/AdvancedRoadTools](https://github.com/LuxStice/AdvancedRoadTools) | 模组 | 道路两侧分区深度和分区移除 | MIT |
| [zilianli/section2change](https://github.com/zilianli/section2change) | 模组 | 整组立交的约束求解、预览和放置 | MIT |
| [tomkpunkt/ParkManager](https://github.com/tomkpunkt/ParkManager) | 模组 | 多边形场地、入口、小径和装饰的程序化预览与建造 | GPL-3.0 |
| [jasperaelvoet/realistic-road-works](https://github.com/jasperaelvoet/realistic-road-works) | 模组 | 施工阶段、工程车辆与道路状态变化 | MIT |
| [Rollocraft/CS2MultiplayerMod](https://github.com/Rollocraft/CS2MultiplayerMod) | 模组 | 多用户建造同步、状态一致性和兼容场景 | CS2 Multiplayer Non-Commercial v1.1 |

### 公共交通与车辆

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [AmicusDeus/TransitTimetables](https://github.com/AmicusDeus/TransitTimetables) | 模组 | 分时段发车、车队规模、终点站等待和退出 | MIT |
| [ZessonsDE/TrafficToolEssentials](https://github.com/ZessonsDE/TrafficToolEssentials) | 模组 | 信号相位、绿波、车库区域、调度与交通统计 | 待确认 |
| [River-Mochi/CS2-AllTransitTrucks](https://github.com/River-Mochi/CS2-AllTransitTrucks) | 模组 | 公共交通车型容量、车库能力和车辆数量 | GPL-3.0 |
| [SonnyX/CS2-TransportPolicyAdjuster](https://github.com/SonnyX/CS2-TransportPolicyAdjuster) | 模组 | 公交等交通线路的车辆数量策略 | 待确认 |
| [klyte45/CS2-ExtendedTransportManager](https://github.com/klyte45/CS2-ExtendedTransportManager) | 模组 | 线路、站点连接、车辆分组和票价 | 待确认 |
| [slyh/Cities2-TrafficLightsEnhancement](https://github.com/slyh/Cities2-TrafficLightsEnhancement) | 模组 | 交叉口的信号配置和存档恢复 | 待确认 |
| [BastiAKA/TrafficClearTheWay](https://github.com/BastiAKA/TrafficClearTheWay) | 模组 | 应急车辆优先、事故现场及拖车流程 | Apache-2.0 |
| [Daotie/CS2-RouteFilter](https://github.com/Daotie/CS2-RouteFilter) | 模组 | 道路和轨道按车辆资产过滤通行 | GPL-3.0 |
| [roll-w/CSL2-StationNaming](https://github.com/roll-w/CSL2-StationNaming) | 模组 | 车站、站点和设施命名 | MIT |
| [ruzbeh0/RealisticPathFinding](https://github.com/ruzbeh0/RealisticPathFinding) | 模组 | 汽车、公交、行人和自行车寻路成本 | 待确认 |
| [ruzbeh0/Time2Work](https://github.com/ruzbeh0/Time2Work) | 模组 | 市民和游客按工作、上学及休闲安排出行 | 待确认 |

### 镜头、角色与城市事件

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [algernon-A/FreeRangeCameraLite](https://github.com/algernon-A/FreeRangeCameraLite) | 模组 | 自由镜头的边界、缩放和编辑器支持 | Apache-2.0 |
| [rodrigmatrix/TimeWeatherAnarchy](https://github.com/rodrigmatrix/TimeWeatherAnarchy) | 模组 | 拍摄时间、天气和跨存档设置 | Apache-2.0 |
| [NyokoDev/PhotoModePreserve](https://github.com/NyokoDev/PhotoModePreserve) | 模组 | 照片模式状态保留 | MIT |
| [Cgameworld/Compass](https://github.com/Cgameworld/Compass) | 模组 | 镜头朝向和地图方位提示 | MIT |
| [Cgameworld/MapTextureReplacer](https://github.com/Cgameworld/MapTextureReplacer) | 模组 | 地表纹理、地图主题和保存 | MIT |
| [qstar-inc/cities2-CitizenModelManager](https://github.com/qstar-inc/cities2-CitizenModelManager) | 模组 | 市民模型查找与外观管理 | AGPL-3.0 |
| [Crystalhihihi/cs2-citylife](https://github.com/Crystalhihihi/cs2-citylife) | 模组 | 由事件驱动的市民聚集、城市信息流和世界提示 | MIT |
| [Daotie/CS2-CityPress](https://github.com/Daotie/CS2-CityPress) | 模组 | 从模拟数据采集城市事件并呈现时间记录 | GPL-3.0 |
| [williamlang/city-storytelling-mod](https://github.com/williamlang/city-storytelling-mod) | 模组 | ECS 城市快照、事件和外部叙事代理 | MIT |

### 地图、地理数据与规划

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [taipei-native/Carto](https://github.com/taipei-native/Carto) | 模组 | 导出城市路网、交通路线、建筑、地形和水深 | MIT |
| [algernon-A/ImageOverlay](https://github.com/algernon-A/ImageOverlay) | 模组 | 导入、对齐和调整游戏地图上的参考图 | Apache-2.0 |
| [algernon-A/ImageOverlayLite](https://github.com/algernon-A/ImageOverlayLite) | 模组 | 早期地图叠图系统 | Apache-2.0 |
| [aous-afk/skyplan](https://github.com/aous-afk/skyplan) | 模组 | 在游戏地图上绘制 SVG 道路、公交和区域草图 | 待确认 |
| [postblink/cs2-map-importer](https://github.com/postblink/cs2-map-importer) | 工具 | 按地点生成高度图与街道参考叠图 | 待确认 |
| [AuroraCrimsonRose/CityPrint](https://github.com/AuroraCrimsonRose/CityPrint) | 工具 | 读取地图导出并交互浏览或生成地图 | Apache-2.0 |
| [kentherussell/ImportEarthLocation](https://github.com/kentherussell/ImportEarthLocation) | 模组 | 真实地点的地形、道路和铁路导入 | MIT |
| [Giscolab/CityTimeline-Mod](https://github.com/Giscolab/CityTimeline-Mod) | 模组 | GeoJSON 图层注入、对齐与历史地理可视化 | 待确认 |
| [Giscolab/cs2-realmap-generator](https://github.com/Giscolab/cs2-realmap-generator) | 工具 | OSM 提取、要素分类、GeoJSON 和地形制备 | MIT |
| [WFMinerva/cs2-real-map-toolkit](https://github.com/WFMinerva/cs2-real-map-toolkit) | 工具与模组 | 真实地理数据制备、铺轨和车站原生放置 | MIT |
| [hansheikimetsamaa/Planboard](https://github.com/hansheikimetsamaa/Planboard) | 模组 | 把工程备注和任务锚定到道路、建筑、区域和地面 | MIT |

### 开发、调试与兼容性

| 仓库 | 类型 | 参考用途 | 许可观察 |
|---|---|---|---|
| [CitiesSkylinesModding/agents-plugins](https://github.com/CitiesSkylinesModding/agents-plugins) | 开发工具 | Unity/ECS、Gameface UI、游戏日志和场景测试工具 | MIT |
| [mayor-modder/Cities2-MCP](https://github.com/mayor-modder/Cities2-MCP) | 开发工具 | 本地游戏百科、资料检索和模组工程辅助 | MIT |
| [krzychu124/SceneExplorer](https://github.com/krzychu124/SceneExplorer) | 模组 | 检查已选对象、ECS 组件和实体引用 | 待确认 |
| [BrokeAssSoftware/cs2-modding-guide](https://github.com/BrokeAssSoftware/cs2-modding-guide) | 文档 | 按技术分类的带源码出处的开发手册 | MIT |
| [Cities2Modding/wiki](https://github.com/Cities2Modding/wiki) | 文档 | 地图编辑器和早期 CS2 开发资料 | CC-BY-NC-SA-4.0 |
| [CitiesSkylinesModding/StockModTemplatesDiffer](https://github.com/CitiesSkylinesModding/StockModTemplatesDiffer) | 模板 | 官方 C# 与 UI 模组模板的版本对比 | Unlicense |
| [Captain-Of-Coit/cities-skylines-2-mod-template](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template) | 模板 | 早期 C#、BepInEx 与发布脚手架 | MIT-template |
| [Captain-Of-Coit/hookui](https://github.com/Captain-Of-Coit/hookui) | 库 | 旧版游戏 UI 注入与 React 嵌入 | MIT |
| [algernon-A/UnifiedIconLibrary](https://github.com/algernon-A/UnifiedIconLibrary) | 库 | 共享游戏风格图标和 UI 资源 | Apache-2.0 |
| [JadHajjar/Skyve-CS2](https://github.com/JadHajjar/Skyve-CS2) | 工具 | 模组配置、兼容性和游玩组合管理 | 待确认 |
| [rcav8tr/CS2Mod-PerformanceMonitor](https://github.com/rcav8tr/CS2Mod-PerformanceMonitor) | 模组 | 帧时间和游戏性能面板 | MIT |
| [Theorist100/VanillaProfiler](https://github.com/Theorist100/VanillaProfiler) | 模组 | Unity 主线程、同步点和内存分析 | MIT-code / separate asset terms |

### CityDirector 可先读的新增入口

- **游戏内验收**。`agents-plugins/plugins/unity-devtools/` 可以参考运行时检查，`plugins/cs2-modding/` 可以参考测试城市、日志和场景检查的组织。 [agents-plugins](https://github.com/CitiesSkylinesModding/agents-plugins)
- **公交运营**。`TimetableDispatchSystem.cs` 和 `LineLayover.cs` 可用于研究线路建好之后的发车与终点等待。Extended Transport Manager 的 `BelzontTLM/Systems/` 补充站点连接、车辆分组和线路观测。 [Transit Timetables](https://github.com/AmicusDeus/TransitTimetables) · [Extended Transport Manager](https://github.com/klyte45/CS2-ExtendedTransportManager)
- **原生批量建造**。ParkManager 提供公园预览与可编辑资产生成案例，`section2change` 提供整座立交一次预览和确认的案例。 [ParkManager](https://github.com/tomkpunkt/ParkManager) · [平改立](https://github.com/zilianli/section2change)
- **城区地图**。Carto 提供导出观察；`cs2-real-map-toolkit` 的 `shared/mod/HarbinBridgeLift/` 可研究铁路与车站放置；skyplan 的 SVG 叠层可研究提交前的规划预览。 [Carto](https://github.com/taipei-native/Carto) · [真实地图工具](https://github.com/WFMinerva/cs2-real-map-toolkit) · [skyplan](https://github.com/aous-afk/skyplan)
- **角色与布景**。CityLife 提供事件驱动的市民聚集参考，TimeWeatherAnarchy 提供拍摄时间与天气参考。精确到关键帧的演员调度仍需独立实现和游戏验证。 [CityLife](https://github.com/Crystalhihihi/cs2-citylife) · [TimeWeatherAnarchy](https://github.com/rodrigmatrix/TimeWeatherAnarchy)

## Flex It

[官方模组页](https://mods.paradoxplaza.com/mods/151744/Windows)，作者 Root Of Business。
GitHub 搜索使用过 `FlexIt`、`FlexIt language:C#`、`FlexIt in:name,description,readme skylines` 等查询，
未找到可核实的源仓库。这只是本次检索结果，不能推断它一定闭源。

Unsquare 的 [README](../third_party/1Chrakv-Unsquare/README.md) 链接 Flex It，
并明确说明它是独立实现、不包含 Flex It 代码。Unsquare 可作为变形技术参考，不冒充 Flex It 源码。

## 优先阅读的实现

- LancerComet：`CS2MCP.Bridge/BridgeSystem.cs` 和 `RequestHandlers.Transit.cs`。
  UIUpdate 消费请求；公共交通沿原生工具与寻路流程执行。它最接近建造、真实公交的底座。
- nachocheesedog：`mod/` 与 `server/`，以及 `skills/cs2-asset-pipeline/references/`。
  研究预览、地图观察和资产导入的实际限制；不自动执行仓库附带的脚本或技能。
- Move It：`yenyang-CS2-MoveIt/Code/MoveIt/`。
  重点是移动后的子对象、道路和更新标记，而不只是改 Transform。
- Unsquare：`Unsquare/Integration/Prefabs/VariantPrefabFactory.cs`、
  `Unsquare/Integration/Systems/VariantSystem.cs`、`Unsquare/Core/Persistence/VariantManifest.cs`。
  变形保存为资产变体；视觉地块与模拟地块必须分开理解。
- BetaTestDrivingMod：`Systems/ReliableDriveRuntime.cs`、`ReliableDriveLaneSyncSystem.cs`。
  研究接管和释放时的状态处理；不能据此宣称任意车辆已有通用时间轴。
- Recolor：`Recolor/Domain/CustomColorVariations.cs`；Copaste：`src/CopasteToolSystem.cs`。
  分别研究颜色作用范围与多对象组合。

## 会影响设计的发现

1. 上游 README 宣称具备某能力，不等于已在本机 Steam build `25127643` 验收。
2. Unsquare 明确保留原矩形模拟地块、居民和工作等功能属性。拉伸外观不能自动解决道路入口或站台连通。
3. Line Tool 的“线”用于摆放，道路的“线”用于网络，公交的“线”用于运营，演员的“线”用于动作；需要四种明确的数据模型。
4. Cinematic Renderer 是旧 BepInEx 项目，其 README 还说明构建涉及修改后的游戏程序集。保留研究，不选作首版运行依赖。
5. 两个 Move It 仓库是历史比较对象，不应把两个版本同时安装。
6. 参考集合覆盖当前最相关的能力，不宣称穷尽所有开源 CS2 模组。
