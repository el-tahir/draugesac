using Amazon.DynamoDBv2;
using Amazon.S3;
using Amazon.SQS;
using Draugesac.Application;
using Draugesac.Application.Interfaces;
using Draugesac.Infrastructure.Services;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add controller services
builder.Services.AddControllers();

// Add health checks
builder.Services.AddHealthChecks();

// Add CORS configuration for Blazor WebAssembly
string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins("http://localhost:5001", // Typical Blazor WASM debug port
                                             "https://localhost:5002", // And its HTTPS variant
                                             "http://localhost:5002",  // HTTP variant for port 5002
                                             "http://localhost:5000",  // Additional common port
                                             "https://localhost:5001", // Additional HTTPS port
                                             "http://localhost:7000",  // Additional development ports
                                             "https://localhost:7001", // Additional HTTPS development port
                                             "http://localhost:5236",  // Current Blazor app port
                                             "https://localhost:7081") // HTTPS variant for current Blazor app
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                      });
});

// Add AWS Services
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddAWSService<IAmazonSQS>();

// Register AWS implementations with configuration
builder.Services.AddSingleton<IFileStore>(sp =>
{
    IAmazonS3 s3Client = sp.GetRequiredService<IAmazonS3>();
    ILogger<S3FileStore> logger = sp.GetRequiredService<ILogger<S3FileStore>>();
    string bucketName = builder.Configuration["AWS:S3BucketName"] ?? throw new ArgumentNullException("AWS:S3BucketName configuration is required");
    return new S3FileStore(s3Client, bucketName, logger);
});

builder.Services.AddSingleton<IDocumentRepository>(sp =>
{
    IAmazonDynamoDB dynamoDbClient = sp.GetRequiredService<IAmazonDynamoDB>();
    ILogger<DynamoDbDocumentRepository> logger = sp.GetRequiredService<ILogger<DynamoDbDocumentRepository>>();
    string tableName = builder.Configuration["AWS:DynamoDbTableName"] ?? throw new ArgumentNullException("AWS:DynamoDbTableName configuration is required");
    return new DynamoDbDocumentRepository(dynamoDbClient, tableName, logger);
});

// Register SQS Message Publisher
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    IAmazonSQS sqsClient = sp.GetRequiredService<IAmazonSQS>();
    ILogger<SqsMessagePublisher> logger = sp.GetRequiredService<ILogger<SqsMessagePublisher>>();
    string queueUrl = builder.Configuration["AWS:SQSQueueUrl"] ?? throw new ArgumentNullException("AWS:SQSQueueUrl configuration is required");
    return new SqsMessagePublisher(sqsClient, queueUrl, logger);
});

// Register DocumentService - Now that all dependencies are available
builder.Services.AddScoped<DocumentService>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable CORS
app.UseCors(MyAllowSpecificOrigins);

app.UseHttpsRedirection();

// Map controllers
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/healthz");

string[] summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    WeatherForecast[] forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Simple endpoint to demonstrate access to Application layer types
app.MapGet("/api/test", () =>
{
    // This demonstrates that the API can access Application layer types
    return new
    {
        Message = "API successfully references Application layer",
        ApplicationLayer = typeof(DocumentService).AssemblyQualifiedName,
        Interfaces = new[]
        {
            nameof(IDocumentRepository),
            nameof(IFileStore),
            nameof(IMessagePublisher)
        }
    };
})
.WithName("TestApplicationAccess")
.WithOpenApi();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
