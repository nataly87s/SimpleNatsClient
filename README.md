# SimpleNatsClient
A [Rx.NET](https://github.com/Reactive-Extensions/Rx.NET) client for the [NATS messaging system](https://nats.io).

[![CircleCI](https://circleci.com/gh/nataly87s/SimpleNatsClient.svg?style=svg)](https://circleci.com/gh/nataly87s/SimpleNatsClient)
[![NuGet](https://img.shields.io/nuget/v/SimpleNatsClient.svg)](https://www.nuget.org/packages/SimpleNatsClient/)

---

## Sample Usage

The client is all asynchronous and reactive.

### Publish

```c#
async Task Publish(string subject, byte[] payload)
{
    using (var connection = await NatsClient.Connect())
    {
        await connection.Publish(subject, payload);
    }
}
```

### Subscribe

```c#
async Task Subscribe(string subject, Action<IncomingMessage> handleMessage)
{
    using (var connection = await NatsClient.Connect())
    {
        var subscription = connection.GetSubscription(subject);
        await subscription.FirstAsync().Do(handleMessage);

    }
}
```

### Request

```c#
async Task Request(string subject, Action<IncomingMessage> handleAnswer)
{
    using (var connection = await NatsClient.Connect())
    {
        var answer = await connection.Request(subject);
        handleAnswer(answer);
    }
}
```

### Reply to request

```c#
async Task Reply(string subject)
{
    using (var connection = await NatsClient.Connect())
    {
        var subscription = connection.GetSubscription(subject);
        await subscription.FirstAsync()
            .SelectMany(message => Observable.FromAsync(ct => connection.Publish(message.ReplyTo, "answer", ct)));
    }
}
```
