using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace psvita_txptool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                if (Directory.Exists(args[0]) && File.GetAttributes(args[0]).HasFlag(FileAttributes.Directory))
                {
                    var inputPath = args[0];
                    var outputPath = Path.Combine(Path.GetDirectoryName(inputPath), Path.GetFileNameWithoutExtension(args[0]) + ".txp");
                    Txp.Create(inputPath, outputPath);
                }
                else if (File.Exists(args[0]))
                {
                    var inputPath = args[0];
                    var outputPath = Path.Combine(Path.GetDirectoryName(inputPath), Path.GetFileNameWithoutExtension(args[0]));
                    Txp.Read(inputPath, outputPath);
                }
            }
            else if (args.Length == 3 && args[0] == "x")
            {
                var inputPath = args[1];
                var outputPath = args[2];
                Txp.Read(inputPath, outputPath);
            }
            else if (args.Length == 3 && args[0] == "c")
            {
                var inputPath = args[1];
                var outputPath = args[2];
                Txp.Create(inputPath, outputPath);
            }
            else if(args.Length == 2 && args[0] == "a")
            {
                string inputPath = args[1];

                foreach (var file in Directory.EnumerateFiles(inputPath, "*.txp", SearchOption.AllDirectories))
                {
                    Console.WriteLine("Reading {0}...", inputPath);
                    Txp.Read(file, file.Replace(".txp", ""));

                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("usage:");
                Console.WriteLine("");
                Console.WriteLine("Extract:");
                Console.WriteLine("\t{0} x filename.txp foldername", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("");
                Console.WriteLine("Create:");
                Console.WriteLine("\t{0} c foldername filename.txp", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("");
                Console.WriteLine("Extract all TXPs in a folder:");
                Console.WriteLine("\t{0} a foldername", AppDomain.CurrentDomain.FriendlyName);
                Console.WriteLine("");
            }

        }
    }
}
