namespace FSharp.Api.Tests

open System
open System.Collections.Concurrent

[<NoComparison; NoEquality>]
type GuidGen = GuidGen of (unit -> Guid)

module GuidGen =

    let random = GuidGen Guid.NewGuid

    let always guid = GuidGen (fun () -> guid)

    let inOrder list =
        let queue = ConcurrentQueue(list)
        GuidGen (fun () ->
            match queue.TryDequeue() with
            | true, guid -> guid
            | _ -> failwith "Ran out of Guids to pick."
        )

    let generate (GuidGen fn) = fn ()