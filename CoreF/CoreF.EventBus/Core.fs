namespace CoreF.EventBus

open CoreF.Common
open CoreF.Domain
open System

type EventBusSubscriberError =
| RetryableSubscriberError of string
| NonRetryableSubscriberError of string
| UnexpectedSubscriberError of exn

type EventBusError =
| UnsupportedEventType of string
| ConnectionError of string
| SubscriberError of EventBusSubscriberError
| TransportError of string
| ErrorPublishingEvent of IEvent * string
| NoEventBusConfigured
| UnhandledErrorProcessingEvent of exn

type IParty =
    abstract member PartyId: string

type IPublisher = inherit IParty

type RetrySetting =
| NoRetries
| LimitedRetries of int
| InfiniteRetries

type ISubscriber = 
    inherit IParty
    abstract member Retries: RetrySetting

type ISubscriber<'event when 'event :> IEvent> =
    inherit ISubscriber
    abstract member HandleEvent: 'event -> AsyncResult<unit, EventBusSubscriberError>

type IDynamicSubscriber =
    inherit ISubscriber
    abstract member HandleEvent: IEvent -> AsyncResult<unit, EventBusSubscriberError>

[<Struct>] type Topic = private Topic of string

module Topic =
    let create (topic: string) =
        if topic |> String.like "EventBus.*"
        then topic
        else sprintf "EventBus.%s" topic 
        |> Topic

    let name (Topic topic) = 
        topic |> String.replace "EventBus." ""

    let value (Topic topic) = topic

type IEventBus =
    abstract member Publish<'event when 'event :> IEvent> : Topic -> 'event -> AsyncResult<unit, EventBusError>
    abstract member PublishMany<'event when 'event :> IEvent> : Topic -> 'event seq -> AsyncResult<unit, EventBusError list>
    
    abstract member Subscribe<'event when 'event :> IEvent> : Topic -> ISubscriber<'event> -> AsyncResult<unit, EventBusError>
    abstract member SubscribeAll : Topic -> IDynamicSubscriber -> AsyncResult<unit, EventBusError>
    
    abstract member Unsubscribe<'event when 'event :> IEvent> : Topic -> ISubscriber<'event> -> AsyncResult<unit, EventBusError>
    abstract member UnsubscribeAll : Topic -> IDynamicSubscriber -> AsyncResult<unit, EventBusError>

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct ||| AttributeTargets.Interface ||| AttributeTargets.Enum, AllowMultiple = false)>]
type RoutingKeyAttribute (routingKey: string) =
    inherit Attribute()
    member __.RoutingKey = routingKey

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct ||| AttributeTargets.Interface ||| AttributeTargets.Enum, AllowMultiple = false)>]
type EventTypeAttribute (eventType: string) =
    inherit Attribute()
    member __.EventType = eventType

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Module, AllowMultiple = true)>]
type SubscriptionsAttribute(partyId: string) =
    inherit Attribute ()
    member __.PartyId = partyId

[<AbstractClass>]
[<AttributeUsage(AttributeTargets.Method ||| AttributeTargets.Property, AllowMultiple = false)>]
type SubscriberAttributeBase(topic: string, retries: RetrySetting) =
    inherit Attribute ()
    let eventBusTopic = topic |> Topic.create
    member __.Topic = eventBusTopic
    member __.Retries = retries

type SubscriberAttribute(topic, retries: RetrySetting) =
    inherit SubscriberAttributeBase(topic, retries)
    new (topic) = SubscriberAttribute(topic, NoRetries)
    new (topic, retryCount: int) = SubscriberAttribute(topic, if retryCount < Int32.MaxValue then LimitedRetries retryCount else InfiniteRetries)

type DynamicSubscriberAttribute(topic, retries: RetrySetting) =
    inherit SubscriberAttributeBase(topic, retries)
    new (topic) = DynamicSubscriberAttribute(topic, NoRetries)
    new (topic, retryCount: int) = DynamicSubscriberAttribute(topic, if retryCount < Int32.MaxValue then LimitedRetries retryCount else InfiniteRetries)