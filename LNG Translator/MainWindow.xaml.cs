using System;
using System.Windows;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows.Controls;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;
using System.Collections;

namespace LNG_Translator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string windowTitle = "LNG Translator";
        private readonly OpenFileDialog OpenFileDialog = new OpenFileDialog();
        private string FileName;
        private List<LNGRow> lngRows = new List<LNGRow>();
        private readonly Dictionary<int, string> encodings = new Dictionary<int, string>
        { { 1200, "1200: UTF-16 LE" }, { 932, "932: Shift-JIS" }, {0,"" } };
        private int curEnc = 1200;

        private Dictionary<string, string> langs;
        private string curFromLang = "ja";
        private string curToLang = "en";

        public MainWindow()
        {
            InitializeComponent();

            this.FillLangs();

            foreach (EncodingInfo enc in Encoding.GetEncodings())
            {
                if (enc.CodePage != 1200 && enc.CodePage != 932)
                {
                    encodings.Add(enc.CodePage, enc.CodePage + ": " + enc.DisplayName);
                }
            }
            foreach (KeyValuePair<int, string> enc in encodings)
            {
                if (enc.Key == 0)
                {
                    Separator ms = new Separator();
                    resEncMenuItem.Items.Add(ms);
                    continue;
                }
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = enc.Value,
                    Tag = enc.Key,
                    IsCheckable = true
                };
                mi.Click += resEncMenuItem_Select;
                if (enc.Key == this.curEnc) { mi.IsChecked = true; }
                resEncMenuItem.Items.Add(mi);
            }

            foreach (KeyValuePair<string, string> lang in langs)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi.Click += langFrom_Select;
                if (lang.Value == curFromLang) { mi.IsChecked = true; }
                langFromMenuItem.Items.Add(mi);
                System.Windows.Controls.MenuItem mi2 = new System.Windows.Controls.MenuItem
                {
                    Header = lang.Key,
                    Tag = lang.Value,
                    IsCheckable = true
                };
                mi2.Click += langTo_Select;
                if (lang.Value == curToLang) { mi2.IsChecked = true; }
                langToMenuItem.Items.Add(mi2);
            }
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;

            OpenFileDialog.Filter = "LNG files (*.LNG)|*.LNG|All files (*.*)|*.*";

            System.Windows.Controls.ContextMenu context = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem exportItem = new System.Windows.Controls.MenuItem() { Header = "Google Translate" };
            exportItem.Click += TranslateContextButtonClick;
            context.Items.Add(exportItem);
            stringsView.ContextMenu = context;

            searchTextBox.GotFocus += RemovePlaceholder;
            searchTextBox.LostFocus += AddSearchText;

            string version = System.Windows.Forms.Application.ProductVersion;
            this.windowTitle += " v." + version.Remove(version.Length - 2);
            MainWindowElement.Title = this.windowTitle;
        }

        private void RemovePlaceholder(object sender, RoutedEventArgs e)
        {
            if (searchTextBox.Text == "Search...")
            {
                searchTextBox.Text = "";
            }
        }
        private void AddSearchText(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                searchTextBox.Text = "Search...";
            }                
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText.Contains(searchTextBox.Text));
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void TranslateContextButtonClick(object sender, RoutedEventArgs e)
        {
            LNGRow lrow = (LNGRow)stringsView.SelectedItem;
            if (lrow == null) { return; }
            lrow.TransText = this.GoogleTranslate(lrow.OrigText);
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void langFrom_Select(object sender, RoutedEventArgs e)
        {
            string lfrom = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curFromLang = lfrom;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
        }
        private void langTo_Select(object sender, RoutedEventArgs e)
        {
            string lto = ((System.Windows.Controls.MenuItem)sender).Tag.ToString();
            curToLang = lto;
            selectTransLangsButton.Header = curFromLang + " => " + curToLang;
        }

        private void resEncMenuItem_Select(object sender, RoutedEventArgs e)
        {
            int enc = Convert.ToInt32(((System.Windows.Controls.MenuItem)sender).Tag.ToString());
            foreach (var item in resEncMenuItem.Items)
            {
                if (item.GetType() == typeof(Separator)) { continue; }
                ((System.Windows.Controls.MenuItem)item).IsChecked =
                    (((System.Windows.Controls.MenuItem)item).Tag == ((System.Windows.Controls.MenuItem)sender).Tag)
                    ? true : false;
            }
            this.curEnc = enc;
            this.UpdateStringsEncoding();
        }

        private void OpenLNGFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                MainWindowElement.Title = this.windowTitle + " - " + OpenFileDialog.FileName;
                this.FileName = OpenFileDialog.FileName;
                this.ReadFile();
            }
        }

        private void ReadFile()
        {
            byte[] knownSignature = new byte[] { 0xA5, 0x5A, 0x5A, 0xA5, 0x01, 0x00, 0x00, 0x01 };
            using (FileStream fs = new FileStream(this.FileName, FileMode.Open, FileAccess.Read))
            {
                byte[] signature = new byte[8];
                fs.Read(signature, 0, signature.Length);
                if (!signature.SequenceEqual(knownSignature))
                {
                    System.Windows.MessageBox.Show("Unknown signature!");
                    return;
                }

                fs.Position = 0x40;
                byte[] addr = new byte[4] { 0xff, 0xff, 0x00, 0x00 };
                byte[] endaddr = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
                byte[] bChar = new byte[2] { 0xff, 0xff };
                byte[] bEndChar = new byte[2] { 0x00, 0x00 };
                List<byte> bText = new List<byte>();
                lngRows.Clear();

                int addrLength = 4;
                fs.Read(addr, 0, addrLength);
                // may be 4 bytes offset
                if (addr[2] != 0 || addr[3] != 0)
                {
                    addrLength = 2;
                    addr[2] = 0; addr[3] = 0;
                    fs.Position -= 2;
                }
                while (!addr.SequenceEqual(endaddr))
                {
                    bText.Clear();
                    long nextAddr = fs.Position;
                    fs.Position = 0x40 + BitConverter.ToUInt32(addr, 0) * 2;
                    fs.Read(bChar, 0, bChar.Length);
                    int maxTextLength = 512;
                    while (maxTextLength > 0 && fs.Position <= fs.Length - 1)
                    {
                        if (!bChar.SequenceEqual(bEndChar))
                        {
                            bText.Add(bChar[0]); bText.Add(bChar[1]);
                            fs.Read(bChar, 0, bChar.Length);
                            maxTextLength--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    string sText = Encoding.GetEncoding(this.curEnc).GetString(bText.ToArray());
                    LNGRow lrow = new LNGRow(addr, sText);
                    lrow.Encoding = curEnc;
                    lrow.TransText = sText;
                    lngRows.Add(lrow);

                    if (fs.Position >= fs.Length - 1)
                    {
                        addr = endaddr;
                    }
                    fs.Position = nextAddr;
                    fs.Read(addr, 0, addrLength);
                }

                ((GridView)stringsView.View).Columns[0].Header = "Original: (found " + lngRows.Count + " strings)";
                stringsView.ItemsSource = lngRows.Where(row => row.OrigText != "");
                stringsView.Items.Refresh();
                AutoSizeColumns(stringsView.View as GridView);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            FileInfo newFile = new FileInfo(this.FileName);
            string newFileName = newFile.Name.Substring(0, newFile.Name.Length - newFile.Extension.Length) + ".new" + newFile.Extension;

            using (FileStream fs = new FileStream(newFileName, FileMode.Create, FileAccess.ReadWrite))
            {

            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            System.Windows.Controls.TextBox tbox = (System.Windows.Controls.TextBox)sender;
            ContentPresenter cp = (ContentPresenter)tbox.TemplatedParent;
            LNGRow lrow = (LNGRow)cp.Content;
            lrow.TransText = tbox.Text;
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.TextBox txtbox = (System.Windows.Controls.TextBox)sender;
            txtbox.IsReadOnly = false;
        }

        private void UpdateStringsEncoding()
        {
            lngRows.ForEach((lrow) => {
                lrow.OrigText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.GetEncoding(lrow.Encoding).GetBytes(lrow.OrigText));
                lrow.TransText = Encoding.GetEncoding(this.curEnc).GetString(Encoding.GetEncoding(lrow.Encoding).GetBytes(lrow.TransText));
                lrow.Encoding = this.curEnc;
            });
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;
            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private void UpdateStringsView()
        {
            stringsView.ItemsSource = (this.skipEmptyStringsButton.IsChecked)
                ? lngRows.Where(row => row.OrigText != "")
                : lngRows;

            stringsView.Items.Refresh();
            AutoSizeColumns(stringsView.View as GridView);
        }

        private string GoogleTranslate(string text)
        {
            // code from https://www.codeproject.com/Tips/5247661/Google-Translate-API-Usage-in-Csharp
            // Set the language from/to in the url (or pass it into this function)
            string url = String.Format 
                ("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
                    curFromLang, curToLang, Uri.EscapeUriString(text));
            HttpClient httpClient = new HttpClient();
            string result = null;
            try
            {
                result = httpClient.GetStringAsync(url).Result;
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(e.ToString());
                return text;
            }

            // Get all json data
            var jsonData = new JavaScriptSerializer().Deserialize<List<dynamic>>(result);

            // Extract just the first array element (This is the only data we are interested in)
            var translationItems = jsonData[0];

            // Translation Data
            string translation = "";

            // Loop through the collection extracting the translated objects
            foreach (object item in translationItems)
            {
                // Convert the item array to IEnumerable
                IEnumerable translationLineObject = item as IEnumerable;

                // Convert the IEnumerable translationLineObject to a IEnumerator
                IEnumerator translationLineString = translationLineObject.GetEnumerator();

                // Get first object in IEnumerator
                translationLineString.MoveNext();

                // Save its value (translated text)
                translation += string.Format(" {0}", Convert.ToString(translationLineString.Current));
            }

            // Remove first blank character
            if (translation.Length > 1) { translation = translation.Substring(1); };

            // Return translation
            return translation;
        }

        private string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
               
        private void SkipEmptyStringsButton_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateStringsView();
        }

        private void AutoSizeColumns(GridView gv)
        {
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the almost same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    else
                    {
                        continue;
                    }
                    c.Width = double.NaN;
                }
            }
        }

        private void FillLangs()
        {
            this.langs = new Dictionary<string, string> {
                {"Japanese","ja"},
                {"English","en"},
                {"Russian","ru"},
                { "Dutch","nl"},
                {"Ukrainian","uk"}
                //{"", "" },

                //{ "Afrikaans", "af" },
                //{"Albanian","sq"},
                //{"Amharic","am"},
                //{"Arabic","ar"},
                //{"Armenian","hy"},
                //{"Azerbaijani","az"},
                //{"Basque","eu"},
                //{"Belarusian","be"},
                //{"Bengali","bn"},
                //{"Bosnian","bs"},
                //{"Bulgarian","bg"},
                //{"Catalan","ca"},
                //{"Chinese(Traditional)","zh-TW"},
                //{"Corsican","co"},
                //{"Croatian","hr"},
                //{"Czech","cs"},
                //{"Danish","da"},
                //{"Esperanto","eo"},
                //{"Estonian","et"},
                //{"Finnish","fi"},
                //{"French","fr"},
                //{"Frisian","fy"},
                //{"Galician","gl"},
                //{"Georgian","ka"},
                //{"German","de"},
                //{"Greek","el"},
                //{"Gujarati","gu"},
                //{"HaitianCreole","ht"},
                //{"Hausa","ha"},
                //{"Hebrew","he"},
                //{"Hindi","hi"},
                //{"Hungarian","hu"},
                //{"Icelandic","is"},
                //{"Igbo","ig"},
                //{"Indonesian","id"},
                //{"Irish","ga"},
                //{"Italian","it"},
                //{"Javanese","jv"},
                //{"Kannada","kn"},
                //{"Kazakh","kk"},
                //{"Khmer","km"},
                //{"Korean","ko"},
                //{"Kurdish","ku"},
                //{"Kyrgyz","ky"},
                //{"Lao","lo"},
                //{"Latin","la"},
                //{"Latvian","lv"},
                //{"Lithuanian","lt"},
                //{"Luxembourgish","lb"},
                //{"Macedonian","mk"},
                //{"Malagasy","mg"},
                //{"Malay","ms"},
                //{"Malayalam","ml"},
                //{"Maltese","mt"},
                //{"Maori","mi"},
                //{"Marathi","mr"},
                //{"Mongolian","mn"},
                //{"Myanmar(Burmese)","my"},
                //{"Nepali","ne"},
                //{"Norwegian","no"},
                //{"Nyanja(Chichewa)","ny"},
                //{"Pashto","ps"},
                //{"Persian","fa"},
                //{"Polish","pl"},
                //{"Portuguese(Portugal,Brazil)","pt"},
                //{"Punjabi","pa"},
                //{"Romanian","ro"},
                //{"Samoan","sm"},
                //{"ScotsGaelic","gd"},
                //{"Serbian","sr"},
                //{"Sesotho","st"},
                //{"Shona","sn"},
                //{"Sindhi","sd"},
                //{"Sinhala(Sinhalese)","si"},
                //{"Slovak","sk"},
                //{"Slovenian","sl"},
                //{"Somali","so"},
                //{"Spanish","es"},
                //{"Sundanese","su"},
                //{"Swahili","sw"},
                //{"Swedish","sv"},
                //{"Tagalog(Filipino)","tl"},
                //{"Tajik","tg"},
                //{"Tamil","ta"},
                //{"Telugu","te"},
                //{"Thai","th"},
                //{"Turkish","tr"},
                //{"Urdu","ur"},
                //{"Uzbek","uz"},
                //{"Vietnamese","vi"},
                //{"Welsh","cy"},
                //{"Xhosa","xh"},
                //{"Yiddish","yi"},
                //{"Yoruba","yo"},
                //{"Zulu","zu"}
            };
        }
        
        private class LNGRow
        {
            private byte[] offset;
            private string origText;
            private string transText;
            private int encoding;

            public LNGRow(byte[] offset, string origText)
            {
                this.offset = offset;
                this.origText = origText;
            }

            public int Encoding
            {
                get { return this.encoding; }
                set { this.encoding = value; }
            }

            public byte[] Offset
            {
                get { return this.offset; }
                set { this.offset = value; }
            }

            public string OrigText
            {
                get { return this.origText; }
                set { this.origText = value; }
            }

            public string TransText
            {
                get { return this.transText; }
                set { this.transText = value; }
            }
        }
    }
}
