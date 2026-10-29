namespace FSharp.Api

open System
open System.Threading.Tasks

[<AutoOpen>]
module Operators =

    let (!) = ignore

[<AutoOpen>]
module Extensions =

    module Async =

        let from x =
            async {
                return x
            }

        let bind fn x =
            async {
                let! a = x
                return! fn a
            }

        let map fn x =
            async {
                let! a = x
                return fn a
            }

        let startAsTask token async =
            Async.StartAsTask(async, cancellationToken = token)

    module Task =

        let from x =
            task { return x }

        let bind (fn:'a -> Task<'b>) x =
            task {
                let! a = x
                return! fn a
            }

        let map fn x =
            task {
                let! a = x
                return fn a
            }

        let toUnit (x:Task) =
            task {
                do! x
                return ()
            }

    module Option =

        let asResult e = function
            | Some x -> Ok x
            | None -> Error e

    module Guid =

        let asString (x:Guid) =
            x.ToString("N")