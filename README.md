### ... how to create a microservice message listener ...

```
[QueueConfig(Directory = "John", Subdirectory = "Doe")]
public class MyEvent
{
    [Required]
    [MaxLength(10)]
    public string Cat { get; set; }

    [Required]
    public DateTimeOffset Dog { get; set; }

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
IRegistrationBus bus = new RabbitMQBus("HostName=xxx;Port=yyy;UserName=zzz;Password=kkk;AppId=www");
bus.Subscribe<MyEventSubscriber, MyEvent>(null, 1, null, null);
```
