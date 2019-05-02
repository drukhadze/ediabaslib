﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NDesk.Options;
using UdsFileReader;

namespace LogfileConverter
{
    class Program
    {
        private static bool _responseFile;
        private static bool _cFormat;
        private static bool _edicCanMode;
        private static bool _edicCanIsoTpMode;
        private static int _edicCanAddr;
        private static int _edicCanTesterAddr;
        private static int _edicCanEcuAddr;

        static int Main(string[] args)
        {
            bool sortFile = false;
            bool showHelp = false;
            List<string> inputFiles = new List<string>();
            string outputFile = null;

            var p = new OptionSet()
            {
                { "i|input=", "input file.",
                  v => inputFiles.Add(v) },
                { "o|output=", "output file (if omitted '.conv' is appended to input file).",
                  v => outputFile = v },
                { "c|cformat", "c format for hex values", 
                  v => _cFormat = v != null },
                { "r|response", "create reponse file", 
                  v => _responseFile = v != null },
                { "s|sort", "sort reponse file", 
                  v => sortFile = v != null },
                { "h|help",  "show this message and exit", 
                  v => showHelp = v != null },
            };

            try
            {
                p.Parse(args);
            }
            catch (OptionException e)
            {
                string thisName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
                Console.Write(thisName + ": ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `" + thisName + " --help' for more information.");
                return 1;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return 0;
            }

            if (inputFiles.Count < 1)
            {
                Console.WriteLine("No input files specified");
                return 1;
            }
            if (outputFile == null)
            {
                outputFile = inputFiles[0] + ".conv";
            }

            foreach (string inputFile in inputFiles)
            {
                if (!File.Exists(inputFile))
                {
                    Console.WriteLine("Input file '{0}' not found", inputFile);
                    return 1;
                }
            }

            if (!ConvertLog(inputFiles, outputFile))
            {
                Console.WriteLine("Conversion failed");
                return 1;
            }
            if (sortFile && _responseFile)
            {
                if (!SortLines(outputFile))
                {
                    Console.WriteLine("Sorting failed");
                    return 1;
                }
            }

            return 0;
        }

