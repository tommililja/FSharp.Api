namespace FSharp.Api.Tests

#nowarn 3391

open System.Net
open System.Net.Http
open FSharp.Api
open RichardSzalay.MockHttp
open Xunit
open Expecto.Flip
open Farse

module UserTests =

    [<Fact>]
    let ``POST /users should register a User and return 201 with the correct location`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            !app.HttpHandlers
                .Expect(HttpMethod.Get, "/test")
                .WithExactQueryString($"?email=%s{user.Email}")
                .Respond("application/json", """{ "data": "123" }""")

            let request =
                JObj [
                    "email", JStr user.Email
                    "birthDate", JStr user.BirthDate
                ]

            let! register = app.RegisterUser(request)
            Expect.equal "Expected equal status codes" HttpStatusCode.Created register.StatusCode

            let! getUser = app.GetUser(user.Id)
            Expect.equal "Expected equal status codes" HttpStatusCode.OK getUser.StatusCode

            let! json = HttpResponseMessage.asJson getUser

            let expected =
                JObj [
                    "id", JStr (Guid.asString user.Id)
                    "email", JStr user.Email
                    "birthDate", JStr user.BirthDate
                    "registeredAt", JStr user.RegisteredAt
                ]

            let actual = json |> Expect.wantOk "Expected valid JSON."

            Expect.equal "Expected equal Location headers" $"/users/%s{Guid.asString user.Id}" (string register.Headers.Location)
            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``POST /users should return 400 when the request is invalid`` () =
        task {
            let user =
                {
                    TestData.user with
                        Email = "invalid"
                        BirthDate = "invalid"
                }

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            let request =
                JObj [
                    "email", JStr user.Email
                    "birthDate", JStr user.BirthDate
                ]

            let! register = app.RegisterUser(request)
            let! json = HttpResponseMessage.asJson register

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                JObj [
                    "errors",
                        JObj [
                            "$.email", JStr.array id [ "Expected '^[^@\s]+@[^@\s]+\.[^@\s]+$' pattern." ]
                            "$.birthDate", JStr.array id [ "Expected 'yyyy-MM-dd' format." ]
                        ]
                    "detail", JStr "Invalid request body."
                    "instance", JStr "/users"
                    "status", JNum 400
                    "title", JStr "One or more validation errors occurred."
                    "traceId", JStr "test-trace-id"
                    "type", JStr "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                ]

            Expect.equal "Expected equal status codes" HttpStatusCode.BadRequest register.StatusCode
            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``POST /users should return 400 when the JSON is invalid`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            let! register = app.RegisterUser("""{ "email: """)
            let! json = HttpResponseMessage.asJson register

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                JObj [
                    "detail", JStr "Invalid JSON."
                    "instance", JStr "/users"
                    "status", JNum 400
                    "title", JStr "Bad Request"
                    "traceId", JStr "test-trace-id"
                    "type", JStr "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                ]

            Expect.equal "Expected equal status codes" HttpStatusCode.BadRequest register.StatusCode
            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``POST /users should return 409 when the User is already registered`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            do! app.InsertUser user

            let request =
                JObj [
                    "email", JStr user.Email
                    "birthDate", JStr user.BirthDate
                ]

            let! register = app.RegisterUser(request)
            let! json = HttpResponseMessage.asJson register

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                JObj [
                    "detail", JStr "User is already registered."
                    "instance", JStr "/users"
                    "status", JNum 409
                    "title", JStr "Conflict"
                    "traceId", JStr "test-trace-id"
                    "type", JStr "https://tools.ietf.org/html/rfc9110#section-15.5.10"
                ]

            Expect.equal "Expected equal status codes" HttpStatusCode.Conflict register.StatusCode
            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``GET /users should return all Users`` () =
        task {
            let user = TestData.user
            let users = 100

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            do! app.InsertUsers users

            let! getUsers = app.GetUsers()
            Expect.equal "Expected equal status codes." HttpStatusCode.OK getUsers.StatusCode

            let! json = HttpResponseMessage.asJson getUsers

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                [ 1 .. users ]
                |> JObj.array (fun n ->
                    [
                        "id", JStr (TestData.userId n)
                        "email", JStr $"user%i{n}@domain.com"
                        "birthDate", JStr "2000-01-01"
                        "registeredAt", JStr "2026-01-01T00:00:00Z"
                    ]
                )

            return Expect.equalJson expected actual
        }

    [<Fact>]
    let ``GET /users/id should return 404 when the User is not found`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            let! getUser = app.GetUser(user.Id)
            let! json = HttpResponseMessage.asJson getUser

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                JObj [
                    "detail", JStr "Resource not found."
                    "instance", JStr $"/users/%s{Guid.asString user.Id}"
                    "status", JNum 404
                    "title", JStr "Not Found"
                    "traceId", JStr "test-trace-id"
                    "type", JStr "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                ]

            Expect.equal "Expected equal status codes" HttpStatusCode.NotFound getUser.StatusCode
            Expect.equalJson expected actual
        }

    [<Fact>]
    let ``DELETE /users/id should unregister the User and return 204`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            do! app.InsertUser user

            let! unregister = app.UnregisterUser(user.Id)
            Expect.equal "Expected equal status codes" HttpStatusCode.NoContent unregister.StatusCode

            let! getUser = app.GetUser(user.Id)
            Expect.equal "Expected equal status codes" HttpStatusCode.NotFound getUser.StatusCode
        }

    [<Fact>]
    let ``DELETE /users/id should return 404 when the user is not found`` () =
        task {
            let user = TestData.user

            use! app =
                user.Id
                |> GuidGen.single
                |> TestApp.create

            let! unregister = app.UnregisterUser(user.Id)
            let! json = HttpResponseMessage.asJson unregister

            let actual = json |> Expect.wantOk "Expected valid JSON."

            let expected =
                JObj [
                    "detail", JStr "Resource not found."
                    "instance", JStr $"/users/%s{Guid.asString user.Id}"
                    "status", JNum 404
                    "title", JStr "Not Found"
                    "traceId", JStr "test-trace-id"
                    "type", JStr "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                ]

            Expect.equal "Expected equal status codes" HttpStatusCode.NotFound unregister.StatusCode
            Expect.equalJson expected actual
        }