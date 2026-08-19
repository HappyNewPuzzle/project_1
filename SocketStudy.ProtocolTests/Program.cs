using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

await RunProtocolRoundTripTestAsync(MessageType.Chat, "alice: hello");
await RunProtocolRoundTripTestAsync(MessageType.Notice, "Welcome.");
await RunProtocolRoundTripTestAsync(MessageType.Command, "/users");
await RunProtocolRoundTripTestAsync(MessageType.Chat, "");
await RunProtocolRoundTripTestAsync(MessageType.Chat, "한글 메시지와 emoji 🙂");
await RunInvalidMessageTypeTestAsync();
await RunIncompleteBodyTestAsync();
await RunTooLargeLengthTestAsync();
await RunTlsProtocolRoundTripTestAsync();
RunTlsCertificateRotationTest();
RunServerOptionsTest();
RunStructuredLoggerTest();
RunServerMetricsTest();
RunServerHealthTest();
RunSessionOwnershipTest();
RunMessageSizeLimitTest();
RunNameRulesTest();
RunServerInfoTest();
RunServerLifecycleTest();
RunConnectionAdmissionControllerTest();
RunConnectionRateLimiterTest();
RunAuthenticationAttemptLimiterTest();
await RunAccountAuthenticationTestAsync();
RunSessionTokenStoreTest();
RunPlayerSessionTest();
await RunPlayerEntityTestAsync();
RunWorldEventTest();
RunMovementTickProcessorTest();
RunMovementRequestQueueTest();
RunWorldTickProcessorTest();
await RunWorldTickLoopTestAsync();
await RunWorldEventQueueTestAsync();
RunWorldRulesTest();
RunWorldGridTest();
await RunWorldGridIndexTestAsync();
MonsterTests.RunMonsterRegistryTest();
MonsterTests.RunMonsterAiTickTest();
await MonsterTests.RunPlayerCombatTickTestAsync();
await MonsterTests.RunMonsterCommandsTestAsync();
await MonsterTests.RunCharacterPersistenceTestAsync();
RunServerPortParseTest();
RunLocalClientOptionParseTest();
RunRemoteClientOptionParseTest();
RunInvalidClientNicknameOptionParseTest();
await RunClientRegistryTracksCountAndNamesAsync();
await RunClientRegistryFindsNamesCaseInsensitiveAsync();
RunClientRegistryIncludesDefaultRoom();
await RunClientRegistryFiltersRoomsAsync();
await RunClientRegistrySnapshotsRoomsCaseInsensitiveAsync();
await RunClientRegistryFindsSpawnedPlayersByMapAsync();
await RunClientRegistryFindsNearbyNamesAsync();
await RunClientRegistryFindsNearbySnapshotsAsync();
await RunClientRegistryLimitsNearbySnapshotsAsync();
await RunClientRegistryDrainsConnectionsAsync();
await RunClientTaskTrackerTestAsync();
await RunHelpCommandTestAsync();
await RunCommandsAliasTestAsync();
await RunWhereCommandTestAsync();
await RunPingCommandTestAsync();
await RunEchoCommandTestAsync();
await RunEmptyEchoCommandTestAsync();
await RunMissingEchoMessageCommandTestAsync();
await RunTimeCommandTestAsync();
await RunUptimeCommandTestAsync();
await RunWhoAmICommandTestAsync();
await RunSessionCommandTestAsync();
await RunRegisterCommandTestAsync();
await RunLoginCommandTestAsync();
await RunResumeAndRevokeSessionCommandTestAsync();
await RunActiveSessionExpiryCommandTestAsync();
await RunAuthenticationBackoffCommandTestAsync();
await RunDrainingRejectsGameCommandsTestAsync();
await RunDuplicateLoginCommandTestAsync();
await RunLoginWhileSpawnedCommandTestAsync();
await RunAuthenticatedSessionCommandTestAsync();
await RunInvalidLoginCommandTestAsync();
await RunMissingLoginCommandTestAsync();
await RunLogoutCommandTestAsync();
await RunLogoutWhileSpawnedCommandTestAsync();
await RunLogoutWhenAnonymousCommandTestAsync();
await RunPositionCommandTestAsync();
await RunMapCommandTestAsync();
await RunMapUsersCommandTestAsync();
await RunWarpCommandTestAsync();
await RunWarpRequiresAuthenticationCommandTestAsync();
await RunWarpWhenNotSpawnedCommandTestAsync();
await RunInvalidWarpCommandTestAsync();
await RunInvalidWarpMapCommandTestAsync();
await RunOutOfBoundsWarpCommandTestAsync();
await RunMoveWhenNotSpawnedCommandTestAsync();
await RunMoveCommandTestAsync();
await RunRepeatedMoveSequenceCommandTestAsync();
await RunMoveCooldownCommandTestAsync();
await RunInvalidMoveCommandTestAsync();
await RunOutOfBoundsMoveCommandTestAsync();
await RunTooFarMoveCommandTestAsync();
await RunNearbyWhenNotSpawnedCommandTestAsync();
await RunNearbyCommandTestAsync();
await RunLookWhenNotSpawnedCommandTestAsync();
await RunLookCommandTestAsync();
await RunSpawnRequiresAuthenticationCommandTestAsync();
await RunSpawnCommandTestAsync();
await RunDuplicateSpawnCommandTestAsync();
await RunDespawnCommandTestAsync();
await RunDespawnWhenNotSpawnedCommandTestAsync();
await RunJoinCommandTestAsync();
await RunMissingJoinRoomCommandTestAsync();
await RunLeaveCommandTestAsync();
await RunInvalidRoomNameCommandTestAsync();
await RunRoomUsersCommandTestAsync();
await RunStatsCommandTestAsync();
await RunAdministratorAuthorizationTestAsync();
await RunMotdCommandTestAsync();
await RunVersionCommandTestAsync();
await RunMeCommandTestAsync();
await RunEmptyMeCommandTestAsync();
await RunMissingMeActionCommandTestAsync();
await RunWhisperCommandTestAsync();
await RunWhisperUnknownUserCommandTestAsync();
await RunInvalidWhisperCommandTestAsync();
await RunMissingWhisperPayloadCommandTestAsync();
await RunRenameCommandTestAsync();
await RunMissingNameCommandTestAsync();
await RunMissingRenameCommandTestAsync();
await RunDuplicateNameCommandTestAsync();
await RunInvalidNameCommandTestAsync();

Console.WriteLine("All socket study tests passed.");

static async Task RunTlsProtocolRoundTripTestAsync()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"socket-study-tls-{Guid.NewGuid():N}");
    string pfxPath = Path.Combine(directory, "server.pfx");
    string certificatePath = Path.Combine(directory, "server.cer");
    string otherPfxPath = Path.Combine(directory, "other.pfx");
    string otherCertificatePath = Path.Combine(directory, "other.cer");
    var listener = new TcpListener(IPAddress.Loopback, 0);

    try
    {
        TlsCertificateManager.CreateDevelopmentCertificate(pfxPath, certificatePath);
        TlsCertificateManager.CreateDevelopmentCertificate(
            otherPfxPath,
            otherCertificatePath);
        using var serverCertificate = new X509Certificate2(
            pfxPath,
            TlsCertificateManager.DevelopmentPassword,
            X509KeyStorageFlags.DefaultKeySet);
        using var pinnedCertificate = new X509Certificate2(certificatePath);
        using var otherCertificate = new X509Certificate2(otherCertificatePath);
        if (TlsCertificateValidator.ValidatePinnedServer(
            serverCertificate,
            SslPolicyErrors.None,
            otherCertificate))
        {
            throw new InvalidOperationException("TLS pin validation should reject a different certificate.");
        }

        using var overlapPins = new TlsPinnedCertificateSet(
            [certificatePath, otherCertificatePath]);
        if (!overlapPins.Validate(serverCertificate, SslPolicyErrors.None) ||
            !overlapPins.Validate(otherCertificate, SslPolicyErrors.None))
        {
            throw new InvalidOperationException("TLS overlap pin set should trust current and next certificates.");
        }

        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            await using var serverTls = new SslStream(accepted.GetStream(), false);
            await serverTls.AuthenticateAsServerAsync(
                serverCertificate,
                clientCertificateRequired: false,
                SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);
            NetworkMessage? request = await MessageProtocol.ReadMessageAsync(serverTls);
            if (request?.Text != "encrypted hello")
            {
                throw new InvalidOperationException("TLS server did not receive the protocol message.");
            }

            await MessageProtocol.WriteMessageAsync(
                serverTls,
                MessageType.Notice,
                "encrypted response");
        });

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        await using var clientTls = new SslStream(
            client.GetStream(),
            false,
            (_, certificate, _, errors) =>
                TlsCertificateValidator.ValidatePinnedServer(
                    certificate,
                    errors,
                    pinnedCertificate));
        await clientTls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = "localhost",
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        });

        await MessageProtocol.WriteMessageAsync(
            clientTls,
            MessageType.Command,
            "encrypted hello");
        NetworkMessage? response = await MessageProtocol.ReadMessageAsync(clientTls);
        if (response?.Type != MessageType.Notice ||
            response.Text != "encrypted response" ||
            !clientTls.IsEncrypted ||
            !clientTls.IsAuthenticated)
        {
            throw new InvalidOperationException("TLS client should authenticate and exchange encrypted protocol messages.");
        }

        await serverTask;
    }
    finally
    {
        listener.Stop();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static void RunTlsCertificateRotationTest()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"socket-study-tls-rotation-{Guid.NewGuid():N}");
    string pfxPath = Path.Combine(directory, "server.pfx");
    string certificatePath = Path.Combine(directory, "server.cer");
    string replacementPfxPath = Path.Combine(directory, "replacement.pfx");
    string replacementCertificatePath = Path.Combine(directory, "replacement.cer");
    var errors = new List<string>();

    try
    {
        TlsCertificateManager.CreateDevelopmentCertificate(pfxPath, certificatePath);
        using var provider = new TlsServerCertificateProvider(
            pfxPath,
            TlsCertificateManager.DevelopmentPassword,
            TimeSpan.FromDays(400),
            logError: errors.Add);
        string originalThumbprint = provider.Thumbprint;
        if (errors.Count == 0)
        {
            throw new InvalidOperationException("Certificate provider should warn before configured expiry.");
        }

        int warningCount = errors.Count;
        provider.RefreshIfChanged();
        if (errors.Count != warningCount)
        {
            throw new InvalidOperationException("Certificate expiry warning should be throttled.");
        }

        TlsCertificateManager.CreateDevelopmentCertificate(
            replacementPfxPath,
            replacementCertificatePath);
        File.Copy(replacementPfxPath, pfxPath, overwrite: true);
        File.SetLastWriteTimeUtc(pfxPath, DateTime.UtcNow.AddSeconds(2));
        if (!provider.RefreshIfChanged() ||
            provider.Thumbprint == originalThumbprint)
        {
            throw new InvalidOperationException("Certificate provider should hot-reload a replacement PFX.");
        }

        string rotatedThumbprint = provider.Thumbprint;
        File.WriteAllText(pfxPath, "invalid pfx");
        File.SetLastWriteTimeUtc(pfxPath, DateTime.UtcNow.AddSeconds(4));
        if (provider.RefreshIfChanged() ||
            provider.Thumbprint != rotatedThumbprint)
        {
            throw new InvalidOperationException("Invalid certificate rotation should preserve the current certificate.");
        }
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

}

static void RunServerOptionsTest()
{
    var invalid = new ServerOptions(
        "Production", 70_000, "data.db", 1, 2,
        TimeSpan.Zero, TimeSpan.Zero, "missing.pfx", "secret-value", true,
        AppLogLevel.Warning, new HashSet<long>());
    string[] errors = invalid.Validate();
    if (errors.Length < 5)
    {
        throw new InvalidOperationException("Server options should collect all configuration errors.");
    }

    string summary = invalid.ToSafeSummary();
    if (summary.Contains("secret-value") || !summary.Contains("environment=Production"))
    {
        throw new InvalidOperationException("Configuration summary should include settings but redact secrets.");
    }
}

static void RunStructuredLoggerTest()
{
    string json = AppLogger.Serialize(
        AppLogLevel.Warning,
        "test.event",
        "test message",
        new Dictionary<string, object?> { ["playerId"] = 1001 },
        DateTimeOffset.UnixEpoch);
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement root = document.RootElement;
    if (root.GetProperty("level").GetString() != "Warning" ||
        root.GetProperty("event").GetString() != "test.event" ||
        root.GetProperty("properties").GetProperty("playerId").GetInt32() != 1001)
    {
        throw new InvalidOperationException("Structured logger should serialize searchable fields.");
    }
}

static void RunServerMetricsTest()
{
    var metrics = new ServerMetrics();
    metrics.ConnectionAccepted();
    metrics.MessageReceived();
    metrics.CommandProcessed(TimeSpan.FromMilliseconds(10));
    metrics.CommandProcessed(TimeSpan.FromMilliseconds(20));
    metrics.ConnectionRejected();
    ServerMetricsSnapshot snapshot = metrics.Snapshot();
    if (snapshot.ActiveConnections != 1 || snapshot.RejectedConnections != 1 ||
        snapshot.ProcessedCommands != 2 || snapshot.AverageCommandMilliseconds != 15)
        throw new InvalidOperationException("Server metrics should aggregate counters and latency.");
    metrics.ConnectionClosed();
    if (metrics.Snapshot().ActiveConnections != 0)
        throw new InvalidOperationException("Active connection gauge should return to zero.");
}

static void RunServerHealthTest()
{
    string directory = Path.Combine(Path.GetTempPath(), $"health-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var lifecycle = new ServerLifecycle();
        lifecycle.MarkRunning();
        var health = new ServerHealthService(() => lifecycle.State,
            () => DateTimeOffset.UnixEpoch.AddHours(1), Path.Combine(directory, "data.db"),
            () => DateTimeOffset.UnixEpoch);
        if (!health.Check().Ready) throw new InvalidOperationException("Running server dependencies should be ready.");
        lifecycle.BeginDraining();
        if (health.Check().Ready || !health.Check().Live)
            throw new InvalidOperationException("Draining server should stay live but stop being ready.");
    }
    finally { Directory.Delete(directory, true); }
}

