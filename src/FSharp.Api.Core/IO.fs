namespace FSharp.Api.Core

open System
open System.Threading.Tasks

[<Struct; NoComparison; NoEquality>]
type AsyncResult<'r, 'e> = AsyncResult of Async<Result<'r, 'e>>

module AsyncResult =

    let from x =
        Ok x
        |> Async.from
        |> AsyncResult

    let fromResult x =
        x
        |> Async.from
        |> AsyncResult

    let fromTask x =
        x
        |> Async.AwaitTask
        |> Async.map Ok
        |> AsyncResult

    let asAsync (AsyncResult x) = x

    let bind fn x =
        async {
            let! a = asAsync x
            return!
                match a with
                | Ok x -> fn x
                | Error e -> Error e |> fromResult
                |> asAsync
        }
        |> AsyncResult

    let map fn = bind (fn >> from)

[<AutoOpen>]
module AsyncResultBuilder =
    open AsyncResult

    type AsyncResultBuilder() =

        member _.Bind(x, fn) = bind fn x
        member _.Return(x) = from x
        member _.ReturnFrom(x) = x
        member _.Zero() = from ()

    let asyncResult = AsyncResultBuilder ()

[<Struct; NoComparison; NoEquality>]
type IO<'c, 'r, 'e> = IO of ('c -> AsyncResult<'r, 'e>)

module IO =

    let create fn = IO (fn >> AsyncResult.from)

    let createFromTask fn = IO (fn >> AsyncResult.fromTask)

    let from x = IO (fun _ -> AsyncResult.from x)

    let fromAsync x = IO (fun _ -> Async.map Ok x |> AsyncResult)

    let fromTask x = IO (fun _ -> AsyncResult.fromTask x)

    let fromResult x = IO (fun _ -> AsyncResult.fromResult x)

    let run c (IO fn) = fn c

    let bind fn x =
        IO (fun c ->
            asyncResult {
                let! a = run c x
                return! run c (fn a)
            }
        )

    let bindResult fn x =
        IO (fun c ->
            run c x
            |> AsyncResult.bind (fn >> AsyncResult.fromResult)
        )

    let map fn x =
        IO (fun c ->
            asyncResult {
                let! a = run c x
                return fn a
            }
        )

    let ignore x = map ignore x

    let someOr e io =
        io
        |> bindResult (Option.asResult e)

[<AutoOpen>]
module IOBuilder =
    open IO

    type IOBuilder() =

        member _.Bind(x:IO<_, _, _>, fn) = bind fn x
        member _.Bind(x:Async<_>, fn) = fromAsync x |> bind fn
        member _.Bind(x:Task<_>, fn) = fromTask x |> bind fn
        member _.Bind(x:Result<_, _>, fn) = fromResult x |> bind fn

        member _.Return(x) = from x
        member _.ReturnFrom(x:IO<_, _, _>) = x
        member _.ReturnFrom(x:Async<_>) = fromAsync x
        member _.ReturnFrom(x:Task<_>) = fromTask x
        member _.ReturnFrom(x:Result<_, _>) = fromResult x

        member _.Delay(fn:unit -> IO<_, _, _>) = IO (fun c -> run c (fn ()))
        member _.Combine(a:IO<'c, unit, 'e>, b:IO<'c, 'r, 'e>) = bind (fun () -> b) a

        member _.Zero() = from ()

    let io = IOBuilder ()