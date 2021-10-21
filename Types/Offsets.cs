/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/D2RAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using D2RAssist.Helpers;
using System;
using System.Diagnostics;
using System.Globalization;

namespace D2RAssist.Types
{
    public class Offsets
    {
        public int PlayerUnit { get; set; }
        public int InGameMap { get; set; }

        public Offsets(ProcessModule processModule, ref IntPtr processHandle)
        {
            PlayerUnit = GetPlayerUnitOffset(processModule, ref processHandle);
            InGameMap = GetInGameMapOffset(processModule, ref processHandle);
        }

        private int GetPlayerUnitOffset(ProcessModule processModule, ref IntPtr processHandle)
        {
            int playerOffset = 0;
            var patternAddress = MemoryScan(processModule, ref processHandle, "57 48 83 ec ? 33 ff 48 8d 05");
            if (patternAddress != IntPtr.Zero)
            {
                var offsetBuffer = new byte[4];
                var resultRelativeAddress = IntPtr.Add(patternAddress, 10);
                if (WindowsExternal.ReadProcessMemory(processHandle, resultRelativeAddress, offsetBuffer, sizeof(int),
                    out _))
                {
                    var offsetAddressToInt = BitConverter.ToInt32(offsetBuffer, 0);
                    var delta = patternAddress.ToInt64() - processModule.BaseAddress.ToInt64();
                    var offset = (int)(delta + 14 + offsetAddressToInt);
                    Console.WriteLine("We found the offset for PlayerUnit at 0x" + offset.ToString("X"));
                    playerOffset = offset;
                }
            }

            return playerOffset;
        }

        private int GetInGameMapOffset(ProcessModule processModule, ref IntPtr processHandle)
        {
            var inGameMapOffset = 0;
            IntPtr patternAddress = MemoryScan(processModule, ref processHandle, "40 84 ed 0f 94 05");
            if (patternAddress != IntPtr.Zero)
            {
                var offsetBuffer = new byte[4];
                var resultRelativeAddress = IntPtr.Add(patternAddress, 6);
                if (WindowsExternal.ReadProcessMemory(processHandle, resultRelativeAddress, offsetBuffer, sizeof(int),
                    out _))
                {
                    var offsetAddressToInt = BitConverter.ToInt32(offsetBuffer, 0);
                    var delta = patternAddress.ToInt64() - processModule.BaseAddress.ToInt64();
                    inGameMapOffset = (int)(delta + 10 + offsetAddressToInt);
                    Console.WriteLine("We found the offset for IsGameMap at 0x" + inGameMapOffset.ToString("X"));
                }
            }

            return inGameMapOffset;
        }

        private static bool CheckPattern(string[] patternBytes, byte[] arrayToCheck)
        {
            var length = arrayToCheck.Length;
            var x = 0;
            foreach (var b in arrayToCheck)
            {
                if (patternBytes[x] == "?")
                    ++x;
                else if (byte.Parse(patternBytes[x], System.Globalization.NumberStyles.HexNumber) == b)
                    ++x;
                else
                    return false;
            }

            return true;
        }

        private static IntPtr MemoryScan(ProcessModule processModule, ref IntPtr processHandle, string pattern)
        {
            IntPtr baseAddress = processModule.BaseAddress;
            var moduleSize = processModule.ModuleMemorySize;
            var patternBytes = pattern.Split(' ');
            var memoryBuffer = new byte[moduleSize];
            if (WindowsExternal.ReadProcessMemory(processHandle, baseAddress, memoryBuffer, moduleSize, out _) == false)
            {
                Console.WriteLine("We failed to read the process memory");
                return IntPtr.Zero;
            }

            try
            {
                for (var y = 0; y < moduleSize; ++y)
                {
                    if (memoryBuffer[y] == byte.Parse(patternBytes[0], NumberStyles.HexNumber))
                    {
                        var arrayToCheck = new byte[patternBytes.Length];
                        for (var x = 0; x < patternBytes.Length; ++x)
                        {
                            arrayToCheck[x] = memoryBuffer[y + x];
                        }

                        if (CheckPattern(patternBytes, arrayToCheck))
                        {
                            return baseAddress + y;
                        }
                        else
                        {
                            y += patternBytes.Length - (patternBytes.Length / 2);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }

            Console.WriteLine("We failed to find the pattern");
            return IntPtr.Zero;
        }
    }
}
