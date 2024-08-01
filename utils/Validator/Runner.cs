using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Json.More;
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
        CustomFormats.RegisterAll();

        var root = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!).Parent!.Parent!.Parent!.Parent!.Parent!.ToString();
        Logger.Info("Using root: {Root}", root);

        var schemaPath = Path.Combine(root, "schemas", "game.json");
        var schema = JsonSchema.FromFile(schemaPath, JsonSerializerOptions.Default);

        var evaluationOptions = new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical,
            RequireFormatValidation = true,
            OnlyKnownFormats = true,
            Log = new EvaluationLogger()
        };

        var idValidator = new IdValidator();

        var numFailed = 0;
        var yamlFilesPath = Path.Combine(root, "games");
        foreach (var file in Directory.GetFiles(yamlFilesPath))
        {
            try
            {
                Logger.Info("Validating file {File}", file);
                if (cancellationToken.IsCancellationRequested) break;

                await using var fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var stream = new YamlStream();
                stream.Load(new StreamReader(fs, Encoding.UTF8));

                var jsonNode = stream.Documents[0].ToJsonNode();
                if (jsonNode is null || ValidateAgainstSchema(jsonNode, schema, evaluationOptions) || ValidateValues(idValidator, file, jsonNode))
                {
                    numFailed++;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception while validating file {File}", file);
            }
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
    private static bool ValidateAgainstSchema(JsonNode jsonNode, JsonSchema schema, EvaluationOptions evaluationOptions)
    {
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

    private static bool ValidateValues(IdValidator idValidator, string file, JsonNode jsonNode)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
        if (!Guid.TryParse(fileNameWithoutExtension, out var idInFileName))
        {
            Logger.Error("File {File} doesn't have a valid GUID as a file name", file);
            return true;
        }

        var idInData = Guid.Parse(jsonNode["id"]!.GetValue<string>());
        if (!idInData.Equals(idInFileName))
        {
            Logger.Error("ID in file name {File} doesn't match with ID in data {ID}", file, idInData.ToString());
            return true;
        }

        idValidator.AddIds(jsonNode);
        return false;
    }
}
