/**
 *  Copyright (c) 2012 Distimo. All rights reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using Microsoft.Phone.Shell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Distimo
{
    public sealed class SDK
    {
        internal static readonly String UUID_KEY = "SovPm8veMhxiAVPreeEY";
        internal static readonly String FIRSTLAUNCH_SENT_KEY = "o1YCoJMBoxYXUHOfBGsL";
        internal static readonly String USER_REGISTERED_KEY = "3V8ySn0o4DcbSii4eo0E";
        internal static readonly String USER_ID_KEY = "IkdjUdvdDzct6x1WmijE";

        internal static readonly String EXCEPTION_FILE_NAME = "g7S7WFtrOCdonSmath2y";

        internal static readonly String SDK_VERSION = "2.6";

        internal static String publicKey;
        internal static String privateKey;
        internal static String uniqueUserID;
        internal static String uniqueHardwareID;
        internal static String bundleID;
        internal static String appVersion;

        private static Boolean started = false;

        private SDK() { }

        /// <summary>
        /// Start the Distimo SDK
        /// </summary>
        /// <param name="sdkKey">The SDK Key for your organization. You should create one in the SDK section in Settings in Distimo App Analytics</param>
        public static void start(String sdkKey)
        {
            if (!started)
            {
                started = true;

                if (sdkKey.Length > 4)
                {
                    publicKey = sdkKey.Substring(0, 4);
                    privateKey = sdkKey.Substring(4, sdkKey.Length - 4);
                    appVersion = Regex.Match(Assembly.GetExecutingAssembly().FullName, @"Version=(\d\.\d\.\d\.\d)").Groups[1].Value;
                    bundleID = XDocument.Load("WMAppManifest.xml").Root.Element("App").Attribute("ProductID").Value.Replace("{", "").Replace("}", "");
                    byte[] hwBytes = (byte[])DeviceExtendedProperties.GetValue("DeviceUniqueId");
                    uniqueHardwareID = Utils.Base64EncodeHexString(BitConverter.ToString(hwBytes).Replace("-", ""));

                    //Read userID from UserExtendedProperties
                    object anid;
                    if (UserExtendedProperties.TryGetValue("ANID", out anid))
                    {
                        if (anid != null && anid.ToString().Length >= (32 + 2))
                        {
                            uniqueUserID = Utils.Base64EncodeHexString(anid.ToString().Substring(2, 32));
                        }
                    }

                    if (uniqueUserID == null)
                    {
                        //Read from IsolatedStorage
                        uniqueUserID = (String)Utils.getApplicationSetting(UUID_KEY);

                        if (uniqueUserID == null)
                        {
                            //Create random GUID
                            uniqueUserID = Utils.Base64EncodeHexString(Guid.NewGuid().ToString().Replace("-", ""));
                            Utils.setApplicationSetting(UUID_KEY, uniqueUserID);
                        }
                    }

                    Utils.log("publicKey        : " + publicKey);
                    Utils.log("privateKey       : " + privateKey);
                    Utils.log("appVersion       : " + appVersion);
                    Utils.log("bundleID         : " + bundleID);
                    Utils.log("uniqueHardwareID : " + uniqueHardwareID);
                    Utils.log("uniqueUserID     : " + uniqueUserID);

                    //Start the event manager
                    EventManager.initialize();

                    //Listen for unhandled exceptions
                    Distimo.SDK.startExceptionHandler();

                    //Send the FirstLaunch event if necessary
                    SDK.sendFirstLaunchEvent();
                }
            }
        }

        /// <summary>
        /// Get the version of the Distimo SDK
        /// </summary>
        /// <returns>The current version of the Distimo SDK</returns>
        public static String version()
        {
            return SDK_VERSION;
        }

        /// <summary>
        /// Log this user as registered.
        /// </summary>
        public static void logUserRegistered()
        {
            bool userRegistered = (bool)Utils.getApplicationSetting(USER_REGISTERED_KEY, false);
            if (userRegistered == false)
            {
                Utils.setApplicationSetting(USER_REGISTERED_KEY, true);

                Distimo.Event userRegisteredEvent = EventManager.EventFactory.createEvent("UserRegistered", null, null);
                EventManager.logEvent(userRegisteredEvent);
            }
            else
            {
                Utils.log("User already marked as registered");
            }
        }

        /// <summary>
        /// Log an in-app purchase.
        /// </summary>
        /// <param name="productID">The product ID of the in-app purchase</param>
        /// <param name="formattedPrice">The value of ProductListing.FormattedPrice</param>
        public static void logInAppPurchase(String productID, String formattedPrice)
        {
            Dictionary<String, String> parameters = new Dictionary<String, String>();
            parameters.Add("productID", productID);
            parameters.Add("formattedPrice", formattedPrice);
            parameters.Add("quantity", "1");

            //Get all available culture and region info for currency guess
            parameters.Add("cultureName", CultureInfo.CurrentCulture.Name);
            parameters.Add("cultureCurrencySymbol", CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol);
            try
            {
                RegionInfo cultureRegionInfo = new RegionInfo(CultureInfo.CurrentCulture.Name);
                if (cultureRegionInfo != null)
                {
                    parameters.Add("cultureISOCurrencySymbol", cultureRegionInfo.ISOCurrencySymbol);
                }
            }
            catch (Exception exc)
            {
                Utils.log(exc.StackTrace);
            }

            parameters.Add("uiCultureName", CultureInfo.CurrentUICulture.Name);
            parameters.Add("uiCultureCurrencySymbol", CultureInfo.CurrentUICulture.NumberFormat.CurrencySymbol);
            try
            {
                RegionInfo uiCultureRegionInfo = new RegionInfo(CultureInfo.CurrentUICulture.Name);
                if (uiCultureRegionInfo != null)
                {
                    parameters.Add("uiCultureISOCurrencySymbol", uiCultureRegionInfo.ISOCurrencySymbol);
                }
            }
            catch (Exception exc)
            {
                Utils.log(exc.StackTrace);
            }
            
            parameters.Add("regionName", RegionInfo.CurrentRegion.Name);
            parameters.Add("regionISOName", RegionInfo.CurrentRegion.TwoLetterISORegionName);
            parameters.Add("regionCurrencySymbol", RegionInfo.CurrentRegion.CurrencySymbol);
            parameters.Add("regionISOCurrencySymbol", RegionInfo.CurrentRegion.ISOCurrencySymbol);

            parameters.Add("timezone", ((int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalMinutes).ToString());

            Distimo.Event purchaseEvent = EventManager.EventFactory.createEvent("InAppPurchase", parameters, null);
            EventManager.logEvent(purchaseEvent);
        }

        /// <summary>
        /// Log an external purchase, like consumer goods or a booking.
        /// </summary>
        /// <param name="productID">The product ID of the external purchase (optional)</param>
        /// <param name="currency">The ISO 4217 currency code of the external purchase</param>
        /// <param name="price">The price of the external purchase</param>
        /// <param name="quantity">The quantity of this external purcahse</param>
        public static void logExternalPurchase(String productID, String currency, double price, int quantity)
        {
            Dictionary<String, String> parameters = new Dictionary<String, String>();
            parameters.Add("productID", productID);
            parameters.Add("currency", currency);
            parameters.Add("price", price.ToString());
            parameters.Add("quantity", quantity.ToString());

            Distimo.Event purchaseEvent = EventManager.EventFactory.createEvent("ExternalPurchase", parameters, null);
            EventManager.logEvent(purchaseEvent);
        }

        /// <summary>
        /// Log a banner click with an optional publisher
        /// </summary>
        /// <param name="publisher">The publisher of this banner (optional)</param>
        public static void logBannerClick(String publisher)
        {
            Dictionary<String, String> parameters = new Dictionary<String, String>();
            if (publisher != null)
            {
                parameters.Add("publisher", publisher);
            }

            Distimo.Event bannerEvent = EventManager.EventFactory.createEvent("BannerClick", parameters, null);
            EventManager.logEvent(bannerEvent);
        }

        /// <summary>
        /// Set a self-defined userID for this user. This userID is used to provide you with detailed
        /// source information that this user originated from.
        /// </summary>
        /// <param name="userID">Your self-defined userID for this user</param>
        public static void setUserID(String userID)
        {
            if (userID == null || userID.Length == 0)
            {
                //Don't send an empty userID
                return;
            }

            String newUserID = userID;
            String storedUserID = (String)Utils.getApplicationSetting(USER_ID_KEY);

            if (storedUserID == null || !newUserID.Equals(storedUserID))
            {
                Utils.setApplicationSetting(USER_ID_KEY, newUserID);

                Dictionary<String, String> parameters = new Dictionary<String, String>();
                parameters.Add("id", newUserID);

                Distimo.Event userIdEvent = EventManager.EventFactory.createEvent("UserID", parameters, null);
                EventManager.logEvent(userIdEvent);
            }
            else
            {
                Utils.log("UserID already set as '" + newUserID + "'");
            }

        }

        /// <summary>
        /// Redirects directly to the AppStore by routing through your AppLink. Use this for tracking
        ///  conversion from within your own apps, e.g. for upselling to your Pro apps.
        /// 
        /// Note: The redirect will happen in the background, this can take a couple of seconds.
        /// </summary>
        /// <param name="applinkHandle">The handle of the AppLink you want to open, e.g. "A00"</param>
        /// <param name="campaignHandle">The handle of the campaign you want to use, e.g. "a" (optional)</param>
        public static void openAppLink(String applinkHandle, String campaignHandle)
        {
            AppLinkManager.openAppLink(applinkHandle, campaignHandle, SDK.uniqueUserID);
        }

        // Private methods

        private static void sendFirstLaunchEvent()
        {
            bool firstLaunchSent = (bool)Utils.getApplicationSetting(FIRSTLAUNCH_SENT_KEY, false);
            if (firstLaunchSent == false)
            {
                Utils.setApplicationSetting(FIRSTLAUNCH_SENT_KEY, true);

                Utils.log("Going to send FirstLaunch event");

                Distimo.Event firstLaunchEvent = EventManager.EventFactory.createEvent("FirstLaunch", null, null, true, true);
                EventManager.logEvent(firstLaunchEvent);
            }
        }

        /// <summary>
        /// Look for any previous crashes with Distimo SDK involvement, and start an exception handler to check for Distimo SDK involvement on a crash
        /// </summary>
        private static void startExceptionHandler()
        {
            //First read a previously stored exception from storage, if it exists we log it in an event an clear the storage.
            // Note: the exception read here is stored by the exception handler we are about to start in the previous run of the application
            string stackTrace = StorageManager.read(EXCEPTION_FILE_NAME, true);

            if (stackTrace != null)
            {
                Event exceptionEvent = EventManager.EventFactory.createEvent("DistimoException", null, stackTrace);
                EventManager.logEvent(exceptionEvent);
            }

            //Add uncaught exception handler
            Application.Current.UnhandledException += new EventHandler<ApplicationUnhandledExceptionEventArgs>(Application_UnhandledException);
        }

        /// <summary>
        /// The uncaught exception handler that will look for Distimo SDK involvement in any crash that occurs in the application
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event args</param>
        private static void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Application_UnhandledException");

            //Disable event manager
            EventManager.setEnabled(false);

            //Get the exception object
            Exception ex = e.ExceptionObject;
            Exception exResult = null;

            //Try to find a class from the Distimo namespace in the stack trace of the exception itself or any of its recursive inner exceptions
            while (ex != null && exResult == null)
            {
                if (ex.StackTrace != null)
                {
                    string[] lines = ex.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.Contains("at Distimo."))
                        {
                            //Found the exception containing the Distimo line
                            exResult = ex;
                            break;
                        }
                    }
                }

                ex = ex.InnerException;
            }

            if (exResult != null)
            {
                //Construct stack trace
                string stackTrace = exResult.GetType().ToString() + ": " + exResult.Message + "\n" + exResult.StackTrace;
                System.Diagnostics.Debug.WriteLine(stackTrace);

                //Store stack trace
                StorageManager.store(stackTrace, EXCEPTION_FILE_NAME);
            }
        }
    }
}
