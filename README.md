Ruri-RipperHook:跨引擎资产管线的 AOP Hook 框架,不改上游源码地扩展 AssetRipper、FModel 的读取、转换与导出,把异源资产(不同引擎版本、自定义容器与序列化)收敛成统一的 Unity 数据表示。
1. 产出的标准 Unity YAML 是 Blender 插件 RuriRipperImporter(https://github.com/ShiyumeMeguri/RuriRipperImporter)的直接输入,插件不经 FBX、不重新绑骨、不丢顶点流。
2. 上游 AssetRipper、FModel 作只读 submodule 冻结,差异一律运行时 Hook 注入;核心 Ruri.Hook 是基于 attribute 的 AOP 框架(前后置、整体替换、构造接管、IL 重写)。
3. 扩展从继承 RipperHookCommon 的类开始,[RipperHook(...)] 标适用范围,[RetargetMethod]/[RetargetMethodFunc]/[RetargetMethodCtorFunc] 标目标方法,启动期发现匹配当前配置的 Hook 类并调用 Initialize()。
4. 仓库结构:Source/Ruri.Hook 是 AOP 框架,Ruri.RipperHook 是 AssetRipper 增强层(GUI 与 CLI 为其入口),Ruri.FModelHook 是 UE 解码器模块与 FModel 增强(依赖方向 FModelHook 到 RipperHook,内核不引用 CUE4Parse),Ruri.ShaderDecompiler 是着色器反编译库,Ruri.Tpk 把类型树打包成 RuriTypeTree.tpk,Ruri.UEShaderTpkDumper 从 UE 源码导出 uniform buffer 布局种子。
5. 编译整个解决方案(dotnet build -c Debug --nologo);入口 Ruri.RipperHook.GUI.exe 与 Ruri.RipperHook.CLI.exe,CLI 用 --list-hooks 列出 Hook,--hook {Id} --load {path} --export {dir} 无头导出。
6. Hot Reload 下偶发的 Hook 失效来自增量残留,重新触发编译即可恢复。
7. 内核约束见 CLAUDE.md,框架事实与坑见 FRAMEWORK.md。
8. 致谢:ds5678(AssetRipper 原作者)、AnimeStudio、nesrak1(USCSandbox 作者)、Razmoth。
