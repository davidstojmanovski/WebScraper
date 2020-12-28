using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
namespace WebScraper
{
    class Program
    {

        //When testing i discovered a bug on the website where there was a duplicate of a currency IDR.
        //The data in the duplicates are identical, so the implemented solution for the bug is that only 
        //one of the two copies is inputed.
        static async Task Main(string[] args)
        {
            //Getting the file path from App.config
            String filePath = ConfigurationManager.AppSettings["Path"];

            WebScrapingUtil util = new WebScrapingUtil(filePath);
            await util.scrapeForCurrencyOptions();


        }
    }
}
