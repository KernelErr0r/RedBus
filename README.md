# RedBus
A simple in memory EventBus / MessageBus library in C# with no dependencies

# Supported Frameworks
* .NET Framework 4.5
* .NET Standard 2.0

# Example Usage

There is a generic PayloadEvent class that can be used, or you can use any custom classes that derive from EventBase

```c#

private void TestMethod()
{
  IEventBus eventBus = new EventBus();
  eventBus.Subscribe<PayloadEvent<int>>(OnIntEvent); 
  eventBus.Subscribe<CustomEventClass>(OnCustomEvent); 
  
  eventBus.Publish(new PayloadEvent<int>(5)); // OnIntEvent will be invoked
  eventBus.Publish(new CustomEventClass()); // OnCustomEvent will be invoked
  
  
  eventBus.Subscribe<PayloadEvent<string>>(s =>
  {
    Console.WriteLine(s.Payload);
  });
  
  eventBus.Publish(new PayloadEvent<string>("Hello"));
}

private void OnCustomEvent(CustomEventClass customEvent)
{
  Console.WriteLine("Received CustomEvent");
}

private void OnIntEvent(PayloadEvent<int> intEvent)
{
  Console.WriteLine(intEvent.Payload);
}
```

Note that RedBus does not have WeakReferences implemented, so you must explicitly call Unsubscribe for the event when disposing
the subscriber to avoid memory leaks. The Subscribe method returns a SubscriptionToken, this is used when unsubscribing.

```csharp

IEventBus eventBus = new EventBus();
var token = eventBus.Subscribe<PayloadEvent<string>>(s =>
{
  Console.WriteLine(s.Payload);
});

eventBus.Unsubscribe(token);

```

# Extensions

```csharp

IEventBus eventBus = new EventBus();
var token = eventBus.Subscribe<PayloadEvent<string>>(s =>
{
  Console.WriteLine(s.Payload);
});

token.Unsubscribe(eventBus)

var payloadEvent = new PayloadEvent<string>("Hello");
payloadEvent.Publish(eventBus);

```

# Configuration

* ThrowSubscriberException  
  
By default, Redbus will catch and swallow any exceptions thrown by subscribers.
It's possible to configure Redbus to rethrow any exceptions which occurred in a subscriber method - please note the following:  
  * This will cause an exception from a subscriber to be thrown for a publisher  
  * This will/may cause subsequent subscribers to *not* receive an event/message after a subscriber has thrown an exception.

```csharp

IEventBus eventBus = new EventBus(new EventBusConfiguration { ThrowSubscriberException = true });

```
