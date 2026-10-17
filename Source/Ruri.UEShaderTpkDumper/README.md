Ruri.UEShaderTpkDumper:直接解析 UE 引擎源码,按引擎版本导出着色器 uniform buffer 布局种子,独立于 Ruri.FModelHook,一次性命令行运行。
1. 用法:dotnet run --project Source/Ruri.UEShaderTpkDumper -- [--ue-root {dir}] [--out-root {dir}] [--filter {regex}] [--list];默认扫 D:\GameStudy\UE 下文件夹名含 X.Y(.Z) 版本号的一级子目录(UE 原始的 UnrealEngine-X.Y.Z-release 命名不必改),输出到 Source/Ruri.FModelHook/EngineUbMetadata,--list 只列发现的引擎不写盘。
2. 引擎事实(uniform buffer 基础类型枚举与整数映射、对齐、哈希公式、usage flags)全部从该引擎源码读出,读不全就整个版本不输出;重跑先删该版本目录下旧的 *_MetaData.json。
3. 输出 {out}/{X.Y.Z}/{BindingName}_{LayoutHash:X8}_MetaData.json:布局来自 BEGIN_*_STRUCT 块,哈希与 FRHIUniformBufferLayoutInitializer::ComputeHash 逐位一致,着色器侧绑定名、BindingFlags、StaticSlot 来自 IMPLEMENT_*_STRUCT 扫描,三者都进哈希。
4. 同目录另出 ShaderType 种子(LAYOUT_FIELD)、_ShaderType/_VertexFactoryType/_ShaderPipelineType 的哈希到类名索引、材质 uniform buffer 配方与 preshader 操作码表。
5. VIEW_UNIFORM_BUFFER_MEMBER_TABLE 这类宏表先递归展开进结构体再走布局遍历。
