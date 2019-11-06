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

type ReaderBuilder<'t, 'u> () = 
    member __.Return (x) = Reader.create x 
    member __.Bind (x, f) = Reader.bind f x 

type InjectionBuilder<'t> () =
    member __.Return (x) : Injected<_> = Reader.create x 
    member __.Bind (x, f) : Injected<_> = Reader.bind f x 

module DependencyInjection =
    type IServiceProvider with 
        member this.GetService<'t>() = 
            this.GetService(typeof<'t>) :?> 't 

    let getService<'t> (context : IServiceProvider) : 't = 
      if typeof<'t>.IsAssignableFrom(typeof<IServiceProvider>)
      then context |> unbox<'t>
      else context.GetService<'t>()

    let resolveAll (reader: Injected<_>) (serviceProvider: IServiceProvider) = 
        let (Reader f) = reader
        f serviceProvider

[<AutoOpen>]
module ReaderMonad =
    let reader<'a, 'b> = ReaderBuilder<'a, 'b>() 

    let inject<'t> = InjectionBuilder<'t>()

    let resolve<'t>() = Reader (fun (context: IServiceProvider) -> DependencyInjection.getService<'t> context) 

