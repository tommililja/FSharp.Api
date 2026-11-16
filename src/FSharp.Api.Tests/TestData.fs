namespace FSharp.Api.Tests

open System
open Microsoft.Data.SqlClient

type TestUser = {
    Id: Guid
    Email: string
    BirthDate: string
}

module TestData =

    let user = {
        Id = Guid.NewGuid ()
        Email = "alice@domain.com"
        BirthDate = "2000-01-01"
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

    let insertUser user conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate)
                VALUES ('%s{string user.Id}', '%s{user.Email}', '%s{user.BirthDate}')
            "

    let userId (i:int) =
        Guid($"00000000-0000-0000-0000-%012i{i}")

    let insertUsers n conn =
        execute conn
            $"
                INSERT INTO [User] (Id, Email, BirthDate)
                SELECT
                    CAST(CONCAT('00000000-0000-0000-0000-', RIGHT(CONCAT('000000000000', value), 12)) AS UNIQUEIDENTIFIER),
                    CONCAT('user', value, '@domain.com'),
                    '2000-01-01'
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

                    INDEX UX_User_Email UNIQUE (Email)
                )
            "

    let resetTables conn =
        execute conn
            "TRUNCATE TABLE [User]"