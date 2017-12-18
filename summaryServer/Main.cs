using System;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using Preprocessing;
using NHunspell;
using System.Web;

namespace summaryServer
{

    class MainClass
    {

        public static void Main(string[] args)
        {
            string ip = "";
            string clientip = "";
            int port = 0;
            string python = "python";
            TextReader tr = new StreamReader("./config.cfg");
            string[] lines = tr.ReadToEnd().Split(new string[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            tr.Close();


            foreach (string li in lines)
            {
                Match m = Regex.Match(li, @"clientip\s*=\s*((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))");
                if (m.Success)
                    clientip = m.Groups[1].ToString();
                m = Regex.Match(li, @"^\s*ip\s*=\s*((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?))");
                if (m.Success)
                    ip = m.Groups[1].ToString();
                m = Regex.Match(li, @"port\s*=\s*(\d{2,5})");
                if (m.Success)
                    port = Convert.ToInt32(m.Groups[1].ToString());
            }

            if (port == 0 || ip == "")
                throw new Exception("Chyba v configuracnim souboru!");

            IPAddress[] IpA = Dns.GetHostAddresses(ip);
            IPAddress ipAddress = IpA[0];

            foreach (string path in Directory.GetFiles(".", "tmp*.txt"))
                File.Delete(path);
            
            TcpListener listener = new TcpListener(ipAddress, port);
            listener.Start();

            Console.WriteLine("server bezi na ip:" + ipAddress + " a portu:" + port);

            Console.WriteLine("Sumarizacni server je spuštěn.");

            SumarizacniVlakno sv = new SumarizacniVlakno();

            Console.WriteLine("Vsechno bezi.");

            while (true)
            {
                try
                {
                    if (listener.Pending())
                    {
                        sv.client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(new WaitCallback(sv.Spust));
                    }
                    Thread.Sleep(100);
                }
                catch (Exception e)
                { Console.WriteLine(e.StackTrace); }
            }
        }
    }

    public class SumarizacniVlakno
    {
        public TcpClient client;
        Preprocessing.Preprocessing prepMorp= new Preprocessing.Preprocessing(new FileInfo("linux.cfg/Preprocessing.cs.txt"));
             
        public SumarizacniVlakno()
        {
        }
        public void Spust(Object state)
        {
            Console.WriteLine("spousti se sumarizacni vlakno");
            try
            {
                Encoding encoder = Encoding.UTF8;
                if (client == null)
                    throw new Exception("client je null");
                Summarization.SummarizationMethod summary = null;
                NetworkStream webStream = client.GetStream();
                string ipPort = client.Client.RemoteEndPoint.ToString();
                string ip = ipPort.Remove(ipPort.IndexOf(':'), ipPort.Length - ipPort.IndexOf(':'));
                ///TODO: replace with ip
                if (ip == Dns.GetHostAddresses("localhost")[1].ToString() || ip == "replace with ip" || ip == "replace with ip")
                {
                    bool received = false;
                    while (received == false)
                    {
                        if (webStream.DataAvailable)
                        {
                            Console.WriteLine("Prichozi zprava v " + DateTime.Now);

                            byte[] message = new byte[client.ReceiveBufferSize];
                            client.Client.Receive(message);
                            string msg = encoder.GetString(message);
                            // Console.WriteLine("Client: " + msg);
                            received = true;

                            XmlDocument doc = new XmlDocument();
                            try
                            {
                                doc.LoadXml(msg);
                                doc.Save("zprava.xml");
                                XmlNode root = doc.DocumentElement;
                                XmlNode node = root.SelectSingleNode("title");
                                string title = node.InnerText;
                                if (title == "")
                                    title = "důležitý významný výsledek"; //TODO: replace with wider list
                                node = root.SelectSingleNode("text");
                                string text = node.InnerText.Replace("\r\n\r\n", ".\r\n\r\n").Replace("\n\n", ".\n\n");
                                node = root.SelectSingleNode("lang");
                                string lang = node.InnerText;
                                node = root.SelectSingleNode("sumCount");
                                bool procenta = true;
                                uint sumCount = 5;
                                if (node.InnerText.Length != 0)
                                {
                                    procenta = false;
                                    try
                                    {
                                        sumCount = Convert.ToUInt32(node.InnerText);
                                    }

                                    catch (FormatException)
                                    {
                                        Console.WriteLine("Prisel nespravny pocet vet (chyba konvertace):" + sumCount);
                                        sumCount = 5;
                                    }
                                }
                                else
                                {
                                    sumCount = 25;
                                    node = root.SelectSingleNode("percent");
                                    try
                                    {
                                        sumCount = Convert.ToUInt32(node.InnerText);
                                    }
                                    catch (FormatException)
                                    {
                                        Console.WriteLine("Chyba v zadani procent (chyba konvertace):" + sumCount);
                                        sumCount = 25;
                                    }
                                }
                                node = root.SelectSingleNode("method");
                                string metoda = node.InnerText;

                                if (!Enum.GetValues(typeof(Preprocessing.Preprocessing.Language))
                                    .Cast<Preprocessing.Preprocessing.Language>()
                                    .Select(v => v.ToString())
                                    .ToList().Contains(lang))
                                {
                                    lang = "sr";
                                }
                                Console.WriteLine("Vstup nacten");

                                text = text.Replace("\r", "\n");

                                if (lang != prepMorp.Lang.ToString())
                                {
                                    prepMorp = new Preprocessing.Preprocessing(new FileInfo("linux.cfg/Preprocessing." + lang + ".txt"));
                                    Console.WriteLine("Preprocessing change to " + lang);
                                }

                                int COS = 0;
                                string[] sum = new string[] { "" };
                                if ("heuristic" == metoda)
                                    summary = new Summarization.Heuristic(prepMorp, text, title);

                                if ("statistic" == metoda)
                                    summary = new Summarization.LuhnMethod(prepMorp, text, title);

                                if ("LSA" == metoda)
                                    summary = new Summarization.LSA(prepMorp, text, title, false);

                                if ("LSAIDF" == metoda)
                                    summary = new Summarization.LSA(prepMorp, text, title, true);

                                summary.CreateSummary();
                                COS = summary.CountOfsents();
                                if (!procenta)
                                    sum = summary.GetSummaryByCountOfsents(sumCount);
                                else
                                    sum = summary.GetSummaryByPercentOfText(sumCount);

                                string text2CL = "";
                                try
                                {
                                    text2CL = summary.LemmatizedText();
                                }
                                catch (NullReferenceException nre)
                                {
                                    client.Client.Send(encoder.GetBytes(HttpUtility.HtmlEncode("neošetřená chyba při preprocesingu")));
                                    throw new ApplicationException("neošetřená chyba při preprocesingu");
                                }

                                try
                                {
                                    StringBuilder response = new StringBuilder();
                                    response.AppendLine("<summary>Nastaveni: <br/> Jazyk:<span id='lang'>" + lang + "</span> <br/>Rozsah souhrnu:<span id='ratio'>");
                                    if (procenta)
                                        response.AppendLine(sumCount + "% (" + Math.Round((double)((double)sumCount / 100) * (COS)) + " vět)</span>");
                                    else
                                        response.AppendLine(Math.Round((double)(sumCount * 100) / COS) + "% (" + sumCount + " vět)</span>");
                                    response.AppendLine("<br/>Celkem vět:" + COS + "<br/><br/><br/>");
                                    response.AppendLine("<h3>Summary:</h3><div id='summary'>");
                                    foreach (string sentence in sum)
                                    {
                                        try
                                        {
                                            response.AppendLine(sentence + "<br/><br/>");
                                        }
                                        catch (NullReferenceException e)
                                        {
                                            response.AppendLine("chybna veta <br/><br/>");
                                            Console.WriteLine(e.Data);
                                        }
                                    }
                                    response.AppendLine("</div></summary>");
                                    client.Client.Send(encoder.GetBytes(HttpUtility.HtmlEncode(response.ToString())));
                                }
                                catch (Exception e)
                                { Console.WriteLine(e.StackTrace); Console.WriteLine(lang + " " + sumCount + " " + sum + "." + sum.Length); }
                            }
                            catch (XmlException)
                            {
                                Console.WriteLine("Příchozí komunikace nebyla ve formátu XML! => zahazuji");
                            }
                        }
                    }
                }
                else
                {
                    client.Client.Send(Encoding.UTF8.GetBytes(HttpUtility.HtmlEncode("Spatna IP clienta.")));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                client.Client.Send(Encoding.UTF8.GetBytes(HttpUtility.HtmlEncode("Chyba:" + e.Message + "</br>" + e.StackTrace)));
            }
            client.Client.Disconnect(true);
            client = null;
        }

    }

}
