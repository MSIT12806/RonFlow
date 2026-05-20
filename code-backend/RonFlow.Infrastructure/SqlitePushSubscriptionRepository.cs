using RonFlow.Domain;

namespace RonFlow.Infrastructure;

public sealed class SqlitePushSubscriptionRepository(SqliteCoreFlowStore store) : IPushSubscriptionRepository
{
    public IReadOnlyList<PushSubscription> GetAll()
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Data FROM PushSubscriptions";

        using var reader = command.ExecuteReader();
        var subscriptions = new List<PushSubscription>();

        while (reader.Read())
        {
            subscriptions.Add(CoreFlowJsonSerializer.DeserializePushSubscription(reader.GetString(0)));
        }

        return subscriptions
            .OrderBy(subscription => subscription.SubscribedAt)
            .ToArray();
    }

    public void Upsert(PushSubscription subscription)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO PushSubscriptions (Endpoint, Data)
VALUES ($endpoint, $data)
ON CONFLICT(Endpoint) DO UPDATE SET Data = excluded.Data;";
        command.Parameters.AddWithValue("$endpoint", subscription.Endpoint);
        command.Parameters.AddWithValue("$data", CoreFlowJsonSerializer.Serialize(subscription));
        command.ExecuteNonQuery();
    }

    public void Remove(string endpoint)
    {
        using var connection = store.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PushSubscriptions WHERE Endpoint = $endpoint";
        command.Parameters.AddWithValue("$endpoint", endpoint);
        command.ExecuteNonQuery();
    }
}