static void RunSessionOwnershipTest()
{
    var ownership = new SessionOwnershipRegistry();
    Guid first = Guid.NewGuid();
    Guid second = Guid.NewGuid();
    if (!ownership.TryAcquire(1001, first) || ownership.TryAcquire(1001, second))
        throw new InvalidOperationException("Only one connection should own a player session.");
    ownership.Release(1001, second);
    if (!ownership.IsOwner(1001, first))
        throw new InvalidOperationException("Non-owner release must not remove ownership.");
    ownership.Release(1001, first);
    if (!ownership.TryAcquire(1001, second))
        throw new InvalidOperationException("Ownership should be reusable after release.");
}

static void RunServerLifecycleTest()
{
    var lifecycle = new ServerLifecycle();
    if (lifecycle.State != ServerLifecycleState.Starting ||
        !lifecycle.MarkRunning() ||
        lifecycle.State != ServerLifecycleState.Running ||
        lifecycle.MarkRunning() ||
        !lifecycle.BeginDraining() ||
        lifecycle.State != ServerLifecycleState.Draining ||
        lifecycle.BeginDraining() ||
        !lifecycle.MarkStopped() ||
        lifecycle.State != ServerLifecycleState.Stopped ||
        lifecycle.MarkRunning())
    {
        throw new InvalidOperationException("Server lifecycle should only allow forward state transitions.");
    }

    var startupFailureLifecycle = new ServerLifecycle();
    if (!startupFailureLifecycle.BeginDraining() ||
        !startupFailureLifecycle.MarkStopped() ||
        startupFailureLifecycle.State != ServerLifecycleState.Stopped)
    {
        throw new InvalidOperationException("Server lifecycle should support cleanup after a startup failure.");
    }
}

static void RunConnectionAdmissionControllerTest()
{
    var admission = new ConnectionAdmissionController(
        maxConnections: 2,
        maxConnectionsPerIp: 1);
    IPAddress firstAddress = IPAddress.Parse("127.0.0.1");
    IPAddress secondAddress = IPAddress.Parse("127.0.0.2");
    IPAddress thirdAddress = IPAddress.Parse("127.0.0.3");

    ConnectionAdmissionResult first = admission.TryAcquire(firstAddress);
    ConnectionAdmissionResult sameIp = admission.TryAcquire(firstAddress);
    ConnectionAdmissionResult second = admission.TryAcquire(secondAddress);
    ConnectionAdmissionResult serverFull = admission.TryAcquire(thirdAddress);

    if (!first.Accepted ||
        sameIp.Status != ConnectionAdmissionStatus.IpLimitReached ||
        !second.Accepted ||
        serverFull.Status != ConnectionAdmissionStatus.ServerFull ||
        admission.ActiveConnections != 2)
    {
        throw new InvalidOperationException("Admission control should enforce global and per-IP limits.");
    }

    first.Lease!.Dispose();
    first.Lease.Dispose();
    ConnectionAdmissionResult reacquired = admission.TryAcquire(firstAddress);
    if (!reacquired.Accepted || admission.ActiveConnections != 2)
    {
        throw new InvalidOperationException("Released admission slots should be reusable exactly once.");
    }

    second.Lease!.Dispose();
    reacquired.Lease!.Dispose();
    if (admission.ActiveConnections != 0)
    {
        throw new InvalidOperationException("Admission leases should release every active connection.");
    }

    var normalizedAdmission = new ConnectionAdmissionController(
        maxConnections: 2,
        maxConnectionsPerIp: 1);
    ConnectionAdmissionResult ipv4 = normalizedAdmission.TryAcquire(firstAddress);
    ConnectionAdmissionResult mappedIpv6 = normalizedAdmission.TryAcquire(
        firstAddress.MapToIPv6());
    if (!ipv4.Accepted ||
        mappedIpv6.Status != ConnectionAdmissionStatus.IpLimitReached)
    {
        throw new InvalidOperationException("IPv4 and IPv4-mapped IPv6 should share one IP limit.");
    }

    ipv4.Lease!.Dispose();
}

static void RunConnectionRateLimiterTest()
{
    DateTimeOffset currentTime = DateTimeOffset.UnixEpoch;
    var limiter = new ConnectionRateLimiter(
        capacity: 2,
        refillTokensPerSecond: 1,
        blockViolationThreshold: 3,
        blockDuration: TimeSpan.FromSeconds(30),
        idleRetention: TimeSpan.FromMinutes(1),
        getCurrentTime: () => currentTime);
    IPAddress address = IPAddress.Parse("10.0.0.1");

    if (!limiter.Check(address).Allowed ||
        !limiter.Check(address).Allowed ||
        limiter.Check(address).Status != ConnectionRateLimitStatus.RateLimited ||
        limiter.Check(address).Status != ConnectionRateLimitStatus.RateLimited)
    {
        throw new InvalidOperationException("Token bucket should allow its burst capacity before limiting.");
    }

    ConnectionRateLimitResult blocked = limiter.Check(address);
    if (blocked.Status != ConnectionRateLimitStatus.TemporarilyBlocked ||
        blocked.RetryAfter != TimeSpan.FromSeconds(30) ||
        limiter.Check(address.MapToIPv6()).Status != ConnectionRateLimitStatus.TemporarilyBlocked)
    {
        throw new InvalidOperationException("Repeated rate-limit violations should temporarily block the IP.");
    }

    currentTime += TimeSpan.FromSeconds(30);
    if (!limiter.Check(address).Allowed)
    {
        throw new InvalidOperationException("A blocked IP should recover after its block duration.");
    }

    currentTime += TimeSpan.FromSeconds(1);
    if (!limiter.Check(address).Allowed)
    {
        throw new InvalidOperationException("Token bucket should refill according to elapsed time.");
    }

    currentTime += TimeSpan.FromMinutes(1);
    limiter.Check(IPAddress.Parse("10.0.0.2"));
    if (limiter.TrackedAddressCount != 1)
    {
        throw new InvalidOperationException("Idle rate-limit buckets should be removed.");
    }
}

static void RunAuthenticationAttemptLimiterTest()
{
    DateTimeOffset currentTime = DateTimeOffset.UnixEpoch;
    var limiter = new AuthenticationAttemptLimiter(
        baseDelay: TimeSpan.FromSeconds(1),
        maxDelay: TimeSpan.FromSeconds(8),
        idleRetention: TimeSpan.FromMinutes(1),
        getCurrentTime: () => currentTime);
    IPAddress firstAddress = IPAddress.Parse("10.0.0.1");
    IPAddress secondAddress = IPAddress.Parse("10.0.0.2");

    if (limiter.RecordFailure(firstAddress, "1001") != TimeSpan.FromSeconds(1) ||
        limiter.Check(firstAddress, "2002").RetryAfter != TimeSpan.FromSeconds(1) ||
        limiter.Check(secondAddress, "1001").RetryAfter != TimeSpan.FromSeconds(1))
    {
        throw new InvalidOperationException("Authentication backoff should apply by both IP and account.");
    }

    currentTime += TimeSpan.FromSeconds(1);
    if (limiter.RecordFailure(firstAddress, "1001") != TimeSpan.FromSeconds(2))
    {
        throw new InvalidOperationException("Authentication failures should increase backoff exponentially.");
    }

    currentTime += TimeSpan.FromSeconds(2);
    limiter.RecordFailure(firstAddress, "1001");
    currentTime += TimeSpan.FromSeconds(4);
    if (!limiter.Check(secondAddress, "1001").Allowed)
    {
        throw new InvalidOperationException("Authentication attempt should resume after its backoff.");
    }

    limiter.RecordFailure(secondAddress, "1001");
    IPAddress thirdAddress = IPAddress.Parse("10.0.0.3");
    if (limiter.Check(thirdAddress, "1001").Allowed)
    {
        throw new InvalidOperationException("A fresh account failure should immediately apply backoff.");
    }

    limiter.RecordSuccess("1001");
    if (!limiter.Check(thirdAddress, "1001").Allowed)
    {
        throw new InvalidOperationException("Successful authentication should clear account failures.");
    }
}

static async Task RunAccountAuthenticationTestAsync()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"socket-study-accounts-{Guid.NewGuid():N}");
    string databasePath = Path.Combine(directory, "accounts.db");
    try
    {
        var repository = new SqliteAccountRepository(databasePath);
        var hasher = new PasswordHasher();
        AccountCredential created = hasher.Hash(1001, "correct-password");

        if (!await repository.CreateAsync(created) ||
            await repository.CreateAsync(created))
        {
            throw new InvalidOperationException("Account repository should reject duplicate player ids.");
        }

        AccountCredential? loaded = await repository.FindAsync(1001);
        if (loaded is null ||
            !hasher.Verify("correct-password", loaded) ||
            hasher.Verify("wrong-password", loaded))
        {
            throw new InvalidOperationException("Stored password hash should only verify the correct password.");
        }

        string firstToken = SessionTokenGenerator.Create();
        string secondToken = SessionTokenGenerator.Create();
        if (firstToken == secondToken || firstToken.Length < 40)
        {
            throw new InvalidOperationException("Session tokens should be long and unpredictable.");
        }
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static void RunSessionTokenStoreTest()
{
    DateTimeOffset currentTime = DateTimeOffset.UnixEpoch;
    var store = new SessionTokenStore(
        TimeSpan.FromMinutes(30),
        () => currentTime);

    string firstToken = store.Issue(1001);
    if (store.Validate(firstToken).PlayerId != 1001)
    {
        throw new InvalidOperationException("Issued session token should validate for its player.");
    }

    string replacementToken = store.Issue(1001);
    if (store.Validate(firstToken).IsValid ||
        !store.Validate(replacementToken).IsValid)
    {
        throw new InvalidOperationException("New login should replace the player's previous token.");
    }

    if (!store.Revoke(replacementToken) ||
        store.Validate(replacementToken).IsValid)
    {
        throw new InvalidOperationException("Revoked session token should become invalid immediately.");
    }

    string expiringToken = store.Issue(2002);
    currentTime += TimeSpan.FromMinutes(30);
    if (store.Validate(expiringToken).IsValid)
    {
        throw new InvalidOperationException("Session token should expire at its configured lifetime.");
    }
}

static async Task RunDrainingRejectsGameCommandsTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Lifecycle.BeginDraining();

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 1001"));
    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 1 1"));

    const string unavailable = "Server shutdown is in progress. This command is unavailable.";
    if (context.Connection.Session.IsAuthenticated ||
        context.SentMessages.Count(message => message.Text == unavailable) != 2)
    {
        throw new InvalidOperationException("Draining server should reject login and game state commands.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/quit"));
    if (!context.SentMessages.Any(message => message.Text == "Goodbye."))
    {
        throw new InvalidOperationException("Draining server should still allow clients to quit.");
    }
}

static async Task RunClientTaskTrackerTestAsync()
{
    var tracker = new ClientTaskTracker();
    var firstCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    tracker.Track(firstCompletion.Task);
    tracker.Track(secondCompletion.Task);

    if (tracker.Count != 2)
    {
        throw new InvalidOperationException("Client task tracker should count active tasks.");
    }

    Task<ClientTaskWaitResult> waitTask = tracker.WaitForAllAsync(TimeSpan.FromSeconds(1));
    firstCompletion.SetResult();
    await Task.Yield();
    if (waitTask.IsCompleted)
    {
        throw new InvalidOperationException("Shutdown should wait until every client task completes.");
    }

    secondCompletion.SetResult();
    ClientTaskWaitResult completed = await waitTask;

    if (!completed.Completed ||
        completed.RemainingTaskCount != 0 ||
        tracker.Count != 0)
    {
        throw new InvalidOperationException("Completed client tasks should be removed from the tracker.");
    }

    var timeoutTracker = new ClientTaskTracker();
    var pendingCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    timeoutTracker.Track(pendingCompletion.Task);

    ClientTaskWaitResult timedOut = await timeoutTracker.WaitForAllAsync(
        TimeSpan.FromMilliseconds(20));
    if (timedOut.Completed ||
        timedOut.RemainingTaskCount != 1 ||
        timedOut.Elapsed < TimeSpan.FromMilliseconds(10))
    {
        throw new InvalidOperationException("Client task tracker should report tasks left after the shutdown timeout.");
    }

    pendingCompletion.SetResult();
    await timeoutTracker.WaitForAllAsync(TimeSpan.FromSeconds(1));
}

static async Task RunProtocolRoundTripTestAsync(MessageType type, string text)
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    int port = ((IPEndPoint)listener.LocalEndpoint).Port;

    using var client = new TcpClient();
    Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

    await client.ConnectAsync(IPAddress.Loopback, port);
    using TcpClient server = await acceptTask;

    await using NetworkStream clientStream = client.GetStream();
    await using NetworkStream serverStream = server.GetStream();

    await MessageProtocol.WriteMessageAsync(clientStream, type, text);
    NetworkMessage? received = await MessageProtocol.ReadMessageAsync(serverStream);

    listener.Stop();

    if (received is null)
    {
        throw new InvalidOperationException("Expected a message, but received null.");
    }

    if (received.Type != type)
    {
        throw new InvalidOperationException($"Expected type {type}, but received {received.Type}.");
    }

    if (received.Text != text)
    {
        throw new InvalidOperationException($"Expected text '{text}', but received '{received.Text}'.");
    }
}

static async Task RunInvalidMessageTypeTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] invalidHeader = [255, 0, 0, 0, 0];
    await pair.ClientStream.WriteAsync(invalidHeader);
    await pair.ClientStream.FlushAsync();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected invalid message type to throw IOException.");
}

static async Task RunIncompleteBodyTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] header = [1, 0, 0, 0, 5];
    byte[] partialBody = [65, 66];
    await pair.ClientStream.WriteAsync(header);
    await pair.ClientStream.WriteAsync(partialBody);
    pair.Client.Close();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected incomplete body to throw IOException.");
}

static async Task RunTooLargeLengthTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();

    byte[] tooLargeHeader = [1, 0, 16, 0, 1];
    await pair.ClientStream.WriteAsync(tooLargeHeader);
    await pair.ClientStream.FlushAsync();

    await AssertThrowsAsync<IOException>(
        () => MessageProtocol.ReadMessageAsync(pair.ServerStream),
        "Expected oversized message length to throw IOException.");
}

