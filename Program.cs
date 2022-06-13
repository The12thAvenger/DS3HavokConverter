using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ERClothToDS3;

public static class Program
{
    public static XElement ClassDict { get; set; }
    
    public static XElement? InputTagfile { get; set; }
    
    public static XElement? OutputPackfile { get; set; }
    
    public static void Main(string[] args)
    {
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        ClassDict = XElement.Load(Path.Join(baseDirectory, "Res", "Ds3ClothClasses.xml"));
        
        InputTagfile = XElement.Load(args[0]);
        OutputPackfile = Create2014Packfile();
        ConvertAndAddObjects(InputTagfile, OutputPackfile.Element("hksection")!);
        
        File.Delete(args[0] + ".bak");
        File.Move(args[0], args[0] + ".bak");

        XmlWriterSettings settings = new()
        {
            Encoding = new ASCIIEncoding(),
            Indent = true,
            CheckCharacters = true
        };
        using XmlWriter writer = XmlWriter.Create(args[0], settings);
        OutputPackfile.Save(writer);
    }

    private static void ConvertAndAddObjects(XElement inputTagfile, XElement outputDataSection)
    {
        string[] ignoredClasses = {"hclStateDependencyGraph"};
        foreach (XElement hkobject in inputTagfile.Elements("object"))
        {
            string className = hkobject.GetTypeName();
            if (ignoredClasses.Contains(className))
            {
                continue;
            }

            if (ClassDict.GetElementWithAttribute("class", className) is { } outputTemplate)
            {
                XElement outputObject = new(outputTemplate);
                TransferMembers(hkobject.Element("record")!, outputObject);
                outputObject.SetAttributeValue("name", ConvertId(hkobject.Attribute("id")!.Value));
                outputDataSection.Add(outputObject);
            }
            else
            {
#if DEBUG
                Console.WriteLine($"No template found for class {className}.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                continue;
#endif
                throw new ArgumentOutOfRangeException(nameof(className), $"No template found for class {className}.");
            }
        }
    }

    private static void TransferMembers(XElement inputObject, XElement outputObject)
    {
        List<string> fieldNames = new List<string>();
        foreach (XElement inputField in inputObject.Elements("field"))
        {
            string fieldName = inputField.Attribute("name")!.Value;
            XElement? outputField = outputObject.GetElementWithAttribute("name", fieldName);

            if (outputField is null)
            {
                outputField = outputObject.GetElementWithAttribute("name", fieldName + "1");
                if (outputField is null) continue;
                int fieldIndex = 1;
                foreach (XElement arrayElement in inputField.Element("array")!.Elements())
                {
                    if (inputField.Element("array")!.GetElementTypeName() == "hkPackedVector3")
                    {
                        foreach (XElement nestedElement in arrayElement.Element("field")!.Element("array")!.Elements())
                        {
                            if (fieldIndex > 1)
                            {
                                outputField = outputObject.GetElementWithAttribute("name", fieldName + fieldIndex)!;
                            }
                            
                            XElement currentInputField = new("field", new XAttribute("name", fieldName + fieldIndex), nestedElement);
                            inputField.Parent!.Add(currentInputField);
                            fieldNames.Add(fieldName + fieldIndex);
                            TransferValues(currentInputField, outputField);
                            fieldIndex++;
                        }
                    }
                    else
                    {
                        if (fieldIndex > 1)
                        {
                            outputField = outputObject.GetElementWithAttribute("name", fieldName + fieldIndex)!;
                        }
                        
                        XElement currentInputField = new("field", new XAttribute("name", fieldName + fieldIndex), arrayElement);
                        fieldNames.Add(fieldName + fieldIndex);
                        inputField.Parent!.Add(currentInputField);
                        TransferValues(currentInputField, outputField);
                        fieldIndex++;
                    }
                }
            }
            else
            {
                fieldNames.Add(fieldName);
                TransferValues(inputField, outputField);
            }
        }

        foreach (string fieldName in outputObject.Elements().Select(x => x.Attribute("name")!.Value))
        {
            if (fieldNames.Contains(fieldName)) continue;
            if (HandleChangedFields(fieldName, inputObject, outputObject)) continue;
            throw new Exception($"Template field {fieldName} in class template {outputObject.Attribute("class")?.Value ?? "unknown"} was not set");
        }
        
    }

    private static void TransferValues(XElement inputField, XElement outputField)
    {
        if (!inputField.HasElements)
        {
            outputField.RemoveNodes();
            outputField.Value = string.Empty;
        }

        if (inputField.Elements().Count() > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(inputField),
                $"Encountered unexpected number of field values in field {inputField.Attribute("name")!.Value} in object {inputField.Parent!.Attribute("id")?.Value} of type {inputField.Parent!.Attribute("typeid")?.Value}");
        }

        if (inputField.Element("array") is { } array)
        {
            if (array.Attribute("count")!.Value == "0")
            {
                outputField.Value = string.Empty;
                if (outputField.Attribute("numelements") != null)
                {
                    outputField.Attribute("numelements")!.Value = "0";
                }
            }
            else
            {
                if (outputField.Attribute("numelements") != null)
                {
                    outputField.Attribute("numelements")!.Value = array.Attribute("count")!.Value;
                }
            }

            string elementTypeName = array.GetElementTypeName();

            XElement? outputSubobjectTemplate = null;
            string outputValue = "\n";
            if (outputField.Value.Contains('(') && array.Element("real") != null)
            {
                switch (array.Attribute("count")!.Value)
                {
                    case "4":
                        outputValue = "(";
                        break;
                    case "16":
                    {
                        outputValue = "";
                        TransferHkMatrix4(array, outputValue);

                        return;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(inputField),
                            $"Unhandled Vector4 type encountered in field {inputField.Attribute("name")!.Value} in object {inputField.Parent!.Attribute("id")?.Value} of type {inputField.Parent!.Attribute("typeid")?.Value}");
                }
            }

            foreach (XElement element in array.Elements())
            {
                if (elementTypeName == "hkQsTransform")
                {
                    foreach (XElement transformRow in element.Elements())
                    {
                        outputValue += "(";

                        foreach (XElement value in transformRow.Element("array")!.Elements())
                        {
                            outputValue += value.Attribute("dec")!.Value.Replace("e", "E") + " ";
                        }

                        outputValue = outputValue[..^1];
                        outputValue += ")";
                    }

                    outputValue += "\n";
                    continue;
                }

                switch (element.Name.ToString())
                {
                    case "pointer":
                        outputValue += ConvertId(element.Attribute("id")!.Value) + "\n";
                        break;
                    case "real":
                        outputValue += element.Attribute("dec")!.Value.Replace("e", "E") + " ";
                        break;
                    case "integer":
                        string stringVal = element.Attribute("value")!.Value;
                        outputValue += (int.TryParse(stringVal, out _)
                            ? stringVal
                            : unchecked((int) uint.Parse(stringVal)).ToString()) + " ";
                        break;
                    case "record":
                        if (!outputField.HasElements)
                        {
                            throw new ArgumentException(
                                $"Output template mismatch. Hkparam {outputField.Attribute("name")!.Value} in hkobject of class {outputField.Parent!.Attribute("class")?.Value ?? "unknown"} does not contain expected subobject");
                        }

                        if (outputSubobjectTemplate == null)
                        {
                            outputSubobjectTemplate = outputField.Element("hkobject")!;
                            outputField.RemoveNodes();
                        }

                        XElement outputSubobject = new(outputSubobjectTemplate);
                        outputField.Add(outputSubobject);
                        TransferMembers(element, outputSubobject);
                        break;
                    case "array":
                        switch (elementTypeName)
                        {
                            case "hkVector4":
                            {
                                outputValue += "(";
                                foreach (XElement vector4Value in element.Elements())
                                {
                                    if (vector4Value.Name != "real")
                                    {
                                        throw new ArgumentOutOfRangeException(nameof(inputField),
                                            $"Unexpected Vector4 value type \"{vector4Value.Name}\" encountered in field {inputField.Attribute("name")!.Value} in object {inputField.Parent!.Attribute("id")?.Value} of type {inputField.Parent!.Attribute("typeid")?.Value}");
                                    }

                                    outputValue += vector4Value.Attribute("dec")!.Value.Replace("e", "E") + " ";
                                }

                                outputValue = outputValue[..^1];
                                outputValue += ")\n";
                                break;
                            }
                            case "hkMatrix4":
                                outputValue = TransferHkMatrix4(element, outputValue);
                                outputValue += "\n";
                                break;
                        }

                        break;
                    default:
                        if (element.HasElements || outputField.HasElements || element.Attribute("value") == null)
                        {
                            throw new ArgumentOutOfRangeException(nameof(inputField),
                                $"Unexpected field type encountered in field {inputField.Attribute("name")!.Value} in object {inputField.Parent!.Attribute("id")?.Value} of type {inputField.Parent!.Attribute("typeid")?.Value}");
                        }

                        outputValue += element.Attribute("value")!.Value + " ";
                        break;
                }
            }

            if (outputField.Value.StartsWith("("))
            {
                outputValue = outputValue[..^1];
                outputValue += ")";
            }

            if (outputValue != "\n")
            {
                outputField.Value = outputValue;
            }
        }
        else if (inputField.Element("pointer") is { } pointer)
        {
            outputField.Value = ConvertId(pointer.Attribute("id")!.Value);
        }
        else if (inputField.Element("real") is { } real)
        {
            outputField.Value = real.Attribute("dec")!.Value.Replace("e", "E");
        }
        else if (inputField.Element("integer") is { } integer)
        {
            string stringVal = integer.Attribute("value")!.Value;
            outputField.Value = int.TryParse(stringVal, out _) ? stringVal : unchecked((int) uint.Parse(stringVal)).ToString();
        }
        else if (inputField.Element("record") is { } record)
        {
            if (!outputField.HasElements)
            {
                throw new ArgumentException(
                    $"Output template incomplete. Hkparam {outputField} in hkobject of class {outputField.Parent!.Attribute("class")?.Value ?? "unknown"} does not contain expected subobject");
            }

            if (outputField.Elements().Count() > 1)
            {
                throw new ArgumentException(
                    $"Output template mismatch. Unexpected subobject array encountered in Hkparam {outputField} in hkobject of class {outputField.Parent!.Attribute("class")?.Value ?? "unknown"}");
            }

            TransferMembers(record, outputField.Element("hkobject")!);
        }
        else
        {
            if (inputField.Elements().First().HasElements || outputField.HasElements ||
                inputField.Elements().First().Attribute("value") == null)
            {
                throw new ArgumentOutOfRangeException(nameof(inputField),
                    $"Unexpected field type encountered in field {inputField.Attribute("name")!.Value} in object {inputField.Parent!.Attribute("id")?.Value} of type {inputField.Parent!.Attribute("typeid")?.Value}");
            }

            outputField.Value = inputField.Elements().First().Attribute("value")!.Value;
        }
    }

