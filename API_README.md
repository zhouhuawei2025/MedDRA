# MedDRA API 开发文档

## 1. 文档目的

本文档面向后端、前端和联调开发人员，说明当前 `MedDRA_Backhend` 项目的 API 设计，包括：

- 每个接口的用途
- 请求体格式
- 返回体格式
- 主要内部调用链路
- 与 Qdrant、Embedding、LLM 相关的关键行为

---

## 2. 服务概览

当前 API 主要包含 4 类能力：

1. 获取 MedDRA 版本列表
2. 上传并解析待编码 Excel
3. 批量执行术语编码
4. 单词条即时编码
5. 导出编码结果 Excel

当前控制器如下：

- [MeddraController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/MeddraController.cs)
- [FilesController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/FilesController.cs)
- [EncodingController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/EncodingController.cs)

---

## 3. 公共约定

### 3.1 基础地址

本地开发时示例地址：

```text
http://localhost:5242
```

### 3.2 数据格式

- 普通接口默认使用 `application/json`
- 文件上传使用 `multipart/form-data`
- 文件导出返回 `xlsx` 二进制流

### 3.3 编码策略

`/api/encoding/run` 的内部策略是：

1. 对待编码术语生成向量
2. 去 Qdrant 对应版本 collection 中检索 Top N 候选
3. 若高置信，则直接返回前 3 个候选
4. 若非高置信，则调用 LLM 在 Top N 候选池中重排，选出 3 个候选
5. 返回完整层级实体给前端

注意：

- AI 不会直接生成新的 MedDRA 编码
- AI 只会在 Qdrant 返回的候选池中做排序
- 最终返回给前端的完整实体，来自后端对原始候选池的回查映射

---

## 4. 获取版本列表

### 4.1 接口

```http
GET /api/meddra/versions
```

### 4.2 用途

给前端下拉框提供可选 MedDRA 版本。

### 4.3 请求体

无

### 4.4 返回体

返回 JSON 数组：

```json
[
  {
    "version": "26.0",
    "collectionName": "meddra_26_0"
  },
  {
    "version": "28.1",
    "collectionName": "meddra_28_1"
  }
]
```

### 4.5 返回字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `version` | `string` | 前端展示和提交时使用的 MedDRA 版本 |
| `collectionName` | `string` | 后端实际检索的 Qdrant collection 名称 |

### 4.6 内部逻辑

调用链：

1. [MeddraController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/MeddraController.cs)
2. [MedDraVersionService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraVersionService.cs)
3. 从 [appsettings.json](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/appsettings.json) 的 `VectorStore.Collections` 读取版本映射

---

## 5. 上传并解析 Excel

### 5.1 接口

```http
POST /api/files/upload
Content-Type: multipart/form-data
```

### 5.2 用途

上传待编码 Excel，后端只解析术语列并返回预览结果。

注意：

- 该接口不执行向量检索
- 该接口不调用 AI
- 该接口只负责把 Excel 转成前端可展示的待编码行

### 5.3 请求体

表单字段：

| 字段名 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `file` | 文件 | 是 | 待编码 Excel 文件 |

### 5.4 Excel 格式约定

第一版默认读取：

- 第一个 worksheet
- 第一行作为表头

支持的术语列名：

- `Term`
- `待编码术语`
- `原始术语`

### 5.5 返回体

```json
{
  "fileName": "test.xlsx",
  "totalRows": 3,
  "rows": [
    {
      "rowNumber": 2,
      "rawTerm": "腹部压榨性疼痛"
    },
    {
      "rowNumber": 3,
      "rawTerm": "腹腔粘连"
    }
  ]
}
```

### 5.6 返回字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `fileName` | `string` | 上传文件名 |
| `totalRows` | `int` | 成功解析出的术语数量 |
| `rows` | `array` | 解析后的术语行列表 |
| `rows[].rowNumber` | `int` | Excel 行号 |
| `rows[].rawTerm` | `string` | 待编码术语 |

### 5.7 内部逻辑

调用链：

