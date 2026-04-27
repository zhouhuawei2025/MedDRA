# MedDRA 快速编码项目

## 1. 项目目标

本项目用于实现一个面向医学词典编码场景的快速编码系统，支持用户上传待编码术语 Excel，基于向量检索和大模型复核，输出候选 MedDRA 编码结果，并支持导出结果 Excel。

项目目标：

- 支持上传 `xlsx` 文件并解析待编码术语
- 支持选择不同的 `MedDRA` 版本进行编码
- 基于 `Embedding + Qdrant` 实现近似术语检索
- 对高置信结果直接返回，对低置信结果调用 `LLM` 做二次判断
- 返回 `最优 / 次优 / 较优` 三档结果
- 支持前端表格查看和结果导出

---

## 2. 总体技术路线

建议采用前后端分离 + 离线导入器的架构：

### 2.1 项目组成

建议拆分为 3 个子项目：

1. `MedDRA.Developer`
   用于离线导入 MedDRA 字典、生成向量、写入 Qdrant

2. `MedDRA.Api`
   基于 `ASP.NET Core Web API`，负责 Excel 解析、编码任务、结果导出、版本管理

3. `MedDRA.Web`
   基于 `React + TypeScript`，负责上传、展示、运行、下载等页面交互

---

## 3. 核心业务流程

### 3.1 数据准备阶段

1. 准备某个版本的 MedDRA 标准术语数据
2. 使用 `MedDRA.Developer` 读取源文件
3. 提取每条术语的层级信息，如 `LLT / PT / HLT / HLGT / SOC`
4. 对用于检索的文本生成向量
5. 写入到 Qdrant 对应的 `collection`

建议每个 MedDRA 版本使用一个独立的 collection，例如：

- `meddra_26_1`
- `meddra_27_0`

### 3.1.1 字典 Excel 字段来源

根据当前提供的 MedDRA 字典 Excel，后端导入时可直接按以下列读取：

- `IsCurrent`
- `LLT`
- `LLTCD`
- `PT`
- `PTCD`
- `HLT`
- `HLTCD`
- `HLGT`
- `HLGTCD`
- `SOC`
- `SOCCD`

其中：

- `LLT` 是最核心的底层术语名称
- `LLTCD` 是 LLT 编码
- `PT / HLT / HLGT / SOC` 是逐级向上的层级信息
- `IsCurrent` 用于区分当前是否为有效术语

建议将这份 Excel 作为 `MedDRA.Developer` 的标准输入格式，并在导入时对列名做显式校验；如果缺少关键列，应直接报错并停止导入。

### 3.2 编码执行阶段

1. 用户在前端上传待编码 `xlsx` 【用户也可以在文本框内输入词条，进行单个编码】
2. 前端调用后端上传接口
3. 后端解析 Excel，并将待编码术语返回前端预览
4. 用户选择 MedDRA 版本并点击运行
5. 后端先用待编码术语按 `llt_name` 做 Qdrant payload 精确匹配
6. 后端生成待编码术语向量，并在对应版本的 Qdrant collection 中检索相似候选
7. 将 `LLT 精确匹配候选` 放在前面，再补充 `向量候选`，并按 `LLTCode` 去重
8. 若结果达到高置信阈值，则直接输出候选结果
9. 若结果置信度不足，则将合并后的候选池交给 LLM 做重排序
10. 后端返回 `最优 / 次优 / 较优` 和是否调用 AI
11. 用户确认后下载结果 Excel

---

## 4. 系统架构

## 4.1 技术选型

- 后端：`ASP.NET Core Web API`
- 前端：`React + TypeScript`
- 向量数据库：`Qdrant`
- Embedding 模型：兼容 OpenAI 协议的向量模型
- 大模型：兼容 OpenAI 协议的对话模型
- Excel 处理：`EPPlus`

### 4.2 模块划分

#### `MedDRA.Api`

分层如下：

- `Controllers`
- `Application`
- `Domain`
- `Infrastructure`

核心服务：

- `ExcelImportService`
- `MedDraEncodingService`
- `EmbeddingService`
- `QdrantSearchService`
- `AiRerankService`
- `ExcelExportService`
- `MedDraVersionService`

#### `MedDRA.Web`

页面功能：

- 上传 Excel 按钮
- MedDRA 版本下拉框
- 数据预览表格
- 运行按钮
- 下载 Excel 按钮
- 编码进度与状态提示

#### `MedDRA.Developer`

能力：

- 读取原始 MedDRA 数据
- 数据清洗与字段映射
- 批量生成向量
- 批量写入 Qdrant
- 输出导入日志