static void RunMessageSizeLimitTest()
{
    string allowedMessage = new('a', MessageProtocol.MaxMessageBytes);
    string tooLargeMessage = new('a', MessageProtocol.MaxMessageBytes + 1);

    if (!MessageProtocol.IsWithinMessageSizeLimit(allowedMessage))
    {
        throw new InvalidOperationException("Expected a message at the size limit to be allowed.");
    }

    if (MessageProtocol.IsWithinMessageSizeLimit(tooLargeMessage))
    {
        throw new InvalidOperationException("Expected a message over the size limit to be rejected.");
    }
}

static void RunNameRulesTest()
{
    if (NameRules.MaxNameLength != 20)
    {
        throw new InvalidOperationException("NameRules should keep the expected max name length.");
    }

    if (!NameRules.HasOnlyAllowedCharacters("alice_123-test"))
    {
        throw new InvalidOperationException("NameRules should allow letters, numbers, '-' and '_'.");
    }

    if (NameRules.HasOnlyAllowedCharacters("bad name"))
    {
        throw new InvalidOperationException("NameRules should reject spaces.");
    }

    if (NameRules.HasOnlyAllowedCharacters("bad!name"))
    {
        throw new InvalidOperationException("NameRules should reject unsupported punctuation.");
    }
}

static void RunServerInfoTest()
{
    if (ServerInfo.Name != "SocketStudy")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected server name.");
    }

    if (ServerInfo.Version != "v1")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected server version.");
    }

    if (ServerInfo.VersionMessage != "SocketStudy server v1")
    {
        throw new InvalidOperationException("ServerInfo should build the expected version message.");
    }

    if (ServerInfo.MessageOfTheDay != "Welcome to SocketStudy. Type /help to see commands.")
    {
        throw new InvalidOperationException("ServerInfo should keep the expected MOTD message.");
    }
}

static void RunPlayerSessionTest()
{
    var session = new PlayerSession();

    if (session.IsAuthenticated || session.PlayerId != PlayerSession.AnonymousPlayerId)
    {
        throw new InvalidOperationException("New player sessions should start anonymous.");
    }

    if (session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("New player sessions should start at the world origin.");
    }

    if (session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("New player sessions should start in the default map.");
    }

    if (session.IsSpawned)
    {
        throw new InvalidOperationException("New player sessions should not start spawned.");
    }

    session.Authenticate(1001);

    if (!session.IsAuthenticated ||
        session.PlayerId != 1001 ||
        string.IsNullOrWhiteSpace(session.SessionToken))
    {
        throw new InvalidOperationException("Player sessions should store authenticated player ids.");
    }

    try
    {
        session.Authenticate(2002);
        throw new InvalidOperationException("Player sessions should reject repeated authentication.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Player session is already authenticated.")
    {
    }

    if (session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Repeated authentication should not replace the player id.");
    }

    DateTimeOffset lastMoveAt = DateTimeOffset.UnixEpoch;
    session.MoveTo(new WorldPosition(10, 20), lastMoveAt, sequence: 1);

    if (session.Position != new WorldPosition(10, 20) ||
        session.LastMoveAt != lastMoveAt ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("Player sessions should store approved movement state.");
    }

    try
    {
        session.MoveTo(new WorldPosition(11, 20), lastMoveAt.AddSeconds(1), sequence: 1);
        throw new InvalidOperationException("Player sessions should reject repeated move sequences.");
    }
    catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "sequence")
    {
    }

    try
    {
        session.ChangeMap(0);
        throw new InvalidOperationException("Player sessions should reject invalid map ids.");
    }
    catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "mapId")
    {
    }

    if (session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("Invalid map changes should preserve the current map.");
    }

    session.ChangeMap(2);

    if (session.Position != new WorldPosition(10, 20) ||
        session.MapId != 2 ||
        session.LastMoveAt is not null ||
        session.LastMoveSequence != 0)
    {
        throw new InvalidOperationException("Player sessions should reset old map movement tracking.");
    }

    session.Spawn();

    if (!session.IsSpawned)
    {
        throw new InvalidOperationException("Player sessions should store spawn state.");
    }

    try
    {
        session.ChangeMap(WorldRules.DefaultMapId);
        throw new InvalidOperationException("Player sessions should reject map changes while spawned.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Spawned player session cannot change maps.")
    {
    }

    try
    {
        session.Logout();
        throw new InvalidOperationException("Player sessions should reject logout while spawned.");
    }
    catch (InvalidOperationException exception) when (exception.Message == "Spawned player session cannot logout.")
    {
    }

    session.Despawn();

    if (session.IsSpawned)
    {
        throw new InvalidOperationException("Player sessions should store despawn state.");
    }

    session.Logout();

    if (session.IsAuthenticated || session.PlayerId != PlayerSession.AnonymousPlayerId ||
        session.SessionToken is not null ||
        session.Position != WorldPosition.Origin || session.MapId != WorldRules.DefaultMapId)
    {
        throw new InvalidOperationException("Player sessions should reset authentication, position, and map on logout.");
    }
}

static async Task RunPlayerEntityTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();
    var connection = new ClientConnection("alice", pair.Client, pair.ClientStream);

    connection.Session.Authenticate(1001);
    connection.Session.MoveTo(new WorldPosition(10, 20));
    connection.Session.ChangeMap(2);
    connection.Session.Spawn();

    PlayerEntity entity = PlayerEntity.FromConnection(connection);

    if (entity.EntityId != 1001 ||
        entity.PlayerId != 1001 ||
        entity.Name != "alice" ||
        entity.MapId != 2 ||
        entity.Position != new WorldPosition(10, 20) ||
        !entity.IsSpawned)
    {
        throw new InvalidOperationException("PlayerEntity should copy the expected world-facing player state.");
    }
}

static void RunWorldEventTest()
{
    var position = new WorldPosition(10, 20);

    if (WorldEvent.PlayerSpawned("alice", 1, position).ToNoticeMessage() != "alice spawned at x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player spawn notices.");
    }

    if (WorldEvent.PlayerMoved("alice", 1, position).ToNoticeMessage() != "alice moved to x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player move notices.");
    }

    if (WorldEvent.PlayerDespawned("alice", 1, position).ToNoticeMessage() != "alice despawned from x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player despawn notices.");
    }

    if (WorldEvent.PlayerLeftMap("alice", 2, position).ToNoticeMessage() != "alice left map 2 from x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player map leave notices.");
    }

    if (WorldEvent.PlayerEnteredMap("alice", 2, position).ToNoticeMessage() != "alice entered map 2 at x=10, y=20")
    {
        throw new InvalidOperationException("WorldEvent should format player map enter notices.");
    }
}

static void RunMovementTickProcessorTest()
{
    var session = new PlayerSession();
    DateTimeOffset firstTick = DateTimeOffset.UnixEpoch;

    MovementTickResult accepted = MovementTickProcessor.Process(
        session,
        new MovementRequest(1, new WorldPosition(4, 6), firstTick));

    if (!accepted.IsAccepted ||
        session.Position != new WorldPosition(4, 6) ||
        session.LastMoveAt != firstTick ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("MovementTickProcessor should apply accepted movement requests.");
    }

    MovementTickResult repeated = MovementTickProcessor.Process(
        session,
        new MovementRequest(1, new WorldPosition(5, 6), firstTick.AddSeconds(1)));

    if (repeated.IsAccepted ||
        repeated.RejectionReason != "Move sequence must be greater than 1." ||
        session.Position != new WorldPosition(4, 6))
    {
        throw new InvalidOperationException("MovementTickProcessor should reject repeated movement sequences without changing state.");
    }

    MovementTickResult cooldown = MovementTickProcessor.Process(
        session,
        new MovementRequest(2, new WorldPosition(5, 6), firstTick.AddMilliseconds(500)));

    if (cooldown.IsAccepted ||
        cooldown.RejectionReason != "You must wait 1 second between moves." ||
        session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("MovementTickProcessor should reject cooldown movement without consuming sequence.");
    }
}

static void RunMovementRequestQueueTest()
{
    var queue = new MovementRequestQueue();
    var firstSession = new PlayerSession();
    var secondSession = new PlayerSession();
    var first = new QueuedMovementRequest(
        firstSession,
        new MovementRequest(1, new WorldPosition(1, 0), DateTimeOffset.UnixEpoch));
    var second = new QueuedMovementRequest(
        secondSession,
        new MovementRequest(2, new WorldPosition(2, 0), DateTimeOffset.UnixEpoch));

    queue.Enqueue(first);
    queue.Enqueue(second);

    if (queue.Count != 2 || !queue.TryDequeue(out QueuedMovementRequest? dequeuedFirst) || dequeuedFirst != first)
    {
        throw new InvalidOperationException("MovementRequestQueue should dequeue the oldest request first.");
    }

    if (!queue.TryDequeue(out QueuedMovementRequest? dequeuedSecond) || dequeuedSecond != second)
    {
        throw new InvalidOperationException("MovementRequestQueue should preserve FIFO order.");
    }

    if (queue.Count != 0 || queue.TryDequeue(out _))
    {
        throw new InvalidOperationException("MovementRequestQueue should report an empty queue after draining.");
    }
}

static void RunWorldTickProcessorTest()
{
    var queue = new MovementRequestQueue();
    var processor = new WorldTickProcessor(queue);
    var firstSession = new PlayerSession();
    var secondSession = new PlayerSession();
    DateTimeOffset tickTime = DateTimeOffset.UnixEpoch.AddSeconds(1);

    firstSession.Spawn();
    secondSession.Spawn();
    queue.Enqueue(new QueuedMovementRequest(
        firstSession,
        new MovementRequest(1, new WorldPosition(1, 0), tickTime)));
    queue.Enqueue(new QueuedMovementRequest(
        secondSession,
        new MovementRequest(1, new WorldPosition(2, 0), tickTime)));

    WorldTickResult result = processor.ProcessOnce();

    if (result.Movements.Count != 2 || queue.Count != 0)
    {
        throw new InvalidOperationException("WorldTickProcessor should drain all queued movement requests in one tick.");
    }

    if (!result.Movements.All(movement => movement.Result.IsAccepted) ||
        firstSession.Position != new WorldPosition(1, 0) ||
        secondSession.Position != new WorldPosition(2, 0))
    {
        throw new InvalidOperationException("WorldTickProcessor should apply each accepted movement request.");
    }

    if (processor.ProcessOnce().Movements.Count != 0)
    {
        throw new InvalidOperationException("WorldTickProcessor should return an empty result when no input is queued.");
    }
}

static async Task RunWorldTickLoopTestAsync()
{
    var queue = new MovementRequestQueue();
    var processor = new WorldTickProcessor(queue);
    var simulationTick = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var loop = new WorldTickLoop(
        processor,
        TimeSpan.FromMilliseconds(5),
        _ => simulationTick.TrySetResult(true));
    using var cancellation = new CancellationTokenSource();
    Task loopTask = loop.RunAsync(cancellation.Token);
    var session = new PlayerSession();
    session.Spawn();
    var queuedRequest = new QueuedMovementRequest(
        session,
        new MovementRequest(1, new WorldPosition(1, 0), DateTimeOffset.UnixEpoch));

    queue.Enqueue(queuedRequest);
    MovementTickResult result = await queuedRequest.Completion.WaitAsync(TimeSpan.FromSeconds(1));
    await simulationTick.Task.WaitAsync(TimeSpan.FromSeconds(1));

    if (!result.IsAccepted ||
        session.Position != new WorldPosition(1, 0))
    {
        throw new InvalidOperationException("WorldTickLoop should process queued movement on a timer tick.");
    }

    cancellation.Cancel();
    await loopTask;
}

static async Task RunWorldEventQueueTestAsync()
{
    await using NetworkPair pair = await NetworkPair.ConnectAsync();
    var source = new ClientConnection("alice", pair.Client, pair.ClientStream);
    var queue = new WorldEventQueue();
    var first = new QueuedWorldEvent(source, WorldEvent.PlayerSpawned("alice", 1, WorldPosition.Origin));
    var second = new QueuedWorldEvent(source, WorldEvent.PlayerMoved("alice", 1, new WorldPosition(1, 0)));

    queue.Enqueue(first);
    queue.Enqueue(second);

    if (queue.Count != 2 || !queue.TryDequeue(out QueuedWorldEvent? dequeuedFirst) || dequeuedFirst != first)
    {
        throw new InvalidOperationException("WorldEventQueue should dequeue the oldest event first.");
    }

    if (!queue.TryDequeue(out QueuedWorldEvent? dequeuedSecond) || dequeuedSecond != second)
    {
        throw new InvalidOperationException("WorldEventQueue should preserve FIFO order.");
    }

    if (queue.Count != 0 || queue.TryDequeue(out _))
    {
        throw new InvalidOperationException("WorldEventQueue should report an empty queue after draining.");
    }
}

