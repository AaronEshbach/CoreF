﻿namespace CoreF.Http.Middleware

open CoreF.Common
open CoreF.DependencyInjection
open CoreF.Http
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open System.IO

module internal Middleware =
    let httpError requestId name message =
        {
            DebugId = requestId |> RequestId.value
            ErrorName = name
            Message = message
            Details = []
            Links = []
        } :> IHttpError

    let httpErrors requestId name message details =
        {
            DebugId = requestId |> RequestId.value
            ErrorName = name
            Message = message
            Details = details
            Links = []
        }

    let requestValidationError requestId failures =
        let deserializationError = failures |> List.choose (fun f -> match f with | ErrorDeserializingRequest ex -> Some ex | _ -> None)

        let getErrorDetail = function
        | MissingRequestBody -> [{Field = "Body"; Value = ""; Location = Location.Body; Issue = "Request body was missing"}]
        | ParameterNotFound name -> [{Field = name; Value = ""; Location = Location.Query; Issue = "Required parameter was missing"}]
        | ErrorDeserializingRequest ex -> [{Field = "Body"; Value = ""; Location = Location.Body; Issue = sprintf "%A" ex}]
        | NoDtoMappingFunctionFound (d,t) -> [{Field = "Body"; Value = ""; Location = Location.Body; Issue = sprintf "No DTO Mapping found for Types %s -> %s" d.Name t.Name}]
        | ParameterDomainValidationError errors -> errors |> Seq.map (fun error -> {Field = ""; Value = ""; Location = Location.Query; Issue = error}) |> Seq.toList
        | other -> [{Field = "Body"; Value = ""; Location = Location.Body; Issue = sprintf "%A" other}]

        let details = failures |> List.collect getErrorDetail

        match deserializationError with
        | error :: _ ->
            Http.badRequest <| httpError requestId "Error Deserializing Request" (sprintf "%A" error)
        | [] ->
            Http.unprocessableEntity <| httpErrors requestId "Parameter Validation Failed" "One or more request parameters could not be validated, please see the error details" details

    let populateResponse (context: HttpContext) (response: CoreF.Http.HttpResponse) = 
        api {
            // To be used when there is no response data
            let noData = Async.create ()

            let writeResponse responseData =
                api {
                    match responseData with
                    | Some data -> 
                        let! json = data |> HttpResponseData.toJson |> Injected.mapError HttpResponseSerializationError
                        return! context.Response.WriteAsync(json) |> Async.AwaitTask
                    | None -> 
                        return! noData
                }

            context.Response.Headers.Add("Content-Type", StringValues("application/json; charset=utf-8"))
            context.Response.StatusCode <- response.StatusCode        

            match response with
            | OK _ ->
                do! response.Data |> writeResponse            
            | Created (_, location) ->
                match location with
                | Some url -> context.Response.Headers.["Location"] <- StringValues(url.AbsoluteUri)
                | None -> ()
                do! response.Data |> writeResponse
            | Accepted -> 
                do! noData
            | NoContent -> 
                do! noData
            | MultiStatus _ -> 
                do! response.Data |> writeResponse
            | TemporaryRedirect redirect ->
                context.Response.Headers.["Location"] <- StringValues(redirect.AbsoluteUri)
                do! context.Response.WriteAsync(redirect.AbsoluteUri) |> Async.AwaitTask
            | PermanentRedirect redirect ->
                context.Response.Headers.["Location"] <- StringValues(redirect.Location.AbsoluteUri)
                do! response.Data |> writeResponse
            | BadRequest _ ->
                do! response.Data |> writeResponse
            | Unauthorized _ ->
                do! response.Data |> writeResponse
            | Forbidden ->
                do! noData
            | NotFound ->
                do! noData
            | NotAcceptable ->
                do! noData
            | UnprocessableEntity _ ->
                do! response.Data |> writeResponse
            | InternalServerError error ->
                match error with
                | Some _ -> 
                    do! response.Data |> writeResponse
                | None -> 
                    do! context.Response.WriteAsync("HTTP 500 - Internal Server Error") |> Async.AwaitTask
        }

    let invoke (context: HttpContext) =
        asyncResult {
            let! request = HttpContext.toRequest context
            let! entryPointResult = HttpRequest.findEntryPoint request |> Injected.run context.RequestServices |> Result.mapError UnresolveDependencies
            
            let! response = 
                match entryPointResult with
                | FoundMatches candidates ->
                    let (entryPoint, parameters) = candidates.BestMatch.EntryPoint, candidates.BestMatch.ParameterResults
            
                    let callService () = 
                        asyncResult {
                            let! injectable = HttpRequest.callEntryPointAsync entryPoint request parameters
                            let! response = injectable |> Injected.run context.RequestServices |> AsyncResult.toAsync
                            return
                                match response with
                                | Ok httpResponse -> 
                                    httpResponse
                                | Error error -> 
                                    match error with
                                    | ErrorMatchingAvailableEntryPoints matchError ->
                                        match matchError with
                                        | HttpRequestTemplateDoesNotMatch _ ->
                                            Http.notFound ()
                                        | EntryPointParametersDoNotMatch parameterError ->
                                            match parameterError with
                                            | MissingRequiredParameters errors ->
                                                match errors with
                                                | [] ->
                                                    Http.badRequest <| httpError request.RequestId "Missing Parameters" "The HTTP request was missing one or more required parameters for this service"
                                                | validationErrors ->
                                                    requestValidationError  request.RequestId validationErrors
                                            | InvalidRequestBodyType -> 
                                                Http.badRequest <| httpError  request.RequestId "Invalid Request" "The Request Body did not match the expected data structure for this service"
                                            | ParameterValidationFailed validationErrors ->
                                                requestValidationError  request.RequestId validationErrors
                                        | _ ->
                                            Http.internalServerError (Some <| httpError  request.RequestId "Error Matching Service Request" (sprintf "%A" error))
                                    | RuntimeParameterValidationFailed validationErrors ->
                                            requestValidationError  request.RequestId validationErrors
                                    | _ ->
                                        Http.internalServerError  (Some <| httpError  request.RequestId "Error Executing Service" (sprintf "%A" error))
                        }

                    callService ()
                | NoMatchesFound nonMatches ->
                    let reason =
                        nonMatches |> List.reduce (fun acc cur -> 
                            match acc with
                            | RequestDoesNotMatchRouteTemplate _ -> 
                                match cur with
                                | RequestParametersDoNotMatch _ -> cur
                                | _ -> acc
                            | RequestDoesNotMatchContext _ -> 
                                match cur with
                                | RequestParametersDoNotMatch _ -> cur
                                | RequestDoesNotMatchRouteTemplate _ -> cur
                                | _ -> acc
                            | RequestParametersDoNotMatch accEntry ->
                                match cur with
                                | RequestParametersDoNotMatch curEntry -> 
                                    match accEntry.Error with
                                    | MissingRequiredParameters errors -> 
                                        match curEntry.Error with
                                        | InvalidRequestBodyType -> acc
                                        | ParameterValidationFailed failures -> 
                                            if failures.Length > errors.Length
                                            then cur
                                            else acc
                                        | _ -> acc
                                    | InvalidRequestBodyType ->
                                        match curEntry.Error with
                                        | ParameterValidationFailed _ -> cur
                                        | _ -> acc
                                    | ParameterValidationFailed accFailures ->
                                        match curEntry.Error with
                                        | ParameterValidationFailed curFailures -> 
                                            if curFailures.Length < accFailures.Length
                                            then cur
                                            else acc
                                        | _ -> acc
                                | _ -> acc
                            | _ -> cur)
                    match reason with
                    | RequestDoesNotMatchContext _ -> 
                        Http.notFound ()
                    | RequestDoesNotMatchRouteTemplate entry -> 
                        match entry.Error with
                        | WrongVerb ->
                            sprintf "The HTTP Verb %A is not supported for the resource %O" request.Method request.Url
                            |> httpError  request.RequestId "Unsupported Method" 
                            |> Http.badRequest
                        | InvalidUrl -> 
                            sprintf "The URL for resource %O is not valid" request.Url
                            |> httpError  request.RequestId "Invalid URL" 
                            |> Http.badRequest
                        | _ -> 
                            Http.notFound ()
                    | RequestParametersDoNotMatch entry ->
                        match entry.Error with
                        | MissingRequiredParameters errors ->
                            match errors with
                            | [] ->
                                Http.unprocessableEntity <| httpError  request.RequestId "Missing Parameter" (sprintf "The HTTP Request for %A is missing one or more required parameters" request.Url)                            
                            | validationErrors ->
                                requestValidationError  request.RequestId validationErrors                            
                        | InvalidRequestBodyType ->
                            Http.badRequest <| httpError  request.RequestId "Invalid Request Body" "The HTTP request body could not be deserialized to the expected type"
                        | ParameterValidationFailed failures ->
                            requestValidationError  request.RequestId failures
                    | _ ->
                        Http.internalServerError None
                    |> AsyncResult.create

            return! response |> populateResponse context |> Injected.run context.RequestServices
        }

type HttpMiddleware (next: RequestDelegate) =
    member __.InvokeAsync (context) =
        asyncResult {
            do! context |> Middleware.invoke 
            do! next.Invoke(context) |> Async.AwaitTask
        } |> AsyncResult.toTask :> System.Threading.Tasks.Task