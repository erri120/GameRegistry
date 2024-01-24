using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Json.Pointer;
using Json.Schema;
using NLog;
using Yaml2JsonNode;
using YamlDotNet.RepresentationModel;

namespace Validator;

public class EvaluationLogger : ILog
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void Write(Func<string> message, int indent = 0)
    {
        var sb = new StringBuilder();
        if (indent != 0) sb.Append('\t', indent);
        sb.Append(message());

        Logger.Trace(sb.ToString());
    }
}

public static class Runner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        var schemaPath = Path.GetFullPath("../../../../../schemas/game.json");
        var schema = JsonSchema.FromFile(schemaPath, JsonSerializerOptions.Default);

        var evaluationOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical,
            Log = new EvaluationLogger()
        };

        var numFailed = 0;

        var yamlFilesPath = Path.GetFullPath("../../../../../games");
        foreach (var file in Directory.GetFiles(yamlFilesPath))
        {
            if (cancellationToken.IsCancellationRequested) break;

            await using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var failed = Validate(file, fs, schema, evaluationOptions);
            if (failed) numFailed++;
        }

        if (numFailed == 0)
        {
            Logger.Info("All files passed validation");
            return false;
        }

        Logger.Error("{Count} file(s) failed validation", numFailed);
        return true;
    }

    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
    private static bool Validate(string file, Stream fs, JsonSchema schema, EvaluationOptions evaluationOptions)
    {
        Logger.Info("Validating file {File}", file);

        var stream = new YamlStream();
        stream.Load(new StreamReader(fs, Encoding.UTF8));

        var jsonNode = stream.Documents[0].ToJsonNode();
        var results = schema.Evaluate(jsonNode, evaluationOptions);

        var stack = new Stack<EvaluationResults>();
        stack.Push(results);

        var failed = false;
        while (stack.TryPop(out var currentResults))
        {
            foreach (var nestedResults in currentResults.Details)
            {
                stack.Push(nestedResults);
            }

            if (currentResults.IsValid) continue;
            if (currentResults.Errors is null) continue;
            failed = true;

            Logger.Error("{ErrorCount} error(s) in Node {Node}:", currentResults.Errors.Count, currentResults.InstanceLocation.ToString(JsonPointerStyle.UriEncoded));
            foreach (var (_, message) in currentResults.Errors)
            {
                Logger.Error("{Message}", message);
            }
        }

        return failed;
    }
}