static void RunWorldRulesTest()
{
    if (!WorldRules.IsInsideWorld(WorldPosition.Origin))
    {
        throw new InvalidOperationException("WorldRules should allow the origin.");
    }

    if (!WorldRules.IsInsideWorld(new WorldPosition(WorldRules.MinCoordinate, WorldRules.MaxCoordinate)))
    {
        throw new InvalidOperationException("WorldRules should allow positions on the boundary.");
    }

    if (WorldRules.IsInsideWorld(new WorldPosition(WorldRules.MaxCoordinate + 1, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject positions outside the boundary.");
    }

    if (!WorldRules.IsNearby(WorldPosition.Origin, new WorldPosition(10, 10)))
    {
        throw new InvalidOperationException("WorldRules should treat close positions as nearby.");
    }

    if (WorldRules.GetDistance(WorldPosition.Origin, new WorldPosition(10, -5)) != 15)
    {
        throw new InvalidOperationException("WorldRules should calculate Manhattan distance.");
    }

    if (WorldRules.IsNearby(WorldPosition.Origin, new WorldPosition(30, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject positions outside view distance.");
    }

    if (WorldRules.MaxNearbySnapshotCount != 10)
    {
        throw new InvalidOperationException("WorldRules should keep the expected nearby snapshot limit.");
    }

    if (!WorldRules.IsWithinMoveDistance(WorldPosition.Origin, new WorldPosition(4, 6)))
    {
        throw new InvalidOperationException("WorldRules should allow movement at the maximum move distance.");
    }

    if (WorldRules.IsWithinMoveDistance(WorldPosition.Origin, new WorldPosition(11, 0)))
    {
        throw new InvalidOperationException("WorldRules should reject movement beyond the maximum move distance.");
    }

    DateTimeOffset movedAt = DateTimeOffset.UnixEpoch;
    if (WorldRules.IsMoveCooldownElapsed(movedAt, movedAt.AddMilliseconds(999)))
    {
        throw new InvalidOperationException("WorldRules should reject movement before the cooldown elapses.");
    }

    if (!WorldRules.IsMoveCooldownElapsed(movedAt, movedAt.AddSeconds(1)))
    {
        throw new InvalidOperationException("WorldRules should allow movement when the cooldown elapses.");
    }
}

static void RunWorldGridTest()
{
    WorldGridCell originCell = WorldGrid.GetCell(WorldRules.DefaultMapId, WorldPosition.Origin);

    if (originCell != new WorldGridCell(WorldRules.DefaultMapId, 0, 0))
    {
        throw new InvalidOperationException("WorldGrid should place the origin in the first cell.");
    }

    if (WorldGrid.GetCell(WorldRules.DefaultMapId, new WorldPosition(WorldRules.GridCellSize, 0)) !=
        new WorldGridCell(WorldRules.DefaultMapId, 1, 0))
    {
        throw new InvalidOperationException("WorldGrid should move to the next cell at the cell boundary.");
    }

    if (WorldGrid.GetCell(WorldRules.DefaultMapId, new WorldPosition(-1, -1)) !=
        new WorldGridCell(WorldRules.DefaultMapId, -1, -1))
    {
        throw new InvalidOperationException("WorldGrid should floor negative coordinates into negative cells.");
    }

    WorldGridCell[] neighborCells = WorldGrid.GetNeighborCells(originCell);

    if (neighborCells.Length != 9 ||
        !neighborCells.Contains(originCell) ||
        !neighborCells.Contains(new WorldGridCell(WorldRules.DefaultMapId, -1, -1)) ||
        !neighborCells.Contains(new WorldGridCell(WorldRules.DefaultMapId, 1, 1)) ||
        neighborCells.Contains(new WorldGridCell(2, 0, 0)))
    {
        throw new InvalidOperationException("WorldGrid should return the 3x3 neighbor cells on the same map.");
    }
}

static async Task RunWorldGridIndexTestAsync()
{
    await using NetworkPair firstPair = await NetworkPair.ConnectAsync();
    await using NetworkPair secondPair = await NetworkPair.ConnectAsync();
    var first = new ClientConnection("alice", firstPair.Client, firstPair.ClientStream);
    var second = new ClientConnection("bob", secondPair.Client, secondPair.ClientStream);
    var index = new WorldGridIndex();

    first.Session.Spawn();
    second.Session.MoveTo(new WorldPosition(WorldRules.GridCellSize, 0));
    second.Session.Spawn();
    index.Refresh(first);
    index.Refresh(second);

    ClientConnection[] candidates = index.SnapshotCandidates(
        WorldGrid.GetCell(first.Session.MapId, first.Session.Position));
    if (index.Count != 2 || !candidates.Contains(first) || !candidates.Contains(second))
    {
        throw new InvalidOperationException("WorldGridIndex should return players from the center and neighboring cells.");
    }

    second.Session.Despawn();
    index.Refresh(second);
    if (index.Count != 1 || index.SnapshotCandidates(WorldGrid.GetCell(1, WorldPosition.Origin)).Contains(second))
    {
        throw new InvalidOperationException("WorldGridIndex should remove despawned players during refresh.");
    }

    index.Clear();
    if (index.Count != 0)
    {
        throw new InvalidOperationException("WorldGridIndex should clear all indexed players.");
    }
}

static void RunServerPortParseTest()
{
    bool parsed = CommandLineOptions.TryReadServerPort(["server", "6500"], out int port);

    if (!parsed || port != 6500)
    {
        throw new InvalidOperationException($"Expected server port 6500, but received {port}.");
    }
}

static void RunLocalClientOptionParseTest()
{
    bool parsed = CommandLineOptions.TryReadClientOptions(
        ["client", "6500", "alice"],
        out string host,
        out int port,
        out string? nickname);

    if (!parsed || host != "127.0.0.1" || port != 6500 || nickname != "alice")
    {
        throw new InvalidOperationException("Local client options were not parsed correctly.");
    }
}

static void RunRemoteClientOptionParseTest()
{
    bool parsed = CommandLineOptions.TryReadClientOptions(
        ["client", "192.168.0.10", "6500", "bob"],
        out string host,
        out int port,
        out string? nickname);

    if (!parsed || host != "192.168.0.10" || port != 6500 || nickname != "bob")
    {
        throw new InvalidOperationException("Remote client options were not parsed correctly.");
    }
}

static void RunInvalidClientNicknameOptionParseTest()
{
    TextWriter originalOutput = Console.Out;
    using var capturedOutput = new StringWriter();

    try
    {
        Console.SetOut(capturedOutput);

        bool parsed = CommandLineOptions.TryReadClientOptions(
            ["client", "6500", "bad name"],
            out _,
            out _,
            out _);

        if (parsed)
        {
            throw new InvalidOperationException("Invalid client nickname option should not be parsed successfully.");
        }
    }
    finally
    {
        Console.SetOut(originalOutput);
    }

    if (!capturedOutput.ToString().Contains(NameRules.NicknameCharacterRuleMessage))
    {
        throw new InvalidOperationException("Invalid client nickname option did not print the expected message.");
    }
}

static async Task RunClientRegistryTracksCountAndNamesAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    int firstCount = registry.Add(bob);
    int secondCount = registry.Add(alice);

    if (firstCount != 1 || secondCount != 2 || registry.Count != 2)
    {
        throw new InvalidOperationException("ClientRegistry did not track add counts correctly.");
    }

    if (!registry.GetNames().SequenceEqual(["alice", "bob"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return sorted client names.");
    }

    int remainingCount = registry.Remove(alice);
    if (remainingCount != 1 || registry.Snapshot().Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry did not remove the expected client.");
    }
}

static async Task RunClientRegistryFindsNamesCaseInsensitiveAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    registry.Add(alice);
    registry.Add(bob);

    if (registry.FindByName("ALICE") != alice)
    {
        throw new InvalidOperationException("ClientRegistry did not find a client name case-insensitively.");
    }

    if (!registry.IsNameInUse("BOB", alice))
    {
        throw new InvalidOperationException("ClientRegistry did not detect a duplicate name.");
    }

    if (registry.IsNameInUse("ALICE", alice))
    {
        throw new InvalidOperationException("ClientRegistry should ignore the current connection when checking names.");
    }
}

static void RunClientRegistryIncludesDefaultRoom()
{
    var registry = new ClientRegistry();

    if (!registry.GetRoomNames().SequenceEqual([ClientRegistry.DefaultRoomName]))
    {
        throw new InvalidOperationException("ClientRegistry should always include the default room.");
    }
}

static async Task RunClientRegistryFiltersRoomsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.MoveToRoom("study");
    clara.MoveToRoom("study");
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);

    if (!registry.GetRoomNames().SequenceEqual(["lobby", "study"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return sorted room names.");
    }

    if (!registry.GetNamesInRoom("STUDY").SequenceEqual(["alice", "clara"]))
    {
        throw new InvalidOperationException("ClientRegistry did not filter room users case-insensitively.");
    }

    if (registry.SnapshotRoom("study", alice).Single() != clara)
    {
        throw new InvalidOperationException("ClientRegistry did not snapshot a room with the expected exclusion.");
    }
}

static async Task RunClientRegistrySnapshotsRoomsCaseInsensitiveAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    alice.MoveToRoom("study");
    bob.MoveToRoom("study");
    registry.Add(alice);
    registry.Add(bob);

    ClientConnection[] roomSnapshot = registry.SnapshotRoom("STUDY", except: alice);

    if (roomSnapshot.Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry room snapshots should ignore room name casing.");
    }
}

static async Task RunClientRegistryFindsSpawnedPlayersByMapAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.Session.Spawn();
    bob.Session.Spawn();
    clara.Session.ChangeMap(2);
    clara.Session.Spawn();
    registry.Add(bob);
    registry.Add(clara);
    registry.Add(alice);

    if (!registry.GetSpawnedPlayerNamesInMap(WorldRules.DefaultMapId).SequenceEqual(["alice", "bob"]))
    {
        throw new InvalidOperationException("ClientRegistry should return spawned players in the requested map.");
    }

    bob.Session.Despawn();

    if (!registry.GetSpawnedPlayerNamesInMap(WorldRules.DefaultMapId).SequenceEqual(["alice"]))
    {
        throw new InvalidOperationException("ClientRegistry should exclude despawned players from map player lists.");
    }
}

static async Task RunClientRegistryFindsNearbyNamesAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);

    alice.Session.Spawn();
    bob.MoveToRoom("trade");
    bob.Session.MoveTo(new WorldPosition(10, 10));
    bob.Session.Spawn();
    clara.Session.ChangeMap(2);
    clara.Session.MoveTo(new WorldPosition(5, 5));
    clara.Session.Spawn();
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);

    if (!registry.GetNearbyNames(alice).SequenceEqual(["bob"]))
    {
        throw new InvalidOperationException("ClientRegistry did not return the expected nearby names.");
    }

    if (registry.SnapshotNearby(alice).Single() != bob)
    {
        throw new InvalidOperationException("ClientRegistry did not return the expected nearby snapshot.");
    }

    bob.Session.Despawn();

    if (registry.GetNearbyNames(alice).Length != 0 || registry.SnapshotNearby(alice).Length != 0)
    {
        throw new InvalidOperationException("ClientRegistry should exclude nearby clients that are not spawned.");
    }
}

static async Task RunClientRegistryFindsNearbySnapshotsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    await using NetworkPair claraPair = await NetworkPair.ConnectAsync();
    await using NetworkPair dylanPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);
    var clara = new ClientConnection("clara", claraPair.Client, claraPair.ClientStream);
    var dylan = new ClientConnection("dylan", dylanPair.Client, dylanPair.ClientStream);

    alice.Session.Spawn();
    bob.Session.Authenticate(2002);
    bob.Session.MoveTo(new WorldPosition(10, 10));
    bob.Session.Spawn();
    clara.Session.Authenticate(3003);
    clara.Session.MoveTo(new WorldPosition(2, 2));
    clara.Session.Spawn();
    dylan.Session.Authenticate(4004);
    dylan.Session.ChangeMap(2);
    dylan.Session.MoveTo(new WorldPosition(5, 5));
    dylan.Session.Spawn();
    registry.Add(alice);
    registry.Add(bob);
    registry.Add(clara);
    registry.Add(dylan);

    NearbySnapshotResult snapshotResult = registry.GetNearbySnapshots(alice);
    NearbyPlayerSnapshot[] snapshots = snapshotResult.Snapshots;

    if (snapshotResult.TotalCount != 2 ||
        snapshotResult.HiddenCount != 0 ||
        snapshots.Length != 2 ||
        snapshots[0].Name != "clara" ||
        snapshots[0].PlayerId != 3003 ||
        snapshots[0].MapId != WorldRules.DefaultMapId ||
        snapshots[0].Position != new WorldPosition(2, 2) ||
        snapshots[0].Distance != 4 ||
        snapshots[1].Name != "bob" ||
        snapshots[1].PlayerId != 2002 ||
        snapshots[1].MapId != WorldRules.DefaultMapId ||
        snapshots[1].Position != new WorldPosition(10, 10) ||
        snapshots[1].Distance != 20)
    {
        throw new InvalidOperationException("ClientRegistry did not return nearby player snapshots ordered by distance.");
    }

    clara.Session.Despawn();
    bob.Session.Despawn();

    NearbySnapshotResult emptySnapshotResult = registry.GetNearbySnapshots(alice);
    if (emptySnapshotResult.TotalCount != 0 ||
        emptySnapshotResult.HiddenCount != 0 ||
        emptySnapshotResult.Snapshots.Length != 0)
    {
        throw new InvalidOperationException("ClientRegistry should exclude despawned players from nearby snapshots.");
    }
}

static async Task RunClientRegistryLimitsNearbySnapshotsAsync()
{
    var registry = new ClientRegistry();
    var pairs = new List<NetworkPair>();

    try
    {
        NetworkPair alicePair = await NetworkPair.ConnectAsync();
        pairs.Add(alicePair);
        var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
        alice.Session.Spawn();
        registry.Add(alice);

        for (int index = 1; index <= WorldRules.MaxNearbySnapshotCount + 2; index++)
        {
            NetworkPair pair = await NetworkPair.ConnectAsync();
            pairs.Add(pair);
            var client = new ClientConnection($"player{index:D2}", pair.Client, pair.ClientStream);
            client.Session.Authenticate(1000 + index);
            client.Session.MoveTo(new WorldPosition(index, 0));
            client.Session.Spawn();
            registry.Add(client);
        }

        NearbySnapshotResult snapshotResult = registry.GetNearbySnapshots(alice);
        NearbyPlayerSnapshot[] snapshots = snapshotResult.Snapshots;

        if (snapshotResult.TotalCount != WorldRules.MaxNearbySnapshotCount + 2 ||
            snapshotResult.HiddenCount != 2 ||
            snapshots.Length != WorldRules.MaxNearbySnapshotCount)
        {
            throw new InvalidOperationException("ClientRegistry should limit nearby snapshot count.");
        }

        if (snapshots[0].Name != "player01" ||
            snapshots[0].Distance != 1 ||
            snapshots[^1].Name != "player10" ||
            snapshots[^1].Distance != 10)
        {
            throw new InvalidOperationException("ClientRegistry should keep the nearest nearby snapshots first.");
        }
    }
    finally
    {
        foreach (NetworkPair pair in pairs)
        {
            await pair.DisposeAsync();
        }
    }
}

static async Task RunClientRegistryDrainsConnectionsAsync()
{
    var registry = new ClientRegistry();
    await using NetworkPair alicePair = await NetworkPair.ConnectAsync();
    await using NetworkPair bobPair = await NetworkPair.ConnectAsync();
    var alice = new ClientConnection("alice", alicePair.Client, alicePair.ClientStream);
    var bob = new ClientConnection("bob", bobPair.Client, bobPair.ClientStream);

    registry.Add(alice);
    registry.Add(bob);

    ClientConnection[] drained = registry.Drain();

    if (drained.Length != 2 || registry.Count != 0)
    {
        throw new InvalidOperationException("ClientRegistry did not drain all connections.");
    }

    if (!drained.Contains(alice) || !drained.Contains(bob))
    {
        throw new InvalidOperationException("ClientRegistry drain did not return the original connections.");
    }
}

