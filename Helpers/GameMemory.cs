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

using D2RAssist.Types;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace D2RAssist.Helpers
{
    class GameMemory
    {
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
                    if (memoryBuffer[y] == byte.Parse(patternBytes[0], System.Globalization.NumberStyles.HexNumber))
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
        private static int GetPlayerUnitOffset(ProcessModule processModule, ref IntPtr processHandle)
        {
            if (Offsets.PlayerUnit == 0)
            {
                var patternAddress = MemoryScan(processModule, ref processHandle, "57 48 83 ec ? 33 ff 48 8d 05");
                if (patternAddress != IntPtr.Zero)
                {
                    var offsetBuffer = new byte[4];
                    var resultRelativeAddress = IntPtr.Add(patternAddress, 10);
                    if (WindowsExternal.ReadProcessMemory(processHandle, resultRelativeAddress, offsetBuffer, sizeof(int), out _))
                    {
                        var offsetAddressToInt = BitConverter.ToInt32(offsetBuffer, 0);
                        var delta = patternAddress.ToInt64() - processModule.BaseAddress.ToInt64();
                        var offset = (int)(delta + 14 + offsetAddressToInt);
                        Console.WriteLine("We found the offset for PlayerUnit at 0x" + offset.ToString("X"));
                        Offsets.PlayerUnit = offset;
                    }
                }
            }
            return Offsets.PlayerUnit;
        }
        private static int GetInGameMapOffset(ProcessModule processModule, ref IntPtr processHandle)
        {
            if (Offsets.InGameMap == 0)
            {
                var patternAddress = MemoryScan(processModule, ref processHandle, "40 84 ed 0f 94 05");
                if (patternAddress != IntPtr.Zero)
                {
                    var offsetBuffer = new byte[4];
                    var resultRelativeAddress = IntPtr.Add(patternAddress, 6);
                    if (WindowsExternal.ReadProcessMemory(processHandle, resultRelativeAddress, offsetBuffer, sizeof(int), out _))
                    {
                        var offsetAddressToInt = BitConverter.ToInt32(offsetBuffer, 0);
                        var delta = patternAddress.ToInt64() - processModule.BaseAddress.ToInt64();
                        var offset = (int)(delta + 10 + offsetAddressToInt);
                        Console.WriteLine("We found the offset for IsGameMap at 0x" + offset.ToString("X"));
                        Offsets.InGameMap = offset;
                    }
                }
            }
            return Offsets.InGameMap;
        }
        public static GameData GetGameData()
        {
            // Clean up and organize, add better exception handeling.
            IntPtr processHandle = IntPtr.Zero;
            try
            {
                Process gameProcess = Process.GetProcessesByName("D2R")[0];
                processHandle =
                    WindowsExternal.OpenProcess((uint)WindowsExternal.ProcessAccessFlags.VirtualMemoryRead, false,
                        gameProcess.Id);
                IntPtr processAddress = gameProcess.MainModule.BaseAddress;
                var playerUnitOffset = GetPlayerUnitOffset(gameProcess.MainModule, ref processHandle);
                IntPtr pPlayerUnit = IntPtr.Add(processAddress, playerUnitOffset);

                var addressBuffer = new byte[8];
                var dwordBuffer = new byte[4];
                var byteBuffer = new byte[1];
                WindowsExternal.ReadProcessMemory(processHandle, pPlayerUnit, addressBuffer, addressBuffer.Length,
                    out _);

                var playerUnit = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);
                IntPtr pPlayer = IntPtr.Add(playerUnit, 0x10);
                IntPtr pAct = IntPtr.Add(playerUnit, 0x20);

                WindowsExternal.ReadProcessMemory(processHandle, pPlayer, addressBuffer, addressBuffer.Length, out _);
                var player = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var playerNameBuffer = new byte[16];
                WindowsExternal.ReadProcessMemory(processHandle, player, playerNameBuffer, playerNameBuffer.Length,
                    out _);
                string playerName = Encoding.ASCII.GetString(playerNameBuffer);

                WindowsExternal.ReadProcessMemory(processHandle, pAct, addressBuffer, addressBuffer.Length, out _);
                var aAct = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                IntPtr pActUnk1 = IntPtr.Add(aAct, 0x70);

                WindowsExternal.ReadProcessMemory(processHandle, pActUnk1, addressBuffer, addressBuffer.Length, out _);
                var aActUnk1 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                IntPtr pGameDifficulty = IntPtr.Add(aActUnk1, 0x830);

                WindowsExternal.ReadProcessMemory(processHandle, pGameDifficulty, byteBuffer, byteBuffer.Length, out _);
                ushort aGameDifficulty = byteBuffer[0];

                IntPtr aDwAct = IntPtr.Add(aAct, 0x20);
                WindowsExternal.ReadProcessMemory(processHandle, aDwAct, dwordBuffer, dwordBuffer.Length, out _);

                IntPtr aMapSeed = IntPtr.Add(aAct, 0x14);

                IntPtr pPath = IntPtr.Add(playerUnit, 0x38);

                WindowsExternal.ReadProcessMemory(processHandle, pPath, addressBuffer, addressBuffer.Length, out _);
                var path = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                IntPtr pRoom1 = IntPtr.Add(path, 0x20);

                WindowsExternal.ReadProcessMemory(processHandle, pRoom1, addressBuffer, addressBuffer.Length, out _);
                var aRoom1 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                IntPtr pRoom2 = IntPtr.Add(aRoom1, 0x18);
                WindowsExternal.ReadProcessMemory(processHandle, pRoom2, addressBuffer, addressBuffer.Length, out _);
                var aRoom2 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                IntPtr pLevel = IntPtr.Add(aRoom2, 0x90);
                WindowsExternal.ReadProcessMemory(processHandle, pLevel, addressBuffer, addressBuffer.Length, out _);
                var aLevel = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                if (addressBuffer.All(o => o == 0))
                {
                    WindowsExternal.CloseHandle(processHandle);
                    return null;
                }

                IntPtr aLevelId = IntPtr.Add(aLevel, 0x1F8);
                WindowsExternal.ReadProcessMemory(processHandle, aLevelId, dwordBuffer, dwordBuffer.Length, out _);
                var dwLevelId = BitConverter.ToUInt32(dwordBuffer, 0);

                IntPtr posXAddress = IntPtr.Add(path, 0x02);
                IntPtr posYAddress = IntPtr.Add(path, 0x06);

                WindowsExternal.ReadProcessMemory(processHandle, aMapSeed, dwordBuffer, dwordBuffer.Length, out _);
                var mapSeed = BitConverter.ToUInt32(dwordBuffer, 0);

                WindowsExternal.ReadProcessMemory(processHandle, posXAddress, addressBuffer, addressBuffer.Length,
                    out _);
                var playerX = BitConverter.ToUInt16(addressBuffer, 0);

                WindowsExternal.ReadProcessMemory(processHandle, posYAddress, addressBuffer, addressBuffer.Length,
                    out _);
                var playerY = BitConverter.ToUInt16(addressBuffer, 0);


                var inGameMapOffset = GetInGameMapOffset(gameProcess.MainModule, ref processHandle);
                IntPtr uiSettingsPath = IntPtr.Add(processAddress, inGameMapOffset);
                WindowsExternal.ReadProcessMemory(processHandle, uiSettingsPath, byteBuffer, byteBuffer.Length,
                    out _);
                var mapShown = BitConverter.ToBoolean(byteBuffer, 0);

                WindowsExternal.CloseHandle(processHandle);

                return new GameData
                {
                    PlayerPosition = new Point(playerX, playerY),
                    MapSeed = mapSeed,
                    Area = (Area)dwLevelId,
                    Difficulty = (Difficulty)aGameDifficulty,
                    MapShown = mapShown,
                    MainWindowHandle = gameProcess.MainWindowHandle
                };
            }
            catch (Exception)
            {
                if (processHandle != IntPtr.Zero)
                    WindowsExternal.CloseHandle(processHandle);
                return null;
            }
        }
    }
}
