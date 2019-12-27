namespace CoreF.Http.Client

open CoreF.Common

module HttpClientCall =
    let create x = 
        HttpOk x |> InjectedAsync.create |> HttpClientCall

    let bind<'a, 'b> (f: 'a -> HttpClientCall<'b>) (HttpClientCall x) =
        injectedAsync {
            let! response = x
            match response with
            | HttpOk value -> 
                let (HttpClientCall result) = f value
                return! result
            | HttpRedirect url ->
                return HttpRedirect url
            | HttpClientError error ->
                return HttpClientError error
            | HttpServerError error ->
                return HttpServerError error
        } |> HttpClientCall

    let map f x = x |> bind (f >> create)
    
    let combine<'a> (acc: HttpClientCall<'a list>) cur =
        acc |> bind (fun values -> cur |> map (fun value -> value :: values))
    
    let join results =
        results |> Seq.fold (combine) (create [])

    let ofAsyncResult (result: AsyncResult<_,_>) : HttpClientCall<_> =
        result |> AsyncResult.map HttpOk |> Reader.create |> HttpClientCall

    let ofAsync (result: Async<_>) : HttpClientCall<_> =
        result |> Async.map Ok |> AsyncResult |> ofAsyncResult

    let ofInjected (injected: Injected<_,_>) : HttpClientCall<_> =
        let future state =
            async {
                let result = injected |> Reader.run state
                return result |> Result.map HttpOk
            } |> AsyncResult
        Reader future |> HttpClientCall

    let ofInjectedAsync (injected: InjectedAsync<_,_>) : HttpClientCall<_> =
        let future state =
            async {
                let result = injected |> Reader.run state
                return! result |> AsyncResult.map HttpOk |> AsyncResult.toAsync
            } |> AsyncResult
        Reader future |> HttpClientCall

    let ofResult (result: Result<_,_>) : HttpClientCall<_> =
        result |> Async.create |> AsyncResult |> ofAsyncResult

    let value (HttpClientCall x) = x

    let run (container: System.IServiceProvider) (HttpClientCall x) =
        x |> DependencyInjection.resolveAsync container

type HttpClientBuilder () =
    member __.Bind (x, f) = HttpClientCall.bind f x
    member __.Return x = HttpClientCall.create x
    member __.ReturnFrom (x: HttpClientCall<_>) : HttpClientCall<_> = x
    member __.ReturnFrom (x: AsyncResult<_,_>) : HttpClientCall<_> = HttpClientCall.ofAsyncResult x
    member __.ReturnFrom (x: Async<_>) : HttpClientCall<_> = HttpClientCall.ofAsync x
    member __.ReturnFrom (x: Result<_,_>) : HttpClientCall<_> = HttpClientCall.ofResult x
    member __.ReturnFrom (x: Injected<_,_>) : HttpClientCall<_> = HttpClientCall.ofInjected x
    member __.ReturnFrom (x: InjectedAsync<'a,_>) : HttpClientCall<'a> = HttpClientCall.ofInjectedAsync x
    member __.Yield x = HttpClientCall.create x
    member __.YieldFrom x = x
    member __.Zero () = HttpClientCall.create ()
    member __.Delay (f) : HttpClientCall<_> = f()
    member __.Combine (a, b) : HttpClientCall<_> =
        a |> HttpClientCall.bind (fun () -> b)
    member this.TryFinally<'a>(body: unit -> HttpClientCall<'a>, compensation) : HttpClientCall<'a> =
        try 
            this.ReturnFrom(body())
        finally 
            compensation()
    member this.Using(resource : 'T when 'T :> System.IDisposable, binder : 'T -> HttpClientCall<_>) : HttpClientCall<_> = 
        let body' = fun () -> binder resource
        this.TryFinally(body', fun () -> 
            match resource with 
                | null -> () 
                | disp -> disp.Dispose())

    member this.While (guard, body: unit -> HttpClientCall<_>) : HttpClientCall<_> =
        if not (guard()) then 
            this.Zero()
        else
            this.Bind(body(), fun () -> this.While(guard, body))

    member this.For (sequence: seq<_>, body) : HttpClientCall<_> =
        this.Using(sequence.GetEnumerator(), fun enum ->
            this.While(enum.MoveNext, fun () -> body enum.Current))

[<AutoOpen; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpClientBuidler =
    let http = HttpClientBuilder()