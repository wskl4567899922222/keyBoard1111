# ByteTyping 键盘手（Unity 复刻版）

复刻 typing.byteworld.cn「键盘手」打字练习：目标键高亮 + 双手手指高亮 + 逐字符判色。

## 快速开始

1. 打开 Unity 工程（2022.3+），等待编译与导入完成（首次导入会把
   `Resources/Art/Hands` 下的 PNG 自动设为 Sprite，无需手动设置）。
2. 新建空场景（File > New Scene）。
3. Hierarchy 右键 > Create Empty，命名如 `ByteTyping`。
4. Inspector 点 Add Component，搜索并添加 `TypingPractice`。
5. 点击 Play 开始练习。

## 操作

- 直接打字：正确字符变绿，当前字符黄色，打错时当前字符闪红并累计错误数。
- `Backspace` 回退。
- 打完一课：`Enter` 下一课，`R` 重打本课。
- 顶部 `<` `>` 按钮切换章节 / 课程；视频课按 `Enter` 跳过。

## 与网站的效果对照

- 键盘目标键紫色高亮、配合键（Shift）深紫高亮、按下闪粉 `#DB73EB`：与网站一致。
- 双手手指高亮：使用从网站提取的同画布 SVG 渲染图层（`Resources/Art/Hands`，12 层），
  叠加方式与网站一致（基础双手 + 左手高亮 + 右手高亮 + Shift 高亮）。
- 大写字母 / 上位符号：目标键 + 对侧手 Shift 同时高亮（网站规则：
  左手字母用右 Shift，右手字母用左 Shift）。
- 唯一差异：普通数字键网站不高亮手指，这里默认高亮，便于练习；
  如需与网站完全一致，把 `FingerMap.HighlightPlainDigits` 设为 `false`。

## 目录结构

- `Scripts/TypingPractice.cs` 主控制器：程序化构建全部 UI，空场景挂载即用
- `Scripts/KeyboardPanel.cs` 程序化键盘（行布局与键宽取自网站前端）
- `Scripts/HandOverlay.cs` 双手图层叠加
- `Scripts/FingerMap.cs` 手指-按键映射（含大写/符号的 Shift 配合规则）
- `Scripts/TypingData.cs` 课程数据模型与加载
- `Editor/ByteTypingImportFixer.cs` 自动把本目录贴图导入为 Sprite
- `Resources/Data/typing_course.json` 6 章 119 课课程数据（含正文）
- `Resources/Art/Hands/*.png` 手部高亮图层（同画布 1880x962 透明 PNG）

## 输入设置

脚本使用旧版输入（`Input.inputString` / `Input.GetKeyDown`）。若工程
Player Settings > Active Input Handling 设为 "Input System Package (New)"，
请改为 **"Input Manager (Old)"** 或 **"Both"**，否则运行时会报错。
