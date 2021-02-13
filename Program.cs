using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.Json;
using System.IO;
using System.Threading;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Interactions;
using System.Net;

namespace minesweeper_recursive
{
    public class LoginArgs
    {
        public string username { get; set; }
        public string password { get; set; }
    }
    class Program
    {
        //hilfe und argumente
        static List<string> helpArgs = new List<string> { "--help", "-h", "/?", "help", "h" };
        static string helpString =
            "Argumente:\n" +
            "n: übergeben wenn keine Flaggen verwendet werden sollen\n" +
            "l: Auswahl des levels (1 - beginner, 2 - intermediate, 3 - expert, 4 - custom)\n" +
            "w: nur für custom-level. Gibt die Breite des Spielfeldes an\n" +
            "h: nur für custom-level. Gibt die Höhe des Spielfeldes an\n" +
            "m: nur für custom-level. Gibt die Anzahl an Minen an\n" +
            "g: Anzahl Spiele, die gespielt werden sollen. Default: Unendlich\n" +
            "u: 'Until Win' (Spielt so lange, bis die Anzahl an wins erzielt wurde)\n" +
            "d: Spiel mit zufälligem Delay (anti-ban)\n" +
            "\n" +
            "Beispiele:\n" +
            "nl3: ein expert-level Spiel, in dem keine Flaggen verwendet werden\n" +
            "l4w15h10m30: ein custom-level der Größe 15x10 mit 30 Minen\n" +
            "g10il3n: 10 expert-level Spiele ohne Anmeldung und ohne Flaggen\n" +
            "\n" +
            "Default: easy-level mit Flaggen."
            ;
        static bool noflag = false, incognito = false, delay = false;
        static short level = 1, width = 9, height = 9, mines = 20, wins = 0, losses = 0, games = -1, untilwin = -1;
        //buttons und web-navigation
        static string login = "/html/body/header/div/div/div/button[2]",
            usernameField = "/html/body/div[6]/div/div/form/div[2]/div[3]/div[1]/div/input",
            passwordField = "/html/body/div[6]/div/div/form/div[2]/div[3]/div[2]/div/input",
            loginEnter = "/html/body/div[6]/div/div/form/div[3]/button[2]",
            customLevel = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[12]/div/div[1]/a[4]/span",
            widthInput = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[13]/div/form/div[1]/input",
            heightInput = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[13]/div/form/div[2]/input",
            minesInput = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[13]/div/form/div[3]/input",
            updateCustom = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[13]/div/form/button",
            closedClass = "cell size24 hd_closed",
            pressedClass = "cell size24 hd_closed hd_pressed",
            flagClass = "cell size24 hd_closed hd_flag",
            wonClass = "top-area-face zoomable hd_top-area-face-win",
            cameraButton = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[18]/table/tbody/tr/td[1]/div/div[2]/div/div/a",
            restartButton = "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[18]/table/tbody/tr/td[1]/div/div[1]/div[2]/div[2]/div[2]/div[5]/div"
            ;
        static List<String> levelSelect = new List<string> { 
            "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[3]/div[2]/div[1]/a/div" ,
            "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[3]/div[2]/div[2]/a/div" ,
            "/html/body/div[3]/div[1]/div/div[1]/div[2]/div/div[3]/div[2]/div[3]/a/div" },
            cellPattern = new List<string> {
                "cell_" , "_" };
        static string jsonFile = "login.json";
        static short[,] field;
        static IWebDriver driver;
        static List<short[]> offsets = new List<short[]> { new short[2] { -1, 0 }, new short[2] { 1, 0 }, new short[2] { 0, -1 }, new short[2] { 0, 1 } };
        static List<short[]> surrounding = new List<short[]> { new short[2] { -1, -1 }, new short[2] { 0, -1 }, new short[2] { 1, -1 }, 
            new short[2] { -1, 0 } , new short[2] { 1, 0 },
            new short[2] { -1, 1 } , new short[2] { 0, 1 } , new short[2] { 1, 1 } ,};

