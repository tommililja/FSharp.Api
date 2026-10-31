namespace FSharp.Api

open System
open System.Text.RegularExpressions

type AggregateId = AggregateId of Guid

module AggregateId =

    let asGuid (AggregateId x) = x

    let asString (AggregateId x) =
        Guid.asString x

type UserId = UserId of AggregateId

module UserId =

    let fromGuid = AggregateId >> UserId

    let asGuid (UserId x) =
        AggregateId.asGuid x

    let asString (UserId x) =
        AggregateId.asString x

type Email = Email of string

module Email =

    let private emailRegex =
        Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)

    let parse (str:string) =
        if emailRegex.IsMatch(str)
        then Ok (Email str)
        else Error "Invalid email address."

    let asString (Email x) = x

type User = {
    Id: UserId
    Email: Email
}