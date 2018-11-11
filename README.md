### ... how to create a MICROSERVICE message listener ...(A)

```(B)
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
```(C)
public class MyEventSubscriber : IPublishSubscriber<MyEvent>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task ConsumeAsync(MyEvent message)
    {

    }
}
```
```(D)
IRegistrationBus rbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=aaa");
rbus.Subscribe<MyEventSubscriber, MyEvent>(null, 1, null, null);
```



### ... how to create a MICROSERVICE rpc responder ...(E)

```(F)
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
```(G)
public class MyRequestSubscriber : IRequestSubscriber<MyRequest>
{
    public IMicroserviceBus Bus { get; set; }

    public async Task<object> ResponseAsync(MyRequest request)
    {

    }
}
```
```(H)
rbus.Subscribe<MyRequestSubscriber, MyRequest>(false);
rbus.RegistrationCompleted();
```



### ... how to make a GATEWAY rpc request ...(I)

```(L)
IGatewayBus gbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=bbb");
```
```(M)
MyResponse response = await gbus.RequestAsync<MyResponse>(new MyRequest() 
{ 
    Lion = 5, 
    Crocodile = DateTimeOffset.Now 
});
```



### ... how to create a message SCHEDULER ...(N)

```(O)
ISchedulerBus sbus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=ccc");
```
```(P)
sbus.Schedule("* * * * *", () =>
{

    return (message);
},
async (Exception e) =>
{

});
```
