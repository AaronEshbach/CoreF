namespace CoreF.Common

open System

/// Defines a standard 'Reader' monad
type Reader<'a, 'b> = Reader of ('a -> 'b) 

/// Defines a specialized Reader monad for Dependency Injection
type Injected<'t> = Reader<IServiceProvider, 't>

module Reader = 
    let run x (Reader f) = 
        f x

    let create x = 
        Reader (fun _ -> x) 
  
    let bind f x = 
        let future state =
            let z = run state x 
            run state (f z)
        Reader future

    let map f x =
        bind (f >> create) x

type ReaderBuilder<'t, 'u> () = 
    member __.Bind (x, f) = Reader.bind f x 
    member __.Return (x) = Reader.create x 
    member __.ReturnFrom x = x  
    member __.Zero () = Reader.create ()
    member __.Delay (f) = f()
    member __.Combine (a, b) =
        a |> Reader.bind (fun () -> b)
    member this.TryFinally(body, compensation) =
        try 
            this.ReturnFrom(body())
        finally 
            compensation()
    member this.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> Reader<'a, 'b>) : Reader<'a, 'b> = 
        let body' = fun () -> binder resource
        this.TryFinally(body', fun () -> 
            match resource with 
                | null -> () 
                | disp -> disp.Dispose())

    member this.While (guard, body: unit -> Reader<_,_>) =
        if not (guard()) then 
            this.Zero()
        else
            this.Bind(body(), fun () -> this.While(guard, body))

    member this.For (sequence: seq<_>, body) =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))

type InjectionBuilder<'t> () =
    member __.Bind (x, f) : Injected<_> = Reader.bind f x 
    member __.Return (x) : Injected<_> = Reader.create x 
    member __.ReturnFrom x = x    
    member __.Zero () : Injected<_> = Reader.create ()
    member __.Delay (f) : Injected<_> = f()
    member __.Combine (a, b) : Injected<_> =
        a |> Reader.bind (fun () -> b)
    member this.TryFinally(body, compensation) : Injected<_> =
        try 
            this.ReturnFrom(body())
        finally 
            compensation()
    member this.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> Injected<'a>) : Injected<'a> = 
        let body' = fun () -> binder resource
        this.TryFinally(body', fun () -> 
            match resource with 
                | null -> () 
                | disp -> disp.Dispose())

    member this.While (guard, body: unit -> Reader<_,_>) : Injected<_> =
        if not (guard()) then 
            this.Zero()
        else
            this.Bind(body(), fun () -> this.While(guard, body))

    member this.For (sequence: seq<_>, body) : Injected<_> =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))

module DependencyInjection =
    type IServiceProvider with 
        member this.GetService<'t>() = 
            this.GetService(typeof<'t>) :?> 't 

    let getService<'t> (context : IServiceProvider) : 't = 
      if typeof<'t>.IsAssignableFrom(typeof<IServiceProvider>)
      then context |> unbox<'t>
      else context.GetService<'t>()

    let resolveAll (serviceProvider: IServiceProvider) (reader: Injected<_>) = 
        let (Reader f) = reader
        f serviceProvider

[<AutoOpen>]
module ReaderMonad =
    let reader<'a, 'b> = ReaderBuilder<'a, 'b>() 

    let injected<'t> = InjectionBuilder<'t>()

    let inject<'t>() = Reader (fun (context: IServiceProvider) -> DependencyInjection.getService<'t> context) 

