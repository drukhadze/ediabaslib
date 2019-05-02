﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace EdiabasLib
{
    public class EdCustomWiFiInterface
    {
#if Android
        public class ConnectParameterType
        {
            public ConnectParameterType(Android.Net.ConnectivityManager connectivityManager, Android.Net.Wifi.WifiManager wifiManager)
            {
                ConnectivityManager = connectivityManager;
                WifiManager = wifiManager;
            }

            public Android.Net.ConnectivityManager ConnectivityManager { get; }
            public Android.Net.Wifi.WifiManager WifiManager { get; }
        }
#endif

        public const string PortId = "DEEPOBDWIFI";
        public static string AdapterIp = "192.168.0.10";
        public static string AdapterIpEspLink = "192.168.4.1";
        public static int AdapterPort = 35000;
        public static int AdapterPortEspLink = 23;
        protected const int TcpReadTimeoutOffset = 1000;
        protected const int EchoTimeout = 1000;
        protected static int ConnectTimeout = 5000;
        private static readonly EdCustomAdapterCommon CustomAdapter =
            new EdCustomAdapterCommon(SendData, ReceiveData, DiscardInBuffer, ReadInBuffer, TcpReadTimeoutOffset, -1, EchoTimeout, true);
        // ReSharper disable once UnusedMember.Global
        protected static Stopwatch StopWatch = new Stopwatch();
        protected static TcpClient TcpClient;
        protected static NetworkStream TcpStream;
        protected static string ConnectPort;
        protected static object ConnectParameter;
        protected static object ConnManager;
        protected static object WifiManager;

        // ReSharper disable once UnusedMember.Global
        public static NetworkStream NetworkStream => TcpStream;

        public static EdiabasNet Ediabas
        {
            get => CustomAdapter.Ediabas;
            set => CustomAdapter.Ediabas = value;
        }

        static EdCustomWiFiInterface()
        {
        }

        public static bool InterfaceConnect(string port, object parameter)
        {
            if (TcpClient != null)
            {
                if (ConnectPort == port)
                {
                    return true;
                }
                Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "Wifi port {0} different, disconnect", port);
                InterfaceDisconnect(true);
                return true;
            }
            if (!port.StartsWith(PortId, StringComparison.OrdinalIgnoreCase))
            {
                InterfaceDisconnect(true);
                return false;
            }
            CustomAdapter.Init();
            try
            {
                ConnectPort = port;
                ConnectParameter = parameter;
                Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "WiFi connect: {0}", port);
                string adapterIp = AdapterIp;
                int adapterPort = AdapterPort;
                ConnManager = null;
                WifiManager = null;
#if Android
                if (ConnectParameter is ConnectParameterType connectParameter)
                {
                    ConnManager = connectParameter.ConnectivityManager;
                    WifiManager = connectParameter.WifiManager;
                }

                if (WifiManager is Android.Net.Wifi.WifiManager wifiManager && wifiManager.ConnectionInfo != null && wifiManager.DhcpInfo != null)
                {
                    string serverIp = TcpClientWithTimeout.ConvertIpAddress(wifiManager.DhcpInfo.ServerAddress);
                    Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "DHCP server IP: {0}", serverIp);
                    if (string.Compare(serverIp, EdCustomWiFiInterface.AdapterIpEspLink, StringComparison.Ordinal) == 0)
                    {
                        Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "ESP-Link detected");
                        adapterIp = AdapterIpEspLink;
                        adapterPort = AdapterPortEspLink;
                    }
                }