        private static bool ConvertLog(List<string> inputFiles, string outputFile)
        {
            try
            {
                using (StreamWriter streamWriter = new StreamWriter(outputFile))
                {
                    foreach (string inputFile in inputFiles)
                    {
                        if (string.Compare(Path.GetExtension(inputFile), ".trc", StringComparison.OrdinalIgnoreCase) == 0)
                        {   // trace file
                            ConvertTraceFile(inputFile, streamWriter);
                        }
                        else
                        {
                            bool ifhLog = false;
                            using (StreamReader streamReader = new StreamReader(inputFile))
                            {
                                string line = streamReader.ReadLine();
                                if (line != null)
                                {
                                    if (Regex.IsMatch(line, @"^dllStartupIFH"))
                                    {
                                        ifhLog = true;
                                    }
                                }
                            }
                            if (ifhLog)
                            {
                                ConvertIfhlogFile(inputFile, streamWriter);
                            }
                            else
                            {
                                ConvertPortLogFile(inputFile, streamWriter);
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static void ConvertPortLogFile(string inputFile, StreamWriter streamWriter)
        {
            _edicCanMode = false;
            _edicCanIsoTpMode = false;
            using (StreamReader streamReader = new StreamReader(inputFile))
            {
                string line;
                string readString = string.Empty;
                string writeString = string.Empty;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        if (!Regex.IsMatch(line, @"^\[\\\\"))
                        {
                            line = Regex.Replace(line, @"^[\d]+[\s]+[\d\.]+[\s]+[\w\.]+[\s]*", String.Empty);
                            if (Regex.IsMatch(line, @"IRP_MJ_WRITE"))
                            {
                                line = Regex.Replace(line, @"^IRP_MJ_WRITE.*\:[\s]*", String.Empty);
                                List<byte> lineValues = NumberString2List(line);
#if false
                                if ((lineValues.Count > 1) && (lineValues[1] == 0x56))
                                {
                                    line = string.Empty;
                                }
#endif
                                if (line.Length > 0)
                                {
                                    bool validWrite = ChecksumValid(lineValues);
                                    if (_responseFile)
                                    {
                                        if (validWrite)
                                        {
                                            if (writeString.Length > 0 && readString.Length > 0)
                                            {
                                                List<byte> writeValues = NumberString2List(writeString);
                                                List<byte> readValues = NumberString2List(readString);
                                                if (ValidResponse(writeValues, readValues))
                                                {
                                                    streamWriter.Write(NumberString2String(writeString, _responseFile || !_cFormat));
                                                    StoreReadString(streamWriter, readString);
                                                }
                                            }
                                            writeString = NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            writeString = string.Empty;
                                        }
                                    }
                                    else
                                    {
                                        StoreReadString(streamWriter, readString);
                                        if (validWrite)
                                        {
                                            line = "w: " + NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            line = "w (Invalid): " + NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                    }
                                    readString = string.Empty;
                                }
                            }
                            else if (Regex.IsMatch(line, @"^Length 1:"))
                            {
                                line = Regex.Replace(line, @"^Length 1:[\s]*", String.Empty);
                                readString += line;
                                line = string.Empty;
                            }
                            else
                            {
                                line = string.Empty;
                            }
                            if (!_responseFile && line.Length > 0)
                            {
                                streamWriter.WriteLine(line);
                            }
                        }
                    }
                }
                if (_responseFile)
                {
                    if (writeString.Length > 0 && readString.Length > 0)
                    {
                        List<byte> writeValues = NumberString2List(writeString);
                        List<byte> readValues = NumberString2List(readString);
                        if (ValidResponse(writeValues, readValues))
                        {
                            streamWriter.Write(NumberString2String(writeString, _responseFile || !_cFormat));
                            StoreReadString(streamWriter, readString);
                        }
                    }
                }
                else
                {
                    StoreReadString(streamWriter, readString);
                }
            }
        }

        private static void ConvertTraceFile(string inputFile, StreamWriter streamWriter)
        {
            _edicCanMode = false;
            _edicCanIsoTpMode = false;
            using (StreamReader streamReader = new StreamReader(inputFile))
            {
                string line;
                string readString = string.Empty;
                string writeString = string.Empty;
                string lastCfgLine = string.Empty;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        if (Regex.IsMatch(line, @"^ \(EDIC CommParameter"))
                        {
                            _edicCanMode = false;
                            _edicCanIsoTpMode = false;
                        }

                        MatchCollection canEdicMatches = Regex.Matches(line, @"^EDIC CAN: (..), Tester: (..), Ecu: (..)");
                        if (canEdicMatches.Count == 1)
                        {
                            if (canEdicMatches[0].Groups.Count == 4)
                            {
                                try
                                {
                                    _edicCanAddr = Convert.ToInt32(canEdicMatches[0].Groups[1].Value, 16);
                                    _edicCanTesterAddr = Convert.ToInt32(canEdicMatches[0].Groups[2].Value, 16);
                                    _edicCanEcuAddr = Convert.ToInt32(canEdicMatches[0].Groups[3].Value, 16);
                                    _edicCanMode = true;
                                    _edicCanIsoTpMode = false;
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                            }
                        }
                        MatchCollection canEdicIsoTpMatches = Regex.Matches(line, @"^EDIC ISO-TP: Tester: (...), Ecu: (...)");
                        if (canEdicIsoTpMatches.Count == 1)
                        {
                            if (canEdicIsoTpMatches[0].Groups.Count == 3)
                            {
                                try
                                {
                                    _edicCanAddr = 0;
                                    _edicCanTesterAddr = Convert.ToInt32(canEdicIsoTpMatches[0].Groups[1].Value, 16);
                                    _edicCanEcuAddr = Convert.ToInt32(canEdicIsoTpMatches[0].Groups[2].Value, 16);
                                    _edicCanMode = false;
                                    _edicCanIsoTpMode = true;
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                            }
                        }

                        MatchCollection canEdicIsoTpEcuOverrideMatches = Regex.Matches(line, @"^Overriding UDS ECU CAN ID with (....)");
                        if (canEdicIsoTpEcuOverrideMatches.Count == 1)
                        {
                            if (canEdicIsoTpEcuOverrideMatches[0].Groups.Count == 2)
                            {
                                try
                                {
                                    _edicCanEcuAddr = Convert.ToInt32(canEdicIsoTpEcuOverrideMatches[0].Groups[1].Value, 16);
                                    _edicCanMode = false;
                                    _edicCanIsoTpMode = true;
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                            }
                        }

                        MatchCollection canEdicIsoTpTesterOverrideMatches = Regex.Matches(line, @"^Overriding UDS tester CAN ID with (....)");
                        if (canEdicIsoTpTesterOverrideMatches.Count == 1)
                        {
                            if (canEdicIsoTpTesterOverrideMatches[0].Groups.Count == 2)
                            {
                                try
                                {
                                    _edicCanTesterAddr = Convert.ToInt32(canEdicIsoTpTesterOverrideMatches[0].Groups[1].Value, 16);
                                    _edicCanMode = false;
                                    _edicCanIsoTpMode = true;
                                }
                                catch (Exception)
                                {
                                    // ignored
                                }
                            }
                        }

                        if (Regex.IsMatch(line, @"^ \((Send|Resp)\):"))
                        {
                            bool send = Regex.IsMatch(line, @"^ \(Send\):");
                            line = Regex.Replace(line, @"^.*\:[\s]*", String.Empty);

                            List<byte> lineValues = NumberString2List(line);
                            if (line.Length > 0)
                            {
                                if (send)
                                {
                                    string cfgLineWrite = null;
                                    if (_edicCanIsoTpMode)
                                    {
                                        if (_responseFile)
                                        {
                                            int deviceAddress = -1;
                                            foreach (VehicleInfoVag.EcuAddressEntry ecuAddressEntry in VehicleInfoVag.EcuAddressArray)
                                            {
                                                if (ecuAddressEntry.IsoTpEcuCanId == _edicCanEcuAddr && ecuAddressEntry.IsoTpTesterCanId == _edicCanTesterAddr)
                                                {
                                                    deviceAddress = (int)ecuAddressEntry.Address;
                                                    break;
                                                }
                                            }

                                            if (deviceAddress >= 0)
                                            {
                                                string cfgLine = $"CFG: {deviceAddress:X02} {(_edicCanEcuAddr >> 8):X02} {(_edicCanEcuAddr & 0xFF):X02} {(_edicCanTesterAddr >> 8):X02} {(_edicCanTesterAddr & 0xFF):X02}";
                                                if (string.Compare(lastCfgLine, cfgLine, StringComparison.Ordinal) != 0)
                                                {
                                                    lastCfgLine = cfgLine;
                                                    cfgLineWrite = cfgLine;
                                                }
                                            }
                                        }

                                        // convert to KWP2000 format
                                        int dataLength = lineValues.Count;
                                        if (dataLength < 0x3F)
                                        {
                                            lineValues.Insert(0, (byte) (0x80 + dataLength));
                                            lineValues.Insert(1, (byte)(_edicCanEcuAddr >> 8));
                                            lineValues.Insert(2, (byte)(_edicCanEcuAddr & 0xFF));
                                        }
                                        else
                                        {
                                            lineValues.Insert(0, 0x80);
                                            lineValues.Insert(1, (byte)(_edicCanEcuAddr >> 8));
                                            lineValues.Insert(2, (byte)(_edicCanEcuAddr & 0xFF));
                                            lineValues.Insert(3, (byte) dataLength);
                                        }
                                        byte checksum = CalcChecksumBmwFast(lineValues, 0, lineValues.Count);
                                        lineValues.Add(checksum);
                                        line = List2NumberString(lineValues);
                                    }
                                    int sendLength = TelLengthBmwFast(lineValues, 0);
                                    if (sendLength > 0 && sendLength == lineValues.Count)
                                    {
                                        // checksum missing
                                        byte checksum = CalcChecksumBmwFast(lineValues, 0, lineValues.Count);
                                        lineValues.Add(checksum);
                                        line += $" {checksum:X02}";
                                    }
                                    bool validWrite = ChecksumValid(lineValues);
                                    if (_responseFile)
                                    {
                                        if (validWrite)
                                        {
                                            if (writeString.Length > 0 && readString.Length > 0)
                                            {
                                                List<byte> writeValues = NumberString2List(writeString);
                                                List<byte> readValues = NumberString2List(readString);
                                                if (ValidResponse(writeValues, readValues))
                                                {
                                                    streamWriter.Write(NumberString2String(writeString,
                                                        _responseFile || !_cFormat));
                                                    StoreReadString(streamWriter, readString);
                                                }
                                            }
                                            writeString = NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            writeString = string.Empty;
                                        }

                                        if (!string.IsNullOrEmpty(cfgLineWrite))
                                        {
                                            streamWriter.WriteLine(cfgLineWrite);
                                        }
                                    }
                                    else
                                    {
                                        StoreReadString(streamWriter, readString);
                                        if (validWrite)
                                        {
                                            line = "w: " + NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            line = "w (Invalid): " +
                                                   NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                    }
                                    readString = string.Empty;
                                }
                                else
                                {   // receive
                                    bool addResponse = true;
                                    if (_edicCanMode)
                                    {
                                        if (lineValues.Count == 6 && lineValues[1] == 0xF1 && lineValues[2] == 0xF1)
                                        {   // filter adapter responses
                                            addResponse = false;
                                        }
                                    }
                                    if (_edicCanIsoTpMode)
                                    {
                                        addResponse = false;
                                        if (lineValues.Count >= 4 && lineValues[0] == 0x01)
                                        {   // standard response
                                            int dataLength = (lineValues[1] << 8) + lineValues[2];
                                            if (dataLength + 4 == lineValues.Count)
                                            {
                                                addResponse = true;
                                                // convert to KWP2000 format
                                                lineValues.RemoveAt(lineValues.Count - 1);
                                                lineValues.RemoveAt(0);
                                                lineValues.RemoveAt(0);
                                                lineValues.RemoveAt(0);

                                                if (dataLength < 0x3F)
                                                {
                                                    lineValues.Insert(0, (byte)(0x80 + dataLength));
                                                    lineValues.Insert(1, (byte)(_edicCanEcuAddr >> 8));
                                                    lineValues.Insert(2, (byte)(_edicCanEcuAddr & 0xFF));
                                                }
                                                else
                                                {
                                                    lineValues.Insert(0, 0x80);
                                                    lineValues.Insert(1, (byte)(_edicCanEcuAddr >> 8));
                                                    lineValues.Insert(2, (byte)(_edicCanEcuAddr & 0xFF));
                                                    lineValues.Insert(3, (byte)dataLength);
                                                }
                                                byte checksum = CalcChecksumBmwFast(lineValues, 0, lineValues.Count);
                                                lineValues.Add(checksum);
                                                line = List2NumberString(lineValues);
                                            }
                                        }
                                    }
                                    if (addResponse)
                                    {
                                        readString += line;
                                    }
                                    line = string.Empty;
                                }
                            }
                            else
                            {
                                readString = string.Empty;
                            }
                            if (!_responseFile && line.Length > 0)
                            {
                                streamWriter.WriteLine(line);
                            }
                        }
                    }
                }
                if (_responseFile)
                {
                    if (writeString.Length > 0 && readString.Length > 0)
                    {
                        List<byte> writeValues = NumberString2List(writeString);
                        List<byte> readValues = NumberString2List(readString);
                        if (ValidResponse(writeValues, readValues))
                        {
                            streamWriter.Write(NumberString2String(writeString, _responseFile || !_cFormat));
                            StoreReadString(streamWriter, readString);
                        }
                    }
                }
                else
                {
                    StoreReadString(streamWriter, readString);
                }
            }
        }

        private static void ConvertIfhlogFile(string inputFile, StreamWriter streamWriter)
        {
            _edicCanMode = false;
            using (StreamReader streamReader = new StreamReader(inputFile))
            {
                Regex regexCleanLine = new Regex(@"^.*\:[\s]*");
                string line;
                string writeString = string.Empty;
                bool ignoreResponse = false;
                bool keyBytes = false;
                bool kwp1281 = false;
                int remoteAddr = -1;
                int wakeAddrPar = -1;
                string lastCfgLine = string.Empty;
                List<byte> lineValuesPar = null;
                List<byte> lineValuesPreface = null;
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.Length > 0)
                    {
                        if (Regex.IsMatch(line, @"^msgIn:"))
                        {
                            if (Regex.IsMatch(line, @"^.*'ifhRequestKeyBytes"))
                            {
                                keyBytes = true;
                            }
                            if (Regex.IsMatch(line, @"^.*'ifhSetParameter"))
                            {
                                //kwp1281 = false;
                                remoteAddr = -1;
                            }
                            if (!Regex.IsMatch(line, @"^.*('ifhSendTelegram'|'ifhGetResult')"))
                            {
                                ignoreResponse = true;
                                writeString = string.Empty;
                            }
                        }
                        if (Regex.IsMatch(line, @"^\((ifhSetParameter|ifhSetTelPreface)\): "))
                        {
                            bool par = Regex.IsMatch(line, @"^\(ifhSetParameter\): ");
                            line = regexCleanLine.Replace(line, String.Empty);
                            List<byte> lineValues = NumberString2List(line);
                            if (par)
                            {
                                lineValuesPar = lineValues;
                            }
                            else
                            {
                                lineValuesPreface = lineValues;
                            }
                            continue;
                        }
                        if (Regex.IsMatch(line, @"^\((ifhSendTelegram|ifhGetResult)\): "))
                        {
                            bool send = Regex.IsMatch(line, @"^\(ifhSendTelegram\): ");
                            line = regexCleanLine.Replace(line, String.Empty);

                            List<byte> lineValues = NumberString2List(line);
                            if (send && lineValues.Count == 0)
                            {
                                if (lineValuesPar?.Count >= 6 && lineValuesPreface?.Count >= 4 &&
                                    lineValuesPar[4] == 0x81 && lineValuesPreface[2] == 0x02 && lineValuesPreface[3] == 0x00)
                                {
                                    byte wakeAddress = (byte)(lineValuesPar[5] & 0x7F);
                                    bool oddParity = true;
                                    for (int i = 0; i < 7; i++)
                                    {
                                        oddParity ^= (wakeAddress & (1 << i)) != 0;
                                    }
                                    if (oddParity)
                                    {
                                        wakeAddress |= 0x80;
                                    }
                                    wakeAddrPar = wakeAddress;
                                }
                            }
                            if (line.Length > 0)
                            {
                                if (send)
                                {
                                    if (lineValues.Count > 0)
                                    {
                                        if (!kwp1281)
                                        {
                                            lineValues = CreateBmwFastTel(lineValues, 0x00, 0xF1);
                                        }
                                        line = List2NumberString(lineValues);
                                    }
                                    bool validWrite;
                                    // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                    if (kwp1281)
                                    {
                                        validWrite = CheckKwp1281Tel(lineValues);
                                    }
                                    else
                                    {
                                        validWrite = ChecksumValid(lineValues);
                                    }
                                    if (_responseFile)
                                    {
                                        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                        if (validWrite)
                                        {
                                            writeString = NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            writeString = string.Empty;
                                        }
                                    }
                                    else
                                    {
                                        if (validWrite)
                                        {
                                            line = "w: " + NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                        else
                                        {
                                            line = "w (Invalid): " + NumberString2String(line, _responseFile || !_cFormat);
                                        }
                                    }
                                }
                                else
                                {   // receive
                                    if (keyBytes)
                                    {
                                        string readString = line;
                                        List<byte> readValues = NumberString2List(readString);
                                        if (readValues.Count >= 5)
                                        {
                                            bool kpw1281Found = readValues[1] != 0x8F;
                                            if (kpw1281Found)
                                            {
                                                kwp1281 = true;
                                            }
                                            if (_responseFile)
                                            {
                                                if (!kpw1281Found)
                                                {
                                                    if (readValues[4] > 0x40)
                                                    {
                                                        // TP20
                                                        if (remoteAddr >= 0)
                                                        {
                                                            string cfgLine = $"CFG: {readValues[2]:X02} {remoteAddr:X02}";
                                                            if (string.Compare(lastCfgLine, cfgLine, StringComparison.Ordinal) != 0)
                                                            {
                                                                streamWriter.WriteLine(cfgLine);
                                                                lastCfgLine = cfgLine;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // KWP2000
                                                        string cfgLine = $"CFG: {readValues[2] ^ 0xFF:X02} {readValues[0]:X02} {readValues[1]:X02}";
                                                        if (string.Compare(lastCfgLine, cfgLine, StringComparison.Ordinal) != 0)
                                                        {
                                                            streamWriter.WriteLine(cfgLine);
                                                            lastCfgLine = cfgLine;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    // KWP1281
                                                    readValues = CleanKwp1281Tel(readValues, true);
                                                    if (wakeAddrPar >= 0 && readValues.Count > 5)
                                                    {
                                                        string cfgLine = $"CFG: {wakeAddrPar:X02} {readValues[0]:X02} {readValues[1]:X02}" +
                                                            "\r\n: " + List2NumberString(readValues.GetRange(5, readValues.Count - 5));

                                                        if (string.Compare(lastCfgLine, cfgLine, StringComparison.Ordinal) != 0)
                                                        {
                                                            streamWriter.WriteLine(cfgLine);
                                                            lastCfgLine = cfgLine;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                streamWriter.WriteLine("KEY: " + NumberString2String(readString, _responseFile || !_cFormat));
                                            }
                                        }
                                    }
                                    if (!ignoreResponse)
                                    {
                                        string readString = line;
                                        if (_responseFile)
                                        {
                                            if (writeString.Length > 0 && readString.Length > 0)
                                            {
                                                List<byte> writeValues = NumberString2List(writeString);
                                                List<byte> readValues = NumberString2List(readString);
                                                if (kwp1281)
                                                {
                                                    readValues = CleanKwp1281Tel(readValues);
                                                    if (readValues.Count > 0)
                                                    {
                                                        readString = List2NumberString(readValues);
                                                        streamWriter.Write(NumberString2String(writeString, _responseFile || !_cFormat));
                                                        streamWriter.WriteLine(" : " + NumberString2String(readString, _responseFile || !_cFormat));
                                                    }
                                                }
                                                else
                                                {
                                                    if (UpdateRequestAddr(writeValues, readValues))
                                                    {
                                                        remoteAddr = writeValues[1];
                                                        writeString = List2NumberString(writeValues);
                                                        if (ValidResponse(writeValues, readValues))
                                                        {
                                                            streamWriter.Write(NumberString2String(writeString, _responseFile || !_cFormat));
                                                            StoreReadString(streamWriter, readString);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (kwp1281)
                                            {
                                                streamWriter.WriteLine("r: " + NumberString2String(readString, _responseFile || !_cFormat));
                                            }
                                            else
                                            {
                                                StoreReadString(streamWriter, readString);
                                            }
                                        }
                                    }
                                    writeString = string.Empty;
                                    line = string.Empty;
                                }
                            }
                            else
                            {
                                writeString = string.Empty;
                            }
                            if (!_responseFile && line.Length > 0)
                            {
                                streamWriter.WriteLine(line);
                            }
                            ignoreResponse = false;
                            keyBytes = false;
                        }
                    }
                }
            }
        }

        private static int LineComparer(string x, string y)
        {
            string lineX = x.Substring(3);
            string lineY = y.Substring(3);

            return String.Compare(lineX, lineY, StringComparison.Ordinal);
        }

        private static bool SortLines(string fileName)
        {
            try
            {
                string[] lines = File.ReadAllLines(fileName);
                Array.Sort(lines, LineComparer);
                using (StreamWriter streamWriter = new StreamWriter(fileName))
                {
                    string lastLine = string.Empty;
                    foreach (string line in lines)
                    {
                        if (line != lastLine)
                        {
                            streamWriter.WriteLine(line);
                        }
                        lastLine = line;
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static void StoreReadString(StreamWriter streamWriter, string readString)
        {
            try
            {
                if (readString.Length > 0)
                {
                    List<byte> lineValues = NumberString2List(readString);
                    bool valid = ChecksumValid(lineValues);
                    if (_responseFile)
                    {
                        if (valid)
                        {
                            streamWriter.WriteLine(" : " + NumberString2String(readString, _responseFile || !_cFormat));
                        }
                        else
                        {
                            streamWriter.WriteLine();
                        }
                    }
                    else
                    {
                        if (valid)
                        {
                            streamWriter.WriteLine("r: " + NumberString2String(readString, _responseFile || !_cFormat));
                        }
                        else
                        {
                            streamWriter.WriteLine("r (Invalid): " + NumberString2String(readString, _responseFile || !_cFormat));
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private static List<byte> NumberString2List(string numberString)
        {
            List<byte> values = new List<byte>();
            string[] numberArray = numberString.Split(' ');
            foreach (string number in numberArray)
            {
                if (number.Length > 0)
                {
                    try
                    {
                        int value = Convert.ToInt32(number, 16);
                        values.Add((byte) value);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            return values;
        }

        private static string List2NumberString(List<byte> dataList)
        {
            StringBuilder sr = new StringBuilder();
            foreach (byte data in dataList)
            {
                sr.Append($"{data:X02} ");
            }
            return sr.ToString();
        }

        private static byte CalcChecksumBmwFast(List<byte> data, int offset, int length)
        {
            byte sum = 0;
            for (int i = 0; i < length; i++)
            {
                sum += data[i + offset];
            }
            return sum;
        }

        // telegram length without checksum
        private static int TelLengthBmwFast(List<byte> telegram, int offset)
        {
            if (telegram.Count - offset < 4)
            {
                return 0;
            }
            int telLength = telegram[0 + offset] & 0x3F;
            if (telLength == 0)
            {   // with length byte
                if (telegram[3 + offset] == 0)
                {
                    if (telegram.Count < 6)
                    {
                        return 0;
                    }
                    telLength = ((telegram[4 + offset] << 8) | telegram[5 + offset]) + 6;
                }
                else
                {
                    telLength = telegram[3 + offset] + 4;
                }
            }
            else
            {
                telLength += 3;
            }
            return telLength;
        }

        private static List<byte> CreateBmwFastTel(List<byte> data, byte dest, byte source)
        {
            List<byte> result = new List<byte>();
            if (data.Count > 0x3F)
            {
                result.Add(0x80);
                result.Add(dest);
                result.Add(source);
                result.Add((byte)data.Count);
            }
            else
            {
                result.Add((byte) (0x80 | data.Count));
                result.Add(dest);
                result.Add(source);
            }
            result.AddRange(data);
            result.Add(CalcChecksumBmwFast(result, 0, result.Count));
            return result;
        }

        private static bool ChecksumValid(List<byte> telegram)
        {
            int offset = 0;
            for (; ; )
            {
                int dataLength = TelLengthBmwFast(telegram, offset);
                if (dataLength == 0) return false;
                if (telegram.Count - offset < dataLength + 1)
                {
                    return false;
                }

                byte sum = CalcChecksumBmwFast(telegram, offset, dataLength);
                if (sum != telegram[dataLength + offset])
                {
                    return false;
                }

                offset += dataLength + 1;    // checksum
                if (offset > telegram.Count)
                {
                    return false;
                }
                if (offset == telegram.Count)
                {
                    break;
                }
            }
            return true;
        }

        private static bool ValidResponse(List<byte> request, List<byte> response)
        {
            bool broadcast = (request[0] & 0xC0) != 0x80;
            if (!ChecksumValid(request) || !ChecksumValid(response))
            {
                return false;
            }
            if (!broadcast && !_edicCanMode && !_edicCanIsoTpMode)
            {
                if (request[1] != response[2])
                {
                    return false;
                }
                if (request[2] != response[1])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool UpdateRequestAddr(List<byte> request, List<byte> response)
        {
            if (!ChecksumValid(request) || !ChecksumValid(response))
            {
                return false;
            }
            if (request.Count < 4)
            {
                return false;
            }
            request[1] = response[2];
            request[2] = response[1];
            request[request.Count - 1] = CalcChecksumBmwFast(request, 0, request.Count - 1);
            return true;
        }

        private static List<byte> CleanKwp1281Tel(List<byte> tel, bool keyBytes = false)
        {
            List<byte> result = new List<byte>();
            int offset = 0;
            if (keyBytes)
            {
                offset = 5;
                if (tel.Count < offset)
                {
                    return new List<byte>();
                }
                result.AddRange(tel.GetRange(0, offset));
            }
            for (;;)
            {
                if (offset >= tel.Count)
                {
                    break;
                }
                byte len = tel[offset];
                if (tel.Count < offset + len + 1)
                {
                    return new List<byte>();
                }
                if (tel[offset + len] != 0x03)
                {
                    return new List<byte>();
                }
                if (len != 3 || tel[offset + 2] != 0x09)
                {   // ack
                    result.AddRange(tel.GetRange(offset, len));
                }
                offset += len + 1;
            }
            return result;
        }

        private static bool CheckKwp1281Tel(List<byte> tel)
        {
            if (tel.Count == 0)
            {
                return false;
            }
            int offset = 0;
            for (;;)
            {
                if (offset >= tel.Count)
                {
                    break;
                }
                byte len = tel[offset];
                if (len == 0)
                {
                    return false;
                }
                if (tel.Count < offset + len)
                {
                    return false;
                }
                if (len == 3 && tel[offset + 2] == 0x09)
                {   // ack
                    return false;
                }
                offset += len;
            }
            return true;
        }

        private static string NumberString2String(string numberString, bool simpleFormat)
        {
            string result = string.Empty;

            List<byte> values = NumberString2List(numberString);

            if (_edicCanMode && values.Count > 0)
            {

                int offset = 0;
                for (;;)
                {
                    int dataLength = TelLengthBmwFast(values, offset);
                    if (dataLength == 0) return string.Empty;
                    if (values.Count - offset < dataLength + 1)
                    {   // error
                        break;
                    }

                    bool updateChecksum = false;
                    if (values[1 + offset] == _edicCanAddr && values[2 + offset] == _edicCanTesterAddr)
                    {
                        values[1 + offset] = (byte)_edicCanEcuAddr;
                        updateChecksum = true;
                    }
                    else if (values[1 + offset] == 0x00 && values[2 + offset] == _edicCanAddr)
                    {
                        values[1 + offset] = (byte)_edicCanTesterAddr;
                        values[2 + offset] = (byte)_edicCanEcuAddr;
                        updateChecksum = true;
                    }
                    if (updateChecksum)
                    {
                        byte sum = CalcChecksumBmwFast(values, offset, dataLength);
                        values[dataLength + offset] = sum;
                    }

                    offset += dataLength + 1;    // checksum
                    if (offset > values.Count)
                    {   // error
                        break;
                    }
                    if (offset == values.Count)
                    {
                        break;
                    }
                }
            }

            foreach (byte value in values)
            {
                if (simpleFormat)
                {
                    if (result.Length > 0)
                    {
                        result += " ";
                    }
                    result += $"{value:X02}";
                }
                else
                {
                    if (result.Length > 0)
                    {
                        result += ", ";
                    }
                    result += $"0x{value:X02}";
                }
            }

            return result;
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: " + Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName) + " [OPTIONS]");
            Console.WriteLine("Convert OBD log files");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
