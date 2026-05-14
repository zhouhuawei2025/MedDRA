# MedDRA 快速编码项目

## 1. 项目目标

本项目用于实现一个面向医学词典编码场景的快速编码系统，支持用户上传待编码术语 Excel，基于公司已有编码库、MedDRA词典向量检索和大模型复核，输出候选 MedDRA 编码结果，并支持导出结果 Excel。

项目目标：

- 支持单个词条输入 或 上传 `xlsx` 文件并解析待编码术语
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

### 3.1 MedDRA.Developer

#### A. 业务流程

1. 准备某个版本的 MedDRA 标准术语数据
2. 读取xlsx源文件
3. 提取每条术语的层级信息，如 `LLT / PT / HLT / HLGT / SOC`
4. 对用于检索的文本生成向量
5. 将向量批量写入到 Qdrant 对应的 `collection`
6. 对LltName增加索引`index`

建议每个 MedDRA 版本使用一个独立的 collection，例如：

- `meddra_26_1`
- `meddra_27_0`

#### B. MedDRA 字典 Excel Spec

根据当前提供的某个版本的 MedDRA 字典 Excel，后端导入时直接按以下列读取：

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

建议：

- 在导入时对列名做显式校验；如果缺少关键列，应直接报错并停止导入。

#### C. 数据模型

**以下实体可作为 Qdrant payload 的基础结构**

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

**Excel 字段映射**

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

其中：

- `IsCurrent = Y` 映射为 `true`
- `IsCurrent = N` 映射为 `false`
- 导入时应对所有编码字段按字符串处理，不要转成数值类型，避免前导零、格式化或科学计数法问题
- 在导入阶段对文本做 `Trim()`，去掉首尾空格
- 如果某行 `LLT` 或 `LLTCD` 为空，直接跳过并记录日志

**向量化文本格式**

```text
SearchText = $"{LltName} | PT: {PtName} | HLT: {Hlt} | HLGT: {Hglt} | SOC: {Soc}"
```

实际落地时，不要把“向量化文本”写死为某一个列，而是统一使用 `SearchText` 字段。这样即使后面调整检索策略，也不需要改动太多业务代码。

#### D. Qdrant设置
**检索策略**
使用余弦相似度匹配

**collection 命名**

每个版本一个 collection，例如：

- `meddra_26_1`
- `meddra_27_0`

**point id建议**

 `PointId`使用更稳定的唯一标识，例如：

```text
{version}_{lltcd}
```

- 将 `{version}_{lltcd}` 进行UTF8编码 → 计算SHA256哈希 → 取哈希前8字节转为ulong


**payload 建议** 

Qdrant payload 保存完整层级信息：

Payload = new Dictionary<string, object>
            {
                ["llt_code"] = item.LltCode,
                ["llt_name"] = item.LltName,
                ["pt_code"] = item.PtCode,
                ["pt_name"] = item.PtName,
                ["hlt"] = item.Hlt,
                ["hlt_code"] = item.HltCode,
                ["hglt"] = item.Hglt,
                ["hglt_code"] = item.HgltCode,
                ["soc"] = item.Soc,
                ["soc_code"] = item.SocCode,
                ["search_text"] = item.SearchText,
                ["version"] = item.Version,
                ["is_current"] = item.IsCurrent
            }

建议：

- 向量字段使用 `SearchText` 生成
- payload 中同时保留原始层级字段，便于前端展示和 LLM 重排
- 默认只导入 `IsCurrent = Y` 的术语；如果后续有业务需求，也可以通过配置决定是否导入`IsCurrent = N`的术语

#### 数据导入规则

为了适配当前这类标准 MedDRA Excel，建议 `MedDRA.Developer` 至少实现以下规则：

1. 默认读取第一个 worksheet
2. 默认第一行是表头
3. 表头名称按精确列名匹配
4. 跳过完全空白的行
5. `LLTCD` 作为 point 唯一标识的一部分
6. 导入前先校验 collection 是否存在，不存在则创建
7. 大批量写入时采用分批 upsert
8. 写入完毕后，对LltName创建索引，加快查询速度

