namespace FSharp.Api.Tests

open System.Collections.Concurrent
open NodaTime

[<NoComparison; NoEquality>]
type TimeGen = TimeGen of (unit -> Instant)

module TimeGen =

    let system = TimeGen SystemClock.Instance.GetCurrentInstant

    let single instant = TimeGen (fun () -> instant)

    let list instants =
        let queue = ConcurrentQueue(instants)
        TimeGen (fun () ->
            match queue.TryDequeue() with
            | true, instant -> instant
            | _ -> failwith "Ran out of Instants to pick."
        )

    let steps (start:Instant) (step:Duration) =
        let mutable next = start
        TimeGen (fun () ->
            let current = next
            next <- current + step
            current
        )

    let generate (TimeGen fn) = fn ()

    let asFn (TimeGen fn) = fn