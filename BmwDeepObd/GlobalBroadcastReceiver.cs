﻿using System;
using Android.Content;
using Android.Support.V4.Content;

namespace BmwDeepObd
{
#if false
    [BroadcastReceiver(Enabled = true, Exported = true, Name = ActivityCommon.AppNameSpace + ".GlobalBroadcastReceiver")]
    [Android.App.IntentFilter(new[] {
        MtcBtSmallon,
        MtcBtSmalloff,
        MicBtReport
    }, Categories = new []{ Intent.CategoryDefault } )]
#else
    [BroadcastReceiver(Exported = false)]
#endif
    public class GlobalBroadcastReceiver : BroadcastReceiver
    {
#if DEBUG
        private static readonly string Tag = typeof(GlobalBroadcastReceiver).FullName;
#endif
        public const string MtcBtSmallon = @"com.microntek.bt.smallon";
        public const string MtcBtSmalloff = @"com.microntek.bt.smalloff";
        public const string MicBtReport = @"com.microntek.bt.report";
        public const string StateBtSmallOn = @"MtcBtSmallOn";
        public const string StateBtConnected = @"MtcBtConnected";
        public const string BtNewMac = @"MtcBtMac";
        public const string BtUpdateList = @"MtcBtUpdateList";
        public const string BtScanFinished = @"MtcBtScanFinished";
        public const string NotificationBroadcastAction = ActivityCommon.AppNameSpace + ".Notification.Action";

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent?.Action == null)
            {
                return;
            }
            switch (intent.Action)
            {
                case MtcBtSmallon:
                case MtcBtSmalloff:
                    try
                    {
                        bool smallOn = intent.Action == MtcBtSmallon;
#if DEBUG
                        Android.Util.Log.Info(Tag, string.Format("BT small on: {0}", smallOn));
#endif
                        Intent broadcastIntent = new Intent(NotificationBroadcastAction);
                        broadcastIntent.PutExtra(StateBtSmallOn, smallOn);
                        LocalBroadcastManager.GetInstance(context).SendBroadcast(broadcastIntent);
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                    break;

                case MicBtReport:
                {
#if DEBUG
                    Android.Util.Log.Info(Tag, "Bt report:");
                    Android.OS.Bundle bundle = intent.Extras;
                    if (bundle != null)
                    {
                        foreach (string key in bundle.KeySet())
                        {
                            Object value = bundle.Get(key);
                            string valueString = string.Empty;
                            if (value != null)
                            {
                                valueString = value.ToString();
                            }
                            Android.Util.Log.Info(Tag, string.Format("Key: {0}={1}", key, valueString));
                        }
                    }
#endif
                    if (intent.HasExtra("bt_state"))
                    {
                        try
                        {
                            int btState = intent.GetIntExtra("bt_state", 0);
#if DEBUG
                            Android.Util.Log.Info(Tag, string.Format("BT bt_state: {0}", btState));
#endif
                            switch (btState)
                            {
                                case 87:
                                case 88:
                                case 89:
#if DEBUG
                                    Android.Util.Log.Info(Tag, "Ignoring bt_state");
#endif
                                    break;

                                case 90:
                                {
#if DEBUG
                                    Android.Util.Log.Info(Tag, "Sending notification: " + BtUpdateList);
#endif
                                    Intent broadcastIntent = new Intent(NotificationBroadcastAction);
                                    broadcastIntent.PutExtra(BtUpdateList, btState);
                                    LocalBroadcastManager.GetInstance(context).SendBroadcast(broadcastIntent);
                                    break;
                                }

                                default:
                                {
#if DEBUG
                                    Android.Util.Log.Info(Tag, "Sending notification: " + BtScanFinished);
#endif
                                    Intent broadcastIntent = new Intent(NotificationBroadcastAction);
                                    broadcastIntent.PutExtra(BtScanFinished, btState);
                                    LocalBroadcastManager.GetInstance(context).SendBroadcast(broadcastIntent);
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                    if (intent.HasExtra("connected_mac"))
                    {
                        try
                        {
                            string mac = intent.GetStringExtra("connected_mac");
#if DEBUG
                            Android.Util.Log.Info(Tag, string.Format("BT connected_mac: {0}", mac));
#endif
                            Intent broadcastIntent = new Intent(NotificationBroadcastAction);
                            broadcastIntent.PutExtra(BtNewMac, mac);
                            LocalBroadcastManager.GetInstance(context).SendBroadcast(broadcastIntent);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                    if (intent.HasExtra("connect_state"))
                    {
                        try
                        {
                            int connectState = intent.GetIntExtra("connect_state", 0);
#if DEBUG
                            Android.Util.Log.Info(Tag, string.Format("BT connect_state: {0}", connectState));
#endif
                            ActivityCommon.MtcBtConnectState = connectState != 0;

                            Intent broadcastIntent = new Intent(NotificationBroadcastAction);
                            broadcastIntent.PutExtra(StateBtConnected, ActivityCommon.MtcBtConnectState);
                            LocalBroadcastManager.GetInstance(context).SendBroadcast(broadcastIntent);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                    break;
                }
            }
        }
    }
}
