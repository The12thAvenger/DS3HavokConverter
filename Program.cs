using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DS3HavokConverter;

public static class Program
{
    private static XElement _classDict = null!;

    private static XElement _inputTagfile = null!;

    private static XElement _outputPackfile = null!;

    private static ImmutableDictionary<string,string> _renamedFields = null!;

    private static ImmutableDictionary<string, string> _removedFieldValues = null!;

    public static void Main(string[] args)
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            _classDict = XElement.Load(Path.Join(baseDirectory, "Res", "Ds3HavokClasses.xml"));
        }
        catch (Exception)
        {
            Console.WriteLine("Unable to load havok class dictionary.");
            Console.ReadKey(true);
            return;
        }
        
        _renamedFields = new Dictionary<string, string>
        {
            { "softContactSeperationVelocity", "softContactSeparationVelocity" }
        }.ToImmutableDictionary();

        _removedFieldValues = new Dictionary<string, string>
        {
            { "subSteps", "0" },
            { "isShared", "false" },
            { "batchSizeSpu", "512" },
            { "padding", "0" }
        }.ToImmutableDictionary();

        _inputTagfile = XElement.Load(args[0]);
        _outputPackfile = Create2014Packfile();
        ConvertAndAddObjects(_inputTagfile, _outputPackfile.Element("hksection")!);
        
        File.Delete(args[0] + ".bak");
        File.Move(args[0], args[0] + ".bak");

        XmlWriterSettings settings = new()
        {
            Encoding = new ASCIIEncoding(),
            Indent = true,
            CheckCharacters = true
        };
        using XmlWriter writer = XmlWriter.Create(args[0], settings);
        _outputPackfile.Save(writer);
    }

    private static void ConvertAndAddObjects(in XElement inputTagfile, XElement outputDataSection)
    {
        string[] ignoredClasses = {"hclStateDependencyGraph"};
        foreach (XElement hkobject in inputTagfile.Elements("object"))
        {
            string className = hkobject.GetObjectTypeName(inputTagfile);
            if (ignoredClasses.Contains(className))
            {
                continue;
            }

            if (_classDict.GetElementByAttribute("class", className) is { } outputTemplate)
            {
                XElement outputObject = GetObject(hkobject.Element("record")!, outputTemplate);
                outputObject.SetAttributeValue("name", ConvertPointer(hkobject.Attribute("id")!.Value));
                outputDataSection.Add(outputObject);
            }
            else
            {
                Console.WriteLine($"No template found for class {className}.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }
    
    private static XElement GetObject(in XElement inputObject, in XElement template)
    {
        XElement outputObject = new(template);
        foreach (XElement outputField in outputObject.Elements())
        {
            XElement? inputFieldValue = GetInputField(outputField, inputObject);
            inputFieldValue ??= inputObject.Parent?.Name == "field"
                ? GetInputField(outputField, inputObject.Parent!.Parent!)
                : null;
            
            if (inputFieldValue is null)
            {
                if (!_removedFieldValues.TryGetValue(outputField.Attribute("name")!.Value, out string? outputValue))
                {
                    throw new Exception($"No input field found for field {outputField.Attribute("name")!.Value} in object of class {outputObject.Attribute("class")?.Value ?? "unknown"}");
                }
                
                outputField.Value = outputValue;
                continue;
            }

            if (outputField.HasElements)
            {
                XElement subTemplate = outputField.Elements().First();
                outputField.RemoveNodes();
                outputField.Add(GetSubObjects(inputFieldValue, subTemplate));
                continue;
            }

            // better but needs optimization
            // string[] vectorTypes = { "hkVector4", "hkMatrix4" };
            // bool isVector = vectorTypes.Contains(inputFieldValue.Ancestors("field").First().GetFieldTypeName(_inputTagfile)) 
            //              || (inputFieldValue.Name == "array"
            //               && vectorTypes.Contains(inputFieldValue.GetElementTypeName(_inputTagfile)));

            bool isVector = outputField.Value.Contains('(');
            outputField.Value = GetValue(inputFieldValue, isVector);
        }

        return outputObject;
    }

    private static XElement? GetInputField(XElement outputField, in XElement inputObject, string? alternateName = null)
    {
        string xmlName = outputField.Attribute("name")!.Value;
        if (alternateName is null)
        {
            _renamedFields.TryGetValue(xmlName, out alternateName);
        }
        string fieldName = alternateName ?? xmlName;
            
        XElement? inputField = inputObject.GetElementByAttribute("name", fieldName);
        if (inputField is not null) return inputField.Elements().Single();

        string numSuffix = string.Concat(fieldName.Reverse().TakeWhile(char.IsNumber).Reverse());
        if (int.TryParse(numSuffix, out int fieldIndex))
        {
            XElement? inputArray = inputObject.GetElementByAttribute("name", fieldName.Replace(numSuffix, ""))?.Elements().Single();
            if (inputArray is not null)
            {
                if (inputArray.GetElementTypeName(_inputTagfile) == "hkPackedVector3")
                {
                    return inputArray.Elements().SelectMany(x => x.Element("field")!.Element("array")!.Elements()).ToList()[fieldIndex - 1];
                }
                
                return inputArray.Elements().ToList()[fieldIndex - 1];
            }
        }

        IEnumerable<XElement> unusedSubObjects = inputObject.Elements()
           .Where(x => outputField.Parent!.Elements()
                          .All(y => y.Attribute("name")!.Value != x.Attribute("name")!.Value) 
                    && (x.Element("record") is not null
                     || x.Element("array")?.GetElementTypeName(_inputTagfile) == "hclSimulateOperator::Config"))
           .Select(x => x.Descendants("record").First());

        foreach (XElement subObject in unusedSubObjects)
        {
            inputField = GetInputField(outputField, subObject, fieldName);
            if (inputField is not null) return inputField;
        }

        return null;
    }

    private static string GetValue(in XElement inputFieldValue, bool isVector)
    {
        switch (inputFieldValue.Name.ToString())
        {
            case "array":
                return GetArrayValue(inputFieldValue, isVector);
            case "pointer":
                return ConvertPointer(inputFieldValue.Attribute("id")!.Value);
            case "real":
                return inputFieldValue.Attribute("dec")!.Value.Replace("e", "E");
            case "integer":
                string stringVal = inputFieldValue.Attribute("value")!.Value;
                return int.TryParse(stringVal, out _) ? stringVal : unchecked((int) uint.Parse(stringVal)).ToString();
            case "record":
                if (inputFieldValue.GetObjectTypeName(_inputTagfile) == "hkQsTransform")
                {
                    return GetHkQsTransform(inputFieldValue);
                }
                
                throw new ArgumentException($"Output template incomplete. Field {inputFieldValue.Parent!.Attribute("name")!.Value} in object of class {inputFieldValue.Ancestors("object").First().GetObjectTypeName(_inputTagfile)} does not contain expected subobject");
            default:
                if (inputFieldValue.HasElements ||
                    inputFieldValue.Attribute("value") == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(inputFieldValue), $"Unexpected field type encountered in field {inputFieldValue.Parent!.Attribute("name")!.Value} in object {inputFieldValue.Ancestors("object").First().Attribute("id")?.Value} of class {inputFieldValue.Ancestors("object").First().GetObjectTypeName(_inputTagfile)}");
                }

                return inputFieldValue.Attribute("value")!.Value;
        }
    }

    private static List<XElement> GetSubObjects(in XElement inputFieldValue, XElement template)
    {
        return inputFieldValue.Name.ToString() switch
        {
            "record" => new List<XElement> { GetObject(inputFieldValue, template) },
            "array" => inputFieldValue.Elements().Select(element => GetObject(element, template)).ToList(),
            _ => throw new ArgumentOutOfRangeException(nameof(inputFieldValue), $"Unexpected field type encountered in field {inputFieldValue.Parent!.Attribute("name")!.Value} in object {inputFieldValue.Ancestors("object").First().Attribute("id")?.Value} of class {inputFieldValue.Ancestors("object").First().GetObjectTypeName(_inputTagfile)}")
        };
    }
    
    private static string GetArrayValue(in XElement array, bool isVector)
    {
        if (array.Attribute("count")!.Value == "0")
        {
            return string.Empty;
        }

        string value = string.Empty;
        foreach (XElement element in array.Elements())
        {
            value += GetValue(element, isVector);
            value += value.Contains('(') || value.Contains('#') ? "\n" : " ";
        }

        if (isVector && !value.Contains('('))
        {
            value = FormatVector(value);
        }

        return value;
    }

    private static string FormatVector(in string value)
    {
        string vectorValue = string.Empty;
        string[] values = value.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < values.Length; i++)
        {
            if (i % 4 == 0)
            {
                vectorValue += "(";
            }

            vectorValue += values[i];

            if (i % 4 == 3)
            {
                vectorValue += ")";
            }
            else
            {
                vectorValue += " ";
            }
        }
        
        return vectorValue;
    }

    private static string GetHkQsTransform(in XElement record)
    {
        return record.Elements().Aggregate(string.Empty, 
            (current, transformRow) => current + GetValue(transformRow.Element("array")!, true));
    }

    private static string ConvertPointer(string id)
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