        static void Main(string[] args)
        {
            //first time only
            if (!File.Exists(jsonFile) || File.ReadAllText(jsonFile) == "")
            {
                Console.WriteLine("File login.json not found!");
            }
            //--------argument-verwaltung-------------
            string arg = "";
            if (args.Length == 0)
            {
                Console.WriteLine(helpString);
                Console.WriteLine("Argumente: ");
                arg = Console.ReadLine();
            }
            foreach (string a in args)//alles in ein string
            {
                if (helpArgs.Contains(a)) //ausgabe der hilfeseite
                {
                    Console.WriteLine(helpString);
                    Environment.Exit(0);
                }
                arg += a;
            }
            Regex argument = new Regex("(?<arg>[nliguwhm])(?<val>[0-9]*)");
            foreach (Match match in argument.Matches(arg))
            {
                switch (match.Groups["arg"].Value) //level und noflag attribute
                {
                    case "n":
                        noflag = true;
                        break;
                    case "l":
                        level = short.TryParse(match.Groups["val"].Value, out short x) ? x : (short)1;
                        break;
                    case "i":
                        incognito = true;
                        break;
                    case "g":
                        games = short.TryParse(match.Groups["val"].Value, out x) ? x : (short)1;
                        break;
                    case "u":
                        untilwin = short.TryParse(match.Groups["val"].Value, out x) ? x : (short)1;
                        break;
                    case "d":
                        delay = true;
                        break;
                }
            }
            switch (level) //level-einstellungen
            {
                case 2:
                    width = 16;
                    height = 16;
                    break;
                case 3:
                    width = 30;
                    height = 16;
                    break;
                case 4:
                    foreach (Match match in argument.Matches(arg)) //erweiterte einstellungen für custom-levels
                    {
                        switch (match.Groups["arg"].Value)
                        {
                            case "w":
                                width = short.TryParse(match.Groups["val"].Value, out short x) ? x : (short)9;
                                break;
                            case "h":
                                height = short.TryParse(match.Groups["val"].Value, out x) ? x : (short)9;
                                break;
                            case "m":
                                mines = short.TryParse(match.Groups["val"].Value, out x) ? x : (short)20;
                                break;
                        }
                    }
                    break;
            }
            string extension = level == 4 ? $", width: {width}, height: {height}, mines: {mines}" : "";
            Console.WriteLine($"incognito: {incognito}, flags: {!noflag}, level: {level}{extension}");

            //-------------funktionen----------------
            void waitForClick(IWebDriver driver, string xPath)
            {
                for (short _ = 0; _ < 10000; _++)
                {
                    try
                    {
                        driver.FindElement(By.XPath(xPath)).Click();
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
                
            } //wait until click is possible

            void waitForInput(IWebDriver driver, string xPath, string input)
            {
                for (short _ = 0; _ < 10000; _++)
                {
                    try
                    {
                        driver.FindElement(By.XPath(xPath)).Clear();
                        driver.FindElement(By.XPath(xPath)).SendKeys(input);
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
            } // wait until input is possible

            short getValue(short x, short y)
            {
                if (field[x, y] != -1) { return field[x, y]; }
                string cls = "";
                for (short _ = 0; _ < 10000; _++)
                {
                    cls = driver.FindElement(By.Id(cellPattern[0] + x.ToString() + cellPattern[1] + y.ToString()))
                        .GetAttribute("class");
                    if (!cls.Equals(pressedClass))
                    {
                        break;
                    }
                    Thread.Sleep(10);
                }
                if (cls.Equals(closedClass)) { return -1; }
                if (cls.Equals(flagClass)) { return 9; }
                short n = 0;
                short end = (short)(cls.Substring(29).Contains(" ") ? cls.IndexOf(" ", 30) - 29 : cls.Length - 29);
                if (!short.TryParse(cls.Substring(29, end), out n))
                {
                    Console.WriteLine("something went wrong");
                }
                return n;
            } //get value of cell from web

            short click(short x, short y)
            {
                if (delay) 
                {
                    Random random = new Random();
                    Thread.Sleep(random.Next(500, 1000));
                }
                for (short _ = 0; _ < 10000; _++)
                {
                    try
                    {
                        driver.FindElement(By.Id(cellPattern[0] + x.ToString() + cellPattern[1] + y.ToString())).Click();
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
                for (short _ = 0; _ < 10000; _++)
                {
                    short n = getValue(x, y);
                    if (n > -1)
                    {
                        return n;
                    }
                }
                return -1;
            } //click at point and return value of opened tile

            void rightClick(short x, short y)
            {
                if (delay)
                {
                    Random random = new Random();
                    Thread.Sleep(random.Next(500, 1000));
                }
                for (short _ = 0; _ < 10000; _++)
                {
                    try
                    {
                        Actions actions = new Actions(driver);
                        actions.ContextClick(driver.FindElement(By.Id(cellPattern[0] + x.ToString() + cellPattern[1] + y.ToString())))
                            .Perform();
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
            } //right-click at point

            List<short[]> getSurrounding(short x, short y, bool[,] done, List<short[]> points) //get points surrounding blank tiles
            {
                done[x, y] = true;
                short n = getValue(x, y);
                field[x, y] = n;
                if (n != -1)
                {
                    if (n != 0) { points.Add(new short[2] { x, y }); }
                    foreach (short[] p in offsets)
                    {
                        short nx = (short)(x + p[0]);
                        short ny = (short)(y + p[1]);
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                        if (!done[nx, ny])
                        {
                            points = getSurrounding(nx, ny, done, points);
                        }
                    }
                }
                return points;
            } //get all tiles opened by a blank one

            bool tryOpening(short x, short y)
            {
                short val = field[x, y];
                short flags = 0;
                foreach (short[] p in surrounding)
                {
                    if (x + p[0] < 0 || x + p[0] >= width || y + p[1] < 0 || y + p[1] >= height) { continue; }
                    if (field[x + p[0], y + p[1]] == 9)
                    {
                        flags++;
                    }
                }
                if (flags == val)
                {
                    foreach (short[] p in surrounding)
                    {
                        short newx = (short)(x + p[0]);
                        short newy = (short)(y + p[1]);
                        if (newx < 0 || newx >= width || newy < 0 || newy >= height) { continue; }
                        switch (field[newx, newy])
                        {
                            case -1:
                                short newval = click(newx, newy);
                                field[newx, newy] = newval;
                                tryFlagging(newx, newy);
                                break;
                            case 10:
                            case 11:
                                return true;
                            case 0: 
                            case 9:
                                break;
                            default:
                                tryFlagging(newx, newy);
                                break;

                        }
                    }
                }
                return false;
            } //try opening tiles around maybe satisfied tile

            bool tryFlagging(short x, short y) //try flagging tile
            {
                short val = field[x, y];
                short clear = 0;
                foreach (short[] p in surrounding)
                {
                    if (x + p[0] < 0 || x + p[0] >= width || y + p[1] < 0 || y + p[1] >= height) { continue; }
                    if (field[x + p[0], y + p[1]] == -1 || field[x + p[0], y + p[1]] == 9)
                    {
                        clear++;
                    }
                }
                if (clear == val)
                {
                    foreach (short[] p in surrounding)
                    {
                        short newx = (short)(x + p[0]);
                        short newy = (short)(y + p[1]);
                        if (newx < 0 || newx >= width || newy < 0 || newy >= height) { continue; }
                        switch (field[newx, newy])
                        {
                            case -1:
                                field[newx, newy] = 9;
                                if (!noflag)
                                {
                                    Random random = new Random();
                                    if (random.Next(2) == 1)
                                    {
                                        rightClick(newx, newy);
                                    }
                                }
                                foreach (short[] p2 in surrounding)
                                {
                                    short nx = (short)(newx + p2[0]);
                                    short ny = (short)(newy + p2[1]);
                                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                                    if (field[nx, ny] > 0 && field[nx, ny] < 9)
                                    {
                                        if (tryOpening(nx, ny)) { return true; }
                                    }
                                }
                                break;
                            case 10:
                            case 11:
                                return true;
                        }
                    }
                }
                return false;
            }

            short[,] evaluate()
            {
                List<short>[,] values = new List<short>[width, height];
                for (short x = 0; x < width; x++)
                {
                    for (short y = 0; y < height; y++)
                    {
                        short flags = 0;
                        if (field[x, y] < 1 || field[x, y] > 8) { continue; }
                        List<short[]> clear = new List<short[]>();
                        foreach (short[] point in surrounding)
                        {
                            short nx = (short)(x + point[0]);
                            short ny = (short)(y + point[1]);
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                            if (field[nx, ny] == -1)
                            {
                                clear.Add(new short[2] { nx, ny });
                            }
                            if (field[nx, ny] == 9)
                            {
                                flags++;
                            }
                        }
                        if (clear.Count > 0)
                        {
                            short p = (short)((field[x, y] - flags) * 100 / clear.Count);
                            foreach (short[] point in clear)
                            {
                                short nx = point[0];
                                short ny = point[1];
                                if (nx < 0 || nx >= width || ny < 0 || ny >= height) { continue; }
                                if (values[nx, ny] == null) { values[nx, ny] = new List<short>(); }
                                values[nx, ny].Add(p);
                            }
                        }
                    }
                }
                short[,] odds = new short[width, height];
                for (short x = 0; x < width; x++)
                {
                    for (short y = 0; y < height; y++)
                    {
                        if (field[x, y] != -1 || values[x, y] == null) 
                        {
                            odds[x, y] = 100;
                            continue; 
                        }
                        short p = 0;
                        foreach (short val in values[x, y])
                        {
                            p += val;
                        }
                        odds[x, y] = (short)(p / values[x, y].Count);
                    }
                }
                return odds;
            }

            bool playRandom(short x, short y)
            {
                short val = click(x, y);
                switch (val)
                {
                    case -1:
                        Console.WriteLine($"Error: Click returned -1 at {x}:{y}");
                        Environment.Exit(0);
                        break;
                    case 10: case 11:
                        return true;
                    case 0:
                        bool[,] done = new bool[width, height];
                        List<short[]> points = new List<short[]>();
                        points = getSurrounding(x, y, done, points);
                        foreach (short[] point in points)
                        {
                            if (tryFlagging(point[0], point[1])) { return true; }
                        }
                        break;
                    default:
                        field[x, y] = val;
                        if (tryFlagging(x, y)){ return true; }
                        break;
                }
                return false;
            }

            void restart(bool won)
            {
                if (won)
                {
                    wins++;
                    Console.WriteLine($"[{wins + losses}] won ({wins}:{losses})");
                    if (untilwin != -1 && untilwin <= wins) { driver.Quit(); Environment.Exit(0); }
                }
                else
                {
                    losses++;
                    Console.WriteLine($"[{wins + losses}] lost ({wins}:{losses})");
                    /*Thread.Sleep(100);
                    driver.FindElement(By.XPath(cameraButton)).Click();
                    Thread.Sleep(100);
                    driver.SwitchTo().Window(driver.WindowHandles[2]);
                    WebClient client = new WebClient();
                    string url = driver.Url;
                    driver.Close();
                    driver.SwitchTo().Window(driver.WindowHandles[0]);
                    client.DownloadFile(url, $"lostgame{losses}.png");*/
                }
                if (games != -1 && losses + wins >= games) { driver.Quit(); Environment.Exit(0); }
                field = new short[width, height];
                for (short x = 0; x < width; x++)
                {
                    for (short y = 0; y < height; y++)
                    {
                        field[x, y] = -1;
                    }
                }
                for (short _ = 0; _ < 10000; _++)
                {
                    try
                    {
                        driver.FindElement(By.XPath(restartButton)).Click();
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
                Thread.Sleep(100);
                Random random = new Random();
                playRandom((short)random.Next(width / 4, width * 3 / 4), (short)random.Next(height / 4, height * 3 / 4)); //start
            }

            //-------------seiten-aufruf und login------------
            driver = new ChromeDriver();
            driver.Navigate().GoToUrl("https://minesweeper.online/");
            if (!incognito)
            {
                waitForClick(driver, login);
                string json = File.ReadAllText(jsonFile);
                LoginArgs loginArgs = JsonSerializer.Deserialize<LoginArgs>(json);
                waitForInput(driver, usernameField, loginArgs.username);
                driver.FindElement(By.XPath(passwordField)).SendKeys(loginArgs.password);
                driver.FindElement(By.XPath(loginEnter)).Click();
            }
            //-----------spiel-auswahl--------------------
            waitForClick(driver, levelSelect[level < 4 ? level - 1 : 0]);
            if (level == 4) //custom-level-parameter
            {
                waitForClick(driver, customLevel);
                waitForInput(driver, widthInput, width.ToString());
                driver.FindElement(By.XPath(heightInput)).Clear();
                driver.FindElement(By.XPath(heightInput)).SendKeys(height.ToString());
                driver.FindElement(By.XPath(minesInput)).Clear();
                driver.FindElement(By.XPath(minesInput)).SendKeys(mines.ToString());
                driver.FindElement(By.XPath(updateCustom)).Click();
            }
            //-------------spiel-start-----------------------
            field = new short[width, height];
            for (short x = 0; x < width; x++)
            {
                for (short y = 0; y < height; y++)
                {
                    field[x, y] = -1;
                }
            } //deklarierung des spielfeldes
            Thread.Sleep(500);
            Random random = new Random();
            playRandom((short)random.Next(width / 4, width * 3 / 4), (short)random.Next(height / 4, height * 3 / 4)); //start

            while (true)
            {
                if (driver.FindElement(By.XPath(restartButton)).GetAttribute("class").Equals(wonClass))
                {
                    restart(true);
                }
                short[,] odds = evaluate();
                short[] least = new short[2] { 0, 0 };
                for (short x = 0; x < width; x++)
                {
                    for (short y = 0; y < height; y++)
                    {
                        if (odds[x, y] < odds[least[0], least[1]])
                        {
                            least = new short[2] { x, y };
                        }
                    }
                }
                if (playRandom(least[0], least[1])) { restart(false); }
            }

        }
    }
}
