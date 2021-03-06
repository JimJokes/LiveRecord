﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using log4net;
using LiveRecordSharp.LiveSites;
using Newtonsoft.Json.Linq;

namespace LiveRecordSharp
{
    public class Record
    {
        public LiveSite LiveSite { get; }
        private ILog Log { get; } = LiveRecordSharp.Log.GetLogger(typeof (Record));
        private string SaveFormat { get; } = ".flv";
        private string Converter { get; } = "ffmpeg";
        private string Player { get; } = "ffplay";

        public Record(LiveSite liveSite)
        {
            LiveSite = liveSite;
        }

        public async Task StartPlayAsync()
        {
            Log.Info("Link Start");
            while (true)
            {
                try
                {
                    if (await LiveSite.IsLiveAsync())
                    {
                        Log.Info($"{LiveSite.LiveRoomName} is live.");
                        var startTime = DateTime.UtcNow.ToUnixTimeStamp();
                        var url = await LiveSite.GetLiveStreamUrlAsync();
                        var p = new Process
                        {
                            StartInfo =
                            {
                                Arguments = url,
                                FileName = Player,
                                RedirectStandardError = true,
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            }
                        };
                        p.ErrorDataReceived += (o, e) => Log.Info(e.Data);
                        p.OutputDataReceived += (o, e) => Log.Info(e.Data);
                        p.Start();
                        p.BeginErrorReadLine();
                        p.BeginOutputReadLine();
                        p.WaitForExit();
                        var stopTime = DateTime.UtcNow.ToUnixTimeStamp();
                        if (stopTime - startTime > 10000) continue;
                    }
                    await Task.Delay(60000);
                }
                catch (Exception e)
                {
                    Log.Fatal(e);
                }
            }
        }

        public async Task StartRecordAsync()
        {
            Log.Info("Link Start");
            while (true)
            {
                try
                {
                    if (await LiveSite.IsLiveAsync())
                    {
                        Log.Info($"{LiveSite.LiveRoomName} is live.");
                        var dirName = LiveSite.LiveRoomName.KeepAlpha();
                        if (string.IsNullOrWhiteSpace(dirName)) dirName = LiveSite.LiveUrl.Substring(LiveSite.LiveUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);
                        if (string.IsNullOrWhiteSpace(dirName)) dirName = LiveSite.LiveUrl.Substring(LiveSite.LiveUrl.Substring(0, LiveSite.LiveUrl.Length - 1).LastIndexOf("/", StringComparison.Ordinal) + 1);
                        var startTime = DateTime.UtcNow.ToUnixTimeStamp();
                        var fileName = Path.Combine("record", dirName, startTime.ToString());
                        var directoryName = new FileInfo(fileName).DirectoryName;
                        if (directoryName != null) Directory.CreateDirectory(directoryName);
                        var url = await LiveSite.GetLiveStreamUrlAsync();
                        var p = new Process
                        {
                            StartInfo =
                            {
                                Arguments = $"-i {url} -acodec copy -vcodec copy -f segment -segment_time 1:00:00 -segment_list {fileName}_out.list {fileName}_%03d{SaveFormat}",
                                FileName = Converter,
                                RedirectStandardError = true,
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            }
                        };
                        Log.Info($"File save path: {fileName}{SaveFormat}");
                        Log.Debug($"Process args: {p.StartInfo.Arguments}");
                        var lastReceive = DateTime.UtcNow;
                        p.ErrorDataReceived += (o, e) =>
                        {
                            Log.Info(e.Data);
                            lastReceive = DateTime.UtcNow;
                        };
                        p.OutputDataReceived += (o, e) =>
                        {
                            Log.Info(e.Data);
                            lastReceive = DateTime.UtcNow;
                        };
                        p.Start();
                        p.BeginErrorReadLine();
                        p.BeginOutputReadLine();
#pragma warning disable 4014
                        Task.Run(async () =>
#pragma warning restore 4014
                            {
                                while (!p.HasExited)
                                {
                                    if (DateTime.UtcNow - lastReceive > TimeSpan.FromSeconds(20)) p.Kill();
                                    await Task.Delay(5000);
                                }
                            });
                        p.WaitForExit();
                        var stopTime = DateTime.UtcNow.ToUnixTimeStamp();
                        var timeJson = new JObject
                        {
                            ["liveRoomName"] = LiveSite.LiveRoomName,
                            ["liveUrl"] = LiveSite.LiveUrl,
                            ["startTime"] = startTime,
                            ["stopTime"] = stopTime,
                            ["fileName"] = startTime + SaveFormat
                        };
                        var jsonFile = new FileInfo($"{fileName}.json");
                        if (jsonFile.Exists) jsonFile.Delete();
                        using (var sw = jsonFile.CreateText())
                        {
                            await sw.WriteAsync(timeJson.ToString());
                        }
                        if (stopTime - startTime > 10000) continue;
                    }
                    await Task.Delay(60000);
                }
                catch (Exception e)
                {
                    Log.Fatal(e);
                }
            }
        }
    }
}
