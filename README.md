### ... how to create a MICROSERVICE message listener ...

```csharp
[QueueConfig(Directory = "John", Subdirectory = "Doe")]
public class MyEvent
{
    public string Cat { get; set; }
    public DateTime Dog { get; set; }
    public Guid Tiger { get; set; }
}
```
```csharp
public class MyEventHandler : IEventHandler<MyEvent>
{
    public IMicroserviceBus Bus { get; set; }
    public ITraceScope TraceScope { get; set; }

    public async Task HandleAsync(MyEvent message)
    {

    }
}
```
```csharp
IMicroserviceLifetime ml = new RabbitMQBus("Host=xxx;VHost=yyy;Port=zzz;User=kkk;Password=www;AppId=mmm");
ml.Subscribe<MyEventHandler, MyEvent>(null, null, null, null);
```



### ... how to create a MICROSERVICE rpc responder ...

```csharp
[QueueConfig(Directory = "John", Subdirectory = "Doe")]
public class MyRequest
{
    public int Lion { get; set; }
    public DateTime Crocodile { get; set; }
    public Guid Horse { get; set; }
}
```
```csharp
public class MyRequestResponder : IResponder<MyRequest>
{
    public IMicroserviceBus Bus { get; set; }
    public ITraceScope TraceScope { get; set; }

    public async Task<object> RespondAsync(MyRequest message)
    {

    }
}
```
```csharp
ml.Subscribe<MyRequestResponder, MyRequest>(null);
ml.RegistrationCompleted();
```



### ... how to make a GATEWAY rpc request ...

```csharp
IGatewayBus gbus = new RabbitMQBus("Host=xxx;VHost=yyy;Port=zzz;User=kkk;Password=www;AppId=ggg");
```
```csharp
MyResponse response = await gbus.RequestAsync<MyResponse>(new MyRequest() 
{ 
    Lion = 5, 
    Crocodile = DateTime.UtcNow,
    Horse = Guid.NewGuid()
});
```



### ... how to create a message SCHEDULER ...

```csharp
ISchedulerBus sbus = new RabbitMQBus("Host=xxx;VHost=yyy;Port=zzz;User=kkk;Password=www;AppId=sss");
```
```csharp
sbus.Schedule("* * * * *", () =>
{

    return message;
},
async (Exception e) =>
{

});
```
