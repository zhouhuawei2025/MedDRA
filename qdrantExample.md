embedding模型可以返回文本对应的向量化结果，下面我们使用Qdrant数据库来存储这些向量
1. 安装Qdrant数据库服务器
   https://qdrant.tech/documentation/quickstart/
   进入qdrant.tech或Docker中搜索Qdrant，根据提示下载Qdrant到Docker中运行。端口号为6333或6334
2. 常用的方法

  //1. 查询数据库是否存在collection
   bool collectionExists = await qdrantClient.CollectionExistsAsync(collectionName);

//2. 查询collection的向量维度
var collectionInfo = await qdrantClient.GetCollectionInfoAsync(collectionName);
var size = collectionInfo.Config.Params.VectorsConfig.Params.Size;

//3. 创建/删除 collection
await qdrantClient.CreateCollectionAsync(
collectionName,
new VectorParams{ Size = vectorSize,  Distance = Distance.Cosine}
);
await qdrantClient.DeleteCollectionAsync(collectionName);

//4. 构建point
var point = new PointStruct
{
Id = new PointId { Num = (ulong)i },  // i 为某个int值
Vectors = embedding,    //embedding为文本向量化后的 float[]
Payload =
{
["text"] = text      //text为原文本
}
};

//5. 向collection中插入point
List<PointStruct> points = new List<PointStruct>();
await qdrantClient.UpsertAsync(collectionName, points);

//6. 检索数据库， 返回相似度最高的向量
var searchResults = await qdrantClient.SearchAsync(
collectionName: collectionName,
vector: queryEmbedding,
limit: 3 );

//7. 获取返回向量的原文本和余弦相似度
var text = searchResults[i].Payload["text"].StringValue;
var similarity = searchResults[i].Score;
3. C#调用向量数据库
   using System.ClientModel;
   using OpenAI;
   using Qdrant.Client;
   using Qdrant.Client.Grpc;

var apiKey = Environment.GetEnvironmentVariable("AI__EmbeddingApiKey");
var endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/";
var deploymentName = ""text-embedding-v4";

var qdrantHost = "127.0.0.1";

//构建embedding client
var openAiClient = new OpenAIClient(
new ApiKeyCredential(apiKey),
new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
);
var embeddingClient = openAiClient.GetEmbeddingClient(deploymentName);

//构建Qdrant Client
//有多种构造函数可供选择
var qdrantClient = new QdrantClient(host: qdrantHost, https: false);
var collectionName = "mySampleCollection";

Console.WriteLine("Please select an option:");
Console.WriteLine("1 - Insert sample texts into vector database");
Console.WriteLine("2 - Query directly from vector database");
Console.Write("\nYour choice (1 or 2): ");

var choice = Console.ReadLine();

if (choice == "1")
{
await InsertRecords();
}
else if (choice == "2")
{
await RunQuery();
}
else
{
Console.WriteLine("\nInvalid choice. Exiting...");
}
async Task InsertRecords()
{
var sampleTexts = new[]
{
"C# is a popular programming language for data science and machine learning.",
"The football match was exciting, with the final score being 3-2.",
"The soccer team won the championship after months of training.",
"Deep learning has revolutionized computer vision and natural language processing."
};

    var sampleEmbeddingResult = await embeddingClient.GenerateEmbeddingAsync(sampleTexts[0]);
    var vectorSize = (ulong)sampleEmbeddingResult.Value.ToFloats().Length;

    Console.WriteLine($"Vector dimension: {vectorSize}");

    // Check if collection exists and compare vector size
    bool needRecreate = false;
    bool collectionExists = await qdrantClient.CollectionExistsAsync(collectionName);

    if (collectionExists)
    {
        var collectionInfo = await qdrantClient.GetCollectionInfoAsync(collectionName);
        var existingVectorSize = collectionInfo.Config.Params.VectorsConfig.Params.Size;

        if (existingVectorSize != vectorSize)
        {
            Console.WriteLine($"Collection exists but vector size mismatch (existing: {existingVectorSize}, new: {vectorSize})");
            needRecreate = true;
        }
        else
        {
            Console.WriteLine($"Collection '{collectionName}' already exists with matching vector size ({vectorSize})");
        }
    }
    else
    {
        Console.WriteLine($"Collection '{collectionName}' does not exist");
        needRecreate = true;
    }

    if (needRecreate)
    {
        if (collectionExists)
        {
            await qdrantClient.DeleteCollectionAsync(collectionName);
            Console.WriteLine($"Deleted existing collection '{collectionName}'");
        }

        await qdrantClient.CreateCollectionAsync(
            collectionName: collectionName,
            vectorsConfig: new VectorParams
            {
                Size = vectorSize,
                Distance = Distance.Cosine
            }
        );

        Console.WriteLine($"Created collection '{collectionName}'");
    }

    Console.WriteLine();
    Console.WriteLine("Generating embeddings and storing in Qdrant...\n");

    // Generate embeddings and insert into Qdrant
    var points = new List<PointStruct>();

    for (int i = 0; i < sampleTexts.Length; i++)
    {
        var text = sampleTexts[i];
        var embeddingResult = await embeddingClient.GenerateEmbeddingAsync(text);
        var embedding = embeddingResult.Value.ToFloats().ToArray();

        var point = new PointStruct
        {
            Id = new PointId { Num = (ulong)i },
            Vectors = embedding,
            Payload =
            {
                ["text"] = text
            }
        };

        points.Add(point);
    }

    // Upsert all points to Qdrant
    await qdrantClient.UpsertAsync(collectionName, points);
    Console.WriteLine($"\nStored {points.Count} embeddings in Qdrant\n");
}
async Task RunQuery()
{
//检查数据库的collection是否已经创建
bool collectionExists = await qdrantClient.CollectionExistsAsync(collectionName);

    if (!collectionExists)
    {
        Console.WriteLine($"\nError: Collection '{collectionName}' does not exist!");
        Console.WriteLine("Please choose option 1 first to insert sample texts.");
        return;
    }

    Console.WriteLine($"\nCollection '{collectionName}' found. Ready to query.\n");
    // Interactive query loop
    while (true)
    {
        Console.WriteLine("\n" + new string('-', 70));
        Console.Write("Enter your query (or 'quit' to exit): ");
        var query = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "quit")
        {
            Console.WriteLine("Goodbye!");
            break;
        }

        Console.WriteLine($"\nSearching for: \"{query}\"");
        Console.WriteLine("Generating query embedding...");

        // Generate embedding for query
        var queryEmbeddingResult = await embeddingClient.GenerateEmbeddingAsync(query);
        var queryEmbedding = queryEmbeddingResult.Value.ToFloats().ToArray();

        // Search in Qdrant
        var searchResults = await qdrantClient.SearchAsync(
            collectionName: collectionName,
            vector: queryEmbedding,
            limit: 3
        );

        Console.WriteLine("\nTop 3 Most Similar Texts:");
        Console.WriteLine(new string('=', 70));

        for (int i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            var text = result.Payload["text"].StringValue;
            var similarity = result.Score;

            Console.WriteLine($"\n{i + 1}. Similarity: {similarity:F4} ({similarity * 100:F2}%)");
            Console.WriteLine($"   Text: {text}");
        }
    }
}
