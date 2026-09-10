namespace Helpdesk.Shared.DTOs.RequestForm;

public static class FormFieldKeyResolver
{
    public static string Resolve(FormField field, int index)
    {
        if (!string.IsNullOrWhiteSpace(field.Key))
        {
            return field.Key.Trim();
        }

        if (string.IsNullOrWhiteSpace(field.Label))
        {
            return $"field{index + 1}";
        }

        var chars = field.Label.Trim().ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) ? c : '_').ToArray();
        var value = new string(chars);
        while (value.Contains("__", StringComparison.Ordinal))
        {
            value = value.Replace("__", "_", StringComparison.Ordinal);
        }

        value = value.Trim('_');
        return string.IsNullOrWhiteSpace(value) ? $"field{index + 1}" : value;
    }

    public static void EnsureKeys(IList<FormField> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            fields[i].Key = Resolve(fields[i], i);
        }
    }
}
