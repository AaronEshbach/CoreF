namespace CoreF

open CoreF.Common
open System

type HttpApi<'response, 'error> = Injected<AsyncResult<'response, 'error>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpApi =
    let run<'response, 'error> (provider: IServiceProvider) (api: HttpApi<'response, 'error>) =
        async {
            let injectionResult =
            let! serviceResult = api |> AsyncResult.toAsync
            match serviceResult with
            | Ok data ->
                match box data with
                | :? HttpResponse as response ->
                    return response
                | _ ->
                    return Http.ok data
            | Error error ->
                return Errors.toHttpError requestId error
        }