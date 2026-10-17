EngineUbMetadata:按 (UBName, LayoutHash) 给 UE 引擎自带 uniform buffer(View、OpaqueBasePass、LumenCardScene 等)的成员命名,这些名字在 UE 5.1 全部 SM 与 5.4 SM5 的 cooked 数据里恢复不出来。
1. 种子由 Ruri.UEShaderTpkDumper 生成,按引擎版本分目录({X.Y.Z}/),文件名 {UBName}_{LayoutHash}_MetaData.json,LayoutHash 为 8 位十六进制。
2. 加载按 (UBName, LayoutHash) 精确命中且不重算哈希(公式只在 dumper 里一份);4.x 引擎里 buffer 的 static slot 下标那一字节运行期才定,是唯一放过的差异;只含常量不绑资源的 buffer 没有哈希,按名字匹配同样不含资源的种子。
3. 扫描顺序:游戏专属目录(FModel 的 EGame 枚举名)、基础目录 GAME_UE{major}_{minor}、含该版本号的版本目录;_ShaderType 子目录是 ShaderType 种子,不参与 uniform buffer 布局加载。
4. 构建只在目标不存在时才把种子复制到输出目录的 EngineUbMetadata/,保护本地手改;要强制刷新就删已部署的文件或整个目录。
5. JSON 字段:name、engineVersion 与 engineSource(仅文档)、layoutHash(判别键)、constantBufferSize(交叉核对)、bindingFlags、members[{offset,name,type}]、resources[{index,offset,name,type}]。
6. resources.index 必须等于 cooked SRT 里 FRHIResourceTableEntry 解出的 ResourceIndex,即引擎源码 Resources[] 的下标,不是绑定寄存器。
7. members.type 不分大小写,认 Float/Int/UInt/Bool/Half 的标量与 2 到 4 维向量及 Float4x4/Float3x3/Float3x4/Float4x3,其余静默跳过;resources.type 取 EUniformBufferBaseType 枚举名(UBMT_TEXTURE 与 UBMT_SRV 绑 t#,UBMT_SAMPLER 绑 s#,UBMT_UAV 绑 u#)。
8. 不命中不是错误:详细日志记一条 no match,着色器照常反编译,成员保持匿名 flat vec4 数组、资源落到带类型的占位符。
9. 核对哈希是否对得上时设 RURI_SRT_DEBUG=1,看 stderr 的 [SRT] LayoutHashes 行。