static async Task RunHelpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/help"));

    if (!handled || context.SentMessages.Count != 1)
    {
        throw new InvalidOperationException("Expected /help to send one notice message.");
    }

    SentMessage sent = context.SentMessages[0];
    if (sent.Type != MessageType.Notice ||
        !sent.Text.Contains("/join <room>") ||
        !sent.Text.Contains("/motd") ||
        !sent.Text.Contains("/echo <message>"))
    {
        throw new InvalidOperationException("/help output did not include expected command list.");
    }
}

static async Task RunCommandsAliasTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/commands"));

    if (!handled || !context.SentMessages.Single().Text.Contains("/commands"))
    {
        throw new InvalidOperationException("/commands did not return the command list.");
    }
}

static async Task RunWhereCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/where"));

    if (!handled || context.SentMessages.Single().Text != "Current room: study")
    {
        throw new InvalidOperationException("/where did not report the current room.");
    }
}

static async Task RunPingCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/ping"));

    if (!handled || context.SentMessages.Single().Text != "pong")
    {
        throw new InvalidOperationException("/ping did not return pong.");
    }
}

static async Task RunEchoCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo hello server"));

    if (!handled || context.SentMessages.Single().Text != "echo: hello server")
    {
        throw new InvalidOperationException("/echo did not return the expected message.");
    }
}

static async Task RunEmptyEchoCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo   "));

    if (!handled || context.SentMessages.Single().Text != "Usage: /echo <message>")
    {
        throw new InvalidOperationException("Empty /echo did not return the expected usage notice.");
    }
}

static async Task RunMissingEchoMessageCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/echo"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /echo <message>")
    {
        throw new InvalidOperationException("Missing /echo message did not return the expected usage notice.");
    }
}

static async Task RunTimeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.CurrentTime = new DateTimeOffset(2026, 6, 24, 10, 30, 0, TimeSpan.FromHours(9));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/time"));

    if (!handled || context.SentMessages.Single().Text != "Server time: 2026-06-24 10:30:00 +09:00")
    {
        throw new InvalidOperationException("/time did not return the injected server time.");
    }
}

static async Task RunUptimeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.ServerStartedAt = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = new DateTimeOffset(2026, 6, 24, 10, 5, 7, TimeSpan.FromHours(9));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/uptime"));

    if (!handled || context.SentMessages.Single().Text != "Server uptime: 00:05:07")
    {
        throw new InvalidOperationException("/uptime did not return the expected elapsed time.");
    }
}

static async Task RunWhoAmICommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whoami"));

    if (!handled || context.SentMessages.Single().Text != "You are alice in room study.")
    {
        throw new InvalidOperationException("/whoami did not return the current client identity.");
    }
}

static async Task RunSessionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/session"));

    if (!handled || context.SentMessages.Single().Text != "Session: player-id=0, state=anonymous, spawn=not-spawned")
    {
        throw new InvalidOperationException("/session did not return the expected anonymous session state.");
    }
}

static async Task RunLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    await context.CreateAccountAsync(1001, "correct-password");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 1001 correct-password"));

    if (!handled || context.Connection.Session.PlayerId != 1001 || !context.Connection.Session.IsAuthenticated)
    {
        throw new InvalidOperationException("/login did not authenticate the player session.");
    }

    if (!context.SentMessages.Single().Text.StartsWith(
        "Logged in as player 1001. Session token: ") ||
        string.IsNullOrWhiteSpace(context.Connection.Session.SessionToken) ||
        !context.SessionTokens.Validate(context.Connection.Session.SessionToken).IsValid)
    {
        throw new InvalidOperationException("/login did not return the expected notice.");
    }
}

static async Task RunResumeAndRevokeSessionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    string token = context.SessionTokens.Issue(1001);

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, $"/resume {token}"));
    if (context.Connection.Session.PlayerId != 1001 ||
        context.Connection.Session.SessionToken != token ||
        context.SentMessages.Single().Text != "Session resumed for player 1001.")
    {
        throw new InvalidOperationException("/resume should authenticate a valid stored session token.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));
    if (context.SessionTokens.Validate(token).IsValid ||
        context.Connection.Session.IsAuthenticated)
    {
        throw new InvalidOperationException("/logout should revoke the active session token.");
    }
}

static async Task RunActiveSessionExpiryCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    string token = context.SessionTokens.Issue(1001);
    context.Connection.Session.Authenticate(1001, token);
    context.CurrentTime += WorldRules.SessionTokenLifetime;

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/ping"));
    if (context.Connection.Session.IsAuthenticated ||
        context.SentMessages.Single().Text != "Session expired. Login again.")
    {
        throw new InvalidOperationException("Expired active session should be saved and logged out.");
    }
}

static async Task RunRegisterCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/register 1001 correct-password"));
    AccountCredential? account = await context.Accounts.FindAsync(1001);
    if (account is null ||
        !context.PasswordHasher.Verify("correct-password", account) ||
        context.SentMessages.Single().Text != "Account created for player 1001.")
    {
        throw new InvalidOperationException("/register should create a verifiable password hash.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/register 1001 another-password"));
    if (context.SentMessages.Last().Text != "Account could not be created.")
    {
        throw new InvalidOperationException("/register should not reveal details for duplicate accounts.");
    }
}

static async Task RunAuthenticationBackoffCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    await context.CreateAccountAsync(1001, "correct-password");

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login invalid bad-password"));
    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 1001 correct-password"));

    if (context.Connection.Session.IsAuthenticated ||
        context.SentMessages.Last().Text !=
            "Login temporarily limited. Retry after 1 seconds.")
    {
        throw new InvalidOperationException("Failed login should delay the next login attempt from the IP.");
    }

    context.CurrentTime += TimeSpan.FromSeconds(1);
    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 1001 correct-password"));
    if (!context.Connection.Session.IsAuthenticated ||
        context.Connection.Session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Login should resume after authentication backoff expires.");
    }
}

static async Task RunAuthenticatedSessionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/session"));

    if (!handled || context.SentMessages.Single().Text != "Session: player-id=1001, state=authenticated, spawn=not-spawned")
    {
        throw new InvalidOperationException("/session did not return the expected authenticated session state.");
    }
}

static async Task RunDuplicateLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 2002"));

    if (!handled || context.SentMessages.Single().Text != "You are already logged in as player 1001.")
    {
        throw new InvalidOperationException("Duplicate /login did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001)
    {
        throw new InvalidOperationException("Duplicate /login should not replace the authenticated player id.");
    }
}

static async Task RunLoginWhileSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login 2002"));

    if (!handled || context.SentMessages.Single().Text != "You cannot login while spawned.")
    {
        throw new InvalidOperationException("Spawned /login did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001 || !context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("Spawned /login should not change player identity or spawn state.");
    }
}

static async Task RunInvalidLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login abc"));

    if (!handled || context.Connection.Session.IsAuthenticated)
    {
        throw new InvalidOperationException("Invalid /login should not authenticate the player session.");
    }

    if (context.SentMessages.Single().Text != "Invalid player id or password.")
    {
        throw new InvalidOperationException("Invalid /login did not return the expected notice.");
    }
}

static async Task RunMissingLoginCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/login"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /login <playerId> <password>")
    {
        throw new InvalidOperationException("Missing /login player id did not return the expected usage notice.");
    }
}

static async Task RunLogoutCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "Logged out.")
    {
        throw new InvalidOperationException("/logout did not return the expected notice.");
    }

    if (context.Connection.Session.IsAuthenticated ||
        context.Connection.Session.PlayerId != PlayerSession.AnonymousPlayerId ||
        context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("/logout did not reset the player session.");
    }
}

static async Task RunLogoutWhileSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "You must despawn before logging out.")
    {
        throw new InvalidOperationException("Spawned /logout did not return the expected notice.");
    }

    if (context.Connection.Session.PlayerId != 1001 ||
        !context.Connection.Session.IsAuthenticated ||
        !context.Connection.Session.IsSpawned ||
        context.Connection.Session.Position != new WorldPosition(10, 20))
    {
        throw new InvalidOperationException("Spawned /logout should preserve the player session.");
    }
}

static async Task RunLogoutWhenAnonymousCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/logout"));

    if (!handled || context.SentMessages.Single().Text != "You are not logged in.")
    {
        throw new InvalidOperationException("Anonymous /logout did not return the expected notice.");
    }
}

static async Task RunPositionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/pos"));

    if (!handled || context.SentMessages.Single().Text != "Position: x=0, y=0")
    {
        throw new InvalidOperationException("/pos did not return the expected default position.");
    }
}

static async Task RunMapCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.ChangeMap(2);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/map"));

    if (!handled || context.SentMessages.Single().Text != "Map: 2")
    {
        throw new InvalidOperationException("/map did not return the current game map id.");
    }
}

static async Task RunMapUsersCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/map-users"));

    if (!handled || context.SentMessages.Single().Text != "Players in map 1 (2): alice, bob")
    {
        throw new InvalidOperationException("/map-users did not return the current map player list.");
    }
}

static async Task RunWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(5, 6));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "Warped to map=2, x=30, y=40")
    {
        throw new InvalidOperationException("/warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != 2 ||
        context.Connection.Session.Position != new WorldPosition(30, 40) ||
        !context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/warp did not update the player world state.");
    }

    string[] expectedNotices =
    [
        "alice left map 1 from x=5, y=6",
        "alice entered map 2 at x=30, y=40"
    ];

    if (!context.NearbyNotices.SequenceEqual(expectedNotices))
    {
        throw new InvalidOperationException("/warp did not notify the old and new map in order.");
    }
}

static async Task RunWarpRequiresAuthenticationCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "You must login before warping.")
    {
        throw new InvalidOperationException("Anonymous /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Anonymous /warp should not change or broadcast world state.");
    }
}

static async Task RunWarpWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 30 40"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before warping.")
    {
        throw new InvalidOperationException("Unspawned /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Unspawned /warp should not change or broadcast world state.");
    }
}

static async Task RunInvalidWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp second-map"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /warp <mapId> <x> <y>")
    {
        throw new InvalidOperationException("Invalid /warp did not return the expected usage notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Invalid /warp should preserve world state.");
    }
}

static async Task RunInvalidWarpMapCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 0 30 40"));

    if (!handled || context.SentMessages.Single().Text != "Map id must be positive.")
    {
        throw new InvalidOperationException("Invalid /warp map did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Invalid /warp map should preserve world state.");
    }
}

static async Task RunOutOfBoundsWarpCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/warp 2 101 0"));

    if (!handled || context.SentMessages.Single().Text != "Position must be between -100 and 100.")
    {
        throw new InvalidOperationException("Out-of-bounds /warp did not return the expected notice.");
    }

    if (context.Connection.Session.MapId != WorldRules.DefaultMapId ||
        context.Connection.Session.Position != WorldPosition.Origin ||
        !context.Connection.Session.IsSpawned ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Out-of-bounds /warp should preserve world state.");
    }
}

static async Task RunMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 4 6"));

    if (!handled || context.Connection.Session.Position != new WorldPosition(4, 6))
    {
        throw new InvalidOperationException("/move did not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Moved to x=4, y=6")
    {
        throw new InvalidOperationException("/move did not return the expected notice.");
    }

    if (context.NearbyNotices.Single() != "alice moved to x=4, y=6")
    {
        throw new InvalidOperationException("/move did not notify nearby players.");
    }

    if (context.Connection.Session.LastMoveSequence != 1)
    {
        throw new InvalidOperationException("/move did not store the movement sequence.");
    }
}

static async Task RunMoveWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 10 20"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("/move should not update an unspawned player position.");
    }

    if (context.SentMessages.Single().Text != "You must spawn before moving.")
    {
        throw new InvalidOperationException("/move did not explain that the player must spawn first.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("/move should not notify nearby players when movement is rejected.");
    }
}

static async Task RunRepeatedMoveSequenceCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    DateTimeOffset firstMoveAt = new(2026, 7, 5, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = firstMoveAt;

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 10 4 0"));

    context.SentMessages.Clear();
    context.NearbyNotices.Clear();
    context.CurrentTime = firstMoveAt.AddSeconds(2);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 10 8 0"));

    if (!handled || context.SentMessages.Single().Text != "Move sequence must be greater than 10.")
    {
        throw new InvalidOperationException("Repeated move sequence did not return the expected notice.");
    }

    if (context.Connection.Session.Position != new WorldPosition(4, 0) ||
        context.Connection.Session.LastMoveAt != firstMoveAt ||
        context.Connection.Session.LastMoveSequence != 10 ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Repeated move sequence should preserve movement state.");
    }
}

static async Task RunMoveCooldownCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    DateTimeOffset firstMoveAt = new(2026, 6, 30, 10, 0, 0, TimeSpan.FromHours(9));
    context.CurrentTime = firstMoveAt;

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 4 0"));

    context.SentMessages.Clear();
    context.NearbyNotices.Clear();
    context.CurrentTime = firstMoveAt.AddMilliseconds(500);

    bool rejected = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 2 8 0"));

    if (!rejected || context.SentMessages.Single().Text != "You must wait 1 second between moves.")
    {
        throw new InvalidOperationException("Early repeated /move did not return the expected cooldown notice.");
    }

    if (context.Connection.Session.Position != new WorldPosition(4, 0) ||
        context.Connection.Session.LastMoveAt != firstMoveAt ||
        context.Connection.Session.LastMoveSequence != 1 ||
        context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Rejected cooldown /move should preserve movement state.");
    }

    context.SentMessages.Clear();
    context.CurrentTime = firstMoveAt.AddSeconds(1);

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 2 8 0"));

    if (!handled || context.SentMessages.Single().Text != "Moved to x=8, y=0")
    {
        throw new InvalidOperationException("/move should succeed when the cooldown has elapsed.");
    }

    if (context.Connection.Session.Position != new WorldPosition(8, 0) ||
        context.Connection.Session.LastMoveAt != context.CurrentTime ||
        context.Connection.Session.LastMoveSequence != 2 ||
        context.NearbyNotices.Single() != "alice moved to x=8, y=0")
    {
        throw new InvalidOperationException("Successful cooldown /move did not update movement state.");
    }
}

static async Task RunInvalidMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move north"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Invalid /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Usage: /move <sequence> <x> <y>")
    {
        throw new InvalidOperationException("Invalid /move did not return the expected usage notice.");
    }
}

