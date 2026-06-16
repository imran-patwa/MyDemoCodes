//var builder = WebApplication.CreateBuilder(args);
//var app = builder.Build();

//app.MapGet("/", () => "Hello World!");

//app.Run();

using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddApplicationInsightsTelemetry();

// Add services
builder.Services.AddControllersWithViews();
//Avoid Interactive Login in Production (Disable browser credential if needed.)

//var optionsDAC = new DefaultAzureCredentialOptions
//{
//    ExcludeInteractiveBrowserCredential = true,
//    ExcludeVisualStudioCredential = true, // Advanced Production Configuration (Sometimes companies restrict credential sources.)
//    ExcludeAzureCliCredential = true, // Advanced Production Configuration (Sometimes companies restrict credential sources.)
//    ManagedIdentityClientId = "070c32e3-1d56-4948-8f73-e1ddcaac0dfd" // "961e6cf8-5d46-4f39-9609-c3df3998ce0f"
//};

//var credential = new DefaultAzureCredential();

//AccessToken token = await credential.GetTokenAsync(
//    new TokenRequestContext(
//        new string[] { "https://cosmos.azure.com/.default" }));

//Console.WriteLine("Token acquired successfully");

var options = new CosmosClientOptions
{
    ApplicationName = "SingletonDemo",
    RequestTimeout = TimeSpan.FromSeconds(60) //Increase SDK Timeout
    //,AllowBulkExecution = true // Bulk Execution example.
};

// Singleton CosmosClient
builder.Services.AddSingleton(s =>
{
    var config = s.GetRequiredService<IConfiguration>();
    Console.WriteLine($"Endpoint: {config["CosmosDb:Account"]}");
    Console.WriteLine($"Key Exists: {!string.IsNullOrEmpty(config["CosmosDb:Key"])}");

    return new CosmosClient(
        config["CosmosDb:Account"],
        config["CosmosDb:Key"],// Using master key
                               //new DefaultAzureCredential(optionsDAC), // Using Managed Identity -- No need of master key in config file.
                               //credential, // Using Managed Identity -- No need of master key in config file.
        options
    );
});

// Register service
builder.Services.AddScoped<IEmployeeService, CosmosEmployeeService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Employee}/{action=Index}/{id?}");

app.Run();



//var host = new HostBuilder()
//    .ConfigureFunctionsWorkerDefaults()
//    .Build();

//host.Run();