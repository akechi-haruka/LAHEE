using System.Text;
using LAHEE;
using Microsoft.Extensions.Logging;

namespace Haruka.Arcade.Hexedit2;

public enum PatchType {
    Binary,
    StringAscii,
    StringUtf8,
    StringShiftJis,
    StringUtf16Le,
    StringUtf16Be
}

class Patch {
    public enum PatchResult {
        Ok,
        OkAlready,
        LengthMismatch,
        ByteMismatch,
        StringTooLong
    }

    internal static byte[] Parse(string patchString, PatchType type, out List<long> unknownBytes) {
        unknownBytes = new List<long>();

        if (type == PatchType.Binary) {
            List<byte> tobytes = new List<byte>();
            unknownBytes = new List<long>();
            string[] array = patchString.Split(new char[] { ',', ' ' });
            for (int i = 0; i < array.Length; i++) {
                string s = array[i];
                if (s.Equals("??") || s.Equals("0x??")) {
                    tobytes.Add(0);
                    unknownBytes.Add(i);
                } else {
                    tobytes.Add(Convert.ToByte(s, 16));
                }
            }

            return tobytes.ToArray();
        } else if (type == PatchType.StringAscii) {
            return Encoding.ASCII.GetBytes(ReplaceSpecialCharacters(patchString));
        } else if (type == PatchType.StringUtf8) {
            return Encoding.UTF8.GetBytes(ReplaceSpecialCharacters(patchString));
        } else if (type == PatchType.StringShiftJis) {
            return Encoding.GetEncoding("SHIFT-JIS").GetBytes(ReplaceSpecialCharacters(patchString));
        } else if (type == PatchType.StringUtf16Le) {
            return Encoding.Unicode.GetBytes(ReplaceSpecialCharacters(patchString));
        } else if (type == PatchType.StringUtf16Be) {
            return Encoding.BigEndianUnicode.GetBytes(ReplaceSpecialCharacters(patchString));
        } else {
            throw new Exception("invalid patch type: " + type);
        }
    }

    internal static String ReplaceSpecialCharacters(string str) {
        return str.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\ ", " ").Replace("\\\u3000", "\u3000").Replace("\\0", "\0").Replace("\\[", "[");
    }

    internal static PatchResult ApplyPatch(ref byte[] data, long offset, byte[] patch, byte[] original, List<long> unknownBytesPatch, List<long> unknownBytesOriginal, bool isString = false) {
        if (original != null) {
            if (isString) {
                if (patch.Length > original.Length) {
                    Log.Hex.LogError("String length (" + patch.Length + ", " + Encoding.UTF8.GetString(patch) + ") is too long for original (" + original.Length + ")");
                    return PatchResult.StringTooLong;
                }
            } else if (original.Length != patch.Length) {
                Log.Hex.LogError("patch and original have different length");
                return PatchResult.LengthMismatch;
            }
        }

        bool allMatch = true;
        for (int i = 0; i < patch.Length; i++) {
            if (!unknownBytesPatch.Contains(i) && data[offset + i] != patch[i]) {
                allMatch = false;
                break;
            }
        }

        if (allMatch) {
            Log.Hex.LogWarning("Patch at 0x" + offset.ToString("X2") + " is already applied");
            return PatchResult.OkAlready;
        }

        if (original != null) {
            for (int i = 0; i < original.Length; i++) {
                if (!unknownBytesOriginal.Contains(i) && data[offset + i] != original[i]) {
                    Log.Hex.LogError("Byte mismatch at location " + $"0x{(offset + i):X}" + ", expected: " + $"0x{(original[i]):X}" + ", got: " + $"0x{(data[offset + i]):X}");
                    return PatchResult.ByteMismatch;
                }
            }
        }

        int j;
        for (j = 0; j < patch.Length; j++) {
            if (!unknownBytesPatch.Contains(j)) {
                data[offset + j] = patch[j];
            }
        }

        if (isString && original != null) {
            for (; j < original.Length; j++) {
                data[offset + j] = 0x00;
            }
        }

        return PatchResult.Ok;
    }

    internal static List<long> SearchOffsets(byte[] data, byte[] patch, List<long> unknownBytes, int maxHits) {
        if (unknownBytes == null) {
            unknownBytes = new List<long>();
        }

        List<long> results = new List<long>();
        if (unknownBytes.Contains(0)) {
            Log.Hex.LogError("The first byte of a search list can't be unknown!");
            return results;
        }

        if (Log.Hex.IsEnabled(LogLevel.Debug)) {
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < patch.Length; i++) {
                if (unknownBytes.Contains(i)) {
                    str.Append("0x?? ");
                } else {
                    str.Append("0x");
                    str.Append(patch[i].ToString("X2"));
                    str.Append(' ');
                }
            }

            Log.Hex.LogDebug("Performing search in " + data.Length + " bytes for " + str);
            Log.Hex.LogDebug("Unknown bytes are: " + string.Join(" ", unknownBytes));
        }

        int hitOffset = 0;
        for (int i = 0; i < data.Length - patch.Length; i++) {
            if (data[i] == patch[hitOffset]) {
                for (hitOffset = 1; i + hitOffset < data.Length - patch.Length && hitOffset < patch.Length; hitOffset++) {
                    if (unknownBytes.Contains(hitOffset) || data[i + hitOffset] == patch[hitOffset]) {
                        if (hitOffset == patch.Length - 1) {
                            Log.Hex.LogInformation("Successful hit at 0x" + i.ToString("X2"));
                            results.Add(i);
                            if (results.Count >= maxHits) {
                                Log.Hex.LogInformation("Max hits reached: " + maxHits);
                                return results;
                            }

                            break;
                        }
                    } else {
                        if (hitOffset > 3) {
                            Log.Hex.LogDebug("Possible hit failed at 0x" + i.ToString("X2"));
                        }

                        break;
                    }
                }

                i += hitOffset;
                hitOffset = 0;
            }
        }

        Log.Hex.LogInformation(results.Count + " hits");
        return results;
    }
}