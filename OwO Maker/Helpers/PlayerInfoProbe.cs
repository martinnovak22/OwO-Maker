using OwO_Maker.Core;
using OwOMaker.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace OwO_Maker.Helpers
{
    /// <summary>
    /// Diagnostic read of the player identity (character name, player id) from a NosTale client.
    /// Logs every step so we can see exactly where the chain breaks if the signature or
    /// offsets have drifted with a client update.
    /// </summary>
    public static class PlayerInfoProbe
    {
        public static List<string> Run(Mem mem)
        {
            var lines = new List<string>();

            try
            {
                lines.Add($"PID {mem.Proc.Id}, module base 0x{(uint)mem.BaseAddress:X8}, size {mem.Proc.MainModule.ModuleMemorySize} bytes");

                // sanity check: a signature we know works today
                var miniGameManagerPattern = mem.FindPattern(Structs.Pattern.TMiniGameManager);
                lines.Add($"TMiniGameManager pattern (sanity check): {(miniGameManagerPattern == 0 ? "NOT FOUND" : $"found at 0x{(uint)miniGameManagerPattern:X8}")}");

                var matches = mem.FindPatterns(Structs.Pattern.TPlayerManager);
                if (matches.Count == 0)
                {
                    lines.Add("TPlayerManager pattern NOT FOUND — signature does not match this client version.");
                    return lines;
                }
                var patternAddr = (nint)matches[0];
                lines.Add($"TPlayerManager pattern: {matches.Count} match(es), using first at 0x{(uint)patternAddr:X8}");

                var staticAddr = mem.ReadMemory<uint>(patternAddr + 6);
                lines.Add($"Static pointer address (A1 operand): 0x{staticAddr:X8}");

                var manager = mem.ReadMemory<IntPtr>(patternAddr + 6, [0x0]);
                lines.Add($"TPlayerManager object: 0x{(uint)manager:X8}");
                if (manager == IntPtr.Zero)
                {
                    lines.Add("TPlayerManager is null — client not fully loaded?");
                    return lines;
                }

                var playerPtr = mem.ReadMemory<IntPtr>(manager + Structs.TPlayerManager.Player);
                var playerId = mem.ReadMemory<int>(manager + Structs.TPlayerManager.PlayerId);
                lines.Add($"Player object: 0x{(uint)playerPtr:X8}, PlayerId: {playerId}");
                if (playerPtr == IntPtr.Zero)
                {
                    lines.Add("Player is null — probably on login / character select screen. Log in and probe again.");
                    return lines;
                }

                // dump the area around the name pointer so we can spot offset drift by eye
                var around = mem.ReadMemoryData(playerPtr + Structs.TMapPlayer.NamePtr - 0x10, null, 0x30);
                lines.Add($"Player+0x{(uint)(Structs.TMapPlayer.NamePtr - 0x10):X}..+0x{(uint)(Structs.TMapPlayer.NamePtr + 0x20):X}: {BitConverter.ToString(around)}");

                var namePtr = mem.ReadMemory<IntPtr>(playerPtr + Structs.TMapPlayer.NamePtr);
                lines.Add($"Name pointer (Player+0x{(uint)Structs.TMapPlayer.NamePtr:X}): 0x{(uint)namePtr:X8}");
                if (namePtr == IntPtr.Zero)
                {
                    lines.Add("Name pointer is null — offset 0x1EC may have drifted, scanning objects for Delphi string fields:");
                    ScanForNames(mem, playerPtr, "Player", 0x1000, lines);
                    ScanForNames(mem, manager, "Manager", 0x400, lines);
                    return lines;
                }

                var nameLen = mem.ReadMemory<int>(namePtr - 4);
                lines.Add($"Delphi length prefix (namePtr - 4): {nameLen}");

                int readLen = nameLen > 0 && nameLen <= CharacterName.MaxLength ? nameLen : 16;
                var raw = mem.ReadMemoryData(namePtr, null, readLen);
                lines.Add($"Raw bytes at namePtr: {BitConverter.ToString(raw)}");

                if (CharacterName.TryDecode(nameLen, raw, out var name))
                {
                    lines.Add($"CHARACTER NAME: '{name}' (PlayerId {playerId})");
                }
                else
                {
                    lines.Add($"Could not decode a plausible name — raw ASCII: '{Encoding.ASCII.GetString(raw).Replace('\0', '.')}'");
                    lines.Add("Offset 0x1EC has probably drifted — scanning objects for Delphi string fields:");
                    ScanForNames(mem, playerPtr, "Player", 0x1000, lines);
                    ScanForNames(mem, manager, "Manager", 0x400, lines);
                }
            }
            catch (Exception ex)
            {
                lines.Add($"Probe failed: {ex.GetType().Name}: {ex.Message}");
            }

            return lines;
        }

        // Treats every 4-byte field of the object as a potential string pointer and reports
        // the ones that decode to a plausible name (Delphi AnsiString and UnicodeString),
        // so a drifted name offset can be read straight out of the log.
        private static void ScanForNames(Mem mem, IntPtr objAddr, string label, int range, List<string> lines)
        {
            if (objAddr == IntPtr.Zero)
                return;

            var buffer = mem.ReadMemoryData(objAddr, null, range);
            int found = 0;

            for (int off = 0; off + 4 <= buffer.Length; off += 4)
            {
                int ptr = BitConverter.ToInt32(buffer, off);
                if (ptr <= 0x10000) // null or clearly not a user-space pointer
                    continue;

                int len = mem.ReadMemory<int>((IntPtr)ptr - 4);
                if (len < 2 || len > CharacterName.MaxLength)
                    continue;

                var data = mem.ReadMemoryData((IntPtr)ptr, null, len * 2);

                if (CharacterName.TryDecode(len, data, out var name))
                {
                    lines.Add($"  {label}+0x{off:X3} -> AnsiString '{name}'");
                    found++;
                }
                else if (CharacterName.TryDecodeUtf16(len, data, out var uname))
                {
                    lines.Add($"  {label}+0x{off:X3} -> UnicodeString '{uname}'");
                    found++;
                }
            }

            lines.Add($"{label}: scanned first 0x{range:X} bytes, {found} string candidate(s)");
        }
    }
}
