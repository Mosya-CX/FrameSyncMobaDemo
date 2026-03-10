using System;
using Unity.Mathematics.FixedPoint;

public class DeterministicRandom : Singleton<DeterministicRandom>, IStateful
{
    private uint state;

    public static void Init(uint seed) => Instance.SetSeed(seed);
    public void SetSeed(uint seed) => state = seed;

    private uint NextUInt()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    public fp NextFP() => fp.FromRaw(NextUInt()); 

    public fp Range(fp min, fp max) => min + NextFP() * (max - min);

    public int Range(int min, int max)
    {
        if (min >= max) throw new ArgumentException("min must be less than max");
        uint range = (uint)(max - min);
        return min + (int)(NextUInt() % range);
    }

    public int RangeInclusive(int min, int max)
    {
        if (min > max) throw new ArgumentException("min must be <= max");
        uint range = (uint)(max - min + 1);
        return min + (int)(NextUInt() % range);
    }

    public bool Bool() => (NextUInt() & 1) == 1;

    public bool Bool(fp probability)
    {
        probability = fpmath.clamp(probability, 0, 1);
        return NextFP() < probability;
    }

    public byte Byte() => (byte)(NextUInt() & 0xFF);

    public void NextBytes(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = Byte();
    }

    public fp Angle() => NextFP() * fpmath.PI_TIMES_2;

    public fp2 UnitVector2()
    {
        fp angle = Angle();
        fp s, c;
        fpmath.sincos(angle, out s, out c);
        return new fp2(c, s);
    }

    public fp3 UnitVector3()
    {
        fp u = NextFP();
        fp v = NextFP();

        fp theta = u * fpmath.PI_TIMES_2;
        fp cosPhi = 2 * v - 1;
        fp sinPhi = fpmath.sqrt(1 - cosPhi * cosPhi);

        fp sTheta, cTheta;
        fpmath.sincos(theta, out sTheta, out cTheta);

        return new fp3(sinPhi * cTheta, sinPhi * sTheta, cosPhi);
    }

    public fp2 InsideUnitCircle()
    {
        while (true)
        {
            fp2 p = new fp2(Range(-fp.one, fp.one), Range(-fp.one, fp.one));
            if (fpmath.lengthsq(p) <= fp.one)
                return p;
        }
    }

    public fp3 InsideUnitSphere()
    {
        while (true)
        {
            fp3 p = new fp3(
                Range(-fp.one, fp.one),
                Range(-fp.one, fp.one),
                Range(-fp.one, fp.one)
            );
            if (fpmath.lengthsq(p) <= fp.one)
                return p;
        }
    }

    public object CaptureState() => state;
    public void RestoreState(object stateObj) => state = (uint)stateObj;
}