static async Task RunOutOfBoundsMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 101 0"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Out-of-bounds /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Position must be between -100 and 100.")
    {
        throw new InvalidOperationException("Out-of-bounds /move did not return the expected notice.");
    }
}

static async Task RunTooFarMoveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/move 1 11 0"));

    if (!handled || context.Connection.Session.Position != WorldPosition.Origin)
    {
        throw new InvalidOperationException("Too-far /move should not update the player session position.");
    }

    if (context.SentMessages.Single().Text != "Move distance must be 10 or less.")
    {
        throw new InvalidOperationException("Too-far /move did not return the expected notice.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Too-far /move should not notify nearby players.");
    }
}

static async Task RunNearbyCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    context.TargetConnection.Session.MoveTo(new WorldPosition(10, 10));
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/nearby"));

    if (!handled || context.SentMessages.Single().Text != "Nearby players (1): bob")
    {
        throw new InvalidOperationException("/nearby did not return the expected nearby player list.");
    }
}

static async Task RunNearbyWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/nearby"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before checking nearby players.")
    {
        throw new InvalidOperationException("/nearby should explain that the player must spawn first.");
    }
}

static async Task RunLookCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Spawn();
    context.TargetConnection.Session.Authenticate(2002);
    context.TargetConnection.Session.MoveTo(new WorldPosition(10, 10));
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/look"));

    if (!handled || context.SentMessages.Single().Text != "Nearby snapshots (1/1, hidden=0): bob[player-id=2002,map=1,x=10, y=10,distance=20]")
    {
        throw new InvalidOperationException("/look did not return the expected nearby player snapshot.");
    }
}

static async Task RunLookWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.TargetConnection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/look"));

    if (!handled || context.SentMessages.Single().Text != "You must spawn before looking around.")
    {
        throw new InvalidOperationException("/look should explain that the player must spawn first.");
    }
}

static async Task RunSpawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "Spawned at x=10, y=20")
    {
        throw new InvalidOperationException("/spawn did not return the expected notice.");
    }

    if (!context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/spawn did not update the player session spawn state.");
    }

    if (context.NearbyNotices.Single() != "alice spawned at x=10, y=20")
    {
        throw new InvalidOperationException("/spawn did not notify nearby players.");
    }
}

static async Task RunSpawnRequiresAuthenticationCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "You must login before spawning.")
    {
        throw new InvalidOperationException("/spawn should explain that the player must login first.");
    }

    if (context.Connection.Session.IsSpawned || context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Rejected anonymous /spawn should not change or broadcast spawn state.");
    }
}

static async Task RunDuplicateSpawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1001);
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn"));

    if (!handled || context.SentMessages.Single().Text != "You are already spawned.")
    {
        throw new InvalidOperationException("Duplicate /spawn should explain that the player is already spawned.");
    }

    if (!context.Connection.Session.IsSpawned || context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("Duplicate /spawn should keep state without broadcasting.");
    }
}

static async Task RunDespawnCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.MoveTo(new WorldPosition(10, 20));
    context.Connection.Session.Spawn();

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/despawn"));

    if (!handled || context.SentMessages.Single().Text != "Despawned from x=10, y=20")
    {
        throw new InvalidOperationException("/despawn did not return the expected notice.");
    }

    if (context.Connection.Session.IsSpawned)
    {
        throw new InvalidOperationException("/despawn did not update the player session spawn state.");
    }

    if (context.NearbyNotices.Single() != "alice despawned from x=10, y=20")
    {
        throw new InvalidOperationException("/despawn did not notify nearby players.");
    }
}

static async Task RunDespawnWhenNotSpawnedCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/despawn"));

    if (!handled || context.SentMessages.Single().Text != "You are not spawned.")
    {
        throw new InvalidOperationException("/despawn should explain when the player is not spawned.");
    }

    if (context.NearbyNotices.Count != 0)
    {
        throw new InvalidOperationException("/despawn should not notify nearby players when already not spawned.");
    }
}

static async Task RunJoinCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join study"));

    if (!handled || context.MovedRooms.Single() != "study")
    {
        throw new InvalidOperationException("/join did not request a room move.");
    }
}

static async Task RunMissingJoinRoomCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /join <room>")
    {
        throw new InvalidOperationException("Missing /join room did not return the expected usage notice.");
    }
}

static async Task RunLeaveCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/leave"));

    if (!handled || context.MovedRooms.Single() != "lobby")
    {
        throw new InvalidOperationException("/leave did not request a move to lobby.");
    }
}

static async Task RunInvalidRoomNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/join bad room"));

    if (!handled || context.MovedRooms.Count != 0)
    {
        throw new InvalidOperationException("Invalid room name should not move the client.");
    }

    if (!context.SentMessages.Single().Text.Contains("Room name can contain only"))
    {
        throw new InvalidOperationException("Invalid room name did not return the expected notice.");
    }
}

static async Task RunRoomUsersCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/room-users"));

    if (!handled || context.SentMessages.Single().Text != "Users in study (1): alice")
    {
        throw new InvalidOperationException("/room-users did not report users in the current room.");
    }
}

static async Task RunAdministratorAuthorizationTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.IsAdministrator = false;
    await context.Handler.TryHandleAsync(context.Connection,
        new NetworkMessage(MessageType.Command, "/metrics"));
    if (context.SentMessages.Single().Text != "Administrator permission required.")
        throw new InvalidOperationException("Operational commands should require administrator permission.");
}

static async Task RunStatsCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.MoveToRoom("study");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/stats"));

    if (!handled || context.SentMessages.Single().Text != "Stats: users=2, rooms=2, current-room-users=1")
    {
        throw new InvalidOperationException("/stats did not return the expected summary.");
    }
}

static async Task RunMotdCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/motd"));

    if (!handled || context.SentMessages.Single().Text != ServerInfo.MessageOfTheDay)
    {
        throw new InvalidOperationException("/motd did not return the expected message.");
    }
}

static async Task RunVersionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/version"));

    if (!handled || context.SentMessages.Single().Text != ServerInfo.VersionMessage)
    {
        throw new InvalidOperationException("/version did not return the expected message.");
    }
}

static async Task RunMeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me waves"));

    if (!handled || context.BroadcastMessages.Single().Text != "* alice waves")
    {
        throw new InvalidOperationException("/me did not broadcast the expected action message.");
    }
}

static async Task RunEmptyMeCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me   "));

    if (!handled || context.SentMessages.Single().Text != "Usage: /me <action>")
    {
        throw new InvalidOperationException("Empty /me did not return the expected usage notice.");
    }
}

static async Task RunMissingMeActionCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/me"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /me <action>")
    {
        throw new InvalidOperationException("Missing /me action did not return the expected usage notice.");
    }
}

static async Task RunWhisperCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper bob hello"));

    if (!handled || context.SentMessages.Count != 2)
    {
        throw new InvalidOperationException("/whisper should send one notice to the target and one to the sender.");
    }

    if (context.SentMessages[0].Connection != context.TargetConnection ||
        context.SentMessages[0].Text != "whisper from alice: hello")
    {
        throw new InvalidOperationException("/whisper did not send the expected target notice.");
    }

    if (context.SentMessages[1].Connection != context.Connection ||
        context.SentMessages[1].Text != "whisper to bob: hello")
    {
        throw new InvalidOperationException("/whisper did not send the expected sender confirmation.");
    }
}

static async Task RunWhisperUnknownUserCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper clara hello"));

    if (!handled || context.SentMessages.Single().Text != "User not found: clara")
    {
        throw new InvalidOperationException("/whisper did not report an unknown target user.");
    }
}

static async Task RunInvalidWhisperCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper bob"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /whisper <nickname> <message>")
    {
        throw new InvalidOperationException("Invalid /whisper did not return the expected usage notice.");
    }
}

static async Task RunMissingWhisperPayloadCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/whisper"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /whisper <nickname> <message>")
    {
        throw new InvalidOperationException("Missing /whisper payload did not return the expected usage notice.");
    }
}

static async Task RunRenameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    TextWriter originalOutput = Console.Out;
    using var capturedOutput = new StringWriter();

    bool handled;
    try
    {
        Console.SetOut(capturedOutput);
        handled = await context.Handler.TryHandleAsync(
            context.Connection,
            new NetworkMessage(MessageType.Command, "/rename clara"));
    }
    finally
    {
        Console.SetOut(originalOutput);
    }

    if (!handled || context.Connection.Name != "clara")
    {
        throw new InvalidOperationException("/rename did not rename the client.");
    }

    if (context.BroadcastNotices.Single() != "alice is now clara")
    {
        throw new InvalidOperationException("/rename did not broadcast the expected notice.");
    }

    if (!capturedOutput.ToString().Contains("[server] alice is now clara"))
    {
        throw new InvalidOperationException("/rename did not log the expected rename message.");
    }
}

static async Task RunMissingNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /name <nickname>")
    {
        throw new InvalidOperationException("Missing /name nickname did not return the expected usage notice.");
    }
}

static async Task RunMissingRenameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/rename"));

    if (!handled || context.SentMessages.Single().Text != "Usage: /rename <nickname>")
    {
        throw new InvalidOperationException("Missing /rename nickname did not return the expected usage notice.");
    }
}

static async Task RunDuplicateNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.DuplicateName = "bob";

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name bob"));

    if (!handled || context.Connection.Name != "alice")
    {
        throw new InvalidOperationException("Duplicate /name should not rename the client.");
    }

    if (context.SentMessages.Single().Text != "Nickname is already in use: bob")
    {
        throw new InvalidOperationException("Duplicate /name did not return the expected notice.");
    }
}

static async Task RunInvalidNameCommandTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");

    bool handled = await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/name bad name"));

    if (!handled || context.Connection.Name != "alice")
    {
        throw new InvalidOperationException("Invalid /name should not rename the client.");
    }

    if (context.SentMessages.Single().Text != "Nickname can contain only letters, numbers, '-' and '_'.")
    {
        throw new InvalidOperationException("Invalid /name did not return the expected notice.");
    }
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string failureMessage)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(failureMessage);
}

sealed class NetworkPair : IAsyncDisposable
{
    private readonly TcpListener listener;

    public TcpClient Client { get; }

    public TcpClient Server { get; }

    public NetworkStream ClientStream { get; }

    public NetworkStream ServerStream { get; }

    private NetworkPair(TcpListener listener, TcpClient client, TcpClient server)
    {
        this.listener = listener;
        Client = client;
        Server = server;
        ClientStream = client.GetStream();
        ServerStream = server.GetStream();
    }

    public static async Task<NetworkPair> ConnectAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var client = new TcpClient();
        Task<TcpClient> acceptTask = listener.AcceptTcpClientAsync();

        await client.ConnectAsync(IPAddress.Loopback, port);
        TcpClient server = await acceptTask;

        return new NetworkPair(listener, client, server);
    }

    public async ValueTask DisposeAsync()
    {
        await ClientStream.DisposeAsync();
        await ServerStream.DisposeAsync();
        Client.Dispose();
        Server.Dispose();
        listener.Stop();
    }
}

sealed record SentMessage(ClientConnection Connection, MessageType Type, string Text);

sealed record BroadcastMessage(ClientConnection Connection, string Text);

sealed class FixedRandomSource : IRandomSource
{
    private readonly double value;

    public FixedRandomSource(double value)
    {
        this.value = value;
    }

    public double NextDouble() => value;
}

sealed class FlakyCharacterRepository : ICharacterRepository
{
    private readonly InMemoryCharacterRepository inner = new();
    private int remainingFailures;

    public FlakyCharacterRepository(int failureCount)
    {
        remainingFailures = failureCount;
    }

    public int SaveCalls { get; private set; }

    public Task<CharacterSaveData> SaveAsync(
        CharacterSaveData character,
        CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        if (remainingFailures-- > 0)
        {
            throw new IOException("Transient save failure.");
        }

        return inner.SaveAsync(character, cancellationToken);
    }

    public Task<CharacterSaveData?> LoadAsync(
        long playerId,
        CancellationToken cancellationToken = default) =>
        inner.LoadAsync(playerId, cancellationToken);
}

