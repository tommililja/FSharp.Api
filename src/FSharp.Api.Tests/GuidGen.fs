namespace FSharp.Api.Tests

open System
open System.Collections.Concurrent

[<NoComparison; NoEquality>]
type GuidGen = GuidGen of (unit -> Guid)

module GuidGen =

    let random = GuidGen Guid.NewGuid

    let single guid = GuidGen (fun () -> guid)

    let list guids =
        let queue = ConcurrentQueue(guids)
        GuidGen (fun () ->
            match queue.TryDequeue() with
            | true, guid -> guid
            | _ -> failwith "Ran out of Guids to pick."
        )

    let map items =
        if List.distinct items <> items then
            failwith "Guid names must be unique."

        let map =
            items
            |> List.map (fun name -> name, Guid.NewGuid())

        let guids =
            map
            |> List.map snd

        list guids, Map map

    let generate (GuidGen fn) = fn ()

    let asFn (GuidGen x) = x