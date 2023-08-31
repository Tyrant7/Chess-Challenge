using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;

namespace Chess_Challenge.src.Tuning
{
    static internal class Tuner
    {
        public static int GlobalID = 0;

        static private JsonSerializerOptions Options => new()
        {
            WriteIndented = true,
            IncludeFields = true
        };

        private static readonly string EvalFileName = "Evaluation.weights";

        // Change file path to your engine's build directory
        private static readonly string BuildDirectory = "D:\\Users\\tyler\\Chess-Challenge\\Chess-Challenge\\bin\\Release\\net6.0";

        private static string MyWeightsPath
            => Path.Combine(BuildDirectory, GlobalID.ToString() + "-" + EvalFileName);

        public static TunedBot NewBot()
        {
            return new TunedBot(ParamsFromFile(MyWeightsPath));
        }

        private static RawParameters ParamsFromFile(string path)
        {
            // Create parameters from file data
            RawParameters rawParameters = new RawParameters();
            if (!File.Exists(path))
            {
                Console.WriteLine("There was no file at the specified path: {0}", path);
                return rawParameters;
            }

            // Read all data from the file and creates a new parameter group
            string jsonData = File.ReadAllText(path);

            rawParameters = JsonSerializer.Deserialize<RawParameters>(jsonData, Options);
            if (rawParameters.Parameters == null)
            {
                Console.WriteLine("Could not retrieve parameters. Please make sure both definitions of RawParameters contain EXACTLY the same fields.");
            }
            return rawParameters;
        }
    }

    public struct RawParameters
    {
        public Dictionary<string, int> Parameters;
    }
}
