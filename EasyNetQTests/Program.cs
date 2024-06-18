WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("EasyTests__");

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ConnectionConfiguration>(builder.Configuration.GetSection(nameof(ConnectionConfiguration)));

bool mustCrush = builder.Configuration.GetValue<bool>("MustCrush");
if (mustCrush)
{
    // The line that brakes everything :D
    builder.Services.AddSingleton<ConnectionConfiguration>(sp => sp.GetRequiredService<IOptions<ConnectionConfiguration>>().Value);
}

builder.Services.RegisterEasyNetQ(
    sr => sr.Resolve<IOptionsMonitor<ConnectionConfiguration>>().CurrentValue,
    register => { });


WebApplication app = builder.Build();

const string exchangeName = "EventsStream";
const string routingKey = "event.some.message";

IAdvancedBus bus = app.Services.GetRequiredService<IAdvancedBus>();

Exchange exchange = await bus.ExchangeDeclareAsync(exchangeName, configuration => configuration.WithType(ExchangeType.Topic).AsDurable(true).AsAutoDelete(true), app.Lifetime.ApplicationStopping);

Queue queue = await bus.QueueDeclareAsync(
    "SomeQueue",
    x => x.AsAutoDelete(false).AsDurable(true),
    app.Lifetime.ApplicationStopping);

await bus.BindAsync(exchange, queue, routingKey, new Dictionary<string, object>(), app.Lifetime.ApplicationStopping);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/send/something", async (string data, CancellationToken cancellationToken) =>
    {
        await bus.PublishAsync(exchange, routingKey, true, new Message<TestMessageDto>(new() {Data = data}), cancellationToken);
        return true;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.MapGet("/", (HttpContext context) => context.Response.Redirect("/swagger")).ExcludeFromDescription();

Task appRunningTask = app.RunAsync();

bus.Consume(queue, x => x.Add<TestMessageDto>((message, info, ct) =>
{
    app.Logger.LogInformation("Message received: {Message}", message.Body);
    return Task.CompletedTask;
}));

await appRunningTask;

public record TestMessageDto
{
    public string Data { get; init; }
}
