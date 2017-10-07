using Facebook;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace FacebookPoster
{
    class Program
    {
        public const string USER_ACCESS_TOKEN = "xxxxxxxxxx";

        static void Main(string[] args)
        {
            while (true)
            {
                var message = File.ReadAllText("text.txt");
                Console.Clear();
                List<string> Pages = File.ReadAllLines("sites.txt").ToList();

                foreach (var line in Pages)
                    Console.WriteLine(line);

                int daysInAdvance;

                Console.WriteLine("Which day to schedule? (0 today, 1 tomorrow, 2 the next day)");
                int.TryParse(Console.ReadLine().ToString(), out daysInAdvance);
                Console.WriteLine($"Day to schedule: {daysInAdvance}  (0 today, 1 tomorrow, 2 the next day)");

                List<DateTime> times = new List<DateTime>();
                ConsoleKey keyPress = ConsoleKey.N;
                while (keyPress != ConsoleKey.Y && keyPress != ConsoleKey.Q)
                {
                    Console.Clear();
                    times = GenerateUTCTimesToPost(Pages.Count, daysInAdvance);

                    Console.WriteLine("These are the generated times:");

                    foreach (var time in times)
                        Console.WriteLine($" UTC Time: { time }  Local Time: {time.ToLocalTime()}");

                    Console.WriteLine("Press Y to Accept these times, Q to quit and any other key to decline.");
                    keyPress = Console.ReadKey().Key;
                }

                if (keyPress == ConsoleKey.Q)
                    continue;

                Console.WriteLine("ABOUT TO POST. PRESS ANY KEY TO CONTINUE.");
                Console.ReadKey();

                var getObjects = new List<object>();
                dynamic data = new object();
                foreach (var page in Pages)
                {
                    Console.WriteLine($"Executing page: {page}");
                    var fbClient = new FacebookClient(USER_ACCESS_TOKEN);
                    var currentObject = fbClient.Get(page);
                    data = new JavaScriptSerializer().Deserialize(currentObject.ToString(), typeof(object));
                    string pageID = data["id"];
                    var fetchPageToken = fbClient.Get($"https://graph.facebook.com/{pageID}?fields=access_token&access_token={USER_ACCESS_TOKEN}");
                    data = new JavaScriptSerializer().Deserialize(fetchPageToken.ToString(), typeof(object));
                    getObjects.Add(data);
                    fbClient = new FacebookClient(data["access_token"]);

                    dynamic parameters = new ExpandoObject();
                    parameters.message = message;
                    parameters.published = "false";
                    parameters.scheduled_publish_time = (int)(times[Pages.IndexOf(page)].Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    parameters.source = new FacebookMediaObject
                    {
                        ContentType = "image/jpeg",
                        FileName = Path.GetFileName("content.jpg")
                    }.SetValue(File.ReadAllBytes("content.jpg"));

                    dynamic hallo = fbClient.Post($"/{pageID}/photos", parameters);
                }
            }
        }


        private static List<DateTime> GenerateUTCTimesToPost(int numberOfGeneratedTimes, int daysInAdvance)
        {
            var result = new List<DateTime>();
            var dayOffset = daysInAdvance;
            var startOffset = 8; // 8 am PST
            var endOffset = 22; // 10 pm PST
            var startDate = DateTime.Now.AddDays(dayOffset).AddHours(-DateTime.Now.Hour + startOffset).AddMinutes(-DateTime.Now.Minute).ToUniversalTime();
            if (startDate < DateTime.Now.ToUniversalTime())
                startDate = DateTime.Now.AddMinutes(5).ToUniversalTime();

            var endDate = DateTime.Now.AddDays(dayOffset).AddHours(-DateTime.Now.Hour + endOffset).AddMinutes(-DateTime.Now.Minute).ToUniversalTime();


            if (startDate > endDate)
                throw new Exception("Cannot schedule for today, because the start date is too late. Try for tomorrow.");

            var endDateLocal = endDate.ToLocalTime();
            var maxAdd = (int)(endDate - startDate).TotalSeconds;

            Console.WriteLine($" Start local time: { startDate.ToLocalTime() }  End local Time: {endDate.ToLocalTime()}");
            Console.WriteLine($"{ numberOfGeneratedTimes} Times/sites to schedule:, over {maxAdd/60} minute(s) ");
            var minuteRange = ((endDate - startDate).TotalMinutes / (numberOfGeneratedTimes*2));
            Console.WriteLine($"Minimum minutes seperating each time slot: { minuteRange} ");
            var random = new Random();
            while(result.Count != numberOfGeneratedTimes)
            {
                var additionalSeconds = random.Next(0, maxAdd);
                var addSeconds = startDate.AddSeconds(additionalSeconds);
                if (!result.Exists(x=> Math.Abs((x - addSeconds).TotalMinutes) <= minuteRange))
                    result.Add(startDate.AddSeconds(additionalSeconds));
            }

            return result;
        }

    }
}
