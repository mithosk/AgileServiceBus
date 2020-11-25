### ... how to create a MICROSERVICE message listener ...

```csharp
[BusNamespace(Directory = "John", Subdirectory = "Doe")]
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
IMicroserviceLifetime ml = new RabbitMQDriver("Host=x;VHost=y;Port=z;User=k;Password=w;AppId=m");
ml.Subscribe<MyEventHandler, MyEvent>(null, null, null, null);
```



### ... how to create a MICROSERVICE rpc responder ...

```csharp
[BusNamespace(Directory = "John", Subdirectory = "Doe")]
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
ml.Startup();
```



### ... how to make a GATEWAY rpc request ...

```csharp
IGatewayBus gbus = new RabbitMQDriver("Host=x;VHost=y;Port=z;User=k;Password=w;AppId=g");
```
```csharp
MyResponse response = await gbus.RequestAsync<MyResponse>(new MyRequest
{ 
    Lion = 5, 
    Crocodile = DateTime.UtcNow,
    Horse = Guid.NewGuid()
});
```



### ... how to create a message SCHEDULER ...

```csharp
ISchedulerBus sbus = new RabbitMQDriver("Host=x;VHost=y;Port=z;User=k;Password=w;AppId=s");
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
