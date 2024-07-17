using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SendDocumentThroughTCP
{
    public class ServerListener : IServerListener
    {
        private static ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 2108);
            listener.Start();
            Console.WriteLine("Server is running on port 9000...");

            try
            {
                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[8192];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string clientId = Encoding.UTF8.GetString(buffer, 0, bytesRead);


            if (!clients.TryAdd(clientId, client))
            {
                Console.WriteLine($"Client ID {clientId} already connected.");
                client.Close();
                return;
            }

            Console.WriteLine($"Client {clientId} connected.");

            try
            {
                while (true)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (message.StartsWith("FILE:"))
                    {
                        string fileMessage = message.Substring("FILE:".Length); // Largo prefixin "FILE:"
                        string[] parts = fileMessage.Split('|');

                        if (parts.Length != 4)
                        {
                            Console.WriteLine($"Invalid file header received from client {clientId}.");
                            continue;
                        }

                        string targetClientId = parts[0];
                        string fileName = parts[1];
                        string fileExtension = parts[2]; // Merr extensionin e file-it
                        int fileSize = int.Parse(parts[3]);

                        if (clients.TryGetValue(targetClientId, out TcpClient targetClient))
                        {
                            NetworkStream targetStream = targetClient.GetStream();

                            // Përgatisni buffer-in për dërgimin e dokumentit me extension
                            byte[] headerBytes = Encoding.UTF8.GetBytes(message);
                            await targetStream.WriteAsync(headerBytes, 0, headerBytes.Length);

                            byte[] fileBuffer = new byte[fileSize];
                            bytesRead = await stream.ReadAsync(fileBuffer, 0, fileBuffer.Length);
                            await targetStream.WriteAsync(fileBuffer, 0, bytesRead);

                            Console.WriteLine($"Document sent from {clientId} to {targetClientId}.");
                        }
                        else
                        {
                            Console.WriteLine($"Target client {targetClientId} not found.");
                        }
                    }
                    else if (message == "GET_CLIENT_IDS")
                    {
                        string clientIds = "CLIENT_IDS:" + string.Join(",", clients.Keys);
                        byte[] clientIdsBytes = Encoding.UTF8.GetBytes(clientIds);
                        await stream.WriteAsync(clientIdsBytes, 0, clientIdsBytes.Length);
                    }
                    else
                    {
                        Console.WriteLine($"Received unexpected message from client {clientId}: {message}");
                    }
                }
            }
            finally
            {
                clients.TryRemove(clientId, out _);
                client.Close();
                Console.WriteLine($"Client {clientId} disconnected.");
            }
        }
    }
}
