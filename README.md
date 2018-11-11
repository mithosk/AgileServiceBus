### ... how to create a MICROSERVICE message listener ...

```
[QueueConfig(Directory = "John", Subdirectory = "Doe")]
public class MyEvent
{
    [Required]
    [MaxLength(10)]
    public string Cat { get; set; }

    [Required]
    public DateTimeOffset? Dog { get; set; }

    public Guid? Tiger { get; set; }
}
```
```
public class MyEventSubscriber : IPublishSubscriber<MyEvent>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task ConsumeAsync(MyEvent message)
    {

    }
}
```
```
IRegistrationBus rbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=aaa");
rbus.Subscribe<MyEventSubscriber, MyEvent>(null, 1, null, null);
```



### ... how to create a MICROSERVICE rpc responder ...

```
[QueueConfig(Directory = "John", Subdirectory = "Doe")]
public class MyRequest
{
    [Required]
    public int? Lion { get; set; }

    [Required]
    public DateTimeOffset? Crocodile { get; set; }

    public Guid? Horse { get; set; }
}
```
```
public class MyRequestSubscriber : IRequestSubscriber<MyRequest>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task<object> ResponseAsync(MyRequest request)
    {

    }
}
```
```
rbus.Subscribe<MyRequestSubscriber, MyRequest>(false);
rbus.RegistrationCompleted();
```



### ... how to make a GATEWAY rpc request ...

```
IGatewayBus gbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=bbb");
```
```
MyResponse response = await gbus.RequestAsync<MyResponse>(new MyRequest() 
{ 
    Lion = 5, 
    Crocodile = DateTimeOffset.Now 
});
```



### ... how to create a message SCHEDULER ...

```
ISchedulerBus sbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=ccc");
```
```
sbus.Schedule("* * * * *", () =>
{

    return (message);
},
async (Exception e) =>
{

});
```
