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
public class MyEventSubscriber : IPublishSubscriber<MyEvent>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task ConsumeAsync(MyEvent message)
    {

    }
}
```
```csharp
IRegistrationBus rbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=aaa");
rbus.Subscribe<MyEventSubscriber, MyEvent>(null, 1, null, null, null);
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
public class MyRequestSubscriber : IRequestSubscriber<MyRequest>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task<object> ResponseAsync(MyRequest message)
    {

    }
}
```
```csharp
rbus.Subscribe<MyRequestSubscriber, MyRequest>(null);
rbus.RegistrationCompleted();
```



### ... how to make a GATEWAY rpc request ...

```csharp
IGatewayBus gbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=bbb");
```
```csharp
MyResponse response = await gbus.RequestAsync<MyResponse>(new MyRequest() 
{ 
    Lion = 5, 
    Crocodile = DateTime.UtcNow 
});
```



### ... how to create a message SCHEDULER ...

```csharp
ISchedulerBus sbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=ccc");
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
