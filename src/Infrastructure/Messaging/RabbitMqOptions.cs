namespace Kart.Search.Infrastructure.Messaging;

/// <summary>Binds the <c>"RabbitMq"</c> config section - the messaging settings not described by
/// the manifest itself.</summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>Defaults to RabbitMQ's standard AMQP port - overridden in tests, where
    /// Testcontainers maps the broker to a randomized host port.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// RabbitMQ's built-in <c>guest</c> user is restricted to true loopback connections by
    /// default (<c>loopback_users</c>) - a connection through Docker's port-forwarding NAT (every
    /// container-to-container hop in docker-compose, and every Testcontainers-mapped host port)
    /// does not satisfy that check and is rejected with <c>ACCESS_REFUSED</c>, even though it
    /// "feels" local to the client. Defaults to <c>guest</c>/<c>guest</c> for the genuinely-local
    /// case (a locally-installed RabbitMQ reached from <c>dotnet run</c> on the same host) -
    /// docker-compose/tests override both to a non-guest user, which is not subject to this
    /// restriction.
    /// </summary>
    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
