using Splendor.Core.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Splendor.ConsoleRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            Run1();
        }

        static void Run1()
        {
            var aiPlayers = new ISpendorAi[]
            {
                new StupidSplendorAi("Robin"),
                new StupidSplendorAi("James"),

                new PredictiveAi(() => new StupidSplendorAi($"{Guid.NewGuid()}"))
                    { Name = "Mat" }
            };

            var runner = new AiGameRunner(aiPlayers, Console.WriteLine);

            runner.Run();

            Console.ReadLine();
        }

        static void Run1000()
        {
            var scoreBoard = new Dictionary<ISpendorAi, int> 
            {
                { new StupidSplendorAi("Robin"), 0},
                { new StupidSplendorAi("James"), 0},
                { new StupidSplendorAi("Mat"), 0},
            };

            for(int i = 0; i < 1000; i++)
            {
                if (i % 100 == 0) Console.Write(".");
                var runner = new AiGameRunner(scoreBoard.Select(p=>p.Key), _ => { });
                var results = runner.Run();
                var max = results.Values.Max();
                foreach(var ai in results.Where(r=>r.Value == max)) scoreBoard[ai.Key]++;
            }

            Console.Write(Environment.NewLine);

            foreach(var row in scoreBoard)
            {
                Console.WriteLine($"{row.Key.Name} {row.Value} wins");
            }
            
            Console.ReadLine();
        }
    }
}