### 3.2 MedDRA.Api

#### A. 业务流程

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

#### B. 文件夹划分

- `Controllers`
- `Contracts`
- `Domain`
- `Infrastructure`
- `Options`
- `Options`
- `Services`

核心服务：

- `ExcelImportService`
- `MedDraEncodingService`
- `EmbeddingService`
- `QdrantSearchService`
- `AiRerankService`
- `ExcelExportService`
- `MedDraVersionService`

#### C. 默认配置

```json
{
  "Embedder": {
    "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/",
    "Model": "text-embedding-v4",
    "ApiKey": "your-apikey"
  },
  "LLM": {
    "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1/",
    "Model": "qwen3-max",
    "ApiKey": "your-apikey"
  },
  "VectorStore": {
    "Endpoint": "http://10.30.55.112:6333",
    "ApiKey": "",
    "DefaultCollectionName": "meddra_28_1",
    "Collections": {
      "28.1": "meddra_28_1",
      "29.0": "meddra_29_0"
    }
  },
  "Encoding": {
    "HighConfidenceThreshold": 0.72,
    "MinimumScoreGap": 0.10,
    "SearchLimit": 15,
    "OnlyCurrentTerms": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

说明：

- `Endpoint`、`ApiKey`、`DefaultCollectionName` 放在配置文件中，不要直接写死到代码逻辑里
- 可以使用单一的 `appsettings.json` 作为实际运行配置
- `Collections` 更适合作为运行时参数，不建议完全写死
- `SearchLimit` 是 Qdrant 向量检索候选上限，当前建议值为 `15`；合并候选时会优先保留 LLT 精确匹配结果。
- `OnlyCurrentTerms = true` 时，后端只保留 `IsCurrent = true` 的候选；更推荐在构建 Qdrant collection 时就仅仅导入 `IsCurrent = Y` 的术语。

#### D. 编码策略

**基本策略**

对于每个待编码术语：

1. 先按 `llt_name` 做 Qdrant payload 精确匹配，返回的结果相似度默认为1.000
2. 再做向量检索，返回一定长度的候选结果，当前默认 `SearchLimit = 15`
3. 合并步骤1 步骤2的候选结果，合并时优先保留精确匹配，再补充向量候选，并按 `LLTCode` 去重
4. 若满足高置信条件，则直接返回相似度top3的结果
5. 若不满足，则调用 LLM 进行候选重排序

补充说明：

- `LLT 精确匹配` 使用 Qdrant `/points/scroll` + payload filter 实现。
- 如果同一个 `llt_name` 命中多条 LLT，会优先排列 `LLTCode = PTCode` 的标准条目。
- 向量检索仍用于处理非标准输入、同义表达、错拼或长描述等模糊匹配场景。

**高置信判定**

- `Top1Score >= 0.72`
- `Top1Score - Top2Score >= 0.1`

因为不同向量模型、不同距离函数下，分数含义可能不同，最好结合真实样本调参。

**LLM 输入**

当调用 LLM 时，输入内容建议包括：

- 原始待编码术语
- 合并后的候选池完整层级信息，来源包括 `LLT 精确匹配候选` 和 `向量相似度候选`
- 明确要求返回最优、次优、较优 3 条，以及排序的依据
- 明确要求输出 JSON，而不是自由文本

示例返回格式：

```json
{
  "candidates": [
    {
      "rank": 1,
      "lltCode": "10012345"
    },
    {
      "rank": 2,
      "lltCode": "10012346"
    },
    {
      "rank": 3,
      "lltCode": "10012347"
    }
  ],
  "usedAi": true,
  "reason": "候选1语义最接近原始术语"
}
```

提示词举例：

```Csharp
private static string BuildPrompt(string rawTerm, IReadOnlyList<MedDraSearchCandidate> candidates)
{
    var sb = new StringBuilder();
    sb.AppendLine($"待编码术语：{rawTerm}");
    sb.AppendLine("候选列表：");

    for (var i = 0; i < candidates.Count; i++)
    {
        var item = candidates[i];
        sb.AppendLine(
            $"{i + 1}. LLT={item.Term.LltName}({item.Term.LltCode}), PT={item.Term.PtName}({item.Term.PtCode}), HLT={item.Term.Hlt}({item.Term.HltCode}), HLGT={item.Term.Hglt}({item.Term.HgltCode}), SOC={item.Term.Soc}({item.Term.SocCode}), Score={item.Score:F4}");
    }

    sb.AppendLine("请只返回如下 JSON：");
    sb.AppendLine("{\"candidates\":[{\"rank\":1,\"lltCode\":\"...\"},{\"rank\":2,\"lltCode\":\"...\"},{\"rank\":3,\"lltCode\":\"...\"}],\"reason\":\"...\"}");
    Debug.WriteLine(sb.ToString());
    return sb.ToString();
}

