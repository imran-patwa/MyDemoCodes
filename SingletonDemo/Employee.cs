using Newtonsoft.Json;

public class Employee
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("department")]
    public string Department { get; set; }

    [JsonProperty("salary")]
    public double Salary { get; set; }
    public string? PartitionKey { get; internal set; }
}