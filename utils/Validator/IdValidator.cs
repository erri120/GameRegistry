using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Json.Pointer;
using NLog;

namespace Validator;

public class IdValidator
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private record IdPointerInfo(string Name, string InputPointer)
    {
        public JsonPointer Pointer { get; init; } = JsonPointer.Parse(InputPointer);
        public string InputPointer { get; init; } = InputPointer;
    }

    private record IdInfo(string Name, string Id);

    private static readonly IdPointerInfo[] IdPointers =
    [
        new IdPointerInfo("Game ID", "#/id"),
        new IdPointerInfo("Steam Game 'appId'", "#/stores/steam/appId"),
        new IdPointerInfo("GOG Game 'productId", "#/stores/gog/productId"),
        new IdPointerInfo("EGS Game 'catalogItemId'", "#/stores/egs/catalogItemId"),
        new IdPointerInfo("Xbox Game 'id'", "#/stores/xbox/id"),
    ];

    private readonly Dictionary<string, List<IdInfo>> _foundIds = new();

    public void AddIds(JsonNode jsonNode)
    {
        foreach (var pointerInfo in IdPointers)
        {
            if (!pointerInfo.Pointer.TryEvaluate(jsonNode, out var result) || result is null) continue;
            var id = result.ToString();
            Logger.Info("Id: {Id} | Pointer: {Pointer}", id, pointerInfo.InputPointer);
        }
    }
}
