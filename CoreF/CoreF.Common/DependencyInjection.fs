namespace CoreF.Common

open System

type DependencyInjectionError =
| NoServiceFound of Type
| UnexpectedDependencyInjectionError of exn

/// Defines a specialized Reader monad for Dependency Injection
type Injected<'t, 'e> = Reader<IServiceProvider, Result<'t, 'e>>

module Injected = 
    let run x f = 
        Reader.run x f

    let create x = 
        Reader.create (Ok x)
  
    let bind<'a, 'b, 'e> (f: 'a -> Injected<'b, 'e>) (x: Injected<'a, 'e>) : Injected<'b, 'e> = 
        let future state =
            let result = run state x 
            match result with
            | Ok z ->
                run state (f z)
            | Error e ->
                Error e
        Reader future

    let bindResult<'a, 'b, 'e> (f: 'a -> Injected<'b, 'e>) (x: Result<'a, 'e>) : Injected<'b, 'e> =
        match x with
        | Ok z -> f z
        | Error e -> Reader (fun _ -> Error e)

    let map f x =
        bind (f >> create) x

    let mapError f (x: Injected<_,_>) : Injected<_,_> =
        let (Reader getResult) = x
        fun provider ->
            let result = getResult provider
            match result with
            | Ok value -> Ok value
            | Error e -> Error (f e)
        |> Reader 

    let ofResult (result: Result<_,_>) : Injected<_,_> =
        Reader.create result

    let join (elements: Injected<'a,'e> seq) : Injected<'a list, 'e> =
        elements |> Seq.fold (fun acc cur ->
            fun provider ->
                let result = run provider acc
                match result with
                | Ok values -> 
                    let next = run provider cur
                    match next with
                    | Ok value -> Ok (values @ [value])
                    | Error error -> Error error
                | Error error ->
                    Error error
            |> Reader) (create [])

    let ignore (i: Injected<_,_>) =
        i |> map ignore

type InjectionBuilder<'t> () =
    member __.Bind (x, f) : Injected<_,_> = Injected.bind f x
    member __.Bind (x, f) : Injected<_,_> = Injected.bindResult f x
    member __.Return (x) : Injected<_,_> = Injected.create x 
    member __.ReturnFrom (x: Injected<_,_>) = x    
    member __.ReturnFrom (x: Result<_,_>) = Injected.ofResult x
    member __.Zero () : Injected<_,_> = Injected.create ()
    member __.Delay (f) : Injected<_,_> = f()
    member __.Combine (a, b) : Injected<_,_> =
        a |> Injected.bind (fun () -> b)
    member this.TryFinally(body: unit -> Injected<_,_>, compensation) : Injected<_,_> =
        try 
            this.ReturnFrom(body())
        finally 
            compensation()
    member this.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> Injected<'a, 'e>) : Injected<'a, 'e> = 
        let body' = fun () -> binder resource
        this.TryFinally(body', fun () -> 
            match resource with 
                | null -> () 
                | disp -> disp.Dispose())

    member this.While (guard, body: unit -> Injected<_,_>) : Injected<_,_> =
        if not (guard()) then 
            this.Zero()
        else
            this.Bind(body(), fun () -> this.While(guard, body))

    member this.For (sequence: seq<_>, body) : Injected<_,_> =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))


/// Injection type for working with Async operations and results
type InjectedAsync<'t, 'e> = Reader<IServiceProvider, AsyncResult<'t, 'e>>

module InjectedAsync = 
    let create x = 
        Reader.create (AsyncResult.create x)
  
    let bind<'a, 'b, 'e> (f: 'a -> InjectedAsync<'b, 'e>) (x: InjectedAsync<'a, 'e>) : InjectedAsync<'b, 'e> = 
        let future state =
            async {
                let! result = Injected.run state x |> AsyncResult.toAsync
                let! futureState =
                    match result with
                    | Ok z ->
                        Injected.run state (f z)
                    | Error e ->
                        Error e |> Async.create |> AsyncResult
                    |> AsyncResult.toAsync
                return futureState
            } |> AsyncResult
        Reader future

    let bindAsync<'a, 'b, 'e> (f: 'a -> InjectedAsync<'b, 'e>) (x: Async<'a>) : InjectedAsync<'b, 'e> =
        let future state =
            async {
                let! result = x 
                let! future = Injected.run state (f result) |> AsyncResult.toAsync
                return future
            } |> AsyncResult
        Reader future

    let bindResult<'a, 'b, 'e> (f: 'a -> InjectedAsync<'b, 'e>) (x: Result<'a, 'e>) : InjectedAsync<'b, 'e> =
        let future state =
            async {
                let! futureState =
                    match x with
                    | Ok z ->
                        Injected.run state (f z)
                    | Error e ->
                        Error e |> Async.create |> AsyncResult
                    |> AsyncResult.toAsync
                return futureState
            } |> AsyncResult
        Reader future   
        
    let bindAsyncResult<'a, 'b, 'e> (f: 'a -> InjectedAsync<'b, 'e>) (x: AsyncResult<'a, 'e>) : InjectedAsync<'b, 'e> =
        let future state =
            async {
                let! result = x |> AsyncResult.toAsync
                let! futureState =
                    match result with
                    | Ok z ->
                        Injected.run state (f z)
                    | Error e ->
                        Error e |> Async.create |> AsyncResult
                    |> AsyncResult.toAsync
                return futureState
            } |> AsyncResult
        Reader future
        
    let bindInjected<'a, 'b, 'e> (f: 'a -> InjectedAsync<'b, 'e>) (x: Injected<'a, 'e>) : InjectedAsync<'b, 'e> =
        let future state =
            async {
                let result = x |> Reader.run state
                let! futureState =
                    match result with
                    | Ok z ->
                        Injected.run state (f z)
                    | Error e ->
                        Error e |> Async.create |> AsyncResult
                    |> AsyncResult.toAsync
                return futureState
            } |> AsyncResult
        Reader future           

    let map f x =
        bind (f >> create) x

    let mapError f (x: InjectedAsync<_,_>) : InjectedAsync<_,_> =
        let (Reader getResult) = x
        fun provider ->
            async {
                let! result = getResult provider |> AsyncResult.toAsync
                match result with
                | Ok value ->
                    return Ok value
                | Error e -> 
                    return Error (f e)
            } |> AsyncResult
        |> Reader 

    let ofAsyncResult (result: AsyncResult<_,_>) : InjectedAsync<_,_> =
        Reader.create result

    let ofAsync (result: Async<_>) : InjectedAsync<_,_> =
        result |> Async.map Ok |> AsyncResult |> ofAsyncResult

    let ofInjected (injected: Injected<_,_>) : InjectedAsync<_,_> =
        let future state =
            async {
                return injected |> Reader.run state
            } |> AsyncResult
        Reader future 

    let ofResult (result: Result<_,_>) : InjectedAsync<_,_> =
        result |> Async.create |> AsyncResult |> ofAsyncResult

