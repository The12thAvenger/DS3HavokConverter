using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class ReferencedObjectsPatcher : IHkPatcher
{
    public string FieldName => "referencedObjects";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        IEnumerable<string> referencedIds = inputObject.GetElementByAttribute("name", "bodyCinfos")!.Element("array")!
            .Elements()
            .Select(x => x.GetElementByAttribute("name", "shape")!.Element("pointer")!.Attribute("id")!.Value)
            .Select(ConvertPointer);

        outputField.Value = string.Join("\n", referencedIds);

        return true;
    }

    private static string ConvertPointer(string id)
    {
        int name = int.Parse(id.Replace("object", ""));
        if (name == 0)
        {
            return "null";
        }

        name += 89;
        return "#" + name;
    }
}