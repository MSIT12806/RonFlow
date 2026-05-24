using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class SqliteUserDirectory(SqliteCoreFlowStore store) : IUserDirectory
{
    public KnownUser? FindByEmail(string email)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT UserId, UserName, Email
FROM KnownUsers
WHERE Email = $email COLLATE NOCASE
LIMIT 1";
        command.Parameters.AddWithValue("$email", email);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new KnownUser(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2));
    }

    public void Upsert(KnownUser user)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO KnownUsers (UserId, UserName, Email)
VALUES ($userId, $userName, $email)
ON CONFLICT(UserId) DO UPDATE SET
    UserName = excluded.UserName,
    Email = excluded.Email;";
        command.Parameters.AddWithValue("$userId", user.UserId.ToString());
        command.Parameters.AddWithValue("$userName", user.UserName);
        command.Parameters.AddWithValue("$email", user.Email);
        command.ExecuteNonQuery();
    }
}