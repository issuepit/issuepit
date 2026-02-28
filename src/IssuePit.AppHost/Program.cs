var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .AddDatabase("issuepit-db");

var kafka = builder.AddKafka("kafka");

var redis = builder.AddRedis("redis");

var api = builder.AddProject<Projects.IssuePit_Api>("api")
    .WithReference(postgres)
    .WithReference(kafka)
    .WithReference(redis)
    .WaitFor(postgres)
    .WaitFor(kafka)
    .WaitFor(redis);

var executionClient = builder.AddProject<Projects.IssuePit_ExecutionClient>("execution-client")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

var cicdClient = builder.AddProject<Projects.IssuePit_CiCdClient>("cicd-client")
    .WithReference(kafka)
    .WaitFor(kafka)
    .WithEnvironment("Kafka__BootstrapServers", kafka.Resource.ConnectionStringExpression);

builder.Build().Run();
