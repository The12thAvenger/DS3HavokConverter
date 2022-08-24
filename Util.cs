using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace DS3HavokConverter;

public static class Util
{
    public static XElement? GetElementByAttribute(this XElement xElement, string attributeName, string attributeValue)
    {
        return xElement.Elements().FirstOrDefault(x => x.Attribute(attributeName)!.Value == attributeValue);
    }

    public static string GetObjectTypeName(this XElement hkobject, XElement tagfile)
    {
        if (hkobject.FirstNode is XComment typeComment)
        {
            return typeComment.Value.Replace("ArrayOf", "").Trim();
        }

        if (hkobject.Attribute("typeid") is { } objTypeId)
        {
            return GetTypeName(objTypeId.Value, tagfile);
        }

        if (hkobject.Parent?.Attribute("elementtypeid") is { } elementTypeId)
        {
            return GetTypeName(elementTypeId.Value, tagfile);
        }

        if (hkobject.Ancestors("field").FirstOrDefault() is { } field)
        {
            return field.GetFieldTypeName(tagfile);
        }

        throw new ArgumentException("Type Id cannot be determined", nameof(hkobject));
    }

    public static string GetFieldTypeName(this XElement field, XElement tagfile, List<string>? fieldPath = null)
    {
        while (true)
        {
            fieldPath ??= new List<string>();
            fieldPath.Insert(0, field.Attribute("name")!.Value);

            if (field.Parent!.Parent!.Attribute("typeid") is not { } objTypeId)
            {
                field = field.Ancestors("field").First();
                continue;
            }

            XElement objectType = GetType(objTypeId.Value, tagfile);

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            // Rider is drunk
            foreach (string fieldName in fieldPath)
            {
                string? typeId = objectType.Element("fields")?.GetElementByAttribute("name", fieldName)
                    ?.Attribute("typeid")!.Value;
                if (typeId is null) return string.Empty;
                objectType = GetType(typeId, tagfile);
            }

            return objectType.Element("name")!.Attribute("value")!.Value;
        }
    }

    public static string GetElementTypeName(this XElement array, XElement tagfile)
    {
        return GetTypeName(array.Attribute("elementtypeid")!.Value, tagfile);
    }

    private static string GetTypeName(string typeId, XElement tagfile)
    {
        return GetType(typeId, tagfile).Element("name")!.Attribute("value")!.Value;
    }

    private static XElement GetType(string typeId, XElement tagfile)
    {
        XElement type = tagfile.Elements("type")
            .First(x => x.Attribute("id")!.Value == typeId);

        if (type.Element("name")!.Attribute("value")!.Value != "hkArray") return type;

        return GetType(type.Element("subtype")!.Attribute("id")!.Value, tagfile);
    }

    public static string ToHkxPackString(this double number)
    {
        return number % 1 == 0
            ? number.ToString("0.0", CultureInfo.InvariantCulture)
            : number.ToString("G16", CultureInfo.InvariantCulture);
    }
}