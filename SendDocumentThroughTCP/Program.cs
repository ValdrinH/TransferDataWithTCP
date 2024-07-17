using Microsoft.Extensions.DependencyInjection;
using SendDocumentThroughTCP;
using System.Collections.Concurrent;
using System.Net.Sockets;

class Program
{
    static async Task Main(string[] args)
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var server = serviceProvider.GetService<IServerListener>();
        await server.StartAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var clients = new ConcurrentDictionary<string, TcpClient>();

        services.AddSingleton(clients);
        services.AddSingleton<IServerListener, ServerListener>();
    }
}