    private static string TransferHkMatrix4(XElement array, string outputValue)
    {
        List<XElement> elements = array.Elements().ToList();
        for (int i = 0; i < array.Elements().Count(); i++)
        {
            if (i % 4 == 0)
            {
                outputValue += "(";
            }

            XElement value = elements[i];
            outputValue += value.Attribute("dec")!.Value.Replace("e", "E") + " ";

            if ((i + 1) % 4 == 0)
            {
                outputValue = outputValue[..^1];
                outputValue += ")";
            }
        }

        return outputValue;
    }

    private static bool HandleChangedFields(string fieldName, XElement inputObject, XElement outputObject)
    {
        string? className = outputObject.Attribute("class")?.Value;
        switch (className)
        {
            case "hclSimClothDataOverridableSimulationInfo":
                switch (fieldName)
                {
                    case "collisionTolerance":
                    {
                        string collisionTolerance =
                            inputObject.Parent!.Parent!.GetElementWithAttribute("name", "landscapeCollisionData")!
                                .Element("record")!.GetElementWithAttribute("name", "collisionTolerance")!
                                .Element("real")!.Attribute("dec")!.Value.Replace("e", "E");
                        outputObject.GetElementWithAttribute("name", "collisionTolerance")!.Value = collisionTolerance;
                        return true;
                    }
                    case "subSteps":
                        outputObject.GetElementWithAttribute("name", "subSteps")!.Value = "0";
                        return true;
                    case "pinchDetectionEnabled":
                    {
                        string pinchDetectionEnabled =
                            inputObject.Parent!.Parent!.GetElementWithAttribute("name", "pinchDetectionEnabled")!
                                .Element("bool")!.Attribute("value")!.Value;
                        outputObject.GetElementWithAttribute("name", "pinchDetectionEnabled")!.Value = pinchDetectionEnabled;
                        return true;
                    }
                    case "landscapeCollisionEnabled":
                    {
                        string landscapeCollisionEnabled =
                            inputObject.Parent!.Parent!.GetElementWithAttribute("name", "landscapeCollisionEnabled")!
                                .Element("bool")!.Attribute("value")!.Value;
                        outputObject.GetElementWithAttribute("name", "landscapeCollisionEnabled")!.Value =
                            landscapeCollisionEnabled;
                        return true;
                    }
                    case "transferMotionEnabled":
                    {
                        string transferMotionEnabled =
                            inputObject.Parent!.Parent!.GetElementWithAttribute("name", "transferMotionEnabled")!
                                .Element("bool")!.Attribute("value")!.Value;
                        outputObject.GetElementWithAttribute("name", "transferMotionEnabled")!.Value = transferMotionEnabled;
                        return true;
                    }
                }

                break;
            case "hclSimulateOperator":
                XElement hclSimulateOperatorConfig = inputObject.GetElementWithAttribute("name", "simulateOpConfigs")!.Element("array")!.Element(
                    "record")!;
                switch (fieldName)
                {
                    case "subSteps":
                        outputObject.GetElementWithAttribute("name", "subSteps")!.Value = hclSimulateOperatorConfig
                            .GetElementWithAttribute("name", "subSteps")!.Element("integer")!.Attribute("value")!.Value;
                        return true;
                    case "numberOfSolveIterations":
                        outputObject.GetElementWithAttribute("name", "numberOfSolveIterations")!.Value = hclSimulateOperatorConfig
                            .GetElementWithAttribute("name", "numberOfSolveIterations")!.Element("integer")!.Attribute("value")!.Value;
                        return true;
                    case "constraintExecution":
                        string constraintExecution = "";
                        foreach (XElement constraintIndex in hclSimulateOperatorConfig
                                     .GetElementWithAttribute("name", "constraintExecution")!.Element("array")!.Elements())
                        {
                            constraintExecution += constraintIndex.Attribute("value")!.Value + " ";
                        }
                        outputObject.GetElementWithAttribute("name", "constraintExecution")!.Value =
                            constraintExecution;
                        return true;
                    case "adaptConstraintStiffness":
                        outputObject.GetElementWithAttribute("name", "adaptConstraintStiffness")!.Value = hclSimulateOperatorConfig
                            .GetElementWithAttribute("name", "adaptConstraintStiffness")!.Element("bool")!.Attribute("value")!.Value;
                        return true;
                }
                break;
            case null:
                if (fieldName == "padding")
                {
                    outputObject.GetElementWithAttribute("name", "padding")!.Value = "0";
                    return true;
                }

                break;
        }

        if (fieldName == "batchSizeSpu")
        {
            outputObject.GetElementWithAttribute("name", "batchSizeSpu")!.Value = "512";
            return true;
        }

        return false;
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

    private static XElement? GetElementWithAttribute(this XElement xElement, string attributeName, string attributeValue)
    {
        return xElement.Elements().FirstOrDefault(x => x.Attribute(attributeName)!.Value == attributeValue);
    }

    private static string GetTypeName(this XElement hkobject)
    {
        return InputTagfile!.Elements("type")
            .First(x => x.Attribute("id")!.Value == hkobject.Attribute("typeid")!.Value)
            .Element("name")!.Attribute("value")!.Value;
    }
    
    private static string GetElementTypeName(this XElement hkobject)
    {
        return InputTagfile!.Elements("type")
            .First(x => x.Attribute("id")!.Value == hkobject.Attribute("elementtypeid")!.Value)
            .Element("name")!.Attribute("value")!.Value;
    }
}