1. [FilesController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/FilesController.cs)
2. [EpplusExcelTermParser.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/EpplusExcelTermParser.cs)
3. 使用 `EPPlus` 解析 Excel
4. 返回 [UploadPreviewResponse.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Contracts/Files/UploadPreviewResponse.cs)

---

## 6. 执行编码

### 6.1 接口

```http
POST /api/encoding/run
Content-Type: application/json
```

### 6.2 用途

对前端提交的术语列表执行编码。

### 6.3 请求体

```json
{
  "version": "28.1",
  "highConfidenceThreshold": 0.72,
  "minimumScoreGap": 0.1,
  "terms": [
    "腹部压榨性疼痛",
    "腹腔粘连"
  ]
}
```

### 6.4 请求字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `version` | `string` | 是 | 前端选中的 MedDRA 版本 |
| `highConfidenceThreshold` | `number` | 否 | 高置信阈值，不传则使用配置值 |
| `minimumScoreGap` | `number` | 否 | Top1 与 Top2 的最小差值，不传则使用配置值 |
| `terms` | `array<string>` | 是 | 待编码术语列表 |

### 6.5 返回体

```json
{
  "version": "28.1",
  "totalCount": 1,
  "results": [
    {
      "rawTerm": "腹部压榨性疼痛",
      "version": "28.1",
      "top1Score": 0.72925425,
      "usedAi": false,
      "remark": "高置信命中，未调用 AI。",
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
          "socName": "各类损伤、中毒及操作并发症",
          "score": 0.72925425
        },
        {
          "rank": 2,
          "lltCode": "10007428",
          "lltName": "肠粘连",
          "ptCode": "10000050",
          "ptName": "腹腔粘连",
          "hltCode": "10034654",
          "hltName": "腹膜和腹膜后纤维化及粘连",
          "hgltCode": "10034652",
          "hgltName": "腹膜及腹膜后类疾病",
          "socCode": "10017947",
          "socName": "胃肠系统疾病",
          "score": 0.47691303
        }
      ]
    }
  ]
}
```

### 6.6 返回字段

#### 顶层字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `version` | `string` | 当前执行的 MedDRA 版本 |
| `totalCount` | `int` | 本次处理的术语数量 |
| `results` | `array` | 每个术语的编码结果 |

#### `results[]` 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `rawTerm` | `string` | 原始待编码术语 |
| `version` | `string` | 使用的 MedDRA 版本 |
| `top1Score` | `number` | Top1 候选的向量检索分数 |
| `usedAi` | `bool` | 是否调用了 LLM 重排 |
| `remark` | `string` | 处理说明 |
| `candidates` | `array` | 候选结果列表，通常取前 3 个 |

#### `candidates[]` 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `rank` | `int` | 排名 |
| `lltCode` | `string` | LLT 编码 |
| `lltName` | `string` | LLT 名称 |
| `ptCode` | `string` | PT 编码 |
| `ptName` | `string` | PT 名称 |
| `hltCode` | `string` | HLT 编码 |
| `hltName` | `string` | HLT 名称 |
| `hgltCode` | `string` | HLGT 编码 |
| `hgltName` | `string` | HLGT 名称 |
| `socCode` | `string` | SOC 编码 |
| `socName` | `string` | SOC 名称 |
| `score` | `number` | 该候选的原始检索分数 |

### 6.7 内部逻辑

调用链：

1. [EncodingController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/EncodingController.cs)
2. [MedDraEncodingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraEncodingService.cs)
3. [DashScopeEmbeddingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/DashScopeEmbeddingService.cs)
4. [QdrantSearchService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/QdrantSearchService.cs)
5. 若有需要，再进入 [DashScopeAiRerankService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/DashScopeAiRerankService.cs)

详细流程：

1. 接收前端提交的 `version + terms`
2. 对每条术语生成 embedding
3. 根据 `version` 映射到对应 collection 名
4. 使用 Qdrant REST API 从对应 collection 检索 Top N
5. 若 `Top1Score` 和 `Top1-Top2` 满足高置信规则，则直接取前 3 个
6. 否则调用 LLM 重排
7. LLM 只返回 `lltCode` 排名结果
8. 后端根据 `lltCode` 从原始候选池中映射回完整实体
9. 返回最终编码结果

