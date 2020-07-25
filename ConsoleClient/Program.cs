using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var tcpClient = new TcpClient("bylins.su", 4000);
            Task.Run(() => ReadDataLoop(tcpClient));
            Task.Run(() => WriteDataLoop(tcpClient));
            
            while (true)
            { 
                Thread.Sleep(1000);
            }
        }

        private static async Task ReadDataLoop(TcpClient client)
        {
            while (true)
            {
                if (!client.Connected)
                    return;

                string xxx = await ReadData(client);
                Console.WriteLine(xxx);
            }
        }

        private static async Task<string> ReadData(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            byte[] myReadBuffer = new byte[1024];

            await using (var ms = new MemoryStream())
            {
                do
                {
                    var numberOfBytesRead = await stream.ReadAsync(myReadBuffer, 0, myReadBuffer.Length);
                    await ms.WriteAsync(myReadBuffer, 0, numberOfBytesRead);
                } while (stream.DataAvailable);

                var resultArray = ms.ToArray();
                int count = resultArray.Length;
                // Catch 0xFF 0xF9 "Go ahead" command
                if (count >= 2)
                {
                    if (resultArray[^1] == 249
                        && resultArray[^2] == 255)
                    {
                        count -= 2;
                    }
                }

                return Encoding.Default.GetString(resultArray, 0, count);
            }
        }

        private static async Task WriteDataLoop(TcpClient client)
        {
            while (true)
            {
                var xxx = Console.ReadLine();
                xxx += Environment.NewLine;
                var bytes = Encoding.Default.GetBytes(xxx);

                await client.GetStream().WriteAsync(bytes, 0, bytes.Length);
            }
        }
    }
}
