using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;

namespace NaturesSwiftnessParse
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
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

            var clientIdArg = new Option<string>(
                name: "clientId",
                description: "WarcraftLogs Client ID found at https://fresh.warcraftlogs.com/api/clients/. Also can be defined in WarcraftLogsClient.json."
            )
            { Arity = ArgumentArity.ZeroOrOne };

            var clientSecretArg = new Option<string>(
                name: "clientSecret",
                description: "WarcraftLogs Client Secret found at https://fresh.warcraftlogs.com/api/clients/ when the client was created. Also can be defined in WarcraftLogsClient.json."
            )
            { Arity = ArgumentArity.ZeroOrOne };

            

            // Root command
            var rootCommand = new RootCommand("Parses Nature's Swiftness usage from a Warcraft Logs report.");
            rootCommand.Add(reportIdArg);
            rootCommand.Add(fightIdArg);
            rootCommand.Add(clientIdArg);
            rootCommand.Add(clientSecretArg);

            // Handler
            rootCommand.SetHandler(async (string reportId, int? fightId, string clientId, string clientSecret) =>
            {
                await NaturesSwiftnessParse.RunNaturesSwiftnessReport(
                    new List<string> { reportId },
                    fightId,
                    clientId,
                    clientSecret
                );
            }, reportIdArg, fightIdArg, clientIdArg, clientSecretArg);

            // Run
            return rootCommand.Invoke(args);
        }
    }
}