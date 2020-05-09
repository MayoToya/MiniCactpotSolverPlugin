using Dalamud.Game.Command;
using Dalamud.Plugin;
using dwyl.GetMgpExpectationFunction.Composition;
using MessagePack;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace dwyl.MiniCactpotSolverPlugin
{
    public class MiniCactpotSolverPlugin : IDalamudPlugin
    {
        public string Name => "Mini Cactpot Solver Plugin";
        private DalamudPluginInterface _pluginInterface;
        private HttpClientWrapper _httpClient;
        private const string CommandName = "/pmcs";
        private static readonly Uri FunctionUri = new Uri("http://dwylfunctions.azurewebsites.net/api/getmgpexpectation");
        private CancellationTokenSource _cancellationTokenSource;
        private static readonly MediaTypeHeaderValue MediaTypeHeaderValue = new MediaTypeHeaderValue(@"application/json");

        #region IDalamudPlugin initialization/deinitialization
        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this._pluginInterface = pluginInterface;
            this._httpClient = new HttpClientWrapper();
            this._cancellationTokenSource = null;
            // Set up command handlers
            pluginInterface.CommandManager.AddHandler(CommandName, new CommandInfo(GetMgpExpectation)
            {
                HelpMessage = "In-Game Mini Cactpot Solver. Usage: /pmcs <top row><middle row><bottom row>"
            });
        }

        public void Dispose()
        {
            this._cancellationTokenSource?.Cancel();

            // Remove command handlers
            this._pluginInterface.CommandManager.RemoveHandler(CommandName);
            this._httpClient.Dispose();
            this._pluginInterface.Dispose();
        }
        #endregion


        private async Task PostAndWriteResultAsync(string argument)
        {
            this._cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = this._cancellationTokenSource.Token;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                this._pluginInterface.Framework.Gui.Chat.Print("Calculating...");

                var byteArrayContent = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(new { numbers = argument }));
                byteArrayContent.Headers.ContentType = MediaTypeHeaderValue;

                cancellationToken.ThrowIfCancellationRequested();

                var (statusCode, bytes) = await this._httpClient.PostAsync(FunctionUri, byteArrayContent, cancellationToken)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (statusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            var results = MessagePackSerializer.Deserialize<V2>(bytes).Result;

                            var maxMean = results.OrderByDescending(x => x.Value.Mean).First().Value.Mean;
                            var maxMedian = results.OrderByDescending(x => x.Value.Median).First().Value.Median;

                            this._pluginInterface.Framework.Gui.Chat.Print(
                                "Line                   Mean      Median       Worst   ");
                            this._pluginInterface.Framework.Gui.Chat.Print(
                                "------------------------------------------------------");

                            foreach (var result in results)
                            {
                                var (Mean, Median, (value, probability)) = result.Value;
                                var line =
                                    $"{result.Key,-17}  {Mean,8:F}{(Mean.Equals(maxMean) ? " *" : "  ")}  {Median,8:F}{(Median.Equals(maxMedian) ? " *" : "  ")}  {value,5:D}({probability,3}%)";

                                this._pluginInterface.Framework.Gui.Chat.Print(line);
                            }

                            break;
                        }

                    case HttpStatusCode.BadRequest:
                        this._pluginInterface.Framework.Gui.Chat.PrintError("Please check your numbers and try again.");
                        break;

                    default:
                        this._pluginInterface.Framework.Gui.Chat.Print("Server error. Please try later.");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                this._pluginInterface.Framework.Gui.Chat.Print("Cancelled.");
            }
            finally
            {
                this._cancellationTokenSource.Dispose();
                this._cancellationTokenSource = null;
            }
        }

        #region Chat command handlers
        private async void GetMgpExpectation(string command, string arguments)
        {
            if (arguments == "c" || arguments == "C" || arguments == "cancel" || arguments == "Cancel")
            {
                if (this._cancellationTokenSource != null)
                {
                    this._cancellationTokenSource.Cancel();
                    return;
                }

                this._pluginInterface.Framework.Gui.Chat.PrintError("Nothing to cancel.");
                return;
            }

            if (this._cancellationTokenSource != null)
            {
                this._pluginInterface.Framework.Gui.Chat.PrintError("Wait a previous request for a moment.");
                return;
            }

            if (string.IsNullOrEmpty(arguments) || arguments.Length != 9)
            {
                this._pluginInterface.Framework.Gui.Chat.PrintError("Enter 9 digits without space.");
                return;
            }

            await PostAndWriteResultAsync(arguments).ConfigureAwait(false);
        }
        #endregion
    }
}