### 6.8 高置信规则

当前逻辑由配置控制：

- `Encoding.HighConfidenceThreshold`
- `Encoding.MinimumScoreGap`

判断逻辑在：

- [MedDraEncodingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraEncodingService.cs)

规则为：

```text
Top1Score >= HighConfidenceThreshold
且
Top1Score - Top2Score >= MinimumScoreGap
```

满足时：

- 不调用 AI
- 直接返回检索前 3 名

不满足时：

- 调用 AI 在 Top N 候选池中重排

---

## 7. 导出结果

### 7.1 接口

```http
POST /api/files/export
Content-Type: application/json
```

### 7.2 用途

将前端当前结果表导出为 Excel。

### 7.3 请求体

请求体是 `EncodingResultDto[]` 数组，也就是前端当前表格中的结果列表。

示例：

```json
[
  {
    "rawTerm": "腹部压榨性疼痛",
    "version": "28.1",
    "top1Score": 0.72925425,
    "usedAi": false,
    "remark": "高置信命中，未调用 AI。",
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
        "socName": "各类损伤、中毒及操作并发症",
        "score": 0.72925425
      }
    ]
  }
]
```

### 7.4 返回体

返回 `xlsx` 文件流。

响应头示例：

```text
Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename=meddra-coding-result-20260402123000.xlsx
```

### 7.5 内部逻辑

调用链：

1. [FilesController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/FilesController.cs)
2. [EpplusExcelExportService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/EpplusExcelExportService.cs)
3. 使用 `EPPlus` 生成 Excel

---

## 8. 单词条即时编码

### 8.1 接口

```http
POST /api/encoding/single
Content-Type: application/json
```

### 8.2 用途

接收前端输入框中的单个术语，返回该术语的最优、次优、较优候选编码。

适用场景：

- 前端单词条即时查询
- 人工辅助编码
- 不通过 Excel 的快速验证

### 8.3 请求体

```json
{
  "version": "28.1",
  "term": "腹部压榨性疼痛",
  "highConfidenceThreshold": 0.72,
  "minimumScoreGap": 0.1
}
```

### 8.4 请求字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `version` | `string` | 是 | 前端选中的 MedDRA 版本 |
| `term` | `string` | 是 | 单个待编码术语 |
| `highConfidenceThreshold` | `number` | 否 | 高置信阈值，不传则使用配置值 |
| `minimumScoreGap` | `number` | 否 | Top1 与 Top2 的最小差值，不传则使用配置值 |

### 8.5 返回体

返回单个 `EncodingResultDto` 对象：

```json
{
  "rawTerm": "腹部压榨性疼痛",
  "version": "28.1",
  "top1Score": 0.72925425,
  "usedAi": false,
  "remark": "高置信命中，未调用 AI。",
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
      "socName": "各类损伤、中毒及操作并发症",
      "score": 0.72925425
    }
  ]
}
```

### 8.6 内部逻辑

该接口不会新建第二套编码逻辑，而是直接复用批量编码接口的核心服务：

1. [EncodingController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/EncodingController.cs)
2. 将 `term` 包装成只有一个元素的 `terms` 列表
3. 调用 [MedDraEncodingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraEncodingService.cs)
4. 从批量结果中取第一条返回给前端

这样做的目的是保证：

- 单条编码和批量编码使用同一套业务规则
- 阈值、Qdrant 检索、AI 重排逻辑完全一致
- 后续维护只需要改一套代码

---

## 9. 配置说明

核心配置文件：

- [appsettings.json](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/appsettings.json)

关键配置项：

### 8.1 `Embedder`

| 字段 | 说明 |
| --- | --- |
| `Endpoint` | Embedding 模型服务地址 |
| `Model` | Embedding 模型名 |
| `ApiKey` | Embedding 模型密钥 |

### 8.2 `LLM`

| 字段 | 说明 |
| --- | --- |
| `Endpoint` | LLM 服务地址 |
| `Model` | LLM 模型名 |
| `ApiKey` | LLM 密钥 |

### 8.3 `VectorStore`

