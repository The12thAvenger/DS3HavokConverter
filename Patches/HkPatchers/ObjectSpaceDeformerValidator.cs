using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class ObjectSpaceDeformerValidator : IHkPatcher
{
    public string FieldName => "objectSpaceDeformer";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        bool hasEightBlend =
            inputObject.GetElementByAttribute("name", "eightBlendEntries")!.Element("array")!.HasElements;
        bool hasSevenBlend =
            inputObject.GetElementByAttribute("name", "sevenBlendEntries")!.Element("array")!.HasElements;
        bool hasSixBlend =
            inputObject.GetElementByAttribute("name", "sixBlendEntries")!.Element("array")!.HasElements;
        bool hasFiveBlend =
            inputObject.GetElementByAttribute("name", "fiveBlendEntries")!.Element("array")!.HasElements;

        if (!hasEightBlend && !hasSevenBlend && !hasSixBlend && !hasFiveBlend) return false;

        XElement hclOperator = inputObject.Ancestors("object").Single();

        string className = hclOperator.Element("record")!.GetObjectTypeName(inputObject.Ancestors("tagfile").Single());
        string operatorName = hclOperator.Element("record")!.GetElementByAttribute("name", "name")!.Element("string")!
            .Attribute("value")!.Value;

        Console.WriteLine(
            $"Warning: The {className} named {operatorName} references vertices weighted to more than 4 bones which will be lost during the conversion process.");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();

        return false;
    }
}