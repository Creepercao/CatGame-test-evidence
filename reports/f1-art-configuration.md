# 美术尺寸配置说明

F1的集中资源是 `Assets/Resources/Settings/WorldArtSettings.asset`。Unity菜单 **CatGame → Art → Select world art settings** 可定位。修改密度、脚线、参考画布或房间布局后，停止Play，执行 **CatGame → Art → Apply world art settings and rebuild Home**。该操作记录迁移结果到Build/F1，更新导入及Home，不删除meta，不重新切片，不恢复已删除三花旧Walk。改动可通过Git审阅与回退。

## 参数含义

- WorldPixelsPerUnit=32：每32个世界逻辑像素对应1世界单位，不代表每世界单位固定32屏幕像素。
- CatCanvasPixels=64：世界中猫的完整画布高度64/32=2世界单位，实际猫身体小于画布（透明留白属于动画设计）。32格旧帧按导入密度与画布高度自动换算，也显示为2单位。
- CatFootPixels=6：从64格画布底部算起的脚线。所有猫帧pivot为(0.5,6/64)，原点就是脚点。动画自身的蹲下/跳跃轮廓不被逐帧抹平。32格旧帧使用同一归一化比例（3格）。
- ReferencePixels=640×360：世界层最低完整视野20×11.25世界单位，窗口更宽/更窄时扩展一边。1920×1080是3倍，2560×1440是4倍。1600×900等采用Point完整铺满回退，会出现像素宽度交替，不能称为严格整数像素。
- UiPixelsPerUnit=100：九宫格UI的独立规则，保留border与原生分辨率文字。图标和ADV猫立绘使用固定UI矩形，不能把世界PPU直接当UI实际屏幕尺寸。
- HomeFootCenterPixels=(74,-34)，HomeHalfPixels=(217,120)：猫脚点活动矩形。RoomWalkSurface还会根据房间源图墙线/家具占地限制可达性；不代表整个矩形都能走。
- ItemGridOffsetPixels=(0,27)：用品逻辑格相对脚点矩形的偏移，保持原先用品分布。42个格子继续隐藏，仅已购买用品显示。
- BowlWidthPixels=40、BedWidthPixels=61、FeederHeightPixels=37、DecorationHeightPixels=22：实际不透明外形的目标尺寸，猫窝约为猫身体可互动大小，独立于透明画布。用品pivot按world_profiles的不透明范围底部中心设置。

## 换算

猫世界高度 = CatCanvasPixels / WorldPixelsPerUnit。
标准视野猫屏幕高度 = 猫世界高度 × 屏幕高度 / (2 × 相机orthographicSize)。
相机半高 = max(参考高度, 参考宽度×屏幕高/屏幕宽) / (2×PPU)。
素材实例缩放 = 目标世界画布高度 / (帧高 / 素材PPU)。

世界层独立低分辨率绘制，Overlay UI、插画CG与字体保持原生窗口分辨率；输入使用恢复后的完整原生viewport。实现需通过Windows/Android实际截图与点击验收，不能仅以公式作为后端已通过。

## 例外与保留项

房间Architecture/家具保留中心pivot与原图布局，家具独立ground控制遮挡；世界PPU统一32。图标/插画独立于世界尺度；旧Park为未加入Build Settings的编辑器预览，保留旧100PPU资源与坐标，不将其散布到正式Home规则。81张猫纹理（含旧兼容）、21用品、7房间与3UI由显式迁移共112张覆盖，原manual标签继续保留，其含义是避免自动重导覆盖；用户主动Apply只改列明的密度/pivot/filter/platform，不改变切片身份和源图像素。

当前配置完成写入；迁移首轮112/112身份一致、源220PNG摘要不变。最终画面/触摸/完整回归仍以[F1测试记录](F1素材坐标与像素显示测试记录.md)后续实际结果为准。发行时Windows与Android必须放入同一个Release。

F1首次完整回归后澄清：HomeFootCenterPixels表示出生脚点锚点；完整身体活动矩形中心HomeFloorCenter由其加上(画布半高−脚线)=26逻辑像素派生。用品格的27像素偏移仍相对出生脚点，运行时ItemGridOffset会扣除26像素后传给完整活动矩形，保持物品位置。CatActor使用上下不对称边界计算脚点安全区，避免上身越界，也不以统一最大半径缩小整片漫游区。