#else
                System.Net.NetworkInformation.NetworkInterface[] adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (System.Net.NetworkInformation.NetworkInterface adapter in adapters)
                {
                    if (adapter.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        if (adapter.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                        {
                            System.Net.NetworkInformation.IPInterfaceProperties properties = adapter.GetIPProperties();
                            if (properties?.DhcpServerAddresses != null)
                            {
                                foreach (IPAddress dhcpServerAddress in properties.DhcpServerAddresses)
                                {
                                    if (dhcpServerAddress.AddressFamily == AddressFamily.InterNetwork)
                                    {
                                        string serverIp = dhcpServerAddress.ToString();
                                        Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "DHCP server IP: {0}", serverIp);
                                        if (string.Compare(serverIp, AdapterIpEspLink, StringComparison.OrdinalIgnoreCase) == 0)
                                        {
                                            Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "ESP-Link detected");
                                            adapterIp = AdapterIpEspLink;
                                            adapterPort = AdapterPortEspLink;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

#endif
                Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "Connecting to: {0}:{1}", adapterIp, adapterPort);
                TcpClientWithTimeout.ExecuteNetworkCommand(() =>
                {
                    TcpClient = new TcpClientWithTimeout(IPAddress.Parse(adapterIp), adapterPort, ConnectTimeout, true).Connect();
                }, ConnManager);
                TcpStream = TcpClient.GetStream();
            }
            catch (Exception ex)
            {
                Ediabas?.LogFormat(EdiabasNet.EdLogLevel.Ifh, "*** Connect failure: {0}", ex.Message);
                InterfaceDisconnect(true);
                return false;
            }
            return true;
        }

        public static bool InterfaceDisconnect()
        {
            return InterfaceDisconnect(false);
        }

        public static bool InterfaceDisconnect(bool forceClose)
        {
            if (!forceClose && Ediabas != null)
            {
                int keepConnectionOpen = 0;
                string prop = Ediabas.GetConfigProperty("ObdKeepConnectionOpen");
                if (prop != null)
                {
                    keepConnectionOpen = (int)EdiabasNet.StringToValue(prop);
                }

                Ediabas.LogFormat(EdiabasNet.EdLogLevel.Ifh, "ObdKeepConnectionOpen: {0}", keepConnectionOpen);
                if (keepConnectionOpen != 0)
                {
                    return true;
                }
            }

            Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "WiFi disconnect");
            bool result = true;
            try
            {
                if (TcpStream != null)
                {
                    TcpStream.Close();
                    TcpStream = null;
                }
            }
            catch (Exception)
            {
                result = false;
            }

            try
            {
                if (TcpClient != null)
                {
                    TcpClient.Close();
                    TcpClient = null;
                }
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }

        public static EdInterfaceObd.InterfaceErrorResult InterfaceSetConfig(EdInterfaceObd.Protocol protocol, int baudRate, int dataBits, EdInterfaceObd.SerialParity parity, bool allowBitBang)
        {
            if (TcpStream == null)
            {
                return EdInterfaceObd.InterfaceErrorResult.ConfigError;
            }

            return CustomAdapter.InterfaceSetConfig(protocol, baudRate, dataBits, parity, allowBitBang);
        }

        public static bool InterfaceSetDtr(bool dtr)
        {
            if (TcpStream == null)
            {
                return false;
            }
            return true;
        }

        public static bool InterfaceSetRts(bool rts)
        {
            if (TcpStream == null)
            {
                return false;
            }
            return true;
        }

        public static bool InterfaceGetDsr(out bool dsr)
        {
            dsr = true;
            if (TcpStream == null)
            {
                return false;
            }
            return true;
        }

        public static bool InterfaceSetBreak(bool enable)
        {
            return false;
        }

        public static bool InterfaceSetInterByteTime(int time)
        {
            return CustomAdapter.InterfaceSetInterByteTime(time);
        }

        public static bool InterfaceSetCanIds(int canTxId, int canRxId, EdInterfaceObd.CanFlags canFlags)
        {
            return CustomAdapter.InterfaceSetCanIds(canTxId, canRxId, canFlags);
        }

        public static bool InterfacePurgeInBuffer()
        {
            if (TcpStream == null)
            {
                return false;
            }
            try
            {
                DiscardInBuffer();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static bool InterfaceAdapterEcho()
        {
            return false;
        }

        public static bool InterfaceHasPreciseTimeout()
        {
            return false;
        }

        public static bool InterfaceHasAutoBaudRate()
        {
            return true;
        }

        public static bool InterfaceHasAutoKwp1281()
        {
            return CustomAdapter.InterfaceHasAutoKwp1281();
        }

        public static bool InterfaceHasIgnitionStatus()
        {
            return true;
        }

        public static bool InterfaceSendData(byte[] sendData, int length, bool setDtr, double dtrTimeCorr)
        {
            if (TcpStream == null)
            {
                Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "*** Port closed");
                return false;
            }

            for (int retry = 0; retry < 2; retry++)
            {
                if (CustomAdapter.ReconnectRequired)
                {
                    Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "Reconnecting");
                    InterfaceDisconnect(true);
                    if (!InterfaceConnect(ConnectPort, null))
                    {
                        CustomAdapter.ReconnectRequired = true;
                        return false;
                    }
                    CustomAdapter.ReconnectRequired = false;
                }

                if (CustomAdapter.InterfaceSendData(sendData, length, setDtr, dtrTimeCorr))
                {
                    return true;
                }

                if (!CustomAdapter.ReconnectRequired)
                {
                    return false;
                }
            }

            return false;
        }

        public static bool InterfaceReceiveData(byte[] receiveData, int offset, int length, int timeout, int timeoutTelEnd, EdiabasNet ediabasLog)
        {
            if (TcpStream == null)
            {
                return false;
            }

            return CustomAdapter.InterfaceReceiveData(receiveData, offset, length, timeout, timeoutTelEnd, ediabasLog);
        }

        public static bool InterfaceSendPulse(UInt64 dataBits, int length, int pulseWidth, bool setDtr, bool bothLines, int autoKeyByteDelay)
        {
            if (TcpStream == null)
            {
                Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "*** Port closed");
                return false;
            }
            if (CustomAdapter.ReconnectRequired)
            {
                Ediabas?.LogString(EdiabasNet.EdLogLevel.Ifh, "Reconnecting");
                InterfaceDisconnect(true);
                if (!InterfaceConnect(ConnectPort, null))
                {
                    CustomAdapter.ReconnectRequired = true;
                    return false;
                }
                CustomAdapter.ReconnectRequired = false;
            }

            return CustomAdapter.InterfaceSendPulse(dataBits, length, pulseWidth, setDtr, bothLines, autoKeyByteDelay);
        }

        private static void SendData(byte[] buffer, int length)
        {
            TcpStream.Write(buffer, 0, length);
        }

        private static bool ReceiveData(byte[] buffer, int offset, int length, int timeout, int timeoutTelEnd, EdiabasNet ediabasLog = null)
        {
            int recLen = 0;
            TcpStream.ReadTimeout = timeout;
            int data;
            try
            {
                data = TcpStream.ReadByte();
            }
            catch (Exception)
            {
                data = -1;
            }
            if (data < 0)
            {
                return false;
            }
            buffer[offset + recLen] = (byte)data;
            recLen++;

            TcpStream.ReadTimeout = timeoutTelEnd;
            for (; ; )
            {
                if (recLen >= length)
                {
                    break;
                }
                try
                {
                    data = TcpStream.ReadByte();
                }
                catch (Exception)
                {
                    data = -1;
                }
                if (data < 0)
                {
                    return false;
                }
                buffer[offset + recLen] = (byte)data;
                recLen++;
            }
            if (recLen < length)
            {
                ediabasLog?.LogData(EdiabasNet.EdLogLevel.Ifh, buffer, offset, recLen, "Rec ");
                return false;
            }
            return true;
        }

        private static void DiscardInBuffer()
        {
            TcpStream.Flush();
            while (TcpStream.DataAvailable)
            {
                TcpStream.ReadByte();
            }
        }

        private static List<byte> ReadInBuffer()
        {
            List<byte> responseList = new List<byte>();
            TcpStream.ReadTimeout = 1;
            while (TcpStream.DataAvailable)
            {
                int data;
                try
                {
                    data = TcpStream.ReadByte();
                }
                catch (Exception)
                {
                    data = -1;
                }
                if (data >= 0)
                {
                    CustomAdapter.LastCommTick = Stopwatch.GetTimestamp();
                    responseList.Add((byte)data);
                }
            }
            return responseList;
        }
    }
}
