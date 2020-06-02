﻿using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BulkReq
{
    class Program
    {

        public static string GenerateUniqueRandomToken()
        {
            const string availableChars =
                "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            using (var generator = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[16];
                generator.GetBytes(bytes);
                var chars = bytes
                    .Select(b => availableChars[b % availableChars.Length]);
                var token = new string(chars.ToArray());
                return token;
            }
        }

        public static async Task TestHTTP()
        {
            List<string> URLs = new List<string>();
            for (int i = 0; i < 1000000; i++)
            {
                URLs.Add("http://scanme.nmap.org/" + GenerateUniqueRandomToken());
            }

            var client = new HttpClient();
            //Start with a list of URLs

            //Start requests for all of them
            var requests = URLs.Select
                (
                    url => client.GetAsync(url)
                ).ToList();

            //Wait for all the requests to finish
            await Task.WhenAll(requests);

            //Get the responses
            var responses = requests.Select
                (
                    task => task.Result
                );

            foreach (var r in responses)
            {
                var s = await r.Content.ReadAsStringAsync();
                Console.WriteLine(r);
            }

            
        }

        public static void TestWMI()
        {

        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<WMIOptions>(args)
                .MapResult(
                (WMIOptions opts) => WMI.RunWMI(opts),
                errs => 1);
        }


        static void HandleParseError(IEnumerable<Error> errs)
        {
            //handle errors
        }
    }
}
