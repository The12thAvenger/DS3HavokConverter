using System.Numerics;

namespace DS3HavokConverter.Patches;

public static class PatchUtils
{
    public static float[] UnpackHkPackedVector3(IEnumerable<ushort> hkPackedVector3)
    {
        int[] intVector = hkPackedVector3.Select(x => x << 16).ToArray();
        float[] hkVector4 = intVector.Select(Convert.ToSingle).ToArray();

        byte[] expBytes = BitConverter.GetBytes(intVector[3]);
        float expCorrection = BitConverter.ToSingle(expBytes, 0);
        hkVector4 = hkVector4.Select(x => x * expCorrection).ToArray();

        return hkVector4;
    }

    public static float[] UnpackHkPackedUnitVector(IEnumerable<ushort> hkPackedUnitVector)
    {
        int[] intVector = hkPackedUnitVector.Select(x => x << 16).ToArray();
        const uint hkPackedUnitVectorOffset = 0x80000000;
        const float hkQuadrealUnpack16UnitVec = 1.0f / (30000.0f * 0x10000);
        float[] hkVector4 = intVector.Select(x => unchecked((int)((uint) x + hkPackedUnitVectorOffset))).Select(Convert.ToSingle).Select(x => x * hkQuadrealUnpack16UnitVec).ToArray();

        return hkVector4;
    }

    public static Vector4 ToVector4(this IList<float> floats)
    {
        return new Vector4(floats[0], floats[1], floats[2], floats[3]);
    }
    
    public static Quaternion ToQuaternion(this IList<float> floats)
    {
        return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
    }

    public static Vector3 ToVector3(this IList<float> floats)
    {
        return new Vector3(floats[0], floats[1], floats[2]);
    }
}