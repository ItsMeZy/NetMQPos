using NetMQ;
using NetMQ.Sockets;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netmq_demo.Client
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

  class Program
  {
    static string serverUrl = "tcp://127.0.0.1:5556";
    static Random rnd = new Random();
    const int maxValue = 10000;
    const int minValue = 5000;
    static DealerSocket savedState = null;
    // Container of every thread
    static ThreadLocal<DealerSocket> clientSocketPerThread = new ThreadLocal<DealerSocket>();

    static void Main(string[] args)
    {
      
      // Manager of the events
      var poller = new NetMQPoller();

      // Start 3 thread to make request to the server 
      for (int i = 0; i < 3; i++)
      {
        // Factory of thread
        Task.Factory.StartNew(state =>
        {
          // Creating a socket
          DealerSocket client = null;
          if (!clientSocketPerThread.IsValueCreated)
          {
            // Creating a new socket
            client = new DealerSocket();
            client.Options.Identity = Encoding.Unicode.GetBytes(state.ToString());
            client.Connect(serverUrl);
            client.ReceiveReady += Client_ReceiveReady;
            clientSocketPerThread.Value = client;
            poller.Add(client);
            savedState = client;
          }
          else
          {
            // Get the socket already created
            client = clientSocketPerThread.Value;
          }

          // Preparing the message
          var messageToServer = new NetMQMessage();
          messageToServer.AppendEmptyFrame();
          messageToServer.Append(Convert.ToInt32(state));
          Console.WriteLine("======================================");
          Console.WriteLine(" OUTGOING MESSAGE TO SERVER ");
          Console.WriteLine("======================================");
          PrintFrames("Client Sending", messageToServer);
          client.SendMultipartMessage(messageToServer);
        }, rnd.Next(minValue, maxValue), TaskCreationOptions.LongRunning);
      }

      // Runs the poller in the foreground thread
      poller.RunAsync();
      
      // Client cycle
      while (true)
      {
        // Do something...
      }

      // Eventualy dispose the poller
      poller.Dispose();
    }

    /// <summary>
    /// Print the frames contained in message paramenter
    /// </summary>
    /// <param name="operationType"> Name of the type of operation</param>
    /// <param name="message"> The message to print</param>
    private static void PrintFrames(string operationType, NetMQMessage message)
    {
      Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, 0, message[0].ConvertToString());
      Console.WriteLine("{0} Socket : Frame[{1}] = {2}", operationType, 1, message[1].ConvertToInt32());
    }

    /// <summary>
    /// Callback called when the client recive a message
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void Client_ReceiveReady(object sender, NetMQ.NetMQSocketEventArgs e)
    {
      bool hasmore = true;
      if (hasmore)
      {
        string result = e.Socket.ReceiveFrameString(out hasmore);
        if (!string.IsNullOrWhiteSpace(result))
        {
          if (result == Signal.OK.ToString() || result == Signal.ERROR.ToString())
          {
            Console.WriteLine("SIGNAL RECIVED:" + result);
          }
          else
          {
            // Job income
            Console.WriteLine("======================================");
            Console.WriteLine(" INCOMING JOB FROM SERVER ");
            Console.WriteLine("Reverse := "+result);
            Console.WriteLine("======================================");
            
            result = ReverseString(result);
            

            // Preparing the message
            var messageToServer = new NetMQMessage();
            messageToServer.AppendEmptyFrame();
            messageToServer.Append(result);
            savedState.SendMultipartMessage(messageToServer);

          }
        }
      }
    }

    private static string ReverseString(string phrase)
    {
      char[] phraseArr = phrase.ToCharArray();
      Array.Reverse(phraseArr);
      return new string(phraseArr);
    }

  }
}
