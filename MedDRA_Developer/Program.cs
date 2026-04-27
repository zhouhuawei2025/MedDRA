using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using MedDRA_Developer.Constants;
using MedDRA_Developer.Infrastructure.Qdrant;
using MedDRA_Developer.Models;
using MedDRA_Developer.Options;
using MedDRA_Developer.Utilities;
using OfficeOpenXml;
using OpenAI;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var importOptions = configuration.GetSection("Import").Get<ImportOptions>()
    ?? throw new InvalidOperationException("缺少 Import 配置。");
var embeddingOptions = configuration.GetSection("Embedder").Get<EmbeddingOptions>()
    ?? throw new InvalidOperationException("缺少 Embedder 配置。");
var vectorStoreOptions = configuration.GetSection("VectorStore").Get<VectorStoreOptions>()
    ?? throw new InvalidOperationException("缺少 VectorStore 配置。");

importOptions.FilePath = PromptForFilePath();

ValidateOptions(importOptions, embeddingOptions, vectorStoreOptions);

ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var terms = ReadTerms(importOptions);
if (terms.Count == 0)
{
    Console.WriteLine("未读取到任何可导入的 MedDRA 术语。");
    return;
}

Console.WriteLine($"读取完成，共 {terms.Count} 条术语。");

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(embeddingOptions.ApiKey),
    new OpenAIClientOptions { Endpoint = new Uri(embeddingOptions.Endpoint) });
var embeddingClient = openAiClient.GetEmbeddingClient(embeddingOptions.Model);
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = embeddingClient.AsIEmbeddingGenerator();

// 已验证可用方案：使用 Qdrant REST API，避免 macOS 本地 gRPC/HTTP2 连接问题。
using var qdrantHttpClient = new HttpClient
{
    BaseAddress = new Uri(vectorStoreOptions.Endpoint)
};
var qdrantClient = new RestQdrantClient(qdrantHttpClient);

// 实验方案：Qdrant.Client 官方 C# SDK 底层走 gRPC，在当前 macOS 环境会报 HTTP/2 连接错误。
// using Qdrant.Client;
// using Qdrant.Client.Grpc;
// var qdrantClient = new QdrantClient("localhost", 6334);

Console.WriteLine("正在获取向量维度...");
var sampleEmbedding = await GenerateVectorAsync(embeddingGenerator, terms[0].SearchText);
var vectorSize = (ulong)sampleEmbedding.Length;
Console.WriteLine($"向量维度:{vectorSize}");

await EnsureCollectionAsync(qdrantClient, importOptions.CollectionName, vectorSize, importOptions.RecreateCollectionIfVectorSizeChanged);
await qdrantClient.CreatePayloadIndexIfPossibleAsync(importOptions.CollectionName, "llt_name", "keyword");
Console.WriteLine("Payload index 已创建或已确认：llt_name(keyword)");

Console.WriteLine("开始生成向量并写入 Qdrant...");
for (var index = 0; index < terms.Count; index += importOptions.BatchSize)
{
    var batch = terms.Skip(index).Take(importOptions.BatchSize).ToArray();
    var points = new List<QdrantPoint>(batch.Length);

    foreach (var item in batch)
    {
        var embedding = await GenerateVectorAsync(embeddingGenerator, item.SearchText);
        points.Add(new QdrantPoint
        {
            Id = StableIdGenerator.From(importOptions.Version, item.LltCode),
            Vector = embedding,
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
        });
    }

    // 分批 upsert，避免一次性提交过多点位导致请求体过大。
    await qdrantClient.UpsertAsync(importOptions.CollectionName, points);
    Console.WriteLine($"已写入 {Math.Min(index + batch.Length, terms.Count)}/{terms.Count}");
}

Console.WriteLine("导入完成。");

//将文本转为向量
static async Task<float[]> GenerateVectorAsync(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string text)
{
    var embedding = await embeddingGenerator.GenerateAsync(text);
    return embedding.Vector.ToArray();
}

static void ValidateOptions(ImportOptions importOptions, EmbeddingOptions embeddingOptions, VectorStoreOptions vectorStoreOptions)
{
    if (string.IsNullOrWhiteSpace(importOptions.Version))
    {
        throw new InvalidOperationException("Import:Version 未配置。");
    }

    if (string.IsNullOrWhiteSpace(importOptions.CollectionName))
    {
        throw new InvalidOperationException("Import:CollectionName 未配置。");
    }

    if (string.IsNullOrWhiteSpace(embeddingOptions.Endpoint))
    {
        throw new InvalidOperationException("Embedder:Endpoint 未配置。");
    }

    if (string.IsNullOrWhiteSpace(embeddingOptions.Model))
    {
        throw new InvalidOperationException("Embedder:Model 未配置。");
    }

    if (string.IsNullOrWhiteSpace(embeddingOptions.ApiKey))
    {
        throw new InvalidOperationException("Embedder:ApiKey 未配置。");
    }

    if (string.IsNullOrWhiteSpace(vectorStoreOptions.Endpoint))
    {
        throw new InvalidOperationException("VectorStore:Endpoint 未配置。");
    }
}

static string PromptForFilePath()
{
    while (true)
    {
        Console.Write("请输入 MedDRA 字典 Excel 的绝对路径，然后回车: ");
        var input = Console.ReadLine()?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("路径不能为空，请重新输入。");
            continue;
        }

        if (!Path.IsPathRooted(input))
        {
            Console.WriteLine("请输入绝对路径。");
            continue;
        }

        if (!File.Exists(input))
        {
            Console.WriteLine("文件不存在，请确认路径后重试。");
            continue;
        }

        return input;
    }
}

// 检查向量数据库的维度是否一致，以及 collection 是否已经存在。
static async Task EnsureCollectionAsync(RestQdrantClient qdrantClient, string collectionName, ulong vectorSize, bool recreateIfVectorSizeChanged)
{
    var exists = await qdrantClient.CollectionExistsAsync(collectionName);
    var needRecreate = false;

    if (exists)
    {
        var existingSize = await qdrantClient.GetVectorSizeAsync(collectionName);
        if (existingSize != vectorSize)
        {
            if (!recreateIfVectorSizeChanged)
            {
                throw new InvalidOperationException(
                    $"Collection {collectionName} 已存在，但向量维度不一致：现有 {existingSize}，当前 {vectorSize}。");
            }

            Console.WriteLine($"Collection {collectionName} 已存在，但向量维度不一致，将重建。");
            needRecreate = true;
        }
    }
    else
    {
        needRecreate = true;
    }

    if (!needRecreate)
    {
        Console.WriteLine($"Collection {collectionName} 已存在，维度匹配。");
        return;
    }

    if (exists)
    {
        await qdrantClient.DeleteCollectionAsync(collectionName);
    }

    await qdrantClient.CreateCollectionAsync(collectionName, vectorSize);

    Console.WriteLine($"Collection {collectionName} 创建成功。");
}

static List<MedDraCode> ReadTerms(ImportOptions importOptions)
{
    using var package = new ExcelPackage(new FileInfo(importOptions.FilePath));
    var worksheet = package.Workbook.Worksheets.FirstOrDefault()
        ?? throw new InvalidOperationException("Excel 中未找到任何工作表。");

    var medDraCodes = new List<MedDraCode>();
    var totalRows = worksheet.Dimension?.Rows ?? 0;

    for (var row = 2; row <= totalRows; row++)
    {
        var llt = GetText(worksheet, row, ExcelColumnIndexes.Llt);
        var lltCode = GetText(worksheet, row, ExcelColumnIndexes.LltCode);
        if (string.IsNullOrWhiteSpace(llt) || string.IsNullOrWhiteSpace(lltCode))
        {
            continue;
        }

        var isCurrent = string.Equals(GetText(worksheet, row, ExcelColumnIndexes.IsCurrent), "Y", StringComparison.OrdinalIgnoreCase);

        var item = new MedDraCode
        {
            IsCurrent = isCurrent,
            LltName = llt,
            LltCode = lltCode,
            PtName = GetText(worksheet, row, ExcelColumnIndexes.Pt),
            PtCode = GetText(worksheet, row, ExcelColumnIndexes.PtCode),
            Hlt = GetText(worksheet, row, ExcelColumnIndexes.Hlt),
            HltCode = GetText(worksheet, row, ExcelColumnIndexes.HltCode),
            Hglt = GetText(worksheet, row, ExcelColumnIndexes.Hglt),
            HgltCode = GetText(worksheet, row, ExcelColumnIndexes.HgltCode),
            Soc = GetText(worksheet, row, ExcelColumnIndexes.Soc),
            SocCode = GetText(worksheet, row, ExcelColumnIndexes.SocCode),
            Version = importOptions.Version
        };

        // SearchText 是唯一参与向量化的文本，后续如果要调检索效果，优先改这里。
        item.SearchText = importOptions.UseHierarchyInSearchText
            ? $"{item.LltName} | PT: {item.PtName} | HLT: {item.Hlt} | HLGT: {item.Hglt} | SOC: {item.Soc}"
            : item.LltName;

        medDraCodes.Add(item);
    }

    return medDraCodes;
}

static string GetText(ExcelWorksheet worksheet, int row, int column)
{
    return worksheet.Cells[row, column].Text.Trim();
}
