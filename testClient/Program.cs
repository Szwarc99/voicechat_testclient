using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace testClient
{
    class Program
    {
        public static class WinApi
        {
            /// <summary>TimeBeginPeriod(). See the Windows API documentation for details.</summary>

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
            [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]

            public static extern uint TimeBeginPeriod(uint uMilliseconds);

            /// <summary>TimeEndPeriod(). See the Windows API documentation for details.</summary>

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1401:PInvokesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage"), SuppressUnmanagedCodeSecurity]
            [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]

            public static extern uint TimeEndPeriod(uint uMilliseconds);
        }
        static void Main(string[] args)
        {
            TcpClient client;
            NetworkStream stream;

            IPEndPoint ipLocalEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                new Random().Next(9300, 9400));
            client = new TcpClient(ipLocalEndPoint);
            client.Connect(IPAddress.Parse("127.0.0.1"), 8080);
            stream = client.GetStream();
            CommProtocol.init(stream);

            if (client.Connected)
            {
                CommProtocol.sendKey(CommProtocol.aes);
            }
            int udpPort = new Random().Next(9100, 9200);
            CommProtocol.read();
            Console.WriteLine(udpPort);            
            CommProtocol.write("con user" + udpPort);            
            Console.WriteLine(CommProtocol.read());
            CommProtocol.write("crm false ");
            Console.WriteLine(CommProtocol.read());            
            CommProtocol.write("jrm 0 user"+ udpPort + " " + udpPort);
            

            //wav file to be cut and played
            var audioFilePath = "C:\\Users\\Piotrek\\Documents\\Audacity\\test2.wav";            
            var source = new AudioFileReader(audioFilePath);

            
            int framesToPlay = 30 * 32000;  // 30s
            byte[] audio1 = new byte[framesToPlay];
            
            source.ToWaveProvider16().Read(audio1, 0, framesToPlay);           


            UdpClient udpClient = new UdpClient(udpPort);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8100);
            udpClient.Connect(serverEP);

            void SendUDP(byte[] bytes)
            {
                Console.WriteLine("bytes " + bytes.Length);
                var bytes2 = CommProtocol.EncryptUDP(bytes, CommProtocol.aes.Key, CommProtocol.aes.IV);
                Console.WriteLine("bytes2 " + bytes2.Length);
                udpClient.Send(bytes2, bytes2.Length);
            }
            byte[] ReceiveUDP()
            {
                IPEndPoint ep = new IPEndPoint(0, 0);
                var bytes = CommProtocol.DecryptUDP(udpClient.Receive(ref ep), CommProtocol.aes.Key, CommProtocol.aes.IV);
                return bytes;
            }

            //Mutex used = new Mutex();
            //Semaphore nonEmpty = new Semaphore(0, 1);
            //Queue<byte[]> queue = new Queue<byte[]>();

            //void SendUDP(byte[] bytes)
            //{
            //    used.WaitOne();
            //    queue.Enqueue(bytes);
            //    if (queue.Count == 1)
            //    {
            //        nonEmpty.Release();
            //    }
            //    used.ReleaseMutex();
            //}

            //byte[] ReceiveUDP()
            //{
            //    nonEmpty.WaitOne();
            //    nonEmpty.Release();

            //    used.WaitOne();
            //    var data = queue.Dequeue();
            //    if(queue.Count == 0)
            //    {
            //        nonEmpty.WaitOne();
            //    }
            //    used.ReleaseMutex();
            //    return data;
            //}

            System.Collections.Generic.IEnumerable<byte[]> CutTo10ms(byte[] bytes)
            {
                int counter2 = 0;
                while (true)
                {
                    Console.WriteLine("Playback started");
                    for (int i = 0; i < bytes.Length; i += 320)
                    {
                        byte[] index = BitConverter.GetBytes(counter2++);
                        byte[] sample = new byte[324];
                        Array.Copy(index, sample, 4);
                        Array.Copy(bytes, i, sample, 4, 320);
                        yield return sample;
                    }
                }
            }

            int counter = 0;
            int playbackCounter = 0;
            Stopwatch stopwatch = new Stopwatch();

            Thread sendingThread = new Thread(unused =>
            {
                while (true)
                {

                    foreach (byte[] elem in CutTo10ms(audio1))
                    {
                        long nextTime = counter * 10;
                        WinApi.TimeBeginPeriod(1);
                        while (stopwatch.ElapsedMilliseconds < nextTime)
                        {                            
                            Thread.Sleep(1);
                        }
                        WinApi.TimeEndPeriod(1);
                        SendUDP(elem);
                        counter++;
                    }
                }
            });

            Dictionary<int, byte[]> buffer = new Dictionary<int, byte[]>();
            int serverOffset = 0;
            double avgTimeAhead = 0.0;

            bool fresh = true;
            Thread receivingThread = new Thread(unused =>
            {
                while (true)
                {
                    var data = ReceiveUDP();
                    byte[] audio = new byte[320];
                    Array.Copy(data, 4, audio, 0, 320);
                    var index = BitConverter.ToInt32(data, 0);

                    lock (buffer)
                    {
                        if (fresh)
                        {
                            serverOffset = playbackCounter - index;
                            avgTimeAhead = playbackCounter * 10 - stopwatch.ElapsedMilliseconds;
                            fresh = false;
                        }
                        int targetFrame = serverOffset + index;
                        double timeAhead = Math.Max(-50, targetFrame * 10 - stopwatch.ElapsedMilliseconds);
                        avgTimeAhead = 0.99 * avgTimeAhead + 0.01 * timeAhead;
                        //Console.WriteLine("target: " + targetFrame);
                        //Console.WriteLine("offset: " + serverOffset);
                        //Console.WriteLine("we have this much time: " + timeAhead);
                        //Console.WriteLine("avg time: " + avgTimeAhead);

                        if (avgTimeAhead < 10.0)
                        {
                            serverOffset = playbackCounter - index;
                            targetFrame = playbackCounter;
                            avgTimeAhead = playbackCounter * 10 - stopwatch.ElapsedMilliseconds;
                        }
                        if (avgTimeAhead > 30.0)
                        {
                            serverOffset--;
                            targetFrame--;
                            avgTimeAhead -= 10.0;
                        }
                        if (targetFrame >= playbackCounter)
                        {
                            buffer[targetFrame] = audio;
                        }
                    }
                }
            });

            BufferedWaveProvider bufferedWave = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));

            WaveOut received = new WaveOut();
            //Console.WriteLine("Latency: " + received.DesiredLatency);
            //received.DesiredLatency = 100;
            received.Init(bufferedWave);

            Thread playingThread = new Thread(unused =>
            {

                received.Play();
                while (true)
                {
                    System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.RealTime;
                    long nextTime = playbackCounter * 10;
                    WinApi.TimeBeginPeriod(1);
                    while (stopwatch.ElapsedMilliseconds < nextTime)
                    {
                        Thread.Sleep(1);                        
                    }
                    //Console.WriteLine("woke up late by: " + (stopwatch.ElapsedMilliseconds - nextTime));
                    WinApi.TimeEndPeriod(1);
                    /*WinApi.TimeBeginPeriod(1);
                    Thread.Sleep(1);
                    WinApi.TimeEndPeriod(1);*/
                    lock (buffer)
                    {
                        //Console.WriteLine("playing: " + playbackCounter + " " + buffer.TryGetValue(playbackCounter, out _) + " " + buffer.Count);
                        if (buffer.TryGetValue(playbackCounter, out byte[] sample))
                        {
                            bufferedWave.AddSamples(sample, 0, 320);
                            buffer.Remove(playbackCounter);
                        }
                        playbackCounter++;
                    }
                }
            });

            stopwatch.Start();
            sendingThread.Start();
            receivingThread.Start();
            playingThread.Start();

            playingThread.Join();
        }
    }
}
