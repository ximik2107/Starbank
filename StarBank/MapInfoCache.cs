﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using StarBank.Bank_Stuffs;

namespace StarBank
{
    public class MapInfoCache
    {
        private readonly string CACHE_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                                                    @"Blizzard Entertainment\Battle.net\Cache");

        private readonly BankInfoCache _bankInfoCache;

        public MapInfoCache(BankInfoCache _bankInfoCache)
        {
            this._bankInfoCache = _bankInfoCache;
        }

        /// <summary>
        /// Returns a list of all SCII maps found on the system (in the cache).
        /// Removed duplicates (only the latest version of a map is returned) based on the map's name
        /// </summary>
        public IEnumerable<MapInfo> GetMaps()
        {
            MapProtection mapProtection = new MapProtection();
            SortedList<string, MapInfo> mapList = new SortedList<string, MapInfo>();
            DirectoryInfo cacheFolder = new DirectoryInfo(CACHE_FOLDER);
            IEnumerable<FileInfo> mapFiles = cacheFolder.GetFiles("*.s2ma", SearchOption.AllDirectories);

            //Hack: Some maps galaxy-script files seem to be misformed.  Until I find out what those
            //are, I'm going to display a list of them to the user and ask them to contact me.
            List<string> malformedMaps = new List<string>();

            foreach (FileInfo file in mapFiles)
            {
                //Hack:  The map-name and author name are listed as comments in the galaxy-script file
                //Open that file, find the comments, and read them

                //Hack:  Also using the galaxy-script file to determine the name of the bank file.
                //Based on that name, we GUESS which bank file is the correct one (there could
                //be multiple with the same name...)
                string galaxyScriptCode = GetGalaxyScriptCode(file);
                string[] lines = galaxyScriptCode.Split('\n');
                if(lines.Length >= 5)
                {
                    MapInfo mapInfo = new MapInfo();
                    mapInfo.CachePath = file.FullName;
                    mapInfo.DateCreated = file.LastWriteTime;
                    if(lines[4].Length < 10)
                    {
                        malformedMaps.Add(mapInfo.CachePath);
                        continue;
                    }
                    mapInfo.Name = lines[4].Substring(10).Trim();
                    if(lines.Length >= 6)
                    {
                        if(lines[5].Length < 10)
                        {
                            malformedMaps.Add(mapInfo.Name);
                        }
                        else
                        {
                            mapInfo.AuthorName = lines[5].Substring(10).Trim();
                        }
                    }
                    else
                    {
                        mapInfo.AuthorName = "(Unknown)";
                    }

                    bool mapAdded = CheckForDuplicatesAndAdd(mapInfo, mapList);
                    if(mapAdded)
                    {
                        //Only do other expensive stuff if map was not a duplicate
                        mapInfo.IsProtected = mapProtection.IsMapProtected(file.FullName);
                        mapInfo.BankInfos = _bankInfoCache.GetBanksFromCode(galaxyScriptCode);
                    }
                }
            }

            DisplayMalformedMaps(malformedMaps);
            return mapList.Values;
        }

        private string GetGalaxyScriptCode(FileInfo mpqFile)
        {
            using (StormLibWrapper.MpqArchive archive = new StormLibWrapper.MpqArchive(mpqFile.FullName))
            {
                using (StormLibWrapper.MpqInternalFile galaxyScriptFile = archive.OpenFile("MapScript.galaxy"))
                {
                    return galaxyScriptFile.ReadFile();
                }
            }
        }

        /// <summary>
        /// Adds the map-info to the list, checking that a newer version of the map has not
        /// already been added
        /// </summary>
        private bool CheckForDuplicatesAndAdd(MapInfo mapInfo, SortedList<string, MapInfo> mapList)
        {
            //Need a single key to reference into the list, but want to include
            //both map-name and author-name.  Just concatenate them with an "@" symbol or something.
            string key = mapInfo.Name + "@" + mapInfo.AuthorName;

            if(mapList.ContainsKey(key))
            {
                MapInfo potentialDuplicate = mapList[key];
                if (potentialDuplicate.DateCreated >= mapInfo.DateCreated)
                    return false;
                mapList.Remove(key);
            }

            mapList.Add(key, mapInfo);
            return true;
        }

        public void ExtractGalaxyScriptFileTo(MapInfo mapInfo, string extractToPath)
        {
            using(StormLibWrapper.MpqArchive mapArchive = new StormLibWrapper.MpqArchive(mapInfo.CachePath))
            {
                mapArchive.ExtractFile("MapScript.galaxy", extractToPath);
            }
        }

        private void DisplayMalformedMaps(IEnumerable<string> malformedMaps)
        {
            if(malformedMaps.Count() == 0)
                return;

            string malformedMapsString = String.Join("\n", malformedMaps);
            MessageBox.Show("The following maps are malformed:\n\n" + malformedMapsString + "\n\nPlease contact me to let me know and I'll try to fix this error.");
        }
    }
}