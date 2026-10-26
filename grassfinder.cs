using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

public enum Version
{
    PostB1_5,
    Post1_8
}

public enum Rotation
{
    None_0,
    Clockwise_90,
    Rotate_180,
    Clockwise_270,
    All
}

public readonly struct Position
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public Position(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Position Rotate(Rotation rotation)
    {
        return rotation switch
        {
            Rotation.None_0 => this,
            Rotation.Clockwise_90 => new Position(-Z, Y, X),
            Rotation.Rotate_180 => new Position(-X, Y, -Z),
            Rotation.Clockwise_270 => new Position(Z, Y, -X),
            _ => this
        };
    }

    public static Position operator +(Position a, Position b)
        => new Position(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Position operator -(Position a, Position b)
        => new Position(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
}

public readonly struct Offset
{
    public readonly sbyte X;
    public readonly sbyte Y;
    public readonly sbyte Z;

    public Offset(sbyte x, sbyte y, sbyte z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Offset Rotate(Rotation rotation)
    {
        return rotation switch
        {
            Rotation.None_0 => this,
            Rotation.Clockwise_90 => new Offset((sbyte)(15 - Z), Y, (sbyte)X),
            Rotation.Rotate_180 => new Offset((sbyte)(15 - X), Y, (sbyte)(15 - Z)),
            Rotation.Clockwise_270 => new Offset((sbyte)Z, Y, (sbyte)(15 - X)),
            _ => this
        };
    }

    public int Abs() => Math.Abs(X) + Math.Abs(Y) + Math.Abs(Z);

    public static Offset operator -(Offset a, Offset b)
        => new Offset((sbyte)(a.X - b.X), (sbyte)(a.Y - b.Y), (sbyte)(a.Z - b.Z));

    public static bool operator ==(Offset a, Offset b)
        => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    public static bool operator !=(Offset a, Offset b)
        => !(a == b);

    public override bool Equals(object? obj)
        => obj is Offset other && this == other;

    public override int GetHashCode()
        => HashCode.Combine(X, Y, Z);
}

public class grassfinder
{
    private const string Path = "offsets.txt";
    private const int XMult = 0x2fc20f;
    private const int ZMult = 0x6ebfff5;
    private const long LcgMult = 0x285b825;
    private const long LcgAddend = 11;

    public static void Main(string[] args)
    {
        int spawnrange = 2000;
        (int yMin, int yMax) yrange = (63, 85);

        List<(Position pos, Offset off)> rawRows = LoadGrassPositions(Path);
        int grassCount = rawRows.Count;
        if (grassCount == 0)
        {
            throw new Exception("no grass found in file offsets.txt");
        }

        bool deltaMode = true;
        double maxAvgDelta = 2.0;
        double maxTotalDelta = maxAvgDelta * grassCount;

        Version version = Version.PostB1_5;
        Rotation selectedRotation = Rotation.None_0;

        foreach (var arg in args)
        {
            if (arg == "--post-1.8")
            {
                version = Version.Post1_8;
            }
            else if (arg.StartsWith("--rot="))
            {
                string rotVal = arg.Substring(6);
                if (Enum.TryParse<Rotation>(rotVal, true, out var parsedRot))
                {
                    selectedRotation = parsedRot;
                }
            }
        }

        Console.WriteLine($"Running with version: {version}");
        Console.WriteLine($"Rotation mode: {selectedRotation}");
        Console.WriteLine($"Thread count: {Environment.ProcessorCount}");

        (int yMin, int yMax) = version switch
        {
            Version.PostB1_5 => yrange,
            Version.Post1_8 => (0, 1),
            _ => (0, 0)
        };

        List<Rotation> rotationsToTest = new List<Rotation>();
        if (selectedRotation == Rotation.All)
        {
            rotationsToTest.Add(Rotation.None_0);
            rotationsToTest.Add(Rotation.Clockwise_90);
            rotationsToTest.Add(Rotation.Rotate_180);
            rotationsToTest.Add(Rotation.Clockwise_270);
        }
        else
        {
            rotationsToTest.Add(selectedRotation);
        }

        foreach (var currentRotation in rotationsToTest)
        {
            Console.WriteLine();

            var rotatedRows = ApplyRotation(rawRows, currentRotation);
            Position recorigin = rotatedRows[0].pos;

            string rotString = currentRotation.ToString();
            string padding = new string(' ', 14 - rotString.Length);
            object consoleLock = new object();

            Parallel.For(-spawnrange, spawnrange, x =>
            {
                for (int z = -spawnrange; z < spawnrange; z++)
                {
                    for (int y = yMin; y < yMax; y++)
                    {
                        Position testpos = new Position(x, y, z);

                        if (deltaMode)
                        {
                            double? maybeAvgDelta = GetPosDelta(testpos, rotatedRows, recorigin, version, grassCount, maxTotalDelta);
                            if (maybeAvgDelta.HasValue)
                            {
                                PrintMatch($"avg delta: {maybeAvgDelta.Value:F3}");
                            }
                        }
                        else if (CheckPos(testpos, rotatedRows, recorigin, version))
                        {
                            PrintMatch("matches");
                        }

                        void PrintMatch(string status)
                        {
                            Position delta = testpos - recorigin;

                            lock (consoleLock)
                            {
                                Console.WriteLine($"[{rotString}]{padding} {x,6}{y,6}{z,6}    {status}    delta: {delta.X,6}{delta.Y,6}{delta.Z,6}");
                            }
                        }
                    }
                }
            });
        }
    }

    private static List<(Position pos, Offset off)> ApplyRotation(List<(Position pos, Offset off)> originalRows, Rotation rotation)
    {
        if (rotation == Rotation.None_0) return originalRows;

        Position originPos = originalRows[0].pos;
        var rotatedList = new List<(Position pos, Offset off)>();

        foreach (var (pos, off) in originalRows)
        {
            Position relPos = pos - originPos;
            Position rotatedRelPos = relPos.Rotate(rotation);
            Offset rotatedOff = off.Rotate(rotation);

            rotatedList.Add((originPos + rotatedRelPos, rotatedOff));
        }

        return rotatedList;
    }

    private static bool CheckPos(Position testpos, List<(Position pos, Offset off)> rows, Position recorigin, Version version)
    {
        foreach (var (pos, off) in rows)
        {
            Position posAbs = testpos + pos - recorigin;
            Offset testoff = GrassOffsetFromPos(posAbs, version);

            if (testoff != off)
            {
                return false;
            }
        }
        return true;
    }

    private static double? GetPosDelta(Position testpos, List<(Position pos, Offset off)> rows, Position recorigin, Version version, int grassCount, double maxTotalDelta)
    {
        double totalDelta = 0.0;
        Position tempPos = testpos - recorigin;

        foreach (var (pos, off) in rows)
        {
            Position posAbs = tempPos + pos;
            Offset testoff = GrassOffsetFromPos(posAbs, version);

            totalDelta += (off - testoff).Abs();
            if (totalDelta >= maxTotalDelta)
            {
                return null;
            }
        }

        return totalDelta / grassCount;
    }

    private static Offset GrassOffsetFromPos(Position p, Version version)
    {
        return GrassOffset(p.X, p.Y, p.Z, version);
    }

    private static Offset GrassOffset(int x, int y, int z, Version version)
    {
        long seed = version switch
        {
            Version.PostB1_5 => GetCoordRandom(x, y, z),
            Version.Post1_8 => GetCoordRandom(x, 0, z),
            _ => 0
        };

        return new Offset(
            (sbyte)(seed >> 16 & 15),
            (sbyte)(seed >> 20 & 15),
            (sbyte)(seed >> 24 & 15)
        );
    }

    private static long GetCoordRandom(int x, int y, int z)
    {
        long seed = (long)(x * XMult) ^ (long)(z * ZMult) ^ (long)y;
        seed = seed * seed * LcgMult + seed * LcgAddend;
        return seed;
    }

    private static List<(Position pos, Offset off)> LoadGrassPositions(string path)
    {
        var list = new List<(Position pos, Offset off)>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);

            var pos = new Position(
                int.Parse(parts[0]),
                int.Parse(parts[1]),
                int.Parse(parts[2])
            );

            var off = new Offset(
                sbyte.Parse(parts[3]),
                sbyte.Parse(parts[4]),
                sbyte.Parse(parts[5])
            );

            list.Add((pos, off));
        }

        return list;
    }
}