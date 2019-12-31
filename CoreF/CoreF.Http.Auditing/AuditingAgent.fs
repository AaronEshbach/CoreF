namespace CoreF.Http.Auditing

open CoreF.Agent
open CoreF.Common
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

type internal AuditMessage =
| Request of (HttpContext * AsyncReplyChannel<CoreF.Http.Auditing.HttpRequest>)
| Response of (CoreF.Http.Auditing.HttpRequest * HttpContext * AsyncReplyChannel<unit>)

module internal AuditingAgent =
    let start numberOfAgents (log: ILogger) (auditor: IHttpAuditor) =
        numberOfAgents
        |> SmallestMailbox
        |> RouterAgent.start (fun _ inbox ->
            let rec loop () =
                async {
                    let! message = inbox.Receive()
                    
                    try 
                        match message with
                        | Request (context, channel) ->
                            let! request = context |> HttpContext.toRequest
                            channel.Reply(request)
                            let! result = auditor.AuditRequest(request) |> AsyncResult.toAsync
                            match result with
                            | Error e -> 
                                log.LogError(sprintf "Error Auditing Request %A: %A" request.RequestId e)
                            | _ -> 
                                ()
                        | Response (request, context, channel) ->
                            let! response = context |> HttpContext.toResponse request
                            channel.Reply()
                            let! result = auditor.AuditResponse(response) |> AsyncResult.toAsync
                            match result with
                            | Error e -> 
                                log.LogError(sprintf "Error Auditing Response %A: %A" response.RequestId e)
                            | _ -> 
                                ()
                    with ex ->
                        log.LogError(ex, sprintf "Error Processing HTTP Audit Message %A" message)
                    
                    return! loop ()
                }
            loop ())