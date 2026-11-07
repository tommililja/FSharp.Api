namespace FSharp.Api

open System.Collections.Generic
open System.Text
open Microsoft.OpenApi
open Oxpecker.ViewEngine

module Docs =

    let document =
        OpenApiDocument (
            Info = OpenApiInfo(Title = "F# REST API", Version = "v1", Description = "An F# REST API."),
            Servers = ResizeArray [ OpenApiServer(Url = "/") ],
            Paths = OpenApi.paths UserPaths.paths,
            Tags = HashSet [ OpenApiTag(Name = "Users") ]
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
                title() { document.Info.Title }
                script(src = "https://unpkg.com/@stoplight/elements/web-components.min.js") { }
                link(rel = "stylesheet", href = "https://unpkg.com/@stoplight/elements/styles.min.css")
            }
            body() {
                RegularNode("elements-api")
                    .attr("apiDescriptionUrl", "openapi.json")
                    .attr("router", "hash")
            }
        }