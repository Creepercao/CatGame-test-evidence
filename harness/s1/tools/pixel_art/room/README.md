# 等距房间像素素材工具

项目根运行`python tools/pixel_art/room/make_isometric.py`，输出到`tools/pixel_art/output/room_iso_v1`。使用现有Python/Pillow，颜色数组交给PixelGrid绘制，不调用绘图API。

make_room.py提供基础素材，其平视方案仅保留为过程记录；make_isometric.py补充墙块及上下两块书柜，组合选定的等距双墙房间。每份输入64×64、最多16色含透明，512×288合成图不能当作64格输入。

当前33份源素材见assets和inventory.json，含两项旧备用块。architecture.png仅建筑，room.png含家具；猫咪合成图用于观感，不是游戏截图。UI单独使用双色遮罩色板供游戏染色，不烘焙文本。

设置`PYTHONPATH=Tests/robot/vendor`后执行`python -m robot --outputdir <新结果目录> tools/pixel_art/room/verify.robot`。独立验证源图、布局、建筑/完整合成、留白/接缝/20道具及整数放大。逐步记录和限制见[试作记录](../../../docs/等距房间与道具试作-2026-10-31.md)。

已按语义分组接入Unity，含地板/家具约束、20道具及UI遮罩。新图鉴公园由make_catalog_park.py生成12块完整64格JSON，verify_catalog.robot独立核对；旧房间/公园原图保留。不为每素材单建Sorting Layer，不调整暂缓的全项目尺寸方案。当前运行核验见docs/全项目核验-2026-11-01.md。
