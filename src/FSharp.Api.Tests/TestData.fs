namespace FSharp.Api.Tests

open System
open Microsoft.Data.SqlClient
open FSharp.Api
open NodaTime

type TestUser = {
    Id: Guid
    Email: string
    BirthDate: string
    RegisteredAt: Instant
}

module TestData =

    let user = {
        Id = Guid.NewGuid ()
        Email = "alice@domain.com"
        BirthDate = "2000-01-01"
        RegisteredAt = Instant.FromUtc(2026, 1, 1, 12, 0)
    }

    let private execute (connectionString:string) (sql:string) =
        task {
            use connection = new SqlConnection(connectionString)
            do! connection.OpenAsync()
            use command = connection.CreateCommand()
            command.CommandText <- sql
            let! _ = command.ExecuteNonQueryAsync()

            ()
        }

    let userId (n:int) = $"00000000000000000000%012i{n}"

    let insertUser user conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate, RegisteredAt)
                VALUES ('%s{string user.Id}', '%s{user.Email}', '%s{user.BirthDate}', '%s{Instant.as8601String user.RegisteredAt}')
            "

    let insertUsers n conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate, RegisteredAt)
                SELECT
                    CAST(CONCAT('00000000-0000-0000-0000-', RIGHT(CONCAT('000000000000', value), 12)) AS UNIQUEIDENTIFIER),
                    CONCAT('user', value, '@domain.com'),
                    '2000-01-01',
                    '2026-01-01T00:00:00'
                FROM GENERATE_SERIES(1, %i{n})
            "

    let createTables conn =
        execute conn
            "
                DROP TABLE IF EXISTS [User]

                CREATE TABLE [User]
                (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Email NVARCHAR(255) NOT NULL,
                    BirthDate DATE NOT NULL,
                    RegisteredAt DATETIME2 NOT NULL,

                    INDEX UX_User_Email UNIQUE (Email),
                    INDEX IX_User_RegisteredAt (RegisteredAt, Id)
                )
            "

    let resetTables conn =
        execute conn
            "TRUNCATE TABLE [User]"