| 字段 | 说明 |
| --- | --- |
| `Endpoint` | Qdrant REST 地址，当前 API 走 REST，不走 gRPC |
| `DefaultCollectionName` | 默认 collection |
| `Collections` | 版本到 collection 的映射 |

### 8.4 `Encoding`

| 字段 | 说明 |
| --- | --- |
| `HighConfidenceThreshold` | 高置信阈值 |
| `MinimumScoreGap` | Top1 与 Top2 的最小差值 |
| `SearchLimit` | Qdrant 检索的候选数，默认建议 10 |
| `OnlyCurrentTerms` | 是否只保留 `IsCurrent = true` 的术语 |

---

## 10. 内部结构说明

### 9.1 `Contracts`

放接口请求体、返回体模型。

示例：

- [EncodingRunRequest.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Contracts/Encoding/EncodingRunRequest.cs)
- [EncodingRunResponse.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Contracts/Encoding/EncodingRunResponse.cs)

### 9.2 `Domain`

放业务实体和业务内部模型。

示例：

- [MedDraTerm.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Domain/MedDraTerm.cs)
- [MedDraSearchCandidate.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Domain/MedDraSearchCandidate.cs)

### 9.3 `Services`

放核心业务逻辑。

示例：

- [MedDraEncodingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraEncodingService.cs)

### 9.4 `Infrastructure`

放外部依赖相关实现。

示例：

- Qdrant REST 模型
- AI JSON 反序列化工具
- LLM 相关返回结构

---

## 11. 当前实现中的关键设计

### 10.1 Qdrant 当前走 REST

当前 API 没有使用 Qdrant gRPC，而是统一走 REST API。

原因：

- 本地环境下 gRPC/HTTP2 可能出现连接兼容问题
- REST 已能满足当前检索需求
- 与导入器保持一致，便于维护

### 10.2 AI 只做重排，不做生成

LLM 的职责是：

- 在 Top N 候选池内排序

LLM 不负责：

- 自由生成新的 MedDRA 编码
- 返回数据库中不存在的术语

### 10.3 前端最终拿到的是完整实体

虽然 LLM 当前只返回 `lltCode`，但最终 API 返回给前端的是完整层级实体。

这是为了：

- 降低 AI 幻觉风险
- 保证返回字段可信
- 方便前端直接展示 `LLT -> SOC`

---

## 12. 联调建议

建议按以下顺序联调：

1. 测试 `GET /api/meddra/versions`
2. 测试 `POST /api/encoding/single`
3. 测试 `POST /api/encoding/run`
4. 测试 `POST /api/files/upload`
5. 最后测试 `POST /api/files/export`

如果没有前端，可以优先使用：

- 浏览器直接访问 `GET`
- 浏览器控制台 `fetch(...)` 测试 `POST`

---

## 13. 常见问题

### 12.1 为什么版本列表里有版本，但运行时报错？

因为配置里存在版本映射，不代表 Qdrant 中已经导入了对应 collection。  
必须确保导入器已经成功导入对应版本。

### 12.2 为什么明明匹配正确，分数却没有到 0.95？

不同 embedding 模型的分值分布不同。  
当前项目不应把 `0.95` 当作固定真理，应根据真实样本校准。

### 12.3 为什么 AI 返回了结果，但最终还是使用原始候选？

因为后端会校验 AI 返回的 `lltCode` 是否存在于原始候选池中。  
如果不存在，会回退到原始检索结果。

---

## 14. 相关文件索引

- [Program.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Program.cs)
- [appsettings.json](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/appsettings.json)
- [EncodingController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/EncodingController.cs)
- [FilesController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/FilesController.cs)
- [MeddraController.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Controllers/MeddraController.cs)
- [SingleEncodingRequest.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Contracts/Encoding/SingleEncodingRequest.cs)
- [MedDraEncodingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/MedDraEncodingService.cs)
- [DashScopeEmbeddingService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/DashScopeEmbeddingService.cs)
- [DashScopeAiRerankService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/DashScopeAiRerankService.cs)
- [QdrantSearchService.cs](/Users/pa/Desktop/MedDRA/MedDRA_Backhend/Services/QdrantSearchService.cs)
