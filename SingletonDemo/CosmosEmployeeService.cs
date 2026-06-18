using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Documents;
using System.Collections.Concurrent;
using System.ComponentModel;
using static Azure.Core.HttpHeader;

//namespace SingletonDemo
//{
public class CosmosEmployeeService : IEmployeeService
{
    private readonly Microsoft.Azure.Cosmos.Container _container;

    public CosmosEmployeeService(CosmosClient cosmosClient, IConfiguration config)
    {
        //var database = cosmosClient.GetDatabase(config["CosmosDb:DatabaseName"]);
        //_container = database.GetContainer(config["CosmosDb:ContainerName"]);
        try
        {
            var databaseName = config["CosmosDb:DatabaseName"];
            var containerName = config["CosmosDb:ContainerName"];
            Console.WriteLine($"Database: {config["CosmosDb:DatabaseName"]}");
            Console.WriteLine($"Container: {config["CosmosDb:ContainerName"]}");
            Console.WriteLine("Connecting to Cosmos...");            
            var database = cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName).Result;
            Console.WriteLine("Database ready");
            var container = database.Database
                .CreateContainerIfNotExistsAsync(containerName, "/id")
                .Result;
            Console.WriteLine("Container ready");
            _container = container.Container;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            throw;
        }


    }

    public async Task<IEnumerable<Employee>> GetEmployeesAsync()
    {

        var query = _container.GetItemQueryIterator<Employee>("SELECT * FROM c",
             requestOptions: new QueryRequestOptions { MaxItemCount = 5 }); // Continuation token - page size example
        var results = new List<Employee>();

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);

            string continuationToken = response.ContinuationToken; //Continuation token 
            Console.WriteLine($"Token: {continuationToken}");
        }

        return results;
    }

    public async Task<Employee> GetEmployeeAsync(string id)
    {
        try
        {
            // Override consistency level in reading the data.
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                ConsistencyLevel = ConsistencyLevel.Eventual
            };

            var response = await _container.ReadItemAsync<Employee>(id, new PartitionKey(id), requestOptions);
            return response.Resource;
        }
        catch
        {
            return null;
        }
    }

    public async Task AddEmployeeAsync(Employee employee)
    {
        employee.Id = Guid.NewGuid().ToString();
        ////// Apply Triggers log here.
        var requestOptions = new ItemRequestOptions
        {
            PreTriggers = new List<string> { "validateAndAddTimestamp" }
        };

        await _container.CreateItemAsync(employee, new PartitionKey(employee.Id), requestOptions);

        #region Calling stored procedure logic start
        //    var response = await _container.Scripts.ExecuteStoredProcedureAsync<dynamic>(
        //        "createEmployee",
        //        new PartitionKey(employee.Id),
        //       new object[]
        //{
        //    employee,  // first parameter (object)
        //    "Create"       // second parameter (string)
        //}
        //    );
        //    Console.WriteLine($"RU: {response}");
        #endregion Calling stored procedure logic End
    }

    public async Task UpdateEmployeeAsync(Employee employee)
    {
        await _container.UpsertItemAsync(employee, new PartitionKey(employee.Id));
    }

    public async Task DeleteEmployeeAsync(string id)
    {
        await _container.DeleteItemAsync<Employee>(id, new PartitionKey(id));
    }
    public async Task BulkInsertAsync(Microsoft.Azure.Cosmos.Container container, List<Employee> employees)
    {
        List<Task> tasks = new List<Task>();

        foreach (var item in employees)
        {
            tasks.Add(container.CreateItemAsync(item, new PartitionKey(item.PartitionKey)));
        }

        await Task.WhenAll(tasks);
    }

    public async Task BulkUpsertAsync(Microsoft.Azure.Cosmos.Container container, List<Employee> employees)
    {
        var tasks = employees.Select(item =>
            container.UpsertItemAsync(item, new PartitionKey(item.PartitionKey))
        );

        await Task.WhenAll(tasks);
    }

    public async Task BulkDeleteAsync(Microsoft.Azure.Cosmos.Container container, List<Employee> employees)
    {
        var tasks = employees.Select(item =>
            container.DeleteItemAsync<Employee>(
                item.Id,
                new PartitionKey(item.PartitionKey)
            )
        );

        await Task.WhenAll(tasks);
    }
    /// <summary>
    /// Approach 1: Limit Concurrency (SemaphoreSlim)
    /// Prevent overwhelming RU/s and getting throttled.
    /// </summary>
    /// <param name="container"></param>
    /// <param name="employees"></param>
    /// <param name="maxConcurrency"></param>
    /// <returns></returns>
    public async Task BulkInsertWithThrottleAsync(
    Microsoft.Azure.Cosmos.Container container,
    List<Employee> employees,
    int maxConcurrency = 50)
    {
        using SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrency);

        List<Task> tasks = new List<Task>();

        foreach (var item in employees)
        {
            await semaphore.WaitAsync();

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await container.CreateItemAsync(
                        item,
                        new PartitionKey(item.PartitionKey)
                    );
                }
                //Approach 2: Adaptive Throttling(Retry Handling)
                //Cosmos SDK automatically retries on 429, but you can log and adapt:
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine($"Throttled! Retry after: {ex.RetryAfter}");
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Approach 3: Batch + Delay Strategy
    /// Useful when you want predictable RU usage.
    /// </summary>
    /// <param name="container"></param>
    /// <param name="employees"></param>
    /// <param name="batchSize"></param>
    /// <param name="delayMs"></param>
    /// <returns></returns>
    public async Task BulkInsertInBatchesAsync(
    Microsoft.Azure.Cosmos.Container container,
    List<Employee> employees,
    int batchSize = 100,
    int delayMs = 200)
    {
        for (int i = 0; i < employees.Count; i += batchSize)
        {
            var batch = employees.Skip(i).Take(batchSize);

            var tasks = batch.Select(item =>
                container.CreateItemAsync(item, new PartitionKey(item.PartitionKey))
            );

            await Task.WhenAll(tasks);

            await Task.Delay(delayMs); // control RU burst
        }
    }

    public async Task OptimizedBulkInsertAsync(
    Microsoft.Azure.Cosmos.Container container,
    List<Employee> employees)
    {
        int maxConcurrency = 100;
        using SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = employees.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                ItemResponse<Employee> response =
                    await container.CreateItemAsync(item, new PartitionKey(item.PartitionKey));

                Console.WriteLine($"RU: {response.RequestCharge}");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                await Task.Delay((int)ex.RetryAfter.Value.TotalMilliseconds);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
//}
