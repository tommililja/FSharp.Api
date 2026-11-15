namespace FSharp.Api

open System
open Orsak
open Farse
open Farse.Operators

type Data = {
    Data: string
}

module Data =
    open Parse

    let parse =
        parser {
            let! data = "data" &= string
            return { Data = data }
        }

module ExampleService =

    let getData email =
        eff {
            let! settings = AppSettings.get ()

            let path = "test" |> Url.withQuery [ "email", Email.asString email ]
            let url = Uri(settings.Integrations.ExampleService, path)

            return! Http.getJson Data.parse url
        }