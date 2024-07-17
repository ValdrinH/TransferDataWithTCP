using System.Net.Sockets;
using System.Text;

class Program
{
    [STAThread]
    static async Task Main(string[] args)
    {
        TcpClient client = null;
        try
        {
            Console.WriteLine("Enter your client ID:");
            string clientId = Console.ReadLine();

            using (client = new TcpClient("127.0.0.1", 2108))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] clientIdBytes = Encoding.UTF8.GetBytes(clientId);
                await stream.WriteAsync(clientIdBytes, 0, clientIdBytes.Length);

                //Create a new Thread for the Method for running in backgorund
                Task receiveTask = Task.Run(() => ReceiveFilesContinuously(stream));

            exitLoop:
                Console.WriteLine("Do you want to (1) send a file or (2) get client IDs or any key to Exit ?");
                string choice = Console.ReadLine();
                if (choice == "1")
                {
                    Console.WriteLine("Enter the client ID to send the file to:");
                    string targetClientId = Console.ReadLine();

                toFile:
                    Console.WriteLine("Enter the file path to send:");
                    string filePath = string.Empty;
                    var thread = new Thread(() => filePath = ChooseFile());
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();

                    if (string.IsNullOrEmpty(filePath))
                    {
                        Console.WriteLine("Invalid file path or file not selected.");
                        goto toFile;
                    }
                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"File '{filePath}' does not exist.");
                        goto toFile;
                    }

                    string fileName = Path.GetFileName(filePath);
                    string fileExtension = Path.GetExtension(filePath).TrimStart('.');
                    byte[] fileData = File.ReadAllBytes(filePath);
                    string header = $"FILE:{targetClientId}|{fileName}|{fileExtension}|{fileData.Length}";
                    byte[] headerBytes = Encoding.UTF8.GetBytes(header);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);

                    await stream.WriteAsync(fileData, 0, fileData.Length);
                    Console.WriteLine("Document sent.");
                    goto exitLoop;

                }
                else if (choice == "2")
                {
                    string request = "GET_CLIENT_IDS";
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    await Task.Delay(2000);
                    goto exitLoop;
                }
                else
                {
                    Console.WriteLine("Disconnected from server.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Console.ReadKey();
            client?.Close();
        }
        Console.ReadKey();

    }
    static string ChooseFile()
    {
        string filePath = string.Empty;
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.InitialDirectory = "c:\\";
            openFileDialog.Filter = "All files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
            }
        }
        return filePath;
    }

    private static async Task ReceiveFilesContinuously(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0)
            {
                Console.WriteLine("Connection closed by server.");
                break;
            }

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (message.StartsWith("FILE:"))
            {
                string fileMessage = message.Substring("FILE:".Length); // Largo prefixin "FILE:"
                string[] parts = fileMessage.Split('|');

                if (parts.Length != 4)
                {
                    Console.WriteLine($"Invalid file header received.");
                    continue;
                }

                string senderClientId = parts[0];
                string fileName = parts[1];
                string fileExtension = parts[2];
                int fileSize = int.Parse(parts[3]);
                byte[] fileBuffer = new byte[fileSize];

                int totalBytesRead = 0;
                while (totalBytesRead < fileSize)
                {
                    bytesRead = await stream.ReadAsync(fileBuffer, totalBytesRead, fileSize - totalBytesRead);
                    totalBytesRead += bytesRead;
                }

                DialogResult dialogResult = MessageBox.Show($"Document received from {senderClientId}. Do you want to accept it?", "Document received", MessageBoxButtons.YesNo);
                if (dialogResult != DialogResult.Yes)
                {
                    MessageBox.Show("Save operation cancelled. Document not saved.", "Save cancelled", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }

                string receivedFilePath = string.Empty;

                var thread = new Thread(() =>
                {
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                    {
                        saveFileDialog.FileName = $"{fileName}_{senderClientId}_{DateTime.Now.Ticks}.{fileExtension}";
                        saveFileDialog.Filter = "All files (*.*)|*.*";
                        saveFileDialog.Title = "Save received document";

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            receivedFilePath = saveFileDialog.FileName;
                        }
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();

                if (!string.IsNullOrEmpty(receivedFilePath))
                {
                    await File.WriteAllBytesAsync(receivedFilePath, fileBuffer);
                    MessageBox.Show($"Document saved to {receivedFilePath}.", "Document saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Save operation cancelled. Document not saved.", "Save cancelled", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

            }
            else if (message.StartsWith("CLIENT_IDS:"))
            {
                string clientIdsMessage = message.Substring("CLIENT_IDS:".Length); // Largo prefixin "CLIENT_IDS:"
                Console.WriteLine("Connected clients: " + clientIdsMessage);
            }
            else
            {
                Console.WriteLine($"Received unexpected message: {message}");
            }
        }
    }
}
