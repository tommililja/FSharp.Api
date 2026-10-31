namespace FSharp.Api

open System.Collections.Generic
open System.Net.Http
open System.Text
open Microsoft.OpenApi
open Oxpecker.ViewEngine

module Docs =

    [<Literal>]
    let JsonFilename = "openapi.json"

    let document =
        OpenApiDocument (
            Info = OpenApiInfo(Title = "F# REST API", Version = "v1", Description = "An F# REST API."),
            Servers = ResizeArray [ OpenApiServer(Url = "/") ],
            Paths =
                OpenApi.paths [
                    "/user", OpenApi.pathItem [
                        HttpMethod.Get, UserPaths.getUsers
                        HttpMethod.Post, UserPaths.registerUser
                    ]
                    "/user/{userId}", OpenApi.pathItem [
                        HttpMethod.Get, UserPaths.getUser
                        HttpMethod.Delete, UserPaths.deleteUser
                    ]
                ],
            Tags = HashSet [ OpenApiTag(Name = "User") ]
        )

    let json =
        let json =
            document
                .SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_2)
                .GetAwaiter()
                .GetResult()

        Encoding.UTF8.GetBytes(json)

    let html =
        html(lang = "en") {
            head() {
                meta(charset = "utf-8")
                meta(name = "viewport", content = "width=device-width, initial-scale=1, shrink-to-fit=no")
                title() { "F# Api" }
                script(src = "https://unpkg.com/@stoplight/elements/web-components.min.js") { }
                link(rel = "stylesheet", href = "https://unpkg.com/@stoplight/elements/styles.min.css")
            }
            body() {
                RegularNode("elements-api")
                    .attr("apiDescriptionUrl", JsonFilename)
                    .attr("router", "hash")
            }
        }