static class MonsterTests
{
public static void RunMonsterRegistryTest()
{
    var registry = new MonsterRegistry();
    var first = new MonsterEntity(1, "slime", 1, new WorldPosition(3, 4));
    var otherMap = new MonsterEntity(2, "goblin", 2, new WorldPosition(5, 6));

    if (!registry.TrySpawn(first) || !registry.TrySpawn(otherMap) || registry.Count != 2)
    {
        throw new InvalidOperationException("MonsterRegistry should store newly spawned monsters.");
    }

    if (registry.TrySpawn(first))
    {
        throw new InvalidOperationException("MonsterRegistry should reject duplicate monster ids.");
    }

    MonsterEntity[] mapMonsters = registry.SnapshotMap(1);
    if (mapMonsters.Length != 1 || mapMonsters[0] != first)
    {
        throw new InvalidOperationException("MonsterRegistry should return only monsters in the requested map.");
    }
}

public static void RunMonsterAiTickTest()
{
    var registry = new MonsterRegistry();
    var monster = new MonsterEntity(1, "slime", 1, WorldPosition.Origin);
    registry.TrySpawn(monster);
    PlayerEntity[] players =
    [
        new PlayerEntity(10, "alice", 1, new WorldPosition(5, 0), true),
        new PlayerEntity(20, "bob", 1, new WorldPosition(2, 1), true),
        new PlayerEntity(1, "other-map", 2, WorldPosition.Origin, true)
    ];
    var processor = new MonsterAiTickProcessor(registry, () => players, (_, _) => null);
    DateTimeOffset firstTick = DateTimeOffset.UnixEpoch;

    MonsterAiTickResult firstResult = processor.Process(firstTick);
    MonsterMovement firstMovement = firstResult.Movements.Single();
    if (firstMovement.TargetPlayerId != 20 ||
        firstMovement.PreviousPosition != WorldPosition.Origin ||
        firstMovement.NextPosition != new WorldPosition(1, 0) ||
        registry.SnapshotMap(1).Single().AiState != MonsterAiState.Chasing)
    {
        throw new InvalidOperationException("Monster AI should move one step toward the nearest player in the same map.");
    }

    MonsterAiTickResult cooldownResult = processor.Process(firstTick.AddMilliseconds(100));
    if (cooldownResult.Movements.Count != 0 ||
        registry.SnapshotMap(1).Single().Position != new WorldPosition(1, 0))
    {
        throw new InvalidOperationException("Monster AI should respect the server movement interval.");
    }

    MonsterAiTickResult nextResult = processor.Process(firstTick + WorldRules.MonsterMoveInterval);
    if (nextResult.Movements.Single().NextPosition != new WorldPosition(2, 0))
    {
        throw new InvalidOperationException("Monster AI should continue tracking its nearest target on a later tick.");
    }

    players =
    [
        new PlayerEntity(
            20,
            "bob",
            1,
            new WorldPosition(WorldRules.MonsterLeashDistance + 1, 0),
            true)
    ];
    MonsterAiTickResult returningResult = processor.Process(
        firstTick + WorldRules.MonsterMoveInterval + WorldRules.MonsterMoveInterval);
    MonsterEntity returningMonster = registry.SnapshotMap(1).Single();
    if (returningResult.Movements.Single().TargetPlayerId is not null ||
        returningMonster.AiState != MonsterAiState.Returning ||
        returningMonster.AggroTargetPlayerId is not null ||
        returningMonster.Position != new WorldPosition(1, 0))
    {
        throw new InvalidOperationException("Monster AI should drop distant targets and return toward its spawn point.");
    }

    processor.Process(firstTick + TimeSpan.FromMilliseconds(1500));
    MonsterEntity idleMonster = registry.SnapshotMap(1).Single();
    if (idleMonster.Position != WorldPosition.Origin || idleMonster.AiState != MonsterAiState.Idle)
    {
        throw new InvalidOperationException("Monster AI should become idle after returning to its spawn point.");
    }

    var distantRegistry = new MonsterRegistry();
    distantRegistry.TrySpawn(new MonsterEntity(2, "wolf", 1, WorldPosition.Origin));
    var distantProcessor = new MonsterAiTickProcessor(
        distantRegistry,
        () =>
        [
            new PlayerEntity(
                30,
                "distant",
                1,
                new WorldPosition(WorldRules.MonsterDetectionDistance + 1, 0),
                true)
        ],
        (_, _) => null);
    if (distantProcessor.Process(firstTick).Movements.Count != 0 ||
        distantRegistry.SnapshotMap(1).Single().AiState != MonsterAiState.Idle)
    {
        throw new InvalidOperationException("Idle monsters should ignore players outside their detection distance.");
    }

    RunMonsterCombatTest();
}

private static void RunMonsterCombatTest()
{
    var registry = new MonsterRegistry();
    registry.TrySpawn(new MonsterEntity(50, "orc", 1, WorldPosition.Origin));
    var player = new PlayerSession();
    player.Authenticate(100);
    player.MoveTo(new WorldPosition(1, 0));
    player.Spawn();
    PlayerEntity[] GetPlayers() =>
    [
        new PlayerEntity(
            player.PlayerId,
            "hero",
            player.MapId,
            player.Position,
            player.IsSpawned)
    ];
    PlayerDamageResult? ApplyDamage(long playerId, int damage) =>
        playerId == player.PlayerId ? player.ApplyDamage(damage) : null;
    var processor = new MonsterAiTickProcessor(registry, GetPlayers, ApplyDamage);
    DateTimeOffset start = DateTimeOffset.UnixEpoch;

    MonsterAiTickResult firstAttackTick = processor.Process(start);
    MonsterAttack firstAttack = firstAttackTick.Attacks.Single();
    if (firstAttack.Damage != WorldRules.MonsterAttackDamage ||
        firstAttack.RemainingHealth != WorldRules.PlayerMaxHealth - WorldRules.MonsterAttackDamage ||
        firstAttack.IsFatal ||
        player.CurrentHealth != firstAttack.RemainingHealth)
    {
        throw new InvalidOperationException("Monster combat should apply authoritative damage in attack range.");
    }

    MonsterAiTickResult cooldownTick = processor.Process(start + TimeSpan.FromMilliseconds(500));
    if (cooldownTick.Attacks.Count != 0 ||
        player.CurrentHealth != WorldRules.PlayerMaxHealth - WorldRules.MonsterAttackDamage)
    {
        throw new InvalidOperationException("Monster combat should respect the attack cooldown.");
    }

    MonsterAttack? fatalAttack = null;
    for (int attackNumber = 2; attackNumber <= 10; attackNumber++)
    {
        DateTimeOffset attackTime = start +
            TimeSpan.FromTicks(WorldRules.MonsterAttackInterval.Ticks * (attackNumber - 1));
        MonsterAiTickResult attackTick = processor.Process(attackTime);
        fatalAttack = attackTick.Attacks.Single();
    }

    if (fatalAttack is null || !fatalAttack.IsFatal || player.IsAlive || player.IsSpawned)
    {
        throw new InvalidOperationException("Fatal monster damage should kill and despawn the player session.");
    }
}

public static async Task RunPlayerCombatTickTestAsync()
{
    var monsters = new MonsterRegistry();
    monsters.TrySpawn(new MonsterEntity(70, "skeleton", 1, new WorldPosition(1, 0)));
    var attacker = new PlayerSession();
    attacker.Authenticate(700);
    attacker.Spawn();
    var queue = new PlayerAttackRequestQueue();
    var groundLoot = new GroundLootRegistry();
    var processor = new CombatTickProcessor(queue, monsters, groundLoot, new FixedRandomSource(0.05));
    DateTimeOffset start = DateTimeOffset.UnixEpoch;

    async Task<PlayerAttackResult> AttackAtAsync(DateTimeOffset serverTime)
    {
        var queued = new QueuedPlayerAttackRequest(
            new PlayerAttackRequest(attacker, 70));
        queue.Enqueue(queued);
        processor.Process(serverTime);
        return await queued.Completion;
    }

    PlayerAttackResult first = await AttackAtAsync(start);
    if (!first.IsAccepted || first.Damage != WorldRules.PlayerAttackDamage || first.RemainingHealth != 30)
    {
        throw new InvalidOperationException("Combat tick should apply an accepted player attack to the monster.");
    }

    PlayerAttackResult cooldown = await AttackAtAsync(start.AddMilliseconds(100));
    if (cooldown.IsAccepted || monsters.Find(70)?.CurrentHealth != 30)
    {
        throw new InvalidOperationException("Combat tick should reject attacks during the player cooldown.");
    }

    PlayerAttackResult second = await AttackAtAsync(start + WorldRules.PlayerAttackInterval);
    PlayerAttackResult fatal = await AttackAtAsync(start + WorldRules.PlayerAttackInterval + WorldRules.PlayerAttackInterval);
    MonsterEntity? defeatedMonster = monsters.Find(70);
    if (!second.IsAccepted ||
        !fatal.IsFatal ||
        fatal.Damage != 10 ||
        fatal.ExperienceAwarded != 30 ||
        attacker.Experience != 30 ||
        !fatal.ItemDrops.Contains(new ItemDrop("bone", 1)) ||
        !fatal.ItemDrops.Contains(new ItemDrop("health-potion", 1)) ||
        defeatedMonster?.IsSpawned != false ||
        defeatedMonster.KillCreditPlayerId != attacker.PlayerId)
    {
        throw new InvalidOperationException("Fatal player damage should despawn the monster and clamp applied damage.");
    }

    GroundLoot[] droppedLoot = groundLoot.SnapshotNearby(attacker, start + TimeSpan.FromSeconds(1));
    if (droppedLoot.Length != 2 || attacker.SnapshotInventory().Length != 0)
    {
        throw new InvalidOperationException("Kill drops should exist on the ground before pickup.");
    }

    var intruder = new PlayerSession();
    intruder.Authenticate(701);
    intruder.Spawn();
    if (groundLoot.TryPickup(droppedLoot[0].LootId, intruder, start + TimeSpan.FromSeconds(1)).IsSuccess)
    {
        throw new InvalidOperationException("Ground loot should reject non-owners during the exclusive period.");
    }

    foreach (GroundLoot entry in droppedLoot)
    {
        if (!groundLoot.TryPickup(entry.LootId, attacker, start + TimeSpan.FromSeconds(1)).IsSuccess)
        {
            throw new InvalidOperationException("Kill owner should be able to pick up ground loot immediately.");
        }
    }

    ExperienceGainResult levelUp = attacker.AddExperience(70);
    if (!levelUp.LeveledUp || attacker.Level != 2 || attacker.ExperienceToNextLevel != 100)
    {
        throw new InvalidOperationException("Player experience should advance levels at the configured threshold.");
    }

    MonsterRewardDefinition fallbackReward = MonsterRewardCatalog.Get("unknown-monster");
    if (fallbackReward.Experience != 20 ||
        fallbackReward.Drops.Single().Drop != new ItemDrop("monster-token", 1))
    {
        throw new InvalidOperationException("Unknown monster types should use the default server reward definition.");
    }

    attacker.AddItem(new ItemDrop("iron-sword", 1));
    ItemActionResult equipResult = attacker.Equip("iron-sword");
    if (!equipResult.IsSuccess || attacker.AttackPower != WorldRules.PlayerAttackDamage + 5)
    {
        throw new InvalidOperationException("Equipping a weapon should consume it and add its attack bonus.");
    }

    attacker.ApplyDamage(20);
    ItemActionResult useResult = attacker.UseItem("health-potion");
    if (!useResult.IsSuccess || attacker.CurrentHealth != attacker.MaxHealth)
    {
        throw new InvalidOperationException("Using a health potion should consume it and restore missing health.");
    }

    ItemActionResult unequipResult = attacker.Unequip(EquipmentSlot.Weapon);
    if (!unequipResult.IsSuccess ||
        attacker.AttackPower != WorldRules.PlayerAttackDamage ||
        !attacker.SnapshotInventory().Contains(new ItemStack("iron-sword", 1)))
    {
        throw new InvalidOperationException("Unequipping should return the item to inventory and remove its bonus.");
    }

    attacker.AddItem(new ItemDrop("leather-armor", 1));
    attacker.Equip("leather-armor");
    int healthBeforeArmorHit = attacker.CurrentHealth;
    PlayerDamageResult armoredDamage = attacker.ApplyDamage(10);
    if (attacker.Defense != 3 || armoredDamage.DamageApplied != 7 || attacker.CurrentHealth != healthBeforeArmorHit - 7)
    {
        throw new InvalidOperationException("Equipped armor should reduce incoming monster damage.");
    }

    GroundLoot publicLoot = groundLoot.Spawn(
        new ItemDrop("monster-token", 1),
        intruder.MapId,
        intruder.Position,
        attacker.PlayerId,
        start);
    if (!groundLoot.TryPickup(
        publicLoot.LootId,
        intruder,
        start + WorldRules.LootExclusiveDuration).IsSuccess)
    {
        throw new InvalidOperationException("Ground loot should become public after its exclusive period.");
    }

    GroundLoot expiringLoot = groundLoot.Spawn(
        new ItemDrop("monster-token", 1),
        attacker.MapId,
        attacker.Position,
        attacker.PlayerId,
        start);
    if (groundLoot.RemoveExpired(start + WorldRules.LootLifetime) == 0 ||
        groundLoot.TryPickup(expiringLoot.LootId, attacker, start + WorldRules.LootLifetime).IsSuccess)
    {
        throw new InvalidOperationException("Expired ground loot should be removed from the world.");
    }

    PlayerAttackResult deadTarget = await AttackAtAsync(start + TimeSpan.FromSeconds(2));
    if (deadTarget.IsAccepted)
    {
        throw new InvalidOperationException("Combat tick should reject attacks against dead monsters.");
    }

    if (processor.Process(start + WorldRules.MonsterRespawnDelay - TimeSpan.FromMilliseconds(1)).RespawnedMonsters.Count != 0)
    {
        throw new InvalidOperationException("Monster should not respawn before its server respawn time.");
    }

    CombatTickResult respawnTick = processor.Process(
        start + WorldRules.PlayerAttackInterval + WorldRules.PlayerAttackInterval + WorldRules.MonsterRespawnDelay);
    MonsterEntity respawned = respawnTick.RespawnedMonsters.Single();
    if (!respawned.IsSpawned ||
        respawned.CurrentHealth != respawned.MaxHealth ||
        respawned.Position != respawned.SpawnPosition ||
        respawned.KillCreditPlayerId is not null)
    {
        throw new InvalidOperationException("Monster should respawn at full health and its original spawn position.");
    }

    await RunCombatEventDispatchLoopTestAsync();
}

private static async Task RunCombatEventDispatchLoopTestAsync()
{
    var events = new CombatEventQueue();
    var dispatched = new List<CombatNotification>();
    var delivered = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    using var cancellation = new CancellationTokenSource();
    var loop = new CombatEventDispatchLoop(
        events,
        notification =>
        {
            dispatched.Add(notification);
            delivered.TrySetResult(true);
            return Task.CompletedTask;
        },
        TimeSpan.FromMilliseconds(1));
    Task loopTask = loop.RunAsync(cancellation.Token);
    var notification = new CombatNotification(100, "combat event");
    events.Enqueue(notification);

    await delivered.Task.WaitAsync(TimeSpan.FromSeconds(1));
    cancellation.Cancel();
    await loopTask;

    if (dispatched.Count != 1 || dispatched[0] != notification)
    {
        throw new InvalidOperationException("Combat event dispatch loop should deliver each queued event once.");
    }
}

public static async Task RunMonsterCommandsTestAsync()
{
    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(1);
    context.Connection.Session.Spawn();

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn-monster 10 slime 3 4"));

    if (context.SentMessages.Last().Text != "Spawned monster slime#10 at map=1, x=3, y=4" ||
        context.Monsters.Count != 1)
    {
        throw new InvalidOperationException("/spawn-monster should create a monster in the player's current map.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/monsters"));

    if (!context.SentMessages.Last().Text.Contains("slime#10[Idle, hp=50/50]@x=3, y=4", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("/monsters should list monsters in the player's current map.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/spawn-monster 10 slime 5 6"));

    if (context.SentMessages.Last().Text != "Monster id is already in use: 10" || context.Monsters.Count != 1)
    {
        throw new InvalidOperationException("/spawn-monster should reject duplicate monster ids.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/health"));
    if (context.SentMessages.Last().Text != "Health: 100/100, state=alive")
    {
        throw new InvalidOperationException("/health should show the authoritative player health state.");
    }

    context.Connection.Session.MoveTo(new WorldPosition(3, 3));
    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/attack 10"));
    if (context.SentMessages.Last().Text != "Attacked slime#10 for 20 damage. HP: 30/50")
    {
        throw new InvalidOperationException("/attack should return the world-tick combat result.");
    }

    if (context.NearbyNotices.LastOrDefault() != "alice hit slime#10 for 20 damage.")
    {
        throw new InvalidOperationException("/attack should notify nearby players about combat.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/experience"));
    if (context.SentMessages.Last().Text != "Experience: 0")
    {
        throw new InvalidOperationException("/experience should show server-owned experience.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/level"));
    if (context.SentMessages.Last().Text != "Level: 1, XP: 0, next level in 100 XP")
    {
        throw new InvalidOperationException("/level should show level progress from server experience.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, "/inventory"));
    if (context.SentMessages.Last().Text != "Inventory (0): (empty)")
    {
        throw new InvalidOperationException("/inventory should show an empty server inventory before drops.");
    }

    context.Connection.Session.AddItem(new ItemDrop("iron-sword", 1));
    await context.Handler.TryHandleAsync(context.Connection, new NetworkMessage(MessageType.Command, "/equip iron-sword"));
    await context.Handler.TryHandleAsync(context.Connection, new NetworkMessage(MessageType.Command, "/equipment"));
    if (!context.SentMessages.Last().Text.Contains("Weapon=iron-sword", StringComparison.Ordinal) ||
        !context.SentMessages.Last().Text.Contains("attack=25", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Equipment commands should expose the equipped weapon bonus.");
    }

    GroundLoot commandLoot = context.GroundLoot.Spawn(
        new ItemDrop("slime-gel", 1),
        context.Connection.Session.MapId,
        context.Connection.Session.Position,
        context.Connection.Session.PlayerId,
        context.CurrentTime);
    await context.Handler.TryHandleAsync(context.Connection, new NetworkMessage(MessageType.Command, "/loot"));
    if (!context.SentMessages.Last().Text.Contains($"#{commandLoot.LootId} slime-gel x1", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("/loot should list nearby ground loot.");
    }

    await context.Handler.TryHandleAsync(
        context.Connection,
        new NetworkMessage(MessageType.Command, $"/pickup {commandLoot.LootId}"));
    if (context.SentMessages.Last().Text != "Picked up slime-gel x1." ||
        !context.Connection.Session.SnapshotInventory().Contains(new ItemStack("slime-gel", 1)))
    {
        throw new InvalidOperationException("/pickup should transfer owned ground loot into inventory.");
    }

    if (ItemCatalog.Find("iron-sword")?.Rarity != ItemRarity.Rare)
    {
        throw new InvalidOperationException("Item catalog should expose server-owned item rarity.");
    }
}

public static async Task RunCharacterPersistenceTestAsync()
{
    var source = new PlayerSession();
    source.Authenticate(900);
    source.MoveTo(new WorldPosition(7, 8));
    source.AddExperience(125);
    source.AddItem(new ItemDrop("iron-sword", 1));
    source.Equip("iron-sword");
    source.AddItem(new ItemDrop("slime-gel", 3));
    CharacterSaveData data = source.CreateSaveData();

    string directory = Path.Combine(Path.GetTempPath(), $"socket-study-{Guid.NewGuid():N}");
    string filePath = Path.Combine(directory, "characters.json");
    try
    {
        var writer = new JsonCharacterRepository(filePath);
        await writer.SaveAsync(data);
        var reader = new JsonCharacterRepository(filePath);
        CharacterSaveData? loadedData = await reader.LoadAsync(source.PlayerId);
        if (loadedData is null)
        {
            throw new InvalidOperationException("JSON character repository should reload a saved character.");
        }

        var restored = new PlayerSession();
        restored.Authenticate(source.PlayerId);
        restored.Restore(loadedData);
        if (restored.Position != source.Position ||
            restored.Experience != source.Experience ||
            restored.AttackPower != source.AttackPower ||
            !restored.SnapshotInventory().Contains(new ItemStack("slime-gel", 3)))
        {
            throw new InvalidOperationException("Character restore should recover world, progression, inventory, and equipment state.");
        }

        string sqlitePath = Path.Combine(directory, "characters.db");
        var sqlite = new SqliteCharacterRepository(sqlitePath);
        CharacterSaveData sqliteVersion1 = await sqlite.SaveAsync(data);
        CharacterSaveData? firstReader = await sqlite.LoadAsync(source.PlayerId);
        CharacterSaveData? staleReader = await sqlite.LoadAsync(source.PlayerId);
        if (firstReader is null || staleReader is null || sqliteVersion1.Version != 1)
        {
            throw new InvalidOperationException("SQLite repository should insert and reload version 1.");
        }

        CharacterSaveData sqliteVersion2 = await sqlite.SaveAsync(firstReader with { Experience = 200 });
        if (sqliteVersion2.Version != 2)
        {
            throw new InvalidOperationException("SQLite repository should increment the character save version.");
        }

        try
        {
            await sqlite.SaveAsync(staleReader with { Experience = 300 });
            throw new InvalidOperationException("SQLite repository should reject a stale character save.");
        }
        catch (CharacterConcurrencyException)
        {
            // Expected: stale version 1 cannot overwrite stored version 2.
        }

        CharacterSaveData? sqliteReloaded = await new SqliteCharacterRepository(sqlitePath)
            .LoadAsync(source.PlayerId);
        if (sqliteReloaded?.Version != 2 || sqliteReloaded.Experience != 200)
        {
            throw new InvalidOperationException("Rejected stale saves must not overwrite the committed SQLite state.");
        }
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    await using CommandHandlerTestContext context = await CommandHandlerTestContext.CreateAsync("alice");
    context.Connection.Session.Authenticate(901);
    context.Connection.Session.MoveTo(new WorldPosition(4, 5));
    context.Connection.Session.AddExperience(30);
    await context.Handler.TryHandleAsync(context.Connection, new NetworkMessage(MessageType.Command, "/save"));
    context.Connection.Session.MoveTo(WorldPosition.Origin);
    await context.Handler.TryHandleAsync(context.Connection, new NetworkMessage(MessageType.Command, "/load"));
    if (context.Connection.Session.Position != new WorldPosition(4, 5) ||
        context.Connection.Session.Experience != 30)
    {
        throw new InvalidOperationException("/save and /load should round-trip the authenticated character.");
    }

    var flakyRepository = new FlakyCharacterRepository(failureCount: 2);
    var saveService = new CharacterSaveService(flakyRepository);
    var dirtySession = new PlayerSession();
    dirtySession.Authenticate(902);
    dirtySession.MoveTo(new WorldPosition(2, 3));
    CharacterSaveOutcome retried = await saveService.SaveIfDirtyAsync(dirtySession);
    if (retried.Status != CharacterSaveStatus.Saved ||
        retried.Attempts != 3 ||
        dirtySession.IsDirty ||
        flakyRepository.SaveCalls != 3)
    {
        throw new InvalidOperationException("Character save service should retry transient failures and clear dirty state.");
    }

    CharacterSaveOutcome clean = await saveService.SaveIfDirtyAsync(dirtySession);
    if (clean.Status != CharacterSaveStatus.NotDirty || flakyRepository.SaveCalls != 3)
    {
        throw new InvalidOperationException("Clean sessions should skip redundant repository saves.");
    }

    dirtySession.MoveTo(new WorldPosition(3, 4));
    var autosave = new CharacterAutosaveLoop(() => [dirtySession], saveService, TimeSpan.FromSeconds(1));
    await autosave.SaveAllAsync(CancellationToken.None);
    if (dirtySession.IsDirty || dirtySession.SaveVersion != 2)
    {
        throw new InvalidOperationException("Autosave should persist dirty authenticated sessions.");
    }
}
}

sealed class CommandHandlerTestContext : IAsyncDisposable
{
    private readonly NetworkPair pair;

    private readonly CancellationTokenSource worldTickCancellation = new();

    private readonly Task worldTickTask;

    public ClientConnection Connection { get; }

    public ClientConnection TargetConnection { get; }

    public ChatCommandHandler Handler { get; }

    public List<SentMessage> SentMessages { get; } = new();

    public List<BroadcastMessage> BroadcastMessages { get; } = new();

    public List<string> BroadcastNotices { get; } = new();

    public List<string> NearbyNotices { get; } = new();

    public List<string> MovedRooms { get; } = new();

    public MonsterRegistry Monsters { get; } = new();

    public GroundLootRegistry GroundLoot { get; } = new();

    public InMemoryCharacterRepository Characters { get; } = new();

    public ServerLifecycle Lifecycle { get; } = new();

    public AuthenticationAttemptLimiter AuthenticationAttempts { get; }

    public InMemoryAccountRepository Accounts { get; } = new();

    public PasswordHasher PasswordHasher { get; } = new();
    public bool IsAdministrator { get; set; } = true;
    public SessionOwnershipRegistry SessionOwnership { get; } = new();

    public SessionTokenStore SessionTokens { get; }

    public string? DuplicateName { get; set; }

    public DateTimeOffset CurrentTime { get; set; } = DateTimeOffset.UnixEpoch;

    public DateTimeOffset ServerStartedAt { get; set; } = DateTimeOffset.UnixEpoch;

    private CommandHandlerTestContext(NetworkPair pair, string name)
    {
        this.pair = pair;
        Connection = new ClientConnection(name, pair.Client, pair.ClientStream);
        TargetConnection = new ClientConnection("bob", pair.Server, pair.ServerStream);

        MovementRequestQueue movementRequests = CreateMovementRequestQueue(out WorldTickProcessor worldTickProcessor);
        var attackRequests = new PlayerAttackRequestQueue();
        var combatTickProcessor = new CombatTickProcessor(attackRequests, Monsters, GroundLoot);
        var characterSaves = new CharacterSaveService(Characters);
        AuthenticationAttempts = new AuthenticationAttemptLimiter(
            WorldRules.AuthenticationBackoffBaseDelay,
            WorldRules.AuthenticationBackoffMaxDelay,
            WorldRules.AuthenticationFailureIdleRetention,
            () => CurrentTime);
        SessionTokens = new SessionTokenStore(
            WorldRules.SessionTokenLifetime,
            () => CurrentTime);
        Lifecycle.MarkRunning();
        worldTickTask = new WorldTickLoop(
            worldTickProcessor,
            TimeSpan.FromMilliseconds(1),
            serverTime => combatTickProcessor.Process(serverTime))
            .RunAsync(worldTickCancellation.Token);

        Handler = new ChatCommandHandler(
            SendToClientAsync,
            BroadcastNoticeAsync,
            BroadcastChatAsync,
            BroadcastNearbyNoticeAsync,
            () => ["alice", "bob"],
            () => ["lobby", "study"],
            roomName => roomName == "study" ? ["alice"] : [],
            _ => ["alice", "bob"],
            GetNearbyNames,
            GetNearbySnapshots,
            IsNameInUse,
            FindClientByName,
            MoveClientToRoomAsync,
            () => CurrentTime,
            () => ServerStartedAt,
            movementRequests,
            attackRequests,
            new WorldEventQueue(),
            _ => { },
            Monsters,
            GroundLoot,
            Characters,
            characterSaves,
            () => Lifecycle.State,
            AuthenticationAttempts,
            Accounts,
            PasswordHasher,
            SessionTokens,
            () => "Metrics: test",
            () => new ServerHealthReport(true, true, []),
            _ => IsAdministrator,
            SessionOwnership);
    }

    private static MovementRequestQueue CreateMovementRequestQueue(out WorldTickProcessor worldTickProcessor)
    {
        var movementRequests = new MovementRequestQueue();
        worldTickProcessor = new WorldTickProcessor(movementRequests);
        return movementRequests;
    }

    public static async Task<CommandHandlerTestContext> CreateAsync(string name)
    {
        NetworkPair pair = await NetworkPair.ConnectAsync();
        return new CommandHandlerTestContext(pair, name);
    }

    public Task<bool> CreateAccountAsync(long playerId, string password)
    {
        return Accounts.CreateAsync(PasswordHasher.Hash(playerId, password));
    }

    public async ValueTask DisposeAsync()
    {
        worldTickCancellation.Cancel();
        await worldTickTask;
        worldTickCancellation.Dispose();
        await pair.DisposeAsync();
    }

    private Task SendToClientAsync(ClientConnection connection, MessageType type, string text)
    {
        SentMessages.Add(new SentMessage(connection, type, text));
        return Task.CompletedTask;
    }

    private Task BroadcastChatAsync(ClientConnection connection, string text)
    {
        BroadcastMessages.Add(new BroadcastMessage(connection, text));
        return Task.CompletedTask;
    }

    private Task BroadcastNoticeAsync(string text)
    {
        BroadcastNotices.Add(text);
        return Task.CompletedTask;
    }

    private Task BroadcastNearbyNoticeAsync(ClientConnection connection, string text)
    {
        NearbyNotices.Add(text);
        return Task.CompletedTask;
    }

    private bool IsNameInUse(string name, ClientConnection currentConnection)
    {
        return string.Equals(name, DuplicateName, StringComparison.OrdinalIgnoreCase);
    }

    private ClientConnection? FindClientByName(string name)
    {
        return string.Equals(name, TargetConnection.Name, StringComparison.OrdinalIgnoreCase)
            ? TargetConnection
            : null;
    }

    private string[] GetNearbyNames(ClientConnection connection)
    {
        return TargetConnection.Session.IsSpawned &&
            connection.Session.MapId == TargetConnection.Session.MapId &&
            WorldRules.IsNearby(connection.Session.Position, TargetConnection.Session.Position)
            ? [TargetConnection.Name]
            : [];
    }

    private NearbySnapshotResult GetNearbySnapshots(ClientConnection connection)
    {
        NearbyPlayerSnapshot[] snapshots = TargetConnection.Session.IsSpawned &&
            connection.Session.MapId == TargetConnection.Session.MapId &&
            WorldRules.IsNearby(connection.Session.Position, TargetConnection.Session.Position)
            ? [new NearbyPlayerSnapshot(
                TargetConnection.Name,
                TargetConnection.Session.PlayerId,
                TargetConnection.Session.MapId,
                TargetConnection.Session.Position,
                WorldRules.GetDistance(connection.Session.Position, TargetConnection.Session.Position))]
            : [];

        return new NearbySnapshotResult(snapshots, snapshots.Length);
    }

    private Task MoveClientToRoomAsync(ClientConnection connection, string roomName)
    {
        MovedRooms.Add(roomName);
        connection.MoveToRoom(roomName);
        return Task.CompletedTask;
    }
}
