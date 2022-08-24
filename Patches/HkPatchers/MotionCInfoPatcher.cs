using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace DS3HavokConverter.Patches.HkPatchers;

[UsedImplicitly]
public class MotionCInfoPatcher : IHkPatcher
{
    public string FieldName => "motionCinfos";

    public bool Patch(XElement outputField, XElement inputObject)
    {
        XElement template = outputField.Element("hkobject")!;
        outputField.RemoveNodes();
        foreach (XElement bodyCinfo in inputObject.GetElementByAttribute("name", "bodyCinfos")!.Element("array")!
                     .Elements())
        {
            outputField.Add(GetMotionCInfo(bodyCinfo, template));
        }

        return true;
    }

    private static XElement GetMotionCInfo(in XElement bodyCinfo, in XElement template)
    {
        XElement motionCinfo = new(template);

        motionCinfo.GetElementByAttribute("name", "motionPropertiesId")!.Value = bodyCinfo
            .GetElementByAttribute("name", "motionPropertiesId")!.Element("integer")!.Attribute("value")!.Value;
        motionCinfo.GetElementByAttribute("name", "enableDeactivation")!.Value = "true";
        motionCinfo.GetElementByAttribute("name", "inverseMass")!.Value = GetInverseMass(bodyCinfo);
        motionCinfo.GetElementByAttribute("name", "massFactor")!.Value = "1";
        motionCinfo.GetElementByAttribute("name", "maxLinearAccelerationDistancePerStep")!.Value =
            "1.8446726481523507E19";
        motionCinfo.GetElementByAttribute("name", "maxRotationToPreventTunneling")!.Value = "1.8446726481523507E19";

        XElement compressedMassProperties = GetCompressedMassProperties(bodyCinfo);
        motionCinfo.GetElementByAttribute("name", "inverseInertiaLocal")!.Value =
            GetInverseInertiaLocal(compressedMassProperties);

        Quaternion motionOrientation = GetMotionOrientation(bodyCinfo, compressedMassProperties);
        motionCinfo.GetElementByAttribute("name", "orientation")!.Value =
            $"({motionOrientation.X} {motionOrientation.Y} {motionOrientation.Z} {motionOrientation.W})";

        motionCinfo.GetElementByAttribute("name", "centerOfMassWorld")!.Value =
            GetCenterOfMassWorld(bodyCinfo, compressedMassProperties, motionOrientation);

        const string vec4Zero = "(0.0 0.0 0.0 0.0)";
        motionCinfo.GetElementByAttribute("name", "linearVelocity")!.Value = vec4Zero;
        motionCinfo.GetElementByAttribute("name", "angularVelocity")!.Value = vec4Zero;

        return motionCinfo;
    }

    private static string GetCenterOfMassWorld(in XElement bodyCinfo, XElement compressedMassProperties,
        Quaternion motionOrientation)
    {
        XElement centerOfMassField =
            compressedMassProperties.GetElementByAttribute("name", "centerOfMass")!.Element("record")!;
        IEnumerable<ushort> packedCenterOfMass = centerOfMassField.Element("field")!.Element("array")!.Elements()
            .Select(x =>
                unchecked((ushort)short.Parse(x.Attribute("value")!.Value)));
        Vector3 centerOfMass = PatchUtils.UnpackHkPackedVector3(packedCenterOfMass).ToVector3();

        centerOfMass = Vector3.Transform(centerOfMass, motionOrientation);

        Vector3 bodyPosition = bodyCinfo.GetElementByAttribute("name", "position")!.Element("array")!.Elements()
            .Select(x => float.Parse(x.Attribute("dec")!.Value, CultureInfo.InvariantCulture)).ToArray().ToVector3();
        centerOfMass += bodyPosition;

        return $"({centerOfMass.X} {centerOfMass.Y} {centerOfMass.Z} 0.0)";
    }

    private static Quaternion GetMotionOrientation(in XElement bodyCinfo, in XElement compressedMassProperties)
    {
        XElement majorAxisSpaceField =
            compressedMassProperties.GetElementByAttribute("name", "majorAxisSpace")!.Element("array")!;

        IEnumerable<ushort> packedMajorAxisSpace = majorAxisSpaceField.Elements()
            .Select(x => unchecked((ushort)short.Parse(x.Attribute("value")!.Value)));
        Quaternion majorAxisSpace = PatchUtils.UnpackHkPackedUnitVector(packedMajorAxisSpace).ToQuaternion();

        Quaternion bodyOrientation = bodyCinfo.GetElementByAttribute("name", "orientation")!.Element("array")!
            .Elements()
            .Select(x => float.Parse(x.Attribute("dec")!.Value, CultureInfo.InvariantCulture)).ToArray().ToQuaternion();

        return Quaternion.Multiply(bodyOrientation, majorAxisSpace);
    }

    private static string GetInverseInertiaLocal(in XElement compressedMassProperties)
    {
        XElement inertia = compressedMassProperties.GetElementByAttribute("name", "inertia")!.Element("record")!;
        IEnumerable<ushort> packedInertia = inertia.Element("field")!.Element("array")!.Elements()
            .Select(x => unchecked((ushort)short.Parse(x.Attribute("value")!.Value)));
        float[] inverseInertiaLocal = PatchUtils.UnpackHkPackedVector3(packedInertia).Select(x => 1 / x).ToArray();

        return $"({inverseInertiaLocal[0]} {inverseInertiaLocal[1]} {inverseInertiaLocal[2]} 1.0)";
    }

    private static XElement GetCompressedMassProperties(in XElement bodyCinfo)
    {
        XElement tagfile = bodyCinfo.Ancestors("hktagfile").First();

        string shapeId =
            bodyCinfo.GetElementByAttribute("name", "shape")!.Element("pointer")!.Attribute("id")!.Value;
        XElement shape = tagfile.Elements("object").First(x => x.Attribute("id")?.Value == shapeId).Element("record")!;

        string propertiesId = shape.GetElementByAttribute("name", "properties")!.Element("pointer")!.Attribute("id")!
            .Value;
        XElement refCountedProperties = tagfile.Elements("object").First(x => x.Attribute("id")?.Value == propertiesId)
            .Element("record")!;

        string shapeMassPropertiesId = refCountedProperties.GetElementByAttribute("name", "entries")!.Element("array")!
            .Element("record")!.GetElementByAttribute("name", "object")!.Element("pointer")!.Attribute("id")!.Value;
        return tagfile.Elements("object").First(x => x.Attribute("id")?.Value == shapeMassPropertiesId)
            .Element("record")!
            .GetElementByAttribute("name", "compressedMassProperties")!.Element("record")!;
    }

    private static string GetInverseMass(in XElement bodyCinfo)
    {
        string massString = bodyCinfo.GetElementByAttribute("name", "mass")!.Element("real")!.Attribute("dec")!.Value;

        return massString == "-1"
            ? "0"
            : (1 / double.Parse(massString, CultureInfo.InvariantCulture)).ToHkxPackString();
    }
}