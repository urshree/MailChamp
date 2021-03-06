﻿using EmailHelper.Models;
using EmailHelper.Utilities;
using Microsoft.Exchange.WebServices.Data;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmailHelper
{
    class Program
    {
        private static AuthenticationResult authResult;
        /// <summary>
        /// Entry to the Program starts at Main
        /// </summary>
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to MailChamp! You can send or recieve Email using this program.\n");

            try
            {
                var auth = authResult;

                if (auth == null)
                {
                    Console.WriteLine("Please login to your Microsoft account! \n");
                    auth = ProcessLoginWithAuthToken(args).Result;
                }

                if (auth != null)
                {
                    var token = auth.AccessToken;
                    PerformUserSelectedOperation(null, null, token);
                }
                else
                {
                    Console.WriteLine("We coudld not process your request! Falling back to manual authentication. \n");
                    ProcessLoginWithManualEntry();
                }

                Console.WriteLine("Please press c to start from main menu. Any other key to quit\n");

                if (Console.ReadKey(true).Key == ConsoleKey.C)
                {
                    Console.Clear();
                    Main(args);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                Console.WriteLine("Please press C to start from main menu. Any other key to quit\n");

                if (Console.ReadKey(true).Key == ConsoleKey.C)
                {
                    Console.Clear();
                    Main(args);
                }
            }
        }

        /// <summary>
        /// Fallback mechanism when Oauth Fails to get the token
        /// Ask the User for Email and password
        /// </summary>
        private static void ProcessLoginWithManualEntry()
        {
            string emailId, password;

            do
            {
                Console.WriteLine("Please enter your valid Office 365 Email ID");

                emailId = Console.ReadLine();

            } while (!Utils.IsValidEmail(emailId)); //Keep asking for a valid email ID if invalid is entered by user

            do
            {
                Console.WriteLine("Please enter your Office 365 Password");

                password = ReadPassword();

            } while (string.IsNullOrEmpty(password)); //Keep asking for a password until entered

            PerformUserSelectedOperation(emailId, password);
        }

        /// <summary>
        /// Ask the user with options to Send or Read Emails
        /// </summary>
        /// <param name="emailId">Email Id</param>
        /// <param name="password">Password</param>
        private static void PerformUserSelectedOperation(string emailId, string password, string token = "")
        {
            Console.WriteLine("\n1) Enter 1 to Compose Email\n2) Enter 2 to Read Top 100 Emails");

            var option = int.TryParse(Console.ReadLine(), out int val);

            if (!option)
            {
                Console.WriteLine("Invalid Option! Please try again!");

                PerformUserSelectedOperation(emailId, password, token); //Recurrsive approach to start this method again
            }

            switch (val)
            {
                case 1:
                    ComposeEmail(emailId, password, token);
                    break;

                case 2:
                    var emails = EmailReader.ReadEmails(new Email { EmailId = emailId, Password = password, Token = token });
                    DisplayReceivedEmails(emails);
                    break;

                default:
                    Console.WriteLine("Invalid Option! Please try again!");
                    PerformUserSelectedOperation(emailId, password, token);
                    break;
            }
        }

        #region Email Sender Methods

        /// <summary>
        /// Composes Email by accepting inputs from user for Subject, Body and Recepients
        /// </summary>
        /// <param name="emailId">User's Email ID</param>
        /// <param name="password">User's Password</param>
        private static void ComposeEmail(string emailId, string password, string token = "")
        {
            var email = new Email
            {
                EmailId = emailId,
                Password = password,
                Token = token
            };

            email.ToRecipients = GetAddresses();

            if (email.ToRecipients == null || !email.ToRecipients.Any())
            {
                Console.WriteLine("No valid recepients were found!");
                return;
            }

            Console.WriteLine("Enter the subject");

            email.Subject = Console.ReadLine() ?? "";

            Console.WriteLine("Please enter the message you want to send. You can include HTML tags also!");

            email.Body = Console.ReadLine() ?? "";

            EmailSender.SendEmail(email);

            Console.WriteLine("Email Sent successfully");
        }

        /// <summary>
        /// Accept and Validate each Email ID entered by user. Invalid's are ignored
        /// </summary>
        /// <returns>List of valid email ids </returns>
        private static List<EmailAddress> GetAddresses()
        {
            Console.WriteLine("Enter recepients emails address(s) as comma(,) seperated");

            var addresses = Console.ReadLine().Trim();

            if (string.IsNullOrEmpty(addresses.Trim()))
                GetAddresses();

            var validEmails = new List<EmailAddress>();

            if (!addresses.Contains(","))
            {
                if (!Utils.IsValidEmail(addresses))
                    return null;

                validEmails.Add(new EmailAddress(addresses));

                return validEmails;
            }

            var emails = addresses.Split(',');

            foreach (var emailAddress in emails)
            {
                if (Utils.IsValidEmail(emailAddress.Trim()))
                {
                    validEmails.Add(new EmailAddress(emailAddress));
                }
                else
                    Console.WriteLine($"Invalid: {emailAddress}. Ignored from the recepients list");
            }

            return validEmails;
        }

        #endregion

        #region Read and Display Methods
        /// <summary>
        /// Process the Responses
        /// </summary>
        /// <param name="emails">All Email Messages</param>
        private static void DisplayReceivedEmails(List<EmailMessage> emails)
        {
            var i = 1;
            foreach (var emailMsg in emails)
            {
                Console.WriteLine($"---------------------------------------- Message {i++} ----------------------------------------------");
                Console.WriteLine($"On: {emailMsg.DateTimeReceived:dd/MMM/yyyy HH:mm}");
                Console.WriteLine($"From: {emailMsg.From.Address}");
                Console.WriteLine($"Subject: {emailMsg.Subject}");
                Console.WriteLine($"Message: {emailMsg.TextBody.Text}");
                Console.WriteLine($"---------------------------------------- End of Message ----------------------------------------------\n\n");
            }
        }

        /// <summary>
        /// Read's Password from Console and replaces it with a asterik (*) to mask the entry.
        /// </summary>
        /// <returns>User's Password</returns>
        private static string ReadPassword()
        {
            string pass = "";

            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*"); //Replace the pressed key with a *.
                }
                else
                {
                    //If Backspace was pressed, remove the last character from the 'pass' string and on the screen
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                    else if (key.Key == ConsoleKey.Enter)
                    {
                        break;
                    }
                }
            } while (true);

            return pass;
        }

        #endregion


        /// <summary>
        /// Gets Token via Oauth
        /// Morre Info: https://docs.microsoft.com/en-us/exchange/client-developer/exchange-web-services/how-to-authenticate-an-ews-application-by-using-oauth
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async System.Threading.Tasks.Task<AuthenticationResult> ProcessLoginWithAuthToken(string[] args)
        {
            // Configure the MSAL client to get tokens            
            var client = Environment.GetEnvironmentVariable("AZ_ClientID");
            var tenant = Environment.GetEnvironmentVariable("AZ_TenantId");

            if (string.IsNullOrEmpty(client) || string.IsNullOrEmpty(tenant))
            {
                Console.WriteLine("Please set the environment variables for ClientID and Tenant ID.");                
                return null;
            }

            var pcaOptions = new PublicClientApplicationOptions
            {
                ClientId = client,
                TenantId = tenant
            };

            var pca = PublicClientApplicationBuilder
                .CreateWithApplicationOptions(pcaOptions).Build();

            var ewsScopes = new string[] { "https://outlook.office.com/EWS.AccessAsUser.All" };

            try
            {
                // Make the interactive token request
                authResult = await pca.AcquireTokenInteractive(ewsScopes).ExecuteAsync();

                return authResult;
            }
            catch (MsalException ex)
            {
                Console.WriteLine($"Error acquiring access token: {ex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }

            return null;
        }
    }
}
