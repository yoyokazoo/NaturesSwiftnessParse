using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
#if DEBUG
                args = new[]
                {
                    "wqWkVPDMHmxXahR8"
                };
#endif

            /*
             * args = new[]
                {
                    "FjNc9ZVRnrDQ32zG",
                    "fightId", "16"
                };
             * */

            var reportIdArg = new Argument<string>(
                name: "reportId",
                description: "Report Id AKA the string found at the end of https://vanilla.warcraftlogs.com/reports/mQKyfvjhrnGXTzdM (mQKyfvjhrnGXTzdM)"
            );

            var fightIdArg = new Option<int?>(
                name: "fightId",
                description: "Optional fightId for debugging single fights https://vanilla.warcraftlogs.com/reports/mQKyfvjhrnGXTzdM?fight=20 (20)"
            )
            { Arity = ArgumentArity.ZeroOrOne };
            fightIdArg.SetDefaultValue(null);

            var playerNameArg = new Option<string>(
                name: "playerName",
                description: "Optional player name to only process that player's Nature's Swiftnesses"
            )
            { Arity = ArgumentArity.ZeroOrOne };
            playerNameArg.SetDefaultValue(null);


            var eventsToPrintArg = new Option<int>(
                name: "eventsToPrint",
                description: "Optional argument to choose how many NS events to print (default 5)"
            )
            { Arity = ArgumentArity.ZeroOrOne };
            eventsToPrintArg.SetDefaultValue(5);

            var clientIdArg = new Option<string>(
                name: "clientId",
                description: $"WarcraftLogs Client ID found at {WarcraftLogsClient.CLIENT_URL}. Also can be defined in WarcraftLogsClient.json."
            )
            { Arity = ArgumentArity.ZeroOrOne };

            var clientSecretArg = new Option<string>(
                name: "clientSecret",
                description: $"WarcraftLogs Client Secret found at {WarcraftLogsClient.CLIENT_URL} when the client was created. Also can be defined in WarcraftLogsClient.json."
            )
            { Arity = ArgumentArity.ZeroOrOne };

            

            // Root command
            var rootCommand = new RootCommand("Parses Nature's Swiftness usage from a Warcraft Logs report.");
            rootCommand.Add(reportIdArg);
            rootCommand.Add(fightIdArg);
            rootCommand.Add(eventsToPrintArg);
            rootCommand.Add(clientIdArg);
            rootCommand.Add(clientSecretArg);
            rootCommand.Add(playerNameArg);

            // Handler
            rootCommand.SetHandler(async (string reportId, int? fightId, int eventsToPrint, string clientId, string clientSecret, string playerName) =>
            {
                await NaturesSwiftnessParse.RunNaturesSwiftnessReport(
                    new List<string> { reportId },
                    fightId,
                    eventsToPrint,
                    clientId,
                    clientSecret,
                    playerName
                );
            }, reportIdArg, fightIdArg, eventsToPrintArg, clientIdArg, clientSecretArg, playerNameArg);

            // Run
            return rootCommand.Invoke(args);
        }
    }
}