﻿using System.Text.Json;
using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Protocol.Transport;
using McpDotNet.Protocol.Types;
using McpDotNet.Tests.Utils;
using Microsoft.Extensions.Logging;

namespace McpDotNet.Tests;

public class SseIntegrationTests
{
    [Fact]
    public async Task ConnectAndReceiveMessage_InMemoryServer()
    {
        // Arrange
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        await using InMemoryTestSseServer server = new(logger: loggerFactory.CreateLogger<InMemoryTestSseServer>());
        await server.StartAsync();


        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        var defaultConfig = new McpServerConfig
        {
            Id = "test_server",
            Name = "In-memory Test Server",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = "http://localhost:5000/sse"
        };

        // Act
        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);

        // Wait for SSE connection to be established
        await server.WaitForConnectionAsync(TimeSpan.FromSeconds(10));

        // Send a test message through POST endpoint
        await client.SendNotificationAsync("test/message", new { message = "Hello, SSE!" });

        // Assert
        Assert.True(true);
    }

    [Fact]
    [Trait("Execution", "Manual")]
    public async Task ConnectAndReceiveMessage_EverythingServerWithSse()
    {
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        int port = 3001;

        await using var fixture = new EverythingSseServerFixture(port);
        await fixture.StartAsync();

        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        var defaultConfig = new McpServerConfig
        {
            Id = "everything",
            Name = "Everything",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = $"http://localhost:{port}/sse"
        };

        // Create client and run tests
        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);
        var tools = await client.ListToolsAsync().ToListAsync();

        // assert
        Assert.NotEmpty(tools);
    }

    [Fact]
    [Trait("Execution", "Manual")]
    public async Task Sampling_Sse_EverythingServer()
    {
        // arrange
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        int port = 3002;

        await using var fixture = new EverythingSseServerFixture(port);
        await fixture.StartAsync();

        var defaultConfig = new McpServerConfig
        {
            Id = "everything",
            Name = "Everything",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = $"http://localhost:{port}/sse"
        };

        int samplingHandlerCalls = 0;
        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new()
            {
                Name = "IntegrationTestClient",
                Version = "1.0.0"
            },
            Capabilities = new()
            {
                Sampling = new()
                {
                    SamplingHandler = (_, _) =>
                    {
                        samplingHandlerCalls++;
                        return Task.FromResult(new CreateMessageResult
                        {
                            Model = "test-model",
                            Role = "assistant",
                            Content = new Content
                            {
                                Type = "text",
                                Text = "Test response"
                            }
                        });
                    },
                },
            },
        };

        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);

        // Call the server's sampleLLM tool which should trigger our sampling handler
        var result = await client.CallToolAsync(
            "sampleLLM",
            new Dictionary<string, object>
            {
                ["prompt"] = "Test prompt",
                ["maxTokens"] = 100
            }
        );

        // assert
        Assert.NotNull(result);
        var textContent = Assert.Single(result.Content);
        Assert.Equal("text", textContent.Type);
        Assert.False(string.IsNullOrEmpty(textContent.Text));
    }

    [Fact]
    public async Task ConnectAndReceiveMessage_InMemoryServer_WithFullEndpointEventUri()
    {
        // Arrange
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        await using InMemoryTestSseServer server = new(logger: loggerFactory.CreateLogger<InMemoryTestSseServer>());
        server.UseFullUrlForEndpointEvent = true;
        await server.StartAsync();


        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        var defaultConfig = new McpServerConfig
        {
            Id = "test_server",
            Name = "In-memory Test Server",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = "http://localhost:5000/sse"
        };

        // Act
        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);

        // Wait for SSE connection to be established
        await server.WaitForConnectionAsync(TimeSpan.FromSeconds(10));

        // Send a test message through POST endpoint
        await client.SendNotificationAsync("test/message", new { message = "Hello, SSE!" });

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task ConnectAndReceiveNotification_InMemoryServer()
    {
        // Arrange
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        await using InMemoryTestSseServer server = new(logger: loggerFactory.CreateLogger<InMemoryTestSseServer>());
        await server.StartAsync();


        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        var defaultConfig = new McpServerConfig
        {
            Id = "test_server",
            Name = "In-memory Test Server",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = "http://localhost:5000/sse"
        };

        // Act
        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);

        // Wait for SSE connection to be established
        await server.WaitForConnectionAsync(TimeSpan.FromSeconds(10));

        var receivedNotification = new TaskCompletionSource<string?>();
        client.AddNotificationHandler("test/notification", (args) =>
            {
                var msg = ((JsonElement?)args.Params)?.GetProperty("message").GetString();
                receivedNotification.SetResult(msg);

                return Task.CompletedTask;
            });

        // Act
        await server.SendTestNotificationAsync("Hello from server!");

        // Assert
        var message = await receivedNotification.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Hello from server!", message);
    }

    [Fact]
    public async Task ConnectTwice_Throws()
    {
        // Arrange
        using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddConsole()
            .SetMinimumLevel(LogLevel.Debug));

        await using InMemoryTestSseServer server = new(logger: loggerFactory.CreateLogger<InMemoryTestSseServer>());
        await server.StartAsync();


        var defaultOptions = new McpClientOptions
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        var defaultConfig = new McpServerConfig
        {
            Id = "test_server",
            Name = "In-memory Test Server",
            TransportType = TransportTypes.Sse,
            TransportOptions = [],
            Location = "http://localhost:5000/sse"
        };

        // Act
        var client = await McpClientFactory.CreateAsync(defaultConfig, defaultOptions, loggerFactory: loggerFactory);
        var mcpClient = (McpClient)client;
        var transport = (SseClientTransport)mcpClient.Transport;

        // Wait for SSE connection to be established
        await server.WaitForConnectionAsync(TimeSpan.FromSeconds(10));

        // Assert
        await Assert.ThrowsAsync<McpTransportException>(async () => await transport.ConnectAsync());
    }
}
