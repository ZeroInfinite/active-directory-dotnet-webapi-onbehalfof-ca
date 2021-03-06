﻿//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// The following using statements were added for this sample.
using System.Globalization;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
using System.Configuration;
using System.Security.Claims;

namespace TodoListClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Redirect URI is the URI where Azure AD will return OAuth responses.
        // The Authority is the sign-in URL of the tenant.
        //
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        private static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        Uri redirectUri = new Uri(ConfigurationManager.AppSettings["ida:RedirectUri"]);

        private static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        private static string todoListResourceId = ConfigurationManager.AppSettings["todo:TodoListResourceId"];
        private static string todoListBaseAddress = ConfigurationManager.AppSettings["todo:TodoListBaseAddress"];

        private HttpClient httpClient = new HttpClient();
        private AuthenticationContext authContext = null;

        // Error Constants
        const String SERVICE_UNAVAILABLE = "temporarily_unavailable";
        const String INTERACTION_REQUIRED = "interaction_required";
        const String FAILED_SILENT = "failed_to_acquire_token_silently";
        const String USER_CANCELED = "authentication_canceled";

        public MainWindow()
        {
            InitializeComponent();

            InitializeLogin();
        }

        private async Task InitializeLogin()
        {
            //
            // As the application starts, try to get an access token without prompting the user.  If one exists, populate the To Do list.  If not, continue.
            //
            authContext = new AuthenticationContext(authority, new FileCache());
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);

                // A valid token is in the cache - get the To Do list.
                SignInButton.Content = "Clear Cache";
                GetTodoList();
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == FAILED_SILENT)
                {
                    // There are no tokens in the cache.  Proceed without calling the To Do list service.
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }
                    MessageBox.Show(message);
                }
                return;
            }
        }

        private async Task GetTodoList()
        {
            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
            }
            catch (AdalException ex)
            {
                // There is no access token in the cache, so prompt the user to sign-in.
                if (ex.ErrorCode == FAILED_SILENT)
                {
                    MessageBox.Show("Please sign in first");
                    SignInButton.Content = "Sign In";
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }
                    MessageBox.Show(message);
                }

                return;
            }

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do list service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/todolist");

            if (response.IsSuccessStatusCode)
            {

                // Read the response and databind to the GridView to display To Do items.
                string s = await response.Content.ReadAsStringAsync();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                List<TodoItem> toDoArray = serializer.Deserialize<List<TodoItem>>(s);

                TodoList.ItemsSource = toDoArray.Select(t => new { t.Title });
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }

            return;
        }

        private async void AddTodoItem(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TodoText.Text) )
            {
                MessageBox.Show("Please enter a value for the To Do item name");
                return;
            }

            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
            }
            catch (AdalException ex)
            {
                // There is no access token in the cache, so prompt the user to sign-in.
                if (ex.ErrorCode == FAILED_SILENT)
                {
                    MessageBox.Show("Please sign in first");
                    SignInButton.Content = "Sign In";
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                return;
            }

            //
            // Call the To Do service.
            //

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Forms encode Todo item, to POST to the todo list web api.
            HttpContent content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("Title", TodoText.Text) });

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.PostAsync(todoListBaseAddress + "/api/todolist", content);

            if (response.IsSuccessStatusCode)
            {
                TodoText.Text = "";
                GetTodoList();
            } else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }
        }

        private async void AccessCAWebAPI(object sender, RoutedEventArgs e)
        {

            //
            // Get an access token to call the To Do service.
            //
            AuthenticationResult result = null;

            try
            {
                result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
            }
            catch (AdalException ex)
            {
                // There is no access token in the cache, so prompt the user to sign-in.
                if (ex.ErrorCode == FAILED_SILENT)
                {
                    MessageBox.Show("Please sign in first");
                    SignInButton.Content = "Sign In";
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                return;
            }

            //
            // Call the To Do service. We may get a claims challenge and need to redo the auth
            //

            // Once the token has been returned by ADAL, add it to the http authorization header, before making the call to access the To Do service.
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            // Call the To Do list service.
            HttpResponseMessage response = await httpClient.GetAsync(todoListBaseAddress + "/api/AccessCaApi");

            if (response.IsSuccessStatusCode)
            {
                // User's token has already had an interactive auth with CA Policy 
                // Call to our api was successful 
                MessageBox.Show("We already Stepped-up.  Successfully called CA protected Web API");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest && response.ReasonPhrase == INTERACTION_REQUIRED)
            {
                // We need to setup the token to account for a Conditional Access Policy
                String claimsParam = await response.Content.ReadAsStringAsync();

                if (String.IsNullOrWhiteSpace(claimsParam))
                {
                    MessageBox.Show("ESTS Returned no Claims on interaction_required");
                    return; 
                }

                await SignInCA(claimsParam, result.UserInfo.DisplayableId);

                try
                {
                    // Stepped up Access Token is in the cache
                    result = await authContext.AcquireTokenSilentAsync(todoListResourceId, clientId);
                }
                catch (AdalException ex)
                {
                    // There is no access token in the cache
                    if (ex.ErrorCode == AdalError.FailedToAcquireTokenSilently)
                    {
                        MessageBox.Show("Please sign in first");
                    }
                    else
                    {
                        // An unexpected error occurred.
                        string message = ex.Message;
                        if (ex.InnerException != null)
                        {
                            message += "Inner Exception : " + ex.InnerException.Message;
                        }

                        MessageBox.Show(message);
                    }

                    return;
                }

                // Valid Access token in result, call our api with new token 
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                HttpResponseMessage responseCA = await httpClient.GetAsync(todoListBaseAddress + "/api/AccessCaApi");

                if (responseCA.IsSuccessStatusCode)
                {
                    MessageBox.Show("Successfully called CA-Protected Web API");

                }
                else
                {
                    MessageBox.Show("Problem calling Web API HTTP " + response.StatusCode);
                }
            }
            else
            {
                MessageBox.Show("An error occurred : " + response.ReasonPhrase);
            }
        }

        private async void SignIn(object sender = null, RoutedEventArgs args = null)
        {
            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == "Clear Cache")
            {
                TodoList.ItemsSource = string.Empty;
                authContext.TokenCache.Clear();
                // Also clear cookies from the browser control.
                ClearCookies();
                SignInButton.Content = "Sign In";
                return;
            }

            //
            // Get an access token to call the To Do list service.
            //
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenAsync(todoListResourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Always));
                SignInButton.Content = "Clear Cache";
                GetTodoList();
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == USER_CANCELED)
                {
                    MessageBox.Show("Sign in was canceled by the user");
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }

                return;
            }

        }

        private async Task SignInCA(String claims, string displayName)
        {
            // If there is already a token in the cache, clear the cache and update the label on the button.
            if (SignInButton.Content.ToString() == "Clear Cache")
            {
                TodoList.ItemsSource = string.Empty;
                authContext.TokenCache.Clear();

                // Also clear cookies from the browser control.
                ClearCookies();
                SignInButton.Content = "Sign In";
            }

            //
            // Get an access token to call the To Do list service w/ CA.
            //
            AuthenticationResult result = null;
            try
            {
                result = await authContext.AcquireTokenAsync(todoListResourceId, clientId, redirectUri, new PlatformParameters(PromptBehavior.Always), 
                    new UserIdentifier(displayName, UserIdentifierType.OptionalDisplayableId), "claims="+claims);

                /* Update UI */
                SignInButton.Content = "Clear Cache";

                /* Re-call the middle tier now that we've stepped/proofed-up */
                GetTodoList();
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == USER_CANCELED)
                {
                    MessageBox.Show("Sign in was canceled by the user");
                }
                else
                {
                    // An unexpected error occurred.
                    string message = ex.Message;
                    if (ex.InnerException != null)
                    {
                        message += "Inner Exception : " + ex.InnerException.Message;
                    }

                    MessageBox.Show(message);
                }
                return;
            }

        }


        // This function clears cookies from the browser control used by ADAL.
        private void ClearCookies()
        {
            const int INTERNET_OPTION_END_BROWSER_SESSION = 42;
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_END_BROWSER_SESSION, IntPtr.Zero, 0);
        }

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

    }
}
