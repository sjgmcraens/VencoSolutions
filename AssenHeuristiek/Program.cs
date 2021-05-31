using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace AssenHeuristiek
{
    class Program
    {
        public static class Settings
        {
            //
            public static int matLen, matDia, cutLen, maxWasteLen, hThreshold;
            public static bool genMachCode;
            public static AxelType[] axTypes;

            // Constructor to de-null setting variables
            static Settings()
            {
                matLen = 2800;
                matDia = 20;
                cutLen = 5;
                maxWasteLen = 50;
                hThreshold = 0;
                genMachCode = true;
                SetVar("axTypes", "A=10x100", setAppSetting: false);
            }
            //
            public static string helpString = "\nList of commands: (ignore quotation marks when entering a command)" +
                "\n\"help\" => Gives information about usable commands." +
                "\n\"go\" => Gives information about usable commands." + 
                "\n\"stop\" => Stops the program." +
                "\n\"settings\" => Gives current settings." +
                "\n\"set\" => Set a setting. See list of setting below." +
                "\n    - \"matLen [material length in mm]\" Ex: \"set matLen 2800\"" +
                "\n    - \"matDia [material diameter in mm]\" Ex: \"set atDia 20\"" +
                "\n    - \"cutLen [cutting width in mm]\" Ex: \"set cutLen 5\"" +
                "\n    - \"maxWasteLen [maximal waste length in mm]\" Ex: \"set maxWasteLen 50\"" +
                "\n    - \"hThreshold [heuristic threshold: amount of remaining items before slow heuristic is used. 0 => only fast method.]\" Ex: \"set hThreshold 500\"" +
                "\n    - \"genMachCode [\"1\" if program should output machine code file, \"0\" otherwise]\" Ex: \"set gemMachCode 1\"" +
                "\n    - \"axTypes [[name]=[amount]x[length in mm]]\" Ex: \"set axelTypes A=1000x512_B=400x123_C=50x709\"";

            public static void ReadAppSettings(bool deleteInvalid = false)
            {
                //
                Console.WriteLine("\nReading app settings...");

                // Read app setting and use Set() to set it as a variable
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings.Count == 0)
                {
                    Console.WriteLine("AppSettings is empty.");
                }
                else
                {
                    foreach (var key in appSettings.AllKeys)
                    {
                        
                        try
                        {
                            SetVar(key, appSettings[key], setAppSetting: false);
                            Console.WriteLine($"Set (\"{key}\" => \"{appSettings[key]}\") from app settings.");
                        }
                        catch
                        {
                            Console.WriteLine($"Error setting this variable. Removing item from app settings");
                            SetAppSetting(key, "", remove: true);
                        }
                    }
                }
            }

            public static void SetVar(string key, string value, bool setAppSetting = false)
            {
                // Write the setting to variable
                switch (key.ToLower())
                {
                    case "axtypes":
                        {
                            List<AxelType> axList = new List<AxelType>();
                            foreach (string axType in value.Split('_'))
                            {
                                int nameLen = axType.IndexOf('=');
                                string name = axType.Substring(0, nameLen);
                                var amountAndLength = axType.Substring(name.Length + 1).Split('x').Select(x => Convert.ToInt32(x)).ToArray();
                                axList.Add(new AxelType
                                {
                                    name = name,
                                    amount = amountAndLength[0],
                                    length = amountAndLength[1],
                                });
                            }
                            axTypes = axList.ToArray();
                            break;
                        }
                    case "matlen":
                        {
                            matLen = Convert.ToInt32(value);
                            break;
                        }
                    case "matdia":
                        {
                            matDia = Convert.ToInt32(value);
                            break;
                        }
                    case "cutlen":
                        {
                            cutLen = Convert.ToInt32(value);
                            break;
                        }
                    case "maxwastelen":
                        {
                            maxWasteLen = Convert.ToInt32(value);
                            break;
                        }
                    case "hthreshold":
                        {
                            hThreshold = Convert.ToInt32(value);
                            break;
                        }
                    case "genmachcode":
                        {
                            switch (value)
                            {
                                case "0":
                                    genMachCode = false;
                                    return;
                                case "1":
                                    genMachCode = true;
                                    return;
                                default:
                                    throw new Exception($"Invalid value \"{value}\" for key \"{key}\"");
                            }
                        }
                    default:
                        {
                            throw new Exception($"Trying to set unknown key \"{key}\".");
                        }
                }

                //
                if (setAppSetting)
                {
                    SetAppSetting(key, value);
                }
            }

            public static void SetAppSetting(string key, string value, bool remove = false)
            {
                // Write the setting to AppSettings    
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var appSettings = configFile.AppSettings.Settings;

                key = key.ToLower();

                if (remove)
                {
                    appSettings.Remove(key);
                    Console.WriteLine($"Removed (\"{key}\"=\"{value}\") from app settings.");
                }
                else
                {
                    appSettings.Add(key, value);
                    Console.WriteLine($"Wrote \"{key}\" => \"{value}\" to app settings.");
                }

                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);

                //Console.WriteLine($"Set \"{key}\" to \"{value}\".");
            }

            public static string GetFullString()
            {
                return "Current settings:" +
                $"\nAxel types:          {axTypes.Aggregate("", (x, next) => x += $"{ next.name}={next.amount}x{next.length}mm, ")}" +
                $"\nMaterial length:     {matLen}mm" +
                $"\nMaterial diameter:   {matDia}mm" +
                $"\nCutting width:       {cutLen}mm" +
                $"\nMax waste length:    {maxWasteLen}mm" +
                $"\nhThreshold:          {hThreshold}" +
                $"\nOutput machine code: {genMachCode}";
            }
        }

        static void Main()
        {
            // I dont know why I have to do this...
            Console.ForegroundColor = ConsoleColor.White;

            // Announce start of program
            Console.WriteLine("Starting axel optimisation. DateTime: " + DateTime.Now.ToString());

            // Read app settings
            Settings.ReadAppSettings();

            // Display current settings.
            Console.WriteLine("\n" + Settings.GetFullString());

            // Ask user if settings are correct
            Console.WriteLine("\nYou can change the current settings or start the algorithm. Enter \"help\" for a list of usable commands.");

            // Main input loop
            while (true)
            {
                // Get user input
                Console.WriteLine("\nUser input:");
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                string userInput = Console.ReadLine().ToLower();
                Console.ForegroundColor = ConsoleColor.White;

                // Main input switch (help, go, stop, settings, set)
                //try
                {
                    switch (userInput)
                    {
                        case "help":
                            {
                                // Write help
                                Console.WriteLine(Settings.helpString);
                                break;
                            }
                        case "go":
                            {
                                // Do calculation
                                Calculate();
                                break;
                            }
                        case "stop":
                            {
                                // Exit
                                Environment.Exit(-1);
                                break;
                            }
                        case "settings":
                            {
                                // Write settings
                                Console.WriteLine("\n" + Settings.GetFullString());
                                break;
                            }
                        default: // Case: Set [key] [value]
                            {
                                // Multiple arguments assumed hereafter
                                var inputArgs = userInput.Split(' ');

                                // Switch for input with multiple arguments
                                switch (inputArgs[0])
                                {
                                    case "set":
                                        {
                                            Settings.SetVar(inputArgs[1], inputArgs[2], setAppSetting: true);
                                        }
                                        break;
                                    default:
                                        {
                                            Console.WriteLine($"Invalid first command argument \"{inputArgs[0]}\".");
                                        }
                                        break;
                                }
                                break;
                            }
                    }
                }
                //catch
                //{
                //    Console.WriteLine("Error processing command. Enter \"help\" for a list of setting commands.");
                //}
            }
        }

        public static (int count, int[] axelUsedAmounts, int[] seq, int wastePerMatLen)[] GetPlanning_Mode_0(int[] amountOfAxelsLeft)
        {
            // debug
            if (amountOfAxelsLeft.Any(x => x < 0))
            {
                throw new Exception();
            }

            // Plan
            List<(int count, int[] axelUsedAmounts, int[] seq, int wastePerMatLen)> plan = new List<(int, int[], int[], int)>();

            

            // Return
            return plan.ToArray();
        }

        public static void Calculate()
        {
            #region Setup

            // Localise some variables for ease
            var axTypes = Settings.axTypes;
            int axAmount = axTypes.Length;
            var matLen = Settings.matLen;

            // Keep track of the amount of axels to make as we go along
            int[] amountOfAxelsLeft = Enumerable.Range(0, axTypes.Length).Select(x => axTypes[x].amount).ToArray();

            // Generate plan (array of tuples containing the order of operations and the waste)
            List<(int count, int[] axAmounts, int[] totalAxAmounts, int[] seq, int wastePerMatLen, int totalWaste)> plan = new List<(int, int[], int[], int[], int, int)>();

            #endregion

            #region Fast heuristic

            // While the total amount of axel to be made is still greater than the hThreshold
            while (amountOfAxelsLeft.Sum() > Settings.hThreshold)
            {
                // Get best combination (assuming this can be made at least once)
                (int[] axAmounts, int waste) C = GetBestMatLenCombination(amountOfAxelsLeft);

                // Determine some fact about this plan
                (int count, int[] axAmounts, int[] totalAxAmounts, int[] seq, int wastePerMatLen, int totalWaste) P;
                P.axAmounts = C.axAmounts;
                P.count = Enumerable.Range(0, P.axAmounts.Length).Select(x => (P.axAmounts[x]==0 ? 999999 : amountOfAxelsLeft[x] / P.axAmounts[x])).Min();
                P.totalAxAmounts = P.axAmounts.Select(x => x *= P.count).ToArray();
                P.seq = Enumerable.Range(0, Settings.axTypes.Length).Aggregate(new int[0], (x, y) => x = x.Concat(Enumerable.Repeat(y, P.axAmounts[y])).ToArray());
                P.wastePerMatLen = C.waste;
                P.totalWaste = P.wastePerMatLen * P.count;

                plan.Add(P);

                // Reduce amount of axels to be made
                Enumerable.Range(0, Settings.axTypes.Length).ToList().ForEach(x => amountOfAxelsLeft[x] -= P.totalAxAmounts[x]);
            }

            #endregion

            #region Print results

            Console.WriteLine("\n" + new String('=', Console.WindowWidth - 1));

            // Print prelimilairy plan to console
            Console.WriteLine("\nResults of fast heuristic:");

            // Re-initialize axelamount variable
            var tempAmountOfAxelsLeft = Settings.axTypes.Select(x => x.amount).ToArray();

            // Determine colors for visuals
            ConsoleColor[] colors = (ConsoleColor[])Enum.GetValues(typeof(ConsoleColor));
            colors = colors.Where(x => (x != ConsoleColor.Black)).ToArray();

            Dictionary<int, ConsoleColor> axelTypeColors = new Dictionary<int, ConsoleColor>();
            foreach (var i in Enumerable.Range(0, Settings.axTypes.Length))
            {
                axelTypeColors.Add(i, colors[i]);
            }

            // Legend
            for (int i = 0; i < Settings.axTypes.Length; i++)
            {
                Console.Write($"\n{Settings.axTypes[i].name}:");
                Console.BackgroundColor = axelTypeColors[i];
                Console.Write(" ");
                Console.BackgroundColor = ConsoleColor.Black;
            }
            Console.Write($"\nWaste is in black.\n");

            foreach (var P in plan)
            {
                Console.WriteLine("");
                // Visualize
                PrintVisualSequence(P.seq, axelTypeColors);

                // 100 x [ABBAC] => Total waste = 1000 mm
                Console.WriteLine($"\n{P.count}x{P.seq.Aggregate("[", (x, nextX) => x += axTypes[nextX].name.ToUpper()) + "]"} => Total waste = {P.totalWaste} mm");
                
                // Waste per material length: 10 mm
                Console.WriteLine($"Waste per material length: {P.wastePerMatLen} mm ({P.wastePerMatLen * 100 / Settings.matLen}%)");
                
                // Axel amounts used: A[200/200], B[200/500], C[100/123]
                Console.WriteLine($"Used: {Enumerable.Range(0,Settings.axTypes.Length).Select(axI => $"{axTypes[axI].name.ToUpper()}[{P.totalAxAmounts[axI]}/{tempAmountOfAxelsLeft[axI]}]").Aggregate((a, b) => a += ", " + b)}");

                // Reduce amount of axels to be made
                Enumerable.Range(0, Settings.axTypes.Length).ToList().ForEach(x => tempAmountOfAxelsLeft[x] -= P.totalAxAmounts[x]);
            }

            // Write summarry
            Console.WriteLine($"\nTotal waste: {(int)plan.Select(x => x.totalWaste).Sum()}/{Settings.matLen * plan.Select(x => x.count).Sum()}mm ({(int)plan.Select(x => x.totalWaste).Sum() * 100 / (Settings.matLen * plan.Select(x => x.count).Sum())}%)");

            // Create ordered plan
            // Re-initialize axelamount variable
            tempAmountOfAxelsLeft = Settings.axTypes.Select(x => x.amount).ToArray();
            List<(int count, int[] axAmounts, int[] totalAxAmounts, int[] seq, int wastePerMatLen, int totalWaste)> orderedPlan = new List<(int count, int[] axAmounts, int[] totalAxAmounts, int[] seq, int wastePerMatLen, int totalWaste)>();

            Console.WriteLine("\n" + new String('=', Console.WindowWidth - 1));

            Console.WriteLine("\nOrdered sequence:");

            while (tempAmountOfAxelsLeft.Any(x => x > 0))
            {
                (int count, int[] axAmounts, int[] totalAxAmounts, int[] seq, int wastePerMatLen, int totalWaste) P;
                int axI = Array.FindIndex(tempAmountOfAxelsLeft, x => x > 0);
                int amount = Settings.matLen / (Settings.axTypes[axI].length + Settings.cutLen);
                amount = amount <= tempAmountOfAxelsLeft[axI] ? amount : tempAmountOfAxelsLeft[axI];
                P.seq = Enumerable.Repeat(axI, amount).ToArray();
                P.count = tempAmountOfAxelsLeft[axI] / amount;
                P.wastePerMatLen = Settings.matLen - (Settings.axTypes[axI].length + Settings.cutLen) * amount;
                P.totalWaste = P.count * P.wastePerMatLen;
                P.axAmounts = Enumerable.Range(0, tempAmountOfAxelsLeft.Length).Select(x => x == axI ? amount : 0).ToArray();
                P.totalAxAmounts = P.axAmounts.Select(x => x * P.count).ToArray();
                orderedPlan.Add(P);

                Enumerable.Range(0, Settings.axTypes.Length).ToList().ForEach(x => tempAmountOfAxelsLeft[x] -= P.totalAxAmounts[x]);

                Console.WriteLine($"\n{P.count}x{P.seq.Aggregate("[", (x, nextX) => x += axTypes[nextX].name.ToUpper()) + "]"} => Total waste = {P.totalWaste} mm");
                Console.WriteLine($"Waste per material length: {P.wastePerMatLen} mm ({P.wastePerMatLen * 100 / Settings.matLen}%)");
            }
            // Write summarry
            Console.WriteLine($"\nTotal waste: {(int)orderedPlan.Select(x => x.totalWaste).Sum()}/{Settings.matLen * orderedPlan.Select(x => x.count).Sum()}mm ({(int)orderedPlan.Select(x => x.totalWaste).Sum() * 100 / (Settings.matLen * orderedPlan.Select(x => x.count).Sum())}%)");

            Console.WriteLine("\n" + new String('=', Console.WindowWidth-1));

            int wasteReduced = orderedPlan.Select(x => x.totalWaste).Sum() - plan.Select(x => x.totalWaste).Sum();
            int totalMatLensUsed = (orderedPlan.Select(x => x.count).Sum() + plan.Select(x => x.count).Sum()) / 2;
            Console.WriteLine($"\nBest solution reduced waste by {wasteReduced}mm over ~{totalMatLensUsed}x{Settings.matLen}mm ({wasteReduced * 100 / (totalMatLensUsed * Settings.matLen)})%");

            Console.WriteLine("\n" + new String('=', Console.WindowWidth - 1));

            #endregion

            #region Slow heuristic

            
            // Do heuristic if any axels left to make
            if (amountOfAxelsLeft.Sum() > 0)
            {

                Console.WriteLine($"\nCommencing s-heuristic with remaining axels:\n{Enumerable.Range(0, axTypes.Length).Aggregate("", (ag, x) => ag += "\n" + $"[{amountOfAxelsLeft[x]}/{axTypes[x].amount}]")}");


                // Create ordered solution
                int[] orderedSeq = Enumerable.Range(0, axTypes.Length)
                    .Select(x => Enumerable.Repeat(x, amountOfAxelsLeft[x]))
                    .Aggregate((y, next) => y = y.Concat(next)).ToArray();

                int totalAxelAmount = orderedSeq.Length;

                // Determine all possible swaps
                (int a, int b)[] swaps = new (int a, int b)[Enumerable.Range(1, totalAxelAmount + 1).Aggregate(0, (x, next) => x += next)];
                int swapI = 0;
                for (int a = 0; a < totalAxelAmount - 1; a++)
                {
                    for (int b = a + 1; b < totalAxelAmount; b++)
                    {
                        swaps[swapI] = (a, b);
                        swapI++;
                    }
                }

                // Random
                var rand = new Random();

                // Create initial solution (ordered, TODO: implement inputting of starting sequence)
                var initSol = orderedSeq.OrderBy(rand.Next);

                // Set try amount
                int tryAmount = 10;

                (int[] seq, int totalWaste)[] optimalSequenceArray = new (int[], int)[tryAmount];
                (int[] subSeq, int remainder)[][] optimalSequenceDataArray = new (int[], int)[tryAmount][];

                for (int tryI = 0; tryI < tryAmount; tryI++)
                {
                    Console.WriteLine("\n" + new String('=', Console.WindowWidth - 1));
                    Console.WriteLine($"\nStarting s-heuristic #{tryI}.");

                    Console.Write("\n");

                    // Save optimal swapped sequence in array (from permuted initSol)
                    optimalSequenceArray[tryI] = GetOptimalSwappedSequence(initSol.OrderBy(x => rand.Next()).ToArray(), swaps);
                    optimalSequenceDataArray[tryI] = FormatSequenceData(optimalSequenceArray[tryI].seq);

                    // Print progress update
                    Console.Write($"Optimal sequence found with {optimalSequenceDataArray[tryI].Aggregate(0, (ag, x) => ag += x.remainder)}mm's of total waste:\n");
                    Console.Write("\n");
                    PrintSequenceShort(optimalSequenceArray[tryI].seq, axelTypeColors);
                    Console.Write("\n");
                    PrintSubSequences(optimalSequenceDataArray[tryI], axelTypeColors);
                    Console.Write("\n");
                }

                // Print conclusion
                Console.WriteLine("\n" + new String('=', Console.WindowWidth - 1));

                (int[] seq, int totalCost, int totalWaste, (int[] seq, int rem)[] subSeqs) bestSeq;
                (bestSeq.seq, bestSeq.totalCost) = optimalSequenceArray.Where(x => x.totalWaste == optimalSequenceArray.Min(x => x.totalWaste)).First();
                bestSeq.subSeqs = FormatSequenceData(bestSeq.seq);
                bestSeq.totalWaste = bestSeq.subSeqs.Aggregate(0, (ag, x) => ag += x.rem);


                Console.WriteLine($"\nFor the remaining axels:\n{Enumerable.Range(0, axTypes.Length).Aggregate("", (ag, x) => ag += "\n" + $"[{ amountOfAxelsLeft[x]}/{ axTypes[x].amount}]")}");
                Console.WriteLine("\nThe best optimal sequence found was:");
                Console.Write("\n");
                PrintSequenceShort(bestSeq.seq, axelTypeColors);
                Console.Write("\n");
                foreach (var subSeq in bestSeq.subSeqs.Select(x => x.seq))
                {
                    PrintVisualSequence(subSeq, axelTypeColors);
                }
                
                Console.Write("\n");
                PrintSubSequences(bestSeq.subSeqs, axelTypeColors);
                Console.Write("\n");
                Console.WriteLine($"\nTotal cost: {bestSeq.totalCost}mm ({bestSeq.subSeqs.Length}x{Settings.matLen}mm)\n" +
                                    $"Total waste: {bestSeq.totalWaste}mm ({(bestSeq.totalWaste * 100.0 / bestSeq.totalCost).ToString("F")}% of cost) ({(bestSeq.totalWaste * 100.0 / bestSeq.seq.Aggregate((ag, x) => ag += axTypes[x].length)).ToString("F")}% of total product length.)");
            }
            

            #endregion
        }

        public static void PrintSubSequences((int[] seq, int rem)[] subSeqs, Dictionary<int, ConsoleColor> axColors)
        {
            //Console.Write(subSeqs.Aggregate("", (ag, x) => ag += "\n" + x.seq.Aggregate("[", (ag2, y) => ag2 += Settings.axTypes[y].name.ToUpper()) + "]"));

            
            foreach (var subSeq in subSeqs)
            {
                Console.Write("\n[");
                foreach(var axI in subSeq.seq)
                {
                    Console.ForegroundColor = axColors[axI];
                    Console.Write($"{Settings.axTypes[axI].name.ToUpper()}");
                }
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("]");
            }
            Console.Write("\n");
        }
        public static void PrintSequenceShort(int[] seq, Dictionary<int, ConsoleColor> axColors)
        {
            Console.Write("[");

            int curI = seq[0];
            int curSubSeqLen = 1;

            for (int i = 1; i <= seq.Length; i++)
            {
                if (i < seq.Length && seq[i] == curI)
                {
                    curSubSeqLen++;
                }
                else
                {
                    // Add to string
                    
                    if (curSubSeqLen == 1)
                    {
                        Console.ForegroundColor = axColors[curI];
                        Console.Write($" {Settings.axTypes[curI].name.ToUpper()}");
                    }
                    else
                    {
                        Console.Write($" {curSubSeqLen}x");
                        Console.ForegroundColor = axColors[curI];
                        Console.Write($"{Settings.axTypes[curI].name.ToUpper()}");
                    }
                    Console.ForegroundColor = ConsoleColor.White;

                    // Break at the end
                    if (i == seq.Length)
                    {
                        break;
                    }

                    // Update curI and subsequence length
                    curI = seq[i];
                    curSubSeqLen = 1;
                }
            }

            Console.Write(" ]\n");
        }

        public static void PrintVisualSequence(int[] seq, Dictionary<int, ConsoleColor> CCs)
        {
            // Visualize
            
            //float[] fracAxPositions = Enumerable.Range(0, seq.Length).Select(x => axFractions.SkipLast(seq.Length - x).Aggregate((y, z) => y += z)).ToArray();

            (int axI, float matLenFrac)[] seqFrac = seq.Select(x => (x, (float)Settings.axTypes[x].length / Settings.matLen)).ToArray();
            float cum = 0;
            (int axI, float cumMatFrac)[] cumMatFrac = new (int axI, float cumMatFrac)[seq.Length];
            foreach (int i in Enumerable.Range(0, seq.Length))
            {
                cumMatFrac[i] = (seqFrac[i].axI, seqFrac.SkipLast(seq.Length - (i+1)).Sum(x => x.matLenFrac));
            }
            float[] windowFracs = Enumerable.Range(0, Console.WindowWidth).Select(cI => (float)cI / Console.WindowWidth).ToArray();
            var lastLineIndexOfAxes = cumMatFrac.Select(x => Array.FindLastIndex(windowFracs, wFrac => wFrac < x.cumMatFrac)).ToArray();

            int curIndex = 0;
            foreach (int i in Enumerable.Range(0, seq.Length))
            {
                int charLen = lastLineIndexOfAxes[i] - curIndex;
                curIndex = lastLineIndexOfAxes[i];

                Console.BackgroundColor = CCs[seq[i]];
                Console.Write(new String(' ', charLen));
            }
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write('\n');
        }

        public static (int[], int) GetBestMatLenCombination(int[] amountOfAxelsToBeMade)
        {
            // combiList is a list of (lists of axelTypes) which contains 
            // all possible combinations of axelTypes on the material length.


            var combiList = GetCombinations(amountOfAxelsToBeMade);

            //(int[], int) combiList = (null, 0);

            var (C, W) = combiList.Where(x => x.W == combiList.Min(x => x.W)).First();

            // Im assuming here that combinations of equal waste are REALLY equal 
            // so that we can take any one of them (the first in this case).

            return GetCombinations(amountOfAxelsToBeMade).Where(x => x.W == combiList.Min(y => y.W)).First();
        }
        public static (int[] C, int W)[] GetCombinations(int[] amountOfAxelsToBeMade)
        {
            // List of combinations
            List<(int[] arr, int len, bool isFull)> combiList = new List<(int[] arr, int len, bool isFull)>();

            // One combination give not the order, but the amount of each axel type to be put on one material length.

            // Add empty starting combination Ex: (0, 0, 0)
            combiList.Add((Enumerable.Repeat(0, amountOfAxelsToBeMade.Length).ToArray(), Settings.matLen, false));

            // Breath-first recursion
            
            // While any combination is not full yet
            while (combiList.Any(x=> x.isFull == false))
            {
                foreach (int i in Enumerable.Range(0, combiList.Count))
                {
                    // Cont. if full
                    if (combiList[i].isFull)
                    {
                        continue;
                    }

                    // Determine which axels are valid to add to this curComb (available and fitting)
                    bool[] fittingAxels = Enumerable.Range(0, Settings.axTypes.Length)
                        .Select(axI => combiList[i].len - Settings.cutLen - Settings.axTypes[axI].length >= 0
                                       && amountOfAxelsToBeMade[axI] > combiList[i].arr[axI]).ToArray();

                    // debug
                    int axLen = Enumerable.Range(0, Settings.axTypes.Length).Aggregate(0, (x, y) => x += Settings.axTypes[y].length * combiList[i].arr[y]);
                    if (axLen > Settings.matLen)
                    {
                        throw new Exception();
                    }

                    // If no axel fits => isFull
                    if (fittingAxels.All(x => x == false))
                    {
                        combiList[i] = (combiList[i].arr, combiList[i].len, true);
                        continue;
                    }

                    // Assuming at least one axel fits
                    var newCombs = new List<(int[] axAmounts, int lenLeft, bool isFull)>();

                    // For each fitting axel type:
                    for (int axI = 0; axI < fittingAxels.Length; axI++)
                    {
                        if (fittingAxels[axI])
                        {
                            // How TF do i do this in one line using linq?
                            var newArr = new int[combiList[i].arr.Length];
                            Array.Copy(combiList[i].arr, newArr, newArr.Length);
                            newArr[axI]++;
                            newCombs.Add((Enumerable.Range(0, combiList[i].arr.Length).Select(x => axI == x ? combiList[i].arr[x] + 1 : combiList[i].arr[x]).ToArray()
                                , combiList[i].len - Settings.cutLen - Settings.axTypes[axI].length, false));
                        }
                    }
                    // Remove old
                    combiList.RemoveAt(i);
                    // Add new
                    combiList.AddRange(newCombs);
                }
            }
            return combiList.Select(x => (x.arr, x.len)).ToArray();
        }

        public static int GetTotalMatCost(int[] seq)
        {
            //Localize
            var axTypes = Settings.axTypes;
            var matLen = Settings.matLen;
            var cutLen = Settings.cutLen;
            //var wasteThreshold = Settings.wasteThreshold
            var wasteThreshold = 1000;

            // Calculate waste
            int totalCost = 0;
            int remainder = matLen;
            foreach (int adjLen in seq.Select(x => axTypes[x].length + cutLen))
            {
                if (remainder - adjLen >= 0)
                {
                    // Remove adjusted length from the remaining material length
                    remainder -= adjLen;
                }
                else
                {
                    // Add previous matLen to total cost
                    totalCost += matLen;
                    // New remainder
                    remainder = matLen - adjLen;
                }
            }
            //
            return totalCost + (remainder < wasteThreshold ? remainder : 0);
        }
        public static int GetOverlapScore(int[] seq)
        {
            return Enumerable.Range(0, Settings.axTypes.Length).Aggregate((x, y) => x += (Array.LastIndexOf(seq, y) - Array.IndexOf(seq, y)) ^ 2);
        }
        public static (int[] sequence, int remainder) GetOptimalSwappedSequence(int[] curSeq, (int a, int b)[] swaps)
        {
            // Print details of first string
            //Console.WriteLine($"\n\nStarting heuristic #{index} with inital solution:\n{curSol.GetSeqString()}\n{curSol.GetDetailsString}");

            int bestCost = GetTotalMatCost(curSeq);

            // While optimal swap not yet found
            bool betterSolFound = true;
            while (betterSolFound)
            {
                betterSolFound = false;

                (int a, int b) bestSwap = (-1,-1);

                // For each valid swap
                foreach ((int a, int b) in swaps.Where(x => curSeq[x.a] != curSeq[x.b]))
                {
                    // Do swap
                    int[] newSeq = curSeq.ToArray();
                    (newSeq[a], newSeq[b]) = (newSeq[b], newSeq[a]);

                    // Get waste score
                    int newCost = GetTotalMatCost(newSeq);

                    // Update best if better
                    if (newCost < bestCost)
                    {
                        betterSolFound = true;
                        bestSwap = (a, b);
                        bestCost = newCost;
                    }
                    else if (newCost == bestCost)
                    {
                        if (GetOverlapScore(newSeq) < GetOverlapScore(curSeq))
                        {
                            betterSolFound = true;
                            bestSwap = (a, b);
                        }
                        ////If waste AND overlapScore are the equal:
                        //else if (GetOverlapScore() == curSol.GetOverlapScore())
                        //{
                        //    if (tempSol.GetRepeatScore() < curSol.GetRepeatScore())
                        //    {
                        //        betterSolFound = true;
                        //        bestSwap = (a, b);
                        //    }
                        //}
                    }
                }

                if (betterSolFound)
                {
                    // Do swap
                    (curSeq[bestSwap.a], curSeq[bestSwap.b]) = (curSeq[bestSwap.b], curSeq[bestSwap.a]);

                    // Give info about best swap (curSol)
                    //Console.WriteLine($"Best swap: ({bestSwap.a}, {bestSwap.b})");
                    //Console.WriteLine($"Yielded Sol: {curSeq.GetSeqString()}");
                    //Console.WriteLine(curSol.GetDetailsString);
                }
                else
                {
                    //Console.WriteLine("No better solutions were found.");
                }
            }

            //Console.WriteLine($"Optimal solution found. Waste: {curSeq.totalWaste}, Overlap: {curSeq.GetOverlapScore()}");

            return (curSeq, bestCost);
        }

        public static (int[] subSeq, int remainder)[] FormatSequenceData(int[] seq)
        {
            //Localize
            var axTypes = Settings.axTypes;
            var matLen = Settings.matLen;
            var cutLen = Settings.cutLen;
            //var wasteThreshold = Settings.wasteThreshold
            var wasteThreshold = 1000;

            // Calculate waste
            List<(List<int> subSeq, int remainder)> subSeqList = new List<(List<int>, int remainder)>();

            int curRemainder = matLen;
            List<int> curSubSeq = new List<int>();
            foreach ((int adjLen, int axI) in seq.Select(x => (axTypes[x].length + cutLen, x)) )
            {
                curSubSeq.Add(axI);
                if (curRemainder - adjLen >= 0)
                {
                    // Remove adjusted length from the remaining material length
                    curRemainder -= adjLen;
                }
                else
                {
                    // Add previous matLen to total cost
                    subSeqList.Add((curSubSeq, curRemainder));
                    // Re-init
                    (curSubSeq, curRemainder) = (new List<int>(), matLen - adjLen);
                }
            }
            //
            return subSeqList.Select(x => (x.subSeq.ToArray(), x.remainder < wasteThreshold ? x.remainder : 0)).ToArray();
        }



        public class Solution
        {
            public static int cutLen, endGap;
            private static int maxWasteLength = Convert.ToInt32(Properties.Resources.maxWasteLength);
            private static string machineCodeCutWasteString = 
                "T101 (Steekbeitel)" +
                "\nG0G54Z[wasteLength]." +
                "\nX-5." +
                "\nG4X0.5" +
                "\nM11" +
                "\nG4X1." +
                "\nM10" +
                "\nG0W2." +
                "\nX[materialDiameter+2]." +
                "\nZ0." +
                "\nM3S1200" +
                "\nG1X-2.F0.03" +
                "\nG0W2" +
                "\nG28U0V0";

            public int axelsUsed, totalWaste, cost;
            public int[] sequence, remainders;
            public List<List<int>> sequencePerMatLen;

            public Solution(int[] seq)
            {
                sequence = seq;
                var matL = Settings.matLen;
                var axTypes = Settings.axTypes;

                var _remainders = new List<int>();

                sequencePerMatLen = new List<List<int>>();
                var _matLenSeq = new List<int>();

                // Calculate cost
                axelsUsed = 1;
                totalWaste = 0;
                int remainder = matL;
                foreach ((AxelType A, int axI) in seq.Select(x => (axTypes[x], x)))
                {
                    if (remainder - A.length - cutLen - endGap >= 0)
                    {
                        remainder -= A.length;
                        _matLenSeq.Add(axI);
                    }
                    else
                    {
                        axelsUsed += 1;
                        _remainders.Add(remainder);
                        totalWaste += remainder;
                        remainder = matL - A.length - cutLen;

                        sequencePerMatLen.Add(_matLenSeq);
                        _matLenSeq = new List<int>() { axI };
                    }
                }
                remainders = _remainders.ToArray();

                sequencePerMatLen.Add(_matLenSeq);

                cost = axelsUsed * matL + totalWaste;
            }

            public string GetSeqString()
            {
                return sequencePerMatLen.Aggregate("[", (x, next) => x = x + " -" + next.Aggregate("", (y, next) => y = y + " " + Settings.axTypes[next].name)) + " ]";
            }

            public string GetMachineCode()
            {
                if (Settings.axTypes.Length > 4)
                {
                    throw new Exception("Cannot create vertical short string with more than 4 axel types.");
                }

                string s = "%\n";

                foreach (var matLenSeq in sequencePerMatLen)
                {
                    int curAxType = matLenSeq[0];
                    int curSubSeqLen = 1;

                    foreach (var axType in matLenSeq.Skip(1))
                    {
                        if (axType == curAxType)
                        {
                            curSubSeqLen++;
                        }
                        else
                        {
                            // Add to string
                            s += $"\nM98P{curSubSeqLen.ToString().PadLeft(3, '0')}{new String('1', curAxType + 1).PadLeft(4, '0')}";

                            curAxType = axType;
                            curSubSeqLen = 1;
                        }
                    }
                    
                    int waste = (Settings.matLen - matLenSeq.Select(x => Settings.axTypes[x].length).ToArray().Aggregate(0, (x, next) => x += next));

                    while (waste > 0)
                    {
                        if (waste > maxWasteLength)
                        {
                            s += $"\n{machineCodeCutWasteString.Replace("[wasteLength]", (maxWasteLength).ToString().Replace(',', '.')).Replace("[materialDiameter+2]", Properties.Resources.matDiameter + 2)}";
                            waste -= maxWasteLength;
                        }
                        else
                        {
                            s += $"\n{machineCodeCutWasteString.Replace("[wasteLength]", (waste).ToString().Replace(',', '.')).Replace("[materialDiameter+2]", Properties.Resources.matDiameter + 2)}";
                            break;
                        }
                    }

                }

                return s + "\n\n%";
            }

            public string GetShortSeqString()
            {
                string s = "[";

                int curI = sequence[0];
                int curSubSeqLen = 1;

                for (int i = 1; i <= sequence.Length; i++)
                {
                    if (i < sequence.Length && sequence[i] == curI)
                    {
                        curSubSeqLen++;
                    }
                    else
                    {
                        // Add to string
                        if (curSubSeqLen == 1)
                        {
                            s += $" {Settings.axTypes[curI].name}";
                        }
                        else
                        {
                            s += $" {curSubSeqLen}x{Settings.axTypes[curI].name}";
                        }

                        // Break at the end
                        if (i == sequence.Length)
                        {
                            break;
                        }

                        // Update curI and subsequence length
                        curI = sequence[i];
                        curSubSeqLen = 1;
                    }
                }

                return s + " ]";
            }

            public string GetDetailsString => $"Axels used: {axelsUsed}\nTotal Waste: {totalWaste}\nCost: {cost}\nOverlapScore: {GetOverlapScore()}\nRepeatScore: {GetRepeatScore()}";

            public Solution GetCopy()
            {
                return new Solution(sequence);
            }

            public Solution GetSwappedCopy(int a, int b)
            {
                int[] _sequence = new int[sequence.Length];
                sequence.CopyTo(_sequence,0);
                _sequence[a] = sequence[b];
                _sequence[b] = sequence[a];
                return new Solution(_sequence);
            }

            public float GetOverlapScore()
            {
                float score = 0;
                foreach (int i in Enumerable.Range(0, Settings.axTypes.Length))
                {
                    score += (Array.LastIndexOf(sequence, i) - Array.IndexOf(sequence, i)) ^ 2;
                }

                return score;
            }

            public int GetRepeatScore()
            {
                // The score will be the amount of occurences of subsequences of repeats in the sequence.
                // In other words: the score is the amount of input's that need to be given to the MyLas machine.
                // Example: AAAAAABBBABBB => 6A3BA3B => score = 7.
                int score = 0;
                int lastI = -1;
                foreach (int i in sequence)
                {
                    if (i != lastI)
                    {
                        score += 1;
                        lastI = i;
                    }
                }

                return score;
            }

            public void Visualize()
            {
                // Determine colors
                ConsoleColor[] colors = (ConsoleColor[])ConsoleColor.GetValues(typeof(ConsoleColor));
                colors = (ConsoleColor[]) colors.Where(x => (x != ConsoleColor.Black)).ToArray();

                Dictionary<int, ConsoleColor> axelTypeColor = new Dictionary<int, ConsoleColor>();
                foreach (var i in Enumerable.Range(0, Settings.axTypes.Length))
                {
                    axelTypeColor.Add(i, colors[i]);
                }

                Console.WriteLine(new String('=', Console.WindowWidth));

                // Legend
                for (int i = 0; i < Settings.axTypes.Length; i++)
                {
                    Console.Write($"\n{Settings.axTypes[i].name}:");
                    Console.BackgroundColor = axelTypeColor[i];
                    Console.Write(" ");
                    Console.BackgroundColor = ConsoleColor.Black;
                }
                Console.Write($"\nWaste is in black.\n");

                // Visualization
                foreach (var seqThisMatLen in sequencePerMatLen)
                {
                    Console.Write("\n");
                    foreach (var A in seqThisMatLen)
                    {
                        Console.BackgroundColor = axelTypeColor[A];
                        int charLen =   (int) ((Settings.axTypes[A].length * 1.0 / Settings.matLen) * Console.WindowWidth);
                        Console.Write(new String(' ', charLen));

                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                }
                Console.WriteLine("\n\n" + new String('=', Console.WindowWidth));
            }
        }

        public class AxelType
        {
            public int length { get; set; }
            public int amount { get; set; }
            public string name { get; set; }
        }

    }
}
