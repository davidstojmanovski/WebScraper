using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebScraper
{
    class WebScrapingUtil
    {

        String url = "https://srh.bankofchina.com/search/whpj/searchen.jsp";
        String filePath;
        public static List<int> DummyLock = new List<int>();

        public WebScrapingUtil(String filePath)
        {
            this.filePath = filePath;
        }

        //Scrapres for currency options and calls scrapeThroughAllPages() for each currency 
        public async Task scrapeForCurrencyOptions()
        {
            //Code for requesting a page. Any page will do because only the currency list is needed.
            CancellationTokenSource cancelationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage request = await httpClient.GetAsync(url);
            cancelationToken.Token.ThrowIfCancellationRequested();
            Stream response = await request.Content.ReadAsStreamAsync();
            cancelationToken.Token.ThrowIfCancellationRequested();
            
            //Code for parsing the page and getting the currency data
            HtmlParser parser = new HtmlParser();
            IHtmlDocument document = parser.ParseDocument(response);
            IEnumerable<IElement> list = document.QuerySelectorAll("option");
            List<String> listOfCurrency = new List<String>();


            //listOfCurrency was needed so the first currency could be removed (not a real currency, just a 'select currency option')
            foreach (IElement i in list)
            {
                listOfCurrency.Add(i.InnerHtml);
            }
            listOfCurrency.RemoveAt(0);
           
            //Implementation of multithreading. Every currency gets its own thread.
            Console.WriteLine("Working...");
            List<Task> tasks = new List<Task>();
            foreach(String l in listOfCurrency)
            {
                     tasks.Add(Task.Run( ()=>scrapeThroughAllPages(l)));
            }

            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("Scraping is complete!");
        }

        //Method that receives a currency and does the scraping for that currency for every page of the pagination.
        public async Task scrapeThroughAllPages(String currency)
        {
            //Setting up all the information needed for form submition.
            Console.WriteLine("Scraping for " + currency+"...");
            CancellationTokenSource cancelationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HtmlParser parser = new HtmlParser();
            String previous = "";
            String currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            String prevDate = DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd");
            int mem = 1;

            var values = new Dictionary<String, String>
            {
                {"erectDate", prevDate },
                {"nothing", currentDate },
                {"pjname", currency }
            };
            values.Add("page", mem.ToString());
            var content = new FormUrlEncodedContent(values);
            
            HttpResponseMessage request = await httpClient.PostAsync(url, content);
            cancelationToken.Token.ThrowIfCancellationRequested();
            String response = await request.Content.ReadAsStringAsync();
            String filePathCur = filePath + "\\" + currency + " - " + prevDate + "-" + currentDate + ".csv";
            if (File.Exists(filePathCur))
            {
                Console.WriteLine("There is a duplicate of "+currency+".Ignoring duplicate...");
                return;
            }
            //Code for extracting the header row and inputing it in the .csv file. Lock is just a precaution mesure if there were duplicates of a currency.
            //This happened with IDR currency where there was a duplicate of this currency on the website.
            try
            {
                lock (DummyLock)
                {       
                    using (TextWriter file = new StreamWriter(filePathCur, true))
                    {
                        IHtmlDocument document = parser.ParseDocument(response);
                        IElement tableElement = document.QuerySelector("body > table:nth-child(4) > tbody");
                        IElement rowElement = tableElement.Children.ElementAt(0);
                        if (tableElement.Children.Length == 2) return;
                        StringBuilder s = new StringBuilder();
                        foreach (IElement el in rowElement.Children)
                        {
                            s.Append(el.InnerHtml.ToString());
                            s.Append(",");
                        }
                        s.Remove(s.Length - 1, 1);
                        FileInfo fInfo = new FileInfo(filePathCur);
                        //The solution for the "duplicate currency problem".
                        //The copy of duplicate will be inputed only once.
                        if (!(fInfo.Length>0)) {
                            file.WriteLine(s.ToString());
                            file.Flush();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("File path is not valid");
                throw new ApplicationException(ex.StackTrace);
            }

            //Iterating through each page in the pagination.
            //This could be done more effective as it is the perfect oportunity for multithreading(for each page),
            //but i was unable to locate the HTML element that contains the total number of pages. 
            while (!response.Equals(previous))
            {
                parsePage(response,filePathCur);
                values.Remove("page");
                mem++;
                previous = response;
                values.Add("page", mem.ToString());
                content = new FormUrlEncodedContent(values);
                request = await httpClient.PostAsync(url, content);
                cancelationToken.Token.ThrowIfCancellationRequested();
                response = await request.Content.ReadAsStringAsync();

            }
            Console.WriteLine("Scraping for " + currency + " is completed!");
        }


        //Parses the given page and appends the values to the file
        private void parsePage(String page,String file)
        {
            HtmlParser parser = new HtmlParser();
            try
            {
                IHtmlDocument document = parser.ParseDocument(page);
                IElement tableElement = document.QuerySelector("body > table:nth-child(4) > tbody");
                
                foreach(IElement row in tableElement.Children)
                {
                    if (row.Index() != 0)
                    {
                        StringBuilder s = new StringBuilder();

                        foreach (IElement c in row.Children)
                        {
                            s.Append(c.InnerHtml.ToString());
                            s.Append(",");
                        }
                        s.Remove(s.Length - 1, 1);
                        lock (DummyLock)
                        {
                            using (StreamWriter streamWriter = new StreamWriter(file, true))
                            {
                                streamWriter.WriteLine(s.ToString());
                            }
                        }
                    }
                }   
            }
            catch (Exception ex)
            {
                Console.WriteLine("File path is not valid.");
                throw new ApplicationException(ex.Message);
            }

        }
    }
}
