using System.Text.Json;
using RonFlow.Application;
using RonFlow.Domain;

namespace RonFlow.Api.Controllers;

internal static class TaskCodeTraceabilityMapper
{
    public static bool TryMap(
        JsonElement? request,
        out TaskCodeTraceability? codeTraceability,
        out ValidationError? validationError)
    {
        codeTraceability = null;
        validationError = null;

        if (request is null || request.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (request.Value.ValueKind != JsonValueKind.Object)
        {
            validationError = new ValidationError("codeTraceability", "程式修改追蹤必須是物件");
            return false;
        }

        if (!TryMapItems(request.Value, "api", "codeTraceability.api", out var apiItems, out validationError)
            || !TryMapItems(request.Value, "frontendPages", "codeTraceability.frontendPages", out var frontendPageItems, out validationError)
            || !TryMapItems(request.Value, "frontendComponents", "codeTraceability.frontendComponents", out var frontendComponentItems, out validationError))
        {
            return false;
        }

        codeTraceability = new TaskCodeTraceability(apiItems, frontendPageItems, frontendComponentItems);
        return true;
    }

    private static bool TryMapItems(
        JsonElement request,
        string propertyName,
        string fieldPrefix,
        out IReadOnlyList<TaskCodeTraceabilityItem> mappedItems,
        out ValidationError? validationError)
    {
        mappedItems = [];
        validationError = null;

        if (!request.TryGetProperty(propertyName, out var itemsElement) || itemsElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            validationError = new ValidationError(fieldPrefix, "程式修改追蹤清單必須是陣列");
            return false;
        }

        var results = new List<TaskCodeTraceabilityItem>();
        var index = 0;

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}]", "程式修改追蹤項目必須是物件");
                return false;
            }

            var changeType = item.TryGetProperty("changeType", out var changeTypeElement) && changeTypeElement.ValueKind != JsonValueKind.Null
                ? (changeTypeElement.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;

            if (changeType is not ("added" or "modified" or "removed"))
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}].changeType", "程式修改類型必須為 added、modified 或 removed");
                return false;
            }

            var target = item.TryGetProperty("target", out var targetElement) && targetElement.ValueKind != JsonValueKind.Null
                ? (targetElement.GetString() ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(target))
            {
                validationError = new ValidationError($"{fieldPrefix}[{index}].target", "程式修改項目名稱為必填欄位");
                return false;
            }

            results.Add(new TaskCodeTraceabilityItem(changeType, target));
            index += 1;
        }

        mappedItems = results;
        return true;
    }
}
