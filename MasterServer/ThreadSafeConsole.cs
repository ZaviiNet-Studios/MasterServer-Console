using PlayFab.EconomyModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{

    internal class TFConsole
    {
        private class writeItem
        {
            public writeItem(string content, bool newLine, ConsoleColor? color)
            {
                Content = content;
                NewLine = newLine;
                Color = color;
            }

            public string Content;
            public bool NewLine;
            public ConsoleColor? Color;
        }

        private static ConcurrentQueue<writeItem> writeQueue = new ConcurrentQueue<writeItem>();

        public static void WriteLine(string content, ConsoleColor? color = null)
        {
            writeQueue.Enqueue(new writeItem(content, true, color));
        }

        public static void WriteLine()
        {
            writeQueue.Enqueue(new writeItem("", true, null));
        }

        public static void Write(string content, ConsoleColor? color = null)
        {
            writeQueue.Enqueue(new writeItem(content, false, color));
        }

        public static void Start()
        {
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (writeQueue.Count == 0)
                            continue;

                        writeItem item = writeQueue.First();
                        if (item == null)
                            continue;

                        if (item.Color != null)
                            Console.ForegroundColor = item.Color ?? ConsoleColor.Gray;

                        if (item.NewLine)
                            Console.WriteLine(item.Content);
                        else
                            Console.Write(item.Content);

                        Console.ResetColor();
                    }
                    catch { }
                }
            }).Start();
        }
    }
}
