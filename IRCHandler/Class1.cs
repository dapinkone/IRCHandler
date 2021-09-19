using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IRCHandler
{
    public class IRCHandler
    {
        public TcpClient tcpClient;
        public NetworkStream netStream;
        public StreamReader reader;
        public StreamWriter writer;
        public string targetAddr;
        public int port;
        private string nick;
        private string username;
        private string password;
        private Boolean SSLflag;
        private List<Callback> Callbacks; // reactions to things observed on the socket.
        public struct Callback
        {
            public string T { get; }
            public Action<string, string, string> F;
            public Callback(string type, Action<string, string, string> f)
            {
                T = type.ToLower();
                F = f;
            }
        }
        // privmsg: user, target, message
        // notice: user, target, message
        // join: user, target
        // part: user, target, message
        // quit: user, message

        public IRCHandler(string targetAddr, int port, string nick, string username, string password="", Boolean SSLflag=false)
        {
            this.targetAddr = targetAddr;
            this.port = port;
            this.nick = nick;
            this.username = username;
            this.password = password;
            this.Callbacks = new List<Callback>();
            this.SSLflag = SSLflag;
        }

        public void Connect()
        {
            this.tcpClient = new(targetAddr, port);
            this.netStream = tcpClient.GetStream();
            //if (false)
            //{
            //    // The following method is invoked by the RemoteCertificateValidationDelegate.
            //    bool ValidateServerCertificate(
            //          object sender,
            //          X509Certificate certificate,
            //          X509Chain chain,
            //          SslPolicyErrors sslPolicyErrors)
            //    {
            //        //// --- We DGAF.
            //        return true;
            //        //if (sslPolicyErrors == SslPolicyErrors.None)
            //            //return true;
                        
            //        //Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            //        // Do not allow this client to communicate with unauthenticated servers.
            //        //return false;
            //    }

            //    var SSLStream = new System.Net.Security.SslStream(this.netStream, true,
            //        userCertificateValidationCallback: ValidateServerCertificate );
            //    //SSLStream.AuthenticateAsClient()
            //    this.reader = new StreamReader(SSLStream);
            //    this.writer = new StreamWriter(SSLStream);
            //}
            //else
            //{
                this.reader = new(netStream, Encoding.UTF8);
            //this.writer = new(netStream, Encoding.UTF8, leaveOpen: true);
            //}
            //Console.WriteLine("Socket connected to {0}", targetAddr);
            if (!password.Equals(""))
            {
                SendLine($"PASS {password}");
            }
            SendLine($"USER {nick} {username} {username} {username}");

            SendLine($"Nick {nick}");
            //if(password != "")            SendLine($"PASS {password}");
            //Join("#thevoid");
            // call onConnect()

            while (!reader.EndOfStream)
            {

                string line = reader.ReadLine();
                Console.WriteLine(line);

                if (line.StartsWith("PING"))
                {
                    SendLine("PONG" + line[4..]);
                    continue;
                }

                // parse out the IRC protocol.
                // We need 4 things: linetype(privmsg/quit/part/notice/etc), user, target, message.
                string linetype;
                string user, target, message;

                // basic message formats:
                //:DaPinkOne!~Dap@user/dap NOTICE dapbot :test notice
                //:DaPinkOne!~Dap@user/dap PRIVMSG dapbot :test pm
                //:DaPinkOne!~Dap@user/dap PRIVMSG #thevoid :channel hello
                //:DaPinkOne!~Dap@user/dap PART #thevoid :testing part
                //:DaPinkOne!~Dap@user/dap JOIN #thevoid

                //TODO: Add support for CTCP, ACTION, /topic, PM, MODE
                //:DaPinkOne!~Dap@user/dap PRIVMSG dapbot :☺TESTING ctcp☺
                //:DaPinkOne!~Dap@user/dap PRIVMSG #thevoid :☺ACTION tests /me☺
                //:DaPinkOne!~Dap@user/dap TOPIC #thevoid :test topic
                //:ChanServ!ChanServ@services.libera.chat MODE #thevoid +o DaPinkOne
                string[] bits = line[1..].Split(" ");
                if (!line.StartsWith(":")) continue; // not a type we're currently worried about.
                else linetype = bits[1].ToLower();

                user = bits[0].Split("!")[0];
                target = bits[2];
                if (linetype.Equals("join"))
                {
                    message = "";
                }
                else
                {
                    message = String.Join(" ", bits[3..]);
                }
                if (message.StartsWith(":")) message = message[1..];
                // execute all the callbacks for their corresponding message type.
                foreach (Callback c in Callbacks.FindAll(e => e.T.Equals(linetype)))
                {
                    c.F(user, target, message);
                }
            }
            Console.WriteLine("Disconnected.");
            netStream.Close();
        }
        public void AddCallback(string t, Action<string, string, string> f)
        {
            //usage: ircClient.addCallback("privmsg", (user, target, msg) => { ... }});
            Callbacks.Add(new Callback(t, f));
        }
        public void Mode(string channel, string msg)
        {
            SendLine($"MODE {channel} {msg}");
        }
        public void Privmsg(string target, string msg)
        {
            SendLine($"PRIVMSG {target} :{msg}");
        }
        public void Notice(string target, string msg)
        {
            SendLine($"NOTICE {target} :{msg}");
        }
        public void Join(string channel)
        {
            SendLine($"JOIN {channel}");
        }
        public void Part(string channel, string msg = "")
        {
            SendLine($"PART {channel} :{msg}");
        }
        public void Quit(string msg = "")
        {
            SendLine($"QUIT :{msg}");
        }
        public void Topic(string target, string msg = "")
        {
            SendLine($"TOPIC {target} :{msg}");
        }


        private void SendLine(string msg)
        {
            if (netStream.CanWrite)
            {
                Byte[] sendBytes = Encoding.UTF8.GetBytes(msg + "\r\n");
                netStream.Write(sendBytes, 0, sendBytes.Length);
                Console.WriteLine($">>> {msg}");
            }
            else
            {
                //Console.WriteLine("You cannot write data to this stream.");
                tcpClient.Close();

                // Closing the tcpClient instance does not close the network stream.
                netStream.Close();
                return;
            }
        }

    }
}
