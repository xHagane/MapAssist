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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms.VisualStyles;

namespace D2RAssist.Helpers
{
    class GameMemory
    {
        private ProcessWrapper _processWrapper;

        public GameData GetGameData()
        {
            // Clean up and organize, add better exception handeling.
            try
            {
                if (_processWrapper is null)
                {
                    _processWrapper = new ProcessWrapper();
                }

                var pPlayerUnit = IntPtr.Add(_processWrapper.ProcessAddress, _processWrapper.Offsets.PlayerUnit);

                var addressBuffer = new byte[8];
                var dwordBuffer = new byte[4];
                var byteBuffer = new byte[1];
                _processWrapper.ReadMemory(pPlayerUnit, addressBuffer, addressBuffer.Length, out _);

                var playerUnit = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);
                var pPlayer = IntPtr.Add(playerUnit, 0x10);
                var pAct = IntPtr.Add(playerUnit, 0x20);

                _processWrapper.ReadMemory(pPlayer, addressBuffer, addressBuffer.Length, out _);
                var player = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var playerNameBuffer = new byte[16];
                _processWrapper.ReadMemory(player, playerNameBuffer, playerNameBuffer.Length, out _);
                var playerName = Encoding.ASCII.GetString(playerNameBuffer);

                _processWrapper.ReadMemory(pAct, addressBuffer, addressBuffer.Length, out _);
                var aAct = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var pActUnk1 = IntPtr.Add(aAct, 0x70);

                _processWrapper.ReadMemory(pActUnk1, addressBuffer, addressBuffer.Length, out _);
                var aActUnk1 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var pGameDifficulty = IntPtr.Add(aActUnk1, 0x830);

                _processWrapper.ReadMemory(pGameDifficulty, byteBuffer, byteBuffer.Length, out _);
                ushort aGameDifficulty = byteBuffer[0];

                var aDwAct = IntPtr.Add(aAct, 0x20);
                _processWrapper.ReadMemory(aDwAct, dwordBuffer, dwordBuffer.Length, out _);

                var aMapSeed = IntPtr.Add(aAct, 0x14);

                var pPath = IntPtr.Add(playerUnit, 0x38);

                _processWrapper.ReadMemory(pPath, addressBuffer, addressBuffer.Length, out _);
                var path = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var pRoom1 = IntPtr.Add(path, 0x20);

                _processWrapper.ReadMemory(pRoom1, addressBuffer, addressBuffer.Length, out _);
                var aRoom1 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var pRoom2 = IntPtr.Add(aRoom1, 0x18);
                _processWrapper.ReadMemory(pRoom2, addressBuffer, addressBuffer.Length, out _);
                var aRoom2 = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                var pLevel = IntPtr.Add(aRoom2, 0x90);
                _processWrapper.ReadMemory(pLevel, addressBuffer, addressBuffer.Length, out _);
                var aLevel = (IntPtr)BitConverter.ToInt64(addressBuffer, 0);

                if (addressBuffer.All(o => o == 0))
                {
                    _processWrapper.Close();
                    _processWrapper = null;
                    return null;
                }

                var aLevelId = IntPtr.Add(aLevel, 0x1F8);
                _processWrapper.ReadMemory(aLevelId, dwordBuffer, dwordBuffer.Length, out _);
                var dwLevelId = BitConverter.ToUInt32(dwordBuffer, 0);

                var posXAddress = IntPtr.Add(path, 0x02);
                var posYAddress = IntPtr.Add(path, 0x06);

                _processWrapper.ReadMemory(aMapSeed, dwordBuffer, dwordBuffer.Length, out _);
                var mapSeed = BitConverter.ToUInt32(dwordBuffer, 0);

                _processWrapper.ReadMemory(posXAddress, addressBuffer, addressBuffer.Length, out _);
                var playerX = BitConverter.ToUInt16(addressBuffer, 0);

                _processWrapper.ReadMemory(posYAddress, addressBuffer, addressBuffer.Length, out _);
                var playerY = BitConverter.ToUInt16(addressBuffer, 0);

                var uiSettingsPath = IntPtr.Add(_processWrapper.ProcessAddress, _processWrapper.Offsets.InGameMap);
                _processWrapper.ReadMemory(uiSettingsPath, byteBuffer, byteBuffer.Length, out _);
                var mapShown = BitConverter.ToBoolean(byteBuffer, 0);

                return new GameData
                {
                    PlayerPosition = new Point(playerX, playerY),
                    MapSeed = mapSeed,
                    Area = (Area)dwLevelId,
                    Difficulty = (Difficulty)aGameDifficulty,
                    MapShown = mapShown,
                    MainWindowHandle = _processWrapper.MainWindowHandle
                };
            }
            catch (Exception)
            {
                if (_processWrapper == null)
                {
                    return null;
                }

                _processWrapper.Close();
                _processWrapper = null;
                return null;
            }
        }
    }
}