---

## 5. 数据模型设计

### 5.1 MedDRA 标准编码实体

以下实体可作为 Qdrant payload 的基础结构：

```csharp
public class MedDraTerm
{
    public string LltCode { get; set; } = string.Empty;
    public string LltName { get; set; } = string.Empty;
    public string PtCode { get; set; } = string.Empty;
    public string PtName { get; set; } = string.Empty;
    public string Hlt { get; set; } = string.Empty;
    public string HltCode { get; set; } = string.Empty;
    public string Hglt { get; set; } = string.Empty;
    public string HgltCode { get; set; } = string.Empty;
    public string Soc { get; set; } = string.Empty;
    public string SocCode { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsCurrent { get; set; } = true;
}
```

### 5.1.1 Excel 字段映射建议

按以下方式完成 Excel 列到实体字段的映射：

| Excel 列名 | 实体字段 |
| --- | --- |
| `IsCurrent` | `IsCurrent` |
| `LLT` | `LltName` |
| `LLTCD` | `LltCode` |
| `PT` | `PtName` |
| `PTCD` | `PtCode` |
| `HLT` | `Hlt` |
| `HLTCD` | `HltCode` |
| `HLGT` | `Hglt` |
| `HLGTCD` | `HgltCode` |
| `SOC` | `Soc` |
| `SOCCD` | `SocCode` |

补充建议：

- `IsCurrent = Y` 映射为 `true`
- `IsCurrent = N` 映射为 `false`
- 导入时应对所有编码字段按字符串处理，不要转成数值类型，避免前导零、格式化或科学计数法问题
- 建议在导入阶段对文本做 `Trim()`，去掉首尾空格
- 如果某行 `LLT` 或 `LLTCD` 为空，建议直接跳过并记录日志

### 5.2 检索文本建议

第一版可以只对 `LltName` 生成向量；但从可扩展性考虑，建议保留一个统一检索文本字段，例如：

```text
LltName | PT: PtName | HLT: Hlt | SOC: Soc
```

这样后续如果检索效果不足，可以更方便升级检索策略:

```text
SearchText = $"{LltName} | PT: {PtName} | HLT: {Hlt} | HLGT: {Hglt} | SOC: {Soc}"
```

实际落地时，不要把“向量化字段”写死为某一个列，而是统一使用 `SearchText` 字段。这样即使后面调整检索策略，也不需要改动太多业务代码。

### 5.3 编码结果模型

建议后端返回的单行编码结果至少包含：

- 原始术语
- 最优结果完整实体
- 次优结果完整实体
- 较优结果完整实体
- Top1 相似度
- 是否调用 AI
- 使用的 MedDRA 版本
- 错误信息或备注

建议不要只返回 `LLTCode`，而是直接返回 3 个完整候选实体，因为前端需要展示从 `LLT` 到 `SOC` 的全层级信息。

可参考如下结构：

```json
{
  "rawTerm": "腹部压榨性疼痛",
  "version": "26.1",
  "top1Score": 0.9721,
  "usedAi": false,
  "candidates": [
    {
      "rank": 1,
      "lltCode": "10000044",
      "lltName": "腹部压榨性疼痛",
      "ptCode": "10000044",
      "ptName": "腹部压榨性疼痛",
      "hltCode": "10083613",
      "hltName": "腹部及胃肠道损伤（不另分类）",
      "hgltCode": "10022114",
      "hgltName": "各种损伤（不另分类）",
      "socCode": "10022117",
      "socName": "各类损伤、中毒及操作并发症"
    },
    {
      "rank": 2,
      "lltCode": "10000050",
      "lltName": "腹腔粘连",
      "ptCode": "10000050",
      "ptName": "腹腔粘连",
      "hltCode": "10034654",
      "hltName": "腹膜和腹膜后纤维化及粘连",
      "hgltCode": "10034652",
      "hgltName": "腹膜及腹膜后类疾病",
      "socCode": "10017947",
      "socName": "胃肠系统疾病"
    },
    {
      "rank": 3,
      "lltCode": "10000051",
      "lltName": "示例术语3",
      "ptCode": "10000051",
      "ptName": "示例PT3",
      "hltCode": "10000052",
      "hltName": "示例HLT3",
      "hgltCode": "10000053",
      "hgltName": "示例HLGT3",
      "socCode": "10000054",
      "socName": "示例SOC3"
    }
  ],
  "remark": ""
}
```

---

## 6. Qdrant

### 6.1 collection 命名

建议每个版本一个 collection，例如：

- `meddra_26_1`
- `meddra_27_0`