chatResponse = await _chatClient.GetResponseAsync(
        [
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, "你是医学词典编码助手，需要对待编码字段选择合适的候选编码词条。请只从给定候选中选择最优、次优、较优 3条，返回纯 JSON。注意：1. 如果发现候选列表的匹配度很低，难以抉择，依旧选择最可能的前3条，严禁虚构不存在的编码；2. 优先选择PT和待编码术语相同，或LLT和和待编码术语相同的结果；3. 如果存在多个PT-LLT相同的结果，优先选择PTcode和LLTcode一致的结果"),
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, prompt)
        ],
        cancellationToken: cancellationToken);
```

补充说明：

由于AI返回结果的不确定性：

- 后端应对返回值做严格反序列化与校验，避免模型输出不规范导致业务失败；
- 后端在接收 LLM 返回后，用 `lltCode` 回查本地候选池，从候选池中拿到编码的完整实体结构。


#### E. 编码结果返回格式

后端返回的单行编码结果至少包含：

- 原始术语
- 最优结果完整实体
- 次优结果完整实体
- 较优结果完整实体
- Top1 相似度
- 是否调用 AI
- 使用的 MedDRA 版本
- 错误信息或备注

不要只返回 `LLTCode`，而是直接返回 3 个完整候选实体，因为前端需要展示从 `LLT` 到 `SOC` 的全层级信息。

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
  "remark": "未调用AI"
}
```

#### F. API 设计

**上传并解析 Excel**

`POST /api/files/upload`

功能：

- 上传 `xlsx`
- 后端解析待编码术语
- 返回前端表格预览数据
- 用户待编码 Excel 建议格式: 仅一列，列名为 `Term`或`待编码术语`或`原始术语`，如果用户上传的 Excel 不包含可识别术语列，接口应直接返回明确错误信息
- 后端解析时建议做列名兼容，但内部统一映射为： `RawTerm`


**获取可用 MedDRA 版本**

`GET /api/meddra/versions`

功能：

- 返回当前系统中已导入的 MedDRA 版本列表

**执行编码**

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

**导出结果**

`POST /api/files/export`

功能：

- 将当前表格结果导出为 `xlsx`

**详细内容详见API文档**


### 3.3 MedDRA.Web

**页面功能**

批量编码：
- 上传 Excel 按钮
- MedDRA 版本下拉框
- 数据预览表格
- 运行按钮
- 下载 Excel 按钮
- 编码进度与状态提示
- 编码结果卡片（可翻页，每10条为一页）

单个编码（等效于批次 = 1的批量编码）：
- 文本输入框
- MedDRA 版本下拉框
- 运行按钮
- 编码进度与状态提示
- 编码结果卡片



#### 4. 目录结构

```text
MedDRA/
├── README.md
├── src/
│   ├── MedDRA.Api/
│   ├── MedDRA.Web/
│   └── MedDRA.Developer/
```