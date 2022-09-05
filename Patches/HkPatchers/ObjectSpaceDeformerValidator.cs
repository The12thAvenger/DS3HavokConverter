using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class ObjectSpaceDeformerValidator : IHkPatcher
{
    public string FieldName => "objectSpaceDeformer";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        XElement objectSpaceDeformer =
            inputObject.GetElementByAttribute("name", "objectSpaceDeformer")!.Element("record")!;
        bool hasEightBlend =
            objectSpaceDeformer.GetElementByAttribute("name", "eightBlendEntries")!.Element("array")!.HasElements;
        bool hasSevenBlend =
            objectSpaceDeformer.GetElementByAttribute("name", "sevenBlendEntries")!.Element("array")!.HasElements;
        bool hasSixBlend =
            objectSpaceDeformer.GetElementByAttribute("name", "sixBlendEntries")!.Element("array")!.HasElements;
        bool hasFiveBlend =
            objectSpaceDeformer.GetElementByAttribute("name", "fiveBlendEntries")!.Element("array")!.HasElements;

        if (!hasEightBlend && !hasSevenBlend && !hasSixBlend && !hasFiveBlend) return false;

        XElement hclOperator = objectSpaceDeformer.Ancestors("object").Single();

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