using MedDRA_Backhend.Options;
using MedDRA_Backhend.Services;
using MedDRA_Backhend.Services.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
    });
});
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.Configure<VectorStoreOptions>(builder.Configuration.GetSection(VectorStoreOptions.SectionName));
builder.Services.Configure<EncodingOptions>(builder.Configuration.GetSection(EncodingOptions.SectionName));

builder.Services.AddSingleton<IEmbeddingService, DashScopeEmbeddingService>();
builder.Services.AddSingleton<IAiRerankService, DashScopeAiRerankService>();
builder.Services.AddHttpClient<IQdrantSearchService, QdrantSearchService>();
builder.Services.AddSingleton<IMedDraVersionService, MedDraVersionService>();
builder.Services.AddScoped<IMedDraEncodingService, MedDraEncodingService>();
builder.Services.AddScoped<IExcelTermParser, EpplusExcelTermParser>();
builder.Services.AddScoped<IExcelExportService, EpplusExcelExportService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
