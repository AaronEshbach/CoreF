namespace CoreF.Http.Client

open CoreF.Common

module HttpClientResponse =
    let create x = HttpOk x |> InjectedAsync.create

    let bind<'a, 'b> (f: 'a -> AsyncHttpClientResponse<'b>) (x: AsyncHttpClientResponse<'a>) =
        injectedAsync {
            let! response = x
            match response with
            | HttpOk value -> 
                return! f value
            | HttpRedirect url ->
                return HttpRedirect url
            | HttpClientError error ->
                return HttpClientError error
            | HttpServerError error ->
                return HttpServerError error
        }

    let map f x = x |> bind (f >> create)
    
    let combine<'a> (acc: AsyncHttpClientResponse<'a list>) (cur: AsyncHttpClientResponse<'a>) =
        acc |> bind (fun values -> cur |> map (fun value -> value :: values))
    
    let join results =
        results |> Seq.fold (combine) (create [])


type HttpClientBuilder () =
    member __.Bind (x, f) = HttpClientResponse.bind f x
    member __.Return x = HttpClientResponse.create x
    member __.ReturnFrom x = x
    member __.Zero () = HttpClientResponse.create ()

[<AutoOpen; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpClientBuidler =
    let http = HttpClientBuilder()