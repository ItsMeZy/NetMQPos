using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Netmq_demo.Server
{
  class Job
  {
    public Job(string phrase)
    {
      Phrase = phrase;
    }
    public string Phrase { get; set; }
  }

  enum Signal { ERROR = 0, OK = 1 }

  class Client : IComparable<Client>, IEquatable<Client>
  {
    public int NumElaboration { get; set; }
    public NetMQFrame Identifier { get; set; }

    /// <summary>
    /// Constructor of the class Client
    /// </summary>
    /// <param name="id">Frame that identified the client</param>
    /// <param name="n">NumElaboration will be initialized by this value</param>
    public Client(NetMQFrame id, int n = 0)
    {
      NumElaboration = n;
      Identifier = id;
    }

    /// <summary>
    /// Order by NumElaboration (int)
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public int CompareTo([AllowNull] Client other)
    {
      if (other == null)
        return 1;
      else
        return this.NumElaboration.CompareTo(other.NumElaboration);
    }

    public bool Equals([AllowNull] Client other)
    {
      if (other == null)
        return false;
      else if (this.Identifier == other.Identifier)
      {
        return true;
      }
      return false;
    }
  }

  class Program
  {
    static string serverUrl = "@tcp://127.0.0.1:5556";
    static List<Client> clients = new List<Client>();
    static void Main(string[] args)
    {
      // Defining
      using (var server = new RouterSocket(serverUrl)) // Initializzation of the server which listen to localhost:5556 
      {
        using (var queue = new NetMQQueue<Job>()) // Queue that contains the id of every client connected
        {
          using (var poller = new NetMQPoller { queue }) // Componenet that manage the events ReceiveReady
          {
            queue.ReceiveReady += (sender, e) =>
            {
              // Job has enqueued
              // Elaboration of the job starts!

              string phrase = e.Queue.Dequeue().Phrase;
              NetMQFrame clientAddress = clients.First().Identifier;

              // Prepare the message to send to client which can be also an object (each element of the object in a frame)
              var messageToClient = new NetMQMessage();
              messageToClient.Append(clientAddress); // address of the client
              messageToClient.AppendEmptyFrame(); // delimiter
              messageToClient.Append(phrase);
              server.SendMultipartMessage(messageToClient);
            };

            // Runs the poller in the foreground thread
            poller.RunAsync();

            Task.Factory.StartNew(() =>
            {
              // Server cycle
              while (true)
              {
                Console.WriteLine("LISTENING...");
                var clientMessage = server.ReceiveMultipartMessage(); // Wait for request to arrive
                Console.WriteLine("======================================");
                Console.WriteLine(" INCOMING CLIENT MESSAGE FROM CLIENT ");
                Console.WriteLine("======================================");

                if (clientMessage.FrameCount == 3) // Valid message
                {
                  var clientAddress = clientMessage[0];
                  Client client = new Client(clientAddress);
                  if (!clients.Any(item => item.Equals(client)))
                  {
                    PrintFrames("Server receiving", clientMessage);
                    clients.Add(client); // Save address of the client

                    // Prepare the message to send to client (ACK)
                    var messageToClient = new NetMQMessage();
                    messageToClient.Append(clientAddress);
                    messageToClient.AppendEmptyFrame();
                    messageToClient.Append(Signal.OK.ToString());
                    server.SendMultipartMessage(messageToClient);

                  }
                  else
                  {
                    // It must be a response from a job

                    Console.WriteLine("Socket := {0}", clientMessage[2].ConvertToString());
                  }
                }
              }
            });

            // Main Thread for input job
            while (true)
            {
              Console.WriteLine("Enter new job:");
              string input = Console.ReadLine();

              queue.Enqueue(new Job(input));

            }
          }
        }
      }
    }

    /// <summary>
    /// Print the frames contained in message paramenter
    /// </summary>
    /// <param name="operationType"> Name of the type of operation</param>
    /// <param name="message"> The message to print</param>
    private static void PrintFrames(string operationType, NetMQMessage message)
    {
      Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, 0, message[0].ConvertToString());
      Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, 1, message[1].ConvertToString());
      Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, 2, message[2].ConvertToInt32());
    }

    /// <summary>
    /// Perform a delay
    /// </summary>
    /// <param name="delay"> time in milliseconds</param>
    private static void PerformOperation(int delay)
    {
      Console.WriteLine("Perfoming task...");
      Task.Delay(delay).Wait();
      Console.WriteLine("Task completed");
    }
  }
}