type AsyncInjectionBuilder<'t> () =
    member __.Bind (x, f) : InjectedAsync<_,_> = InjectedAsync.bind f x
    member __.Bind (x, f) : InjectedAsync<_,_> = InjectedAsync.bindResult f x
    member __.Bind (x, f) : InjectedAsync<_,_> = InjectedAsync.bindAsyncResult f x
    member __.Bind (x, f) : InjectedAsync<_,_> = InjectedAsync.bindAsync f x
    member __.Bind (x, f) : InjectedAsync<_,_> = InjectedAsync.bindInjected f x
    member __.Return (x) : InjectedAsync<_,_> = InjectedAsync.create x 
    member __.ReturnFrom (x: InjectedAsync<_,_>) = x    
    member __.ReturnFrom (x: AsyncResult<_,_>) = InjectedAsync.ofAsyncResult x
    member __.ReturnFrom (x: Async<_>) = InjectedAsync.ofAsync x
    member __.ReturnFrom (x: Result<_,_>) = InjectedAsync.ofResult x
    member __.ReturnFrom (x: Injected<_,_>) = InjectedAsync.ofInjected x
    member __.Zero () : InjectedAsync<_,_> = InjectedAsync.create ()
    member __.Delay (f) : InjectedAsync<_,_> = f()
    member __.Combine (a, b) : InjectedAsync<_,_> =
        a |> InjectedAsync.bind (fun () -> b)
    member this.TryFinally(body: unit -> InjectedAsync<_,_>, compensation) : InjectedAsync<_,_> =
        try 
            this.ReturnFrom(body())
        finally 
            compensation()
    member this.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> InjectedAsync<'a, 'e>) : InjectedAsync<'a, 'e> = 
        let body' = fun () -> binder resource
        this.TryFinally(body', fun () -> 
            match resource with 
                | null -> () 
                | disp -> disp.Dispose())

    member this.While (guard, body: unit -> InjectedAsync<_,_>) : InjectedAsync<_,_> =
        if not (guard()) then 
            this.Zero()
        else
            this.Bind(body(), fun () -> this.While(guard, body))

    member this.For (sequence: seq<_>, body) : InjectedAsync<_,_> =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))


module DependencyInjection =
    type IServiceProvider with 
        member this.GetService<'t>() = 
            let serviceType = typeof<'t>
            try
                match this.GetService(serviceType) with
                | null -> Error <| NoServiceFound serviceType
                | :? 't as service -> Ok service
                | _ -> Error <| NoServiceFound serviceType
            with ex ->
                Error <| UnexpectedDependencyInjectionError ex

    let getService<'t> (context : IServiceProvider) = 
        if typeof<'t>.IsAssignableFrom(typeof<IServiceProvider>)
        then context |> unbox<'t> |> Ok
        else context.GetService<'t>()

    let resolve (container: IServiceProvider) (reader: Injected<_,_>) = 
        let (Reader f) = reader
        f container

    let resolveAsync (container: IServiceProvider) (reader: InjectedAsync<_,_>) = 
        async {
            let (Reader f) = reader
            return! f container |> AsyncResult.toAsync
        } |> AsyncResult

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DependencyInjectionBuilder =
    let injected<'t> = InjectionBuilder<'t>()
    let injectedAsync<'t> = AsyncInjectionBuilder<'t>()

    let inject<'t>() : Injected<'t, DependencyInjectionError> = 
        Reader (fun (context: IServiceProvider) -> DependencyInjection.getService<'t> context)

    let injectAsync<'t>() : InjectedAsync<'t, DependencyInjectionError> =
        Reader (fun (context: IServiceProvider) -> DependencyInjection.getService<'t> context |> Async.create |> AsyncResult)