### 6.2 point id

不建议直接使用循环序号作为 `PointId`。建议使用更稳定的唯一标识，例如：

- `LltCode`
- 或 `Version + LltCode`

这样更有利于重建、增量导入、幂等更新。

### 6.3 payload

Qdrant payload 保存完整层级信息，包括：

- `llt_code`
- `llt_name`
- `pt_code`
- `pt_name`
- `hlt`
- `hlt_code`
- `hglt`
- `hglt_code`
- `soc`
- `soc_code`
- `search_text`
- `version`
- `is_current`

建议：

- 向量字段使用 `SearchText` 生成
- payload 中同时保留原始层级字段，便于前端展示和 LLM 重排
- 默认只导入 `IsCurrent = Y` 的术语；如果后续有业务需求，也可以通过配置决定是否导入非当前术语

### 6.4 导入器处理规则

为了适配当前这类标准 MedDRA Excel，建议 `MedDRA.Importer` 至少实现以下规则：

1. 默认读取第一个 worksheet
2. 默认第一行是表头
3. 表头名称按精确列名匹配
4. 跳过完全空白的行
5. `LLTCD` 作为 point 唯一标识的一部分
6. 导入前先校验 collection 是否存在，不存在则创建
7. 大批量写入时采用分批 upsert

推荐 point id 形式：

```text
{version}_{lltcd}
```

---

## 7. AI 编码策略

### 7.1 基本策略

对于每个待编码术语：

1. 先按 `llt_name` 做 Qdrant payload 精确匹配，避免标准 LLT 词面完全一致时被向量排序挤出候选池
2. 再做向量检索，当前默认 `SearchLimit = 15`
3. 合并候选时优先保留精确匹配，再补充向量候选，并按 `LLTCode` 去重
4. 若满足高置信条件，则直接返回结果
5. 若不满足，则调用 LLM 进行候选重排序

补充说明：

- `LLT 精确匹配` 使用 Qdrant `/points/scroll` + payload filter 实现，不需要后端缓存 9 万条编码词典。
- 如果同一个 `llt_name` 命中多条 LLT，会优先排列 `LLTCode = PTCode` 的标准条目。
- 向量检索仍用于处理非标准输入、同义表达、错拼或长描述等模糊匹配场景。

### 7.2 高置信判定

- `Top1Score >= 0.72`
- `Top1Score - Top2Score >= 0.1`

因为不同向量模型、不同距离函数下，分数含义可能不同，最好结合真实样本调参。

### 7.3 LLM 输入

当调用 LLM 时，输入内容建议包括：

- 原始待编码术语
- 合并后的候选池完整层级信息，来源包括 `LLT 精确匹配候选` 和 `向量相似度候选`
- 明确要求返回最优、次优、较优 3 条
- 明确要求输出 JSON，而不是自由文本

示例返回格式：

```json
{
  "candidates": [
    {
      "rank": 1,
      "lltCode": "10012345",
      "lltName": "示例LLT1",
      "ptCode": "10022345",
      "ptName": "示例PT1",
      "hltCode": "10032345",
      "hltName": "示例HLT1",
      "hgltCode": "10042345",
      "hgltName": "示例HLGT1",
      "socCode": "10052345",
      "socName": "示例SOC1"
    },
    {
      "rank": 2,
      "lltCode": "10012346",
      "lltName": "示例LLT2",
      "ptCode": "10022346",
      "ptName": "示例PT2",
      "hltCode": "10032346",
      "hltName": "示例HLT2",
      "hgltCode": "10042346",
      "hgltName": "示例HLGT2",
      "socCode": "10052346",
      "socName": "示例SOC2"
    },
    {
      "rank": 3,
      "lltCode": "10012347",
      "lltName": "示例LLT3",
      "ptCode": "10022347",
      "ptName": "示例PT3",
      "hltCode": "10032347",
      "hltName": "示例HLT3",
      "hgltCode": "10042347",
      "hgltName": "示例HLGT3",
      "socCode": "10052347",
      "socName": "示例SOC3"
    }
  ],
  "usedAi": true,
  "reason": "候选1语义最接近原始术语"
}
```

后端应对返回值做严格反序列化与校验，避免模型输出不规范导致业务失败。
同时建议后端在接收 LLM 返回后，用 `lltCode` 回查本地候选池或 Qdrant payload，确保最终返回给前端的是完整实体结构，而不是只有模型生成的碎片字段。

---

## 8. 配置设计

结构如下：

```json
{
  "Embedder": {
    "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/",
    "Model": "text-embedding-v4",
    "ApiKey": ""
  },
  "LLM": {
    "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/",
    "Model": "qwen3-max",
    "ApiKey": ""
  },
  "VectorStore": {
    "Endpoint": "http://localhost:6333",
    "ApiKey": "",
    "CollectionName": "meddra_26_1"
  },
  "Encoding": {
    "HighConfidenceThreshold": 0.72,
    "MinimumScoreGap": 0.10,
    "SearchLimit": 15,
    "OnlyCurrentTerms": true
  }
}
```

说明：

- 本项目如果仅在局域网内单环境运行，可以不必引入复杂的多环境配置体系
- `Endpoint`、`ApiKey`、`DefaultCollectionName` 仍建议放在配置文件中，不要直接写死到代码逻辑里
- 可以使用单一的 `appsettings.json` 作为实际运行配置
- 如果团队协作开发，仍建议把真实密钥放在不纳入版本控制的本地配置文件中
- `CollectionName` 更适合作为运行时参数，不建议完全写死
- `SearchLimit` 是 Qdrant 向量检索候选上限，当前建议值为 `15`；合并候选时会优先保留 LLT 精确匹配结果。
- `OnlyCurrentTerms = true` 时，后端只保留 `IsCurrent = true` 的候选；更推荐在构建 Qdrant collection 时就只导入 `IsCurrent = Y` 的术语。

---

## 9. API 设计

### 9.1 上传并解析 Excel

`POST /api/files/upload`

功能：

- 上传 `xlsx`
- 后端解析待编码术语
- 返回前端表格预览数据

建议这里区分两类 Excel：

- 字典导入 Excel：供 `MedDRA.Importer` 使用，结构固定，包含 `LLT / PT / HLT / HLGT / SOC` 等完整列
- 用户待编码 Excel：供前端上传使用，结构应尽量简单，第一版建议只要求一个术语列

### 9.1.1 用户待编码 Excel 建议格式

第一版建议仅要求至少一列：

- `Term`

也可以兼容中文列名，例如：

- `待编码术语`
- `原始术语`

后端解析时建议做列名兼容，但内部统一映射为：

- `RawTerm`

如果用户上传的 Excel 不包含可识别术语列，接口应直接返回明确错误信息。

### 9.2 获取可用 MedDRA 版本

`GET /api/meddra/versions`

功能：

- 返回当前系统中已导入的 MedDRA 版本列表

### 9.3 执行编码

`POST /api/encoding/run`

请求内容建议包含：

- 待编码术语列表
- 指定 MedDRA 版本
- 可选参数，如相似度阈值

返回内容建议包含：

- 每条原始术语的编码结果
- 是否调用 AI
- 相似度分数
- 执行状态

### 9.4 导出结果

`POST /api/files/export`

功能：

- 将当前表格结果导出为 `xlsx`

---

## 10. 前端页面

页面包含以下元素：

- 页面标题
- MedDRA 版本下拉框
- 上传 Excel 按钮
- 数据表格
- 运行按钮
- 下载结果按钮
- 状态提示区域

表格列：

- 原始术语
- 最优结果 LLT - SOC
- 次优结果 LLT - SOC
- 较优结果 LLT - SOC
- Top1Score
- 是否调用 AI
- AI remarks

---

## 11. Excel 处理

项目中可使用 `EPPlus` 进行 Excel 导入导出。

### 11.1 字典 Excel 导入约定

- 第一行为表头
- 字段名使用：
  `IsCurrent`、`LLT`、`LLTCD`、`PT`、`PTCD`、`HLT`、`HLTCD`、`HLGT`、`HLGTCD`、`SOC`、`SOCCD`
- 编码列统一按文本读取
- 文本列读取后做空白清理
- 导入日志记录总行数、成功行数、跳过行数、失败行数

---

## 12. 性能与可扩展性

### 12.1 Embedding 调用

不要对每条术语完全串行调用 embedding。建议：

- 做批处理
- 控制并发数
- 增加重试机制

### 12.2 LLM 调用

LLM 只处理低置信候选，避免全部走大模型，减少成本和延迟。

---


## 13. 后续可扩展方向

后续可以考虑增加：

- 人工审核确认功能
- 术语标准化预处理
- 同义词、缩写、错拼纠正
- 多语言支持
- 历史编码缓存
- 任务异步化与队列化
- 编码结果审核报告

---

## 16. 目录结构

```text
MedDRA/
├── README.md
├── docs/
├── src/
│   ├── MedDRA.Api/
│   ├── MedDRA.Web/
│   └── MedDRA.Developer/
```