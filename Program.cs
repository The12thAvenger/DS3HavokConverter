using System.Xml.Linq;

namespace ERClothToDS3;

public static class Program
{
    public static XElement ClassDict { get; set; } = XElement.Load("Res/Ds3ClothClasses.xml");
    
    public static void Main(string[] args)
    {
        XElement inputTagfile = XElement.Load(args[0]);
        XElement outputPackfile = Create2014Packfile();
        ConvertAndAddObjects(inputTagfile, outputPackfile.Element("hksection")!);
    }

    private static void ConvertAndAddObjects(XElement inputTagfile, XElement outputDataSection)
    {
        foreach (XElement hkobject in inputTagfile.Elements("object"))
        {
            XElement typeInfo = inputTagfile.Elements("type")
                .First(x => x.Attribute("id")?.Value == hkobject.Attribute("typeid")?.Value);
            string className = typeInfo.Element("name")!.Attribute("value")!.Value;
            XElement outputObject =
                new XElement(ClassDict.Elements().FirstOrDefault(x => x.Attribute("class")!.Value == className) ??
                             throw new ArgumentOutOfRangeException(nameof(className)));
            TransferMembers(hkobject, typeInfo, outputObject);
            outputDataSection.Add(outputObject);
        }
    }

    private static void TransferMembers(XElement inputObject, XElement typeInfo, XElement outputObject)
    {
        foreach (XElement field in inputObject.Elements("field"))
        {
            XElement outputField =
                outputObject.Elements()
                    .FirstOrDefault(x => x.Attribute("name")!.Value == field.Attribute("name")!.Value) ??
                throw new ArgumentOutOfRangeException(nameof(inputObject),
                    $"Encountered unexpected field {field.Attribute("name")!.Value} in object {inputObject.Attribute("id")?.Value} of type {inputObject.Attribute("typeid")?.Value}");
            if (!field.HasElements)
            {
                outputField.Value = string.Empty;
            }

            if (field.Elements().Count() > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(inputObject),
                    $"Encountered unexpected number of field values in field {field.Attribute("name")!.Value} in object {inputObject.Attribute("id")?.Value} of type {inputObject.Attribute("typeid")?.Value}");
            }
            
            if (field.Elements().First().Name == "array")
            {
                
            }
            else if (field.Elements().First().Name == "pointer")
            {
                outputField.Value = ConvertId(field.Elements().First().Attribute("id")!.Value);
            }
            else if (field.Elements().First().Name == "record")
            {
                outputField.Value = ConvertId(field.Elements().First().Attribute("id")!.Value);
            }
            else
            {
                outputField.Value = field.Elements().First().Attribute("value")!.Value;
            }
        }
        
    }

    private static string ConvertId(string id)
    {
        return id.Replace("object", "#");
    }

    private static XElement Create2014Packfile()
    {
        return new XElement("hkpackfile", new XAttribute("classversion", "11"),
            new XAttribute("contentsversion", "hk_2014.1.0-r1"),
            new XElement("hksection", new XAttribute("name", "__data__")));
    }
}