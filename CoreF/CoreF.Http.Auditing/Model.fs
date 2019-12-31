namespace CoreF.Http.Auditing

open CoreF.Common
open CoreF.Http
open System

type AuditingError =
| AuditingConfigurationNotFound
| ErrorCollectingAuditData of IHttpRequest * exn
| UnhandledAuditingError of exn

type IHttpAuditor =
    inherit IDisposable
    abstract member Enabled: bool
    abstract member NumberOfAgents: int
    abstract member AuditRequest: IHttpRequest -> AsyncResult<unit, AuditingError> 
    abstract member AuditResponse: IHttpResponse -> AsyncResult<unit, AuditingError>