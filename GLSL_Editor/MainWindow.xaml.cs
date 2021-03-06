﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GLSL_Editor
{
    public partial class MainWindow : Window
    {
        struct ErrorData
        {
            public TextEditorTypeAndScrollHelper.ShaderType shaderType;
            public int lineNumber;
            public ErrorData(TextEditorTypeAndScrollHelper.ShaderType shaderType, int lineNumber)
            {
                this.shaderType = shaderType;
                this.lineNumber = lineNumber;
            }
        }

        struct LineNumberErrorLink
        {
            public RichTextBox lineTextBox;
            public List<int> lineNumbers;

            public LineNumberErrorLink(RichTextBox textBox, List<int> lines)
            {
                lineTextBox = textBox;
                lineNumbers = lines;
            }
        }

        string vertexShaderSaveLocation, fragmentShaderSaveLocation;
        string defaultVertexSaveType = "(.vs)|*.vs";
        string defaultFragmentSaveType = "(.fs)|*.fs";
        Process process;

        Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D|struct)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex miscRegex = new Regex(@"(in\s|out\s|inout\s|attribute\s|varying\s|uniform\s|const\s|\sfalse|\strue)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex commentRegex = new Regex(@"(\/\/.*)|(\/\*[\s\S]*\/)|(\/\/.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex vertexShaderSpecial = new Regex(@"gl_Position\s|gl_PointSize\s|gl_VertexID\s|gl_InstanceID\s", RegexOptions.Compiled);
        Regex fragmentShaderSpecial = new Regex(@"gl_FragCoord\s|gl_FrontFacing\s|gl_FragDepth\s", RegexOptions.Compiled);

        SolidColorBrush typesColour, miscColour, commentColour, shaderSpecificColour, baseTextColour;
        SolidColorBrush mainBrush;
        SolidColorBrush subBrush;
        SolidColorBrush tertBrush;

        List<TextEditorTypeAndScrollHelper> textBoxCollection;
        TabItem currentOpenTabItem;
        RichTextBox currentTextBox;

        readonly string[] glslKeywords = new string[] { "in", "out", "inout", "attribute", "varying", "vec1", "vec2", "vec3", "vec4", "mat2", "mat3", "mat4", "float", "int", "uint",
            "void", "double", "bool", "sampler1D", "sampler2D", "sampler3D", "struct", "uniform", "const",
            "true", "false", "gl_Position", "gl_PointSize", "gl_VertexID", "gl_InstanceID",
            "gl_FragCoord", "gl_FrontFacing", "gl_FragDepth" };

        bool clearDebugWindowOnRun;

        List<LineNumberErrorLink> lineNumberLinks;

        public MainWindow()
        {
            InitializeComponent();
            vertexShaderSaveLocation = string.Empty;
            fragmentShaderSaveLocation = string.Empty;
            mainBrush = (SolidColorBrush)FindResource("bgBrush");
            subBrush = (SolidColorBrush)FindResource("subBrush");
            tertBrush = (SolidColorBrush)FindResource("tertBrush");
            baseTextColour = (SolidColorBrush)FindResource("baseTextBrush");

            typesColour = new SolidColorBrush(Color.FromRgb(50, 180, 250));
            miscColour = new SolidColorBrush(Color.FromRgb(200, 200, 255));
            commentColour = new SolidColorBrush(Color.FromRgb(100, 255, 100));
            shaderSpecificColour = new SolidColorBrush(Color.FromRgb(100, 255, 255));

            textBoxCollection = new List<TextEditorTypeAndScrollHelper>
            {
                new TextEditorTypeAndScrollHelper(TextEditorTypeAndScrollHelper.ShaderType.VERTEX, glslVertexTextbox, lineNumberVertex_TextBox),
                new TextEditorTypeAndScrollHelper(TextEditorTypeAndScrollHelper.ShaderType.FRAGMENT, glslFragmentTextbox, lineNumberFragment_TextBox)
            };

            currentOpenTabItem = vertexShaderTabItem;
            currentTextBox = glslVertexTextbox;
            clearDebugWindowOnRun = false;
            lineNumberLinks = new List<LineNumberErrorLink>();
        }
        private void TextBox_keyUp(object sender, KeyEventArgs e)
        {
            string sval = currentOpenTabItem.Header.ToString();
            if (!sval.EndsWith("*"))
                currentOpenTabItem.Header = currentOpenTabItem.Header + "*";
            currentTextBox = (RichTextBox)sender;
            SetUpLineAndFormat(currentTextBox);
            //__________ List Box Setup ___________
            if (e.Key != Key.Space || e.Key != Key.LeftShift)
                SetUpAutoCompletionListBox(currentTextBox);
            else
                choiceList.Visibility = Visibility.Hidden;
        }

        private void SetUpAutoCompletionListBox(RichTextBox currentTextBox)
        {
            choiceList.Visibility = Visibility.Hidden;
            choiceList.Items.Clear();

            Regex reg = new Regex(@"\w+");
            string richText = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.CaretPosition).Text;
            string lastWordBeforeCurrentCursorPosition;
            try
            {
                lastWordBeforeCurrentCursorPosition = richText.Substring(richText.LastIndexOf(' '), richText.Length - richText.LastIndexOf(' ')).Trim();
            }
            catch (ArgumentOutOfRangeException)
            {
                lastWordBeforeCurrentCursorPosition = "";
            }

            foreach (Match match in reg.Matches(richText))
            {
                if (lastWordBeforeCurrentCursorPosition != "")
                {
                    if (match.Value.StartsWith(lastWordBeforeCurrentCursorPosition))
                    {
                        if (!choiceList.Items.Contains(match.Value))
                            choiceList.Items.Add(match.Value);
                    }
                }
                else
                {
                    choiceList.Items.Clear();
                }
            }
            if (choiceList.Items.Count > 1)
            {
                choiceList.Visibility = Visibility.Visible;
                Rect caret = currentTextBox.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
                Canvas.SetTop(choiceList, currentTextBox.TranslatePoint(new Point((int)caret.X, (int)caret.Y + (int)caret.Height), canvas).Y);
                Canvas.SetLeft(choiceList, currentTextBox.TranslatePoint(new Point((int)caret.X, (int)caret.Y + (int)caret.Height), canvas).X);
            }
        }

        private void SetUpLineAndFormat(RichTextBox currentTextBox)
        {
            RichTextBox lineNumberTextBox = new RichTextBox();
            FlowDocument flowDoc = new FlowDocument();
            TextEditorTypeAndScrollHelper helper = new TextEditorTypeAndScrollHelper();
            foreach (TextEditorTypeAndScrollHelper txtEditHelp in textBoxCollection)
            {
                if (txtEditHelp.GetShaderTextBox() == currentTextBox)
                {
                    lineNumberTextBox = txtEditHelp.GetCorrespondingLineTextBox();
                    flowDoc = lineNumberTextBox.Document;
                    helper = txtEditHelp;
                    break;
                }
            }

            int someBigNumber = int.MaxValue;
            int currentLineNumber;
            TextPointer tp = currentTextBox.CaretPosition;
            currentTextBox.CaretPosition = currentTextBox.Document.ContentEnd;
            currentTextBox.CaretPosition.GetLineStartPosition(-someBigNumber, out int lineMoved);
            currentTextBox.CaretPosition = tp;
            currentLineNumber = -lineMoved + 1;

            lineNumberTextBox.Document.Blocks.Clear();
            for (int i = 1; i <= currentLineNumber; i++)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run(i.ToString()));
                flowDoc.Blocks.Add(paragraph);
            }

            Regex shaderSpecificRegex;
            if (helper.GetShaderType() == TextEditorTypeAndScrollHelper.ShaderType.VERTEX)
                shaderSpecificRegex = vertexShaderSpecial;
            else
                shaderSpecificRegex = fragmentShaderSpecial;

            TextRange range = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, baseTextColour);
            var start = currentTextBox.Document.ContentStart;
            start = PerformSytaxHighlighting(currentTextBox, shaderSpecificRegex, start);

        }

        private TextPointer PerformSytaxHighlighting(RichTextBox currentTextBox, Regex shaderSpecificRegex, TextPointer start)
        {
            while (start != null && start.CompareTo(currentTextBox.Document.ContentEnd) < 0)
            {
                if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var match1 = commentRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match2 = typesRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match3 = miscRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match4 = shaderSpecificRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    //Comments
                    if (match1.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match1.Index, LogicalDirection.Forward),
                            start.GetPositionAtOffset(match1.Index + match1.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, commentColour);
                    }
                    //types
                    else if (match2.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match2.Index, LogicalDirection.Forward),
                            start.GetPositionAtOffset(match2.Index + match2.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, typesColour);
                    }
                    //misc
                    else if (match3.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match3.Index, LogicalDirection.Forward),
                            start.GetPositionAtOffset(match3.Index + match3.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, miscColour);
                    }
                    else if (match4.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match4.Index, LogicalDirection.Forward),
                            start.GetPositionAtOffset(match4.Index + match4.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, shaderSpecificColour);
                    }
                }
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
            return start;
        }

        private void DebugWindow_ClearButtonOnLeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 0, 0, 0));
            debugTextBox.Clear();
        }

        private void ToolBar_SaveButton_OnLeftMouseUp(object sender, MouseButtonEventArgs e)
        {

            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 0, 0, 0));
            bool isVertexShader = true;
            foreach (TextEditorTypeAndScrollHelper helper in textBoxCollection)
            {
                if (helper.GetShaderTextBox() == currentTextBox)
                    isVertexShader = (helper.GetShaderType() == TextEditorTypeAndScrollHelper.ShaderType.VERTEX) ? true : false;
            }

            if (isVertexShader)
            {
                if (vertexShaderSaveLocation == string.Empty)
                {
                    System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog()
                    {
                        Filter = "Vertex shader" + defaultVertexSaveType
                    };
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        vertexShaderSaveLocation = sfd.FileName;
                        using (FileStream fs = File.OpenWrite(sfd.FileName))
                        {
                            TextRange text = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
                            text.Save(fs, DataFormats.Text);
                        }
                        SavedFileModalWindow("Vertex Shader Saved", vertexShaderSaveLocation, "OK");
                    }
                }
                else
                {
                    if (File.Exists(vertexShaderSaveLocation))
                        File.Delete(vertexShaderSaveLocation);
                    using (FileStream fs = File.OpenWrite(vertexShaderSaveLocation))
                    {
                        TextRange text = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
                if (vertexShaderSaveLocation.Length > 2)
                {
                    int len = vertexShaderSaveLocation.Split('\\').Length;
                    TabItem item = (TabItem)shaderSetTabControl.SelectedItem;
                    ((TabItem)((TabControl)((Grid)item.Content).Children[0]).SelectedItem).Header = vertexShaderSaveLocation.Split('\\')[len - 1];
                }
            }
            else
            {
                if (fragmentShaderSaveLocation == string.Empty)
                {
                    System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog()
                    {
                        Filter = "Fragment Shader" + defaultFragmentSaveType
                    };
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        fragmentShaderSaveLocation = sfd.FileName;
                        using (FileStream fs = File.OpenWrite(sfd.FileName))
                        {
                            TextRange text = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
                            text.Save(fs, DataFormats.Text);
                        }
                        SavedFileModalWindow("Fragment Shader Saved", fragmentShaderSaveLocation, "OK");
                    }
                }
                else
                {
                    if (File.Exists(fragmentShaderSaveLocation))
                        File.Delete(fragmentShaderSaveLocation);
                    using (FileStream fs = File.OpenWrite(fragmentShaderSaveLocation))
                    {
                        TextRange text = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
                if (fragmentShaderSaveLocation.Length > 2)
                {
                    int len = fragmentShaderSaveLocation.Split('\\').Length;
                    TabItem item = (TabItem)shaderSetTabControl.SelectedItem;
                    ((TabItem)((TabControl)((Grid)item.Content).Children[0]).SelectedItem).Header = fragmentShaderSaveLocation.Split('\\')[len - 1];
                }
            }
        }

        private void GenericButton_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            foreach (TextEditorTypeAndScrollHelper helper in textBoxCollection)
            {
                if (((ScrollViewer)sender).Content == helper.GetShaderTextBox())
                    helper.GetCorrespondingLineTextBox().ScrollToVerticalOffset(((ScrollViewer)sender).VerticalOffset);
            }
        }

        private void RichTextbox_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetUpLineAndFormat((RichTextBox)sender);
        }

        private void ModalWindowButton_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender == optionSetButton)
            {
                defaultFragmentSaveType = "(." + fragmentExtensionText.Text.Replace(".", "") + ")|*." + fragmentExtensionText.Text.Replace(".", "");
                defaultVertexSaveType = "(." + vertexExtensionText.Text.Replace(".", "") + ")|*." + vertexExtensionText.Text.Replace(".", "");
            }
            coverGrid.Visibility = Visibility.Hidden;
        }
        private void SavedFileModalWindow(string topLabelText, string subLabelText, string buttonText)
        {
            coverGrid.Visibility = Visibility.Visible;
            optionsGrid.Visibility = Visibility.Hidden;
            addShaderSetGrid.Visibility = Visibility.Hidden;
            saveAndIdeErrorGrid.Visibility = Visibility.Visible;
            saveAndIdeErrorGrid_Toplabel.Content = topLabelText;
            saveAndIdeErrorGrid_SubLabel.Content = subLabelText;
            saveAndIdeErrorGrid_Buttonlabel.Content = buttonText;
        }

        private void TextColour_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (sender == dataTypeColour_R || sender == dataTypeColour_G || sender == dataTypeColour_B)
                    typesColour.Color = Color.FromRgb((byte)dataTypeColour_R.Value, (byte)dataTypeColour_G.Value, (byte)dataTypeColour_B.Value);
                if (sender == commentColour_R || sender == commentColour_G || sender == commentColour_B)
                    commentColour.Color = Color.FromRgb((byte)commentColour_R.Value, (byte)commentColour_G.Value, (byte)commentColour_B.Value);
                if (sender == varColour_R || sender == varColour_G || sender == varColour_B)
                    shaderSpecificColour.Color = Color.FromRgb((byte)varColour_R.Value, (byte)varColour_G.Value, (byte)varColour_B.Value);
                if (sender == miscColour_R || sender == miscColour_G || sender == miscColour_B)
                    miscColour.Color = Color.FromRgb((byte)miscColour_R.Value, (byte)miscColour_G.Value, (byte)miscColour_B.Value);
                if (sender == baseTextColour_R || sender == baseTextColour_G || sender == baseTextColour_B)
                    baseTextColour.Color = Color.FromRgb((byte)baseTextColour_R.Value, (byte)baseTextColour_G.Value, (byte)baseTextColour_B.Value);
            }
            catch { }
        }

        private void ShaderSetAdd_OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            coverGrid.Visibility = Visibility.Visible;
            optionsGrid.Visibility = Visibility.Hidden;
            saveAndIdeErrorGrid.Visibility = Visibility.Hidden;
            addShaderSetGrid.Visibility = Visibility.Visible;
        }

        private void AddShaderSetToTabs(string shaderSetName)
        {
            TabItem newTabItem = new TabItem()
            {
                Header = shaderSetName,
                Margin = new Thickness(-2, 0, 1, -5),
                Foreground = tertBrush,
                Height = 27,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = mainBrush,
                BorderBrush = tertBrush
            };

            Grid grid = new Grid()
            {
                Background = subBrush
            };
            TabControl tabCntrl = new TabControl()
            {
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = null,
                Foreground = null
            };

            TabItem tItem1 = GenerateShaderTabItem("Vertex Shader");
            TabItem tItem2 = GenerateShaderTabItem("Fragment Shader");
            tItem1.GotFocus += TabItem_OnGotFocus;
            tItem2.GotFocus += TabItem_OnGotFocus;

            Grid insideGrid1 = GenerateInsideGrid();
            Grid insideGrid2 = GenerateInsideGrid();

            ScrollViewer scViewer1 = GenerateScrollViewer();
            ScrollViewer scViewer2 = GenerateScrollViewer();

            RichTextBox acVerTextBox = GenerateShaderEditorTextBox();
            RichTextBox acFragTextBox = GenerateShaderEditorTextBox();

            acVerTextBox.KeyUp += TextBox_keyUp;
            acVerTextBox.Loaded += RichTextbox_OnLoaded;

            acFragTextBox.KeyUp += TextBox_keyUp;
            acFragTextBox.Loaded += RichTextbox_OnLoaded;

            acVerTextBox.Document = GenerateFlowDoc();
            acFragTextBox.Document = GenerateFlowDoc();

            acVerTextBox.PreviewMouseDown += RichTextbox_OnMouseDown;
            acFragTextBox.PreviewMouseDown += RichTextbox_OnMouseDown;

            scViewer1.Content = acVerTextBox;
            scViewer2.Content = acFragTextBox;

            RichTextBox lineNumberTextBox1 = GenerateLineNumberTextBox();
            RichTextBox lineNumberTextBox2 = GenerateLineNumberTextBox();

            lineNumberTextBox1.Document = new FlowDocument() { LineHeight = 3, PagePadding = new Thickness(0) };
            lineNumberTextBox2.Document = new FlowDocument() { LineHeight = 3, PagePadding = new Thickness(0) };

            insideGrid1.Children.Add(lineNumberTextBox1);
            insideGrid1.Children.Add(scViewer1);

            insideGrid2.Children.Add(lineNumberTextBox2);
            insideGrid2.Children.Add(scViewer2);

            textBoxCollection.Add(new TextEditorTypeAndScrollHelper(TextEditorTypeAndScrollHelper.ShaderType.VERTEX, acVerTextBox, lineNumberTextBox1));
            textBoxCollection.Add(new TextEditorTypeAndScrollHelper(TextEditorTypeAndScrollHelper.ShaderType.FRAGMENT, acFragTextBox, lineNumberTextBox2));

            tItem1.Content = insideGrid1;
            tItem2.Content = insideGrid2;

            tabCntrl.Items.Add(tItem1);
            tabCntrl.Items.Add(tItem2);

            grid.Children.Add(tabCntrl);
            newTabItem.Content = grid;
            //--end inside objs
            TabItem temp = addShaderSetTabIncItem;
            shaderSetTabControl.Items.Remove(addShaderSetTabIncItem);
            shaderSetTabControl.Items.Add(newTabItem);
            shaderSetTabControl.Items.Add(temp);
            shaderSetTabControl.SelectedIndex = shaderSetTabControl.Items.Count - 2;
        }

        private TabItem GenerateShaderTabItem(string tabItemHeader)
        {
            return new TabItem()
            {
                Header = tabItemHeader,
                Background = mainBrush,
                BorderBrush = null,
                Foreground = tertBrush,
                Margin = new Thickness(35, 0, -40, 0)
            };
        }

        private static Grid GenerateInsideGrid()
        {
            return new Grid()
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 100, 100, 100)),
                Margin = new Thickness(-2, 0, 0, -3)
            };
        }

        private ScrollViewer GenerateScrollViewer()
        {
            ScrollViewer scViewer = new ScrollViewer();
            scViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            return scViewer;
        }

        private FlowDocument GenerateFlowDoc()
        {
            FlowDocument fDoc = new FlowDocument()
            {
                LineHeight = 3,
                PagePadding = new Thickness(0)
            };

            Paragraph para = new Paragraph(new Run("# version 140"))
            {
                Foreground = subBrush,
                Background = mainBrush
            };
            fDoc.Blocks.Add(para);
            return fDoc;
        }

        private RichTextBox GenerateShaderEditorTextBox()
        {
            return new RichTextBox()
            {
                AcceptsTab = true,
                Margin = new Thickness(37, 0, 0, 0),
                Background = subBrush,
                FontFamily = new FontFamily("consolas"),
                FontSize = 14,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                IsUndoEnabled = false
            };
        }

        private RichTextBox GenerateLineNumberTextBox()
        {
            return new RichTextBox()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                IsReadOnly = true,
                Width = 37,
                FontFamily = new FontFamily("consolas"),
                FontSize = 14,
                Background = mainBrush,
                Foreground = tertBrush,
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };
        }

        private void AddShaderText_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (shaderSetText.Text.Length < 1)
                return;
            AddShaderSetToTabs(shaderSetText.Text);
            coverGrid.Visibility = Visibility.Hidden;
        }

        private void TabItem_OnGotFocus(object sender, RoutedEventArgs e)
        {
            currentOpenTabItem = sender as TabItem;
            currentTextBox = ((ScrollViewer)(((Grid)currentOpenTabItem.Content).Children[1])).Content as RichTextBox;
        }

        private void AutoCompleteListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Inline tempText = currentTextBox.CaretPosition.Paragraph.Inlines.LastInline;
            TextRange range = new TextRange(tempText.ElementStart, tempText.ElementEnd);
            string textToAdd = choiceList.SelectedItem.ToString();
            if (range.Text.Contains(" "))
            {
                range.Text = range.Text.Remove(range.Text.LastIndexOf(" "));
                textToAdd = " " + textToAdd;
            }
            currentTextBox.CaretPosition.Paragraph.Inlines.Add(new Run(textToAdd)
            {
                Foreground = baseTextColour
            });
            choiceList.Visibility = Visibility.Hidden;
        }

        private void RichTextbox_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            choiceList.Visibility = Visibility.Hidden;
        }

        private void DebugWindow_ClearOnRunButtonOnLeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            clearDebugWindowOnRun = !clearDebugWindowOnRun;
            ((Rectangle)sender).Fill = (clearDebugWindowOnRun) ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)) : new SolidColorBrush(Color.FromArgb((int)(255 * 20f / 100), 0, 0, 0));
        }

        private void OptionsModalWindow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            coverGrid.Visibility = Visibility.Visible;
            optionsGrid.Visibility = Visibility.Visible;
            saveAndIdeErrorGrid.Visibility = Visibility.Hidden;
            addShaderSetGrid.Visibility = Visibility.Hidden;
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 0, 0, 0));
        }

        private void LoadButton_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string line;
                currentTextBox.Document.Blocks.Clear();
                StreamReader file = new StreamReader(ofd.FileName);
                while ((line = file.ReadLine()) != null)
                {
                    Paragraph para = new Paragraph(new Run(line) { Foreground = baseTextColour })
                    {
                        Background = mainBrush
                    };
                    currentTextBox.Document.Blocks.Add(para);
                }
                file.Close();
            }
            SetUpLineAndFormat(currentTextBox);
        }

        private void Colour_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mainBrush != null)
                mainBrush.Color = Color.FromRgb((byte)bgColour_R.Value, (byte)bgColour_G.Value, (byte)bgColour_B.Value);
        }

        private void Window_LayoutUpdated(object sender, EventArgs e)
        {
            if (lineNumberLinks != null)
            {
                if (lineNumberLinks.Count > 0)
                {
                    foreach (LineNumberErrorLink link in lineNumberLinks)
                        SetUpLineErrorDisplay(link.lineTextBox, link.lineNumbers);
                }
            }
        }

        private void SubColour_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (subBrush != null)
                subBrush.Color = Color.FromRgb((byte)subColour_R.Value, (byte)subColour_G.Value, (byte)subColour_B.Value);
        }

        private void TertColour_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tertBrush != null)
                tertBrush.Color = Color.FromRgb((byte)tertColour_R.Value, (byte)tertColour_G.Value, (byte)tertColour_B.Value);
        }

        private void ToolBar_RunButton_OnLeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (clearDebugWindowOnRun)
                debugTextBox.Clear();

            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 0, 0, 0));

            Grid selectedShaderSetGrid = ((Grid)shaderSetTabControl.SelectedContent);
            TabControl conTabCntl = (TabControl)selectedShaderSetGrid.Children[0];
            ItemCollection iCol = conTabCntl.Items;

            TabItem vertexShaderTabItem = ((TabItem)iCol[0]);
            TabItem fragmentShaderTabItem = ((TabItem)iCol[1]);

            RichTextBox vertexLineNumberBox = ((RichTextBox)((Grid)vertexShaderTabItem.Content).Children[0]);
            RichTextBox fragmentLineNumberBox = ((RichTextBox)((Grid)fragmentShaderTabItem.Content).Children[0]);

            string CvertexShaderSaveLocation = vertexShaderTabItem.Header.ToString();
            string CfragmentShaderSaveLocation = fragmentShaderTabItem.Header.ToString();
            if (CvertexShaderSaveLocation != string.Empty && CfragmentShaderSaveLocation != string.Empty &&
                CvertexShaderSaveLocation != "Vertex Shader" && CfragmentShaderSaveLocation != "Fragment Shader")
            {
                process = new Process();
                process.StartInfo.FileName = @"ShaderTool.exe";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                string vertexLocation = "\"" + vertexShaderSaveLocation + "\"";
                string fragmentLocation = "\"" + fragmentShaderSaveLocation + "\"";
                process.StartInfo.Arguments = vertexShaderSaveLocation + " " + fragmentShaderSaveLocation;
                process.Start();
                StringBuilder completeLog = new StringBuilder();
                while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    completeLog.Append(line + Environment.NewLine);
                }
                completeLog = completeLog.Replace(Environment.NewLine + Environment.NewLine, " ");
                debugTextBox.Text = completeLog.ToString();
                ErrorData[] errors = ProcessConsoleString(completeLog.ToString()).ToArray();
                List<int> vertexErrorLine = new List<int>();
                List<int> fragmentErrorLine = new List<int>();
                foreach (ErrorData edata in errors)
                {
                    if (edata.shaderType == TextEditorTypeAndScrollHelper.ShaderType.VERTEX)
                        vertexErrorLine.Add(edata.lineNumber);
                    if (edata.shaderType == TextEditorTypeAndScrollHelper.ShaderType.FRAGMENT)
                        fragmentErrorLine.Add(edata.lineNumber);
                }
                if (vertexErrorLine.Count > 0)
                {
                    lineNumberLinks.Add(new LineNumberErrorLink(vertexLineNumberBox, vertexErrorLine));
                    SetUpLineErrorDisplay(vertexLineNumberBox, vertexErrorLine);
                }
                if (fragmentErrorLine.Count > 0)
                {
                    lineNumberLinks.Add(new LineNumberErrorLink(fragmentLineNumberBox, fragmentErrorLine));
                    SetUpLineErrorDisplay(fragmentLineNumberBox, fragmentErrorLine);
                }
            }
            else
                SavedFileModalWindow("Cannot Run Shaders", "Save all shaders in shader set to file, Then Run", "OK");
        }

        private static void SetUpLineErrorDisplay(RichTextBox lineNumberBox, List<int> vertexErrorLine)
        {
            BlockCollection collection = lineNumberBox.Document.Blocks;
            int k = 0;
            foreach (Block b in collection)
            {
                k++;
                if (vertexErrorLine.Contains(k))
                    b.Background = Brushes.Coral;
            }
        }

        private List<ErrorData> ProcessConsoleString(string data)
        {
            List<ErrorData> errors = new List<ErrorData>();
            string checkVertex = "VERTEX shader compilation error :";
            string checkFrag = "FRAGMENT shader compilation error :";
            string checkProg = "PROGRAM linking error of type : PROGRAM";
            if (data.Contains(checkProg))
            {
                Regex errorRegex = new Regex(@"(ERROR:\s\d:\d*)");
                if (data.Contains(checkVertex))
                {
                    int indexOfvert = data.IndexOf(checkVertex);
                    int indexOfFrag = data.IndexOf("FRAGMENT shader compilation");
                    string vertexErrorString = data.Substring(indexOfvert + checkVertex.Length, indexOfFrag - (indexOfvert + checkVertex.Length));
                    foreach (Match match in errorRegex.Matches(vertexErrorString))
                    {
                        string numValue = match.Value.Substring(match.Value.LastIndexOf(':') + 1);
                        errors.Add(new ErrorData(TextEditorTypeAndScrollHelper.ShaderType.VERTEX, Int32.Parse(numValue)));
                    }
                }
                if (data.Contains(checkFrag))
                {
                    int indexOfFrag = data.IndexOf(checkFrag);
                    int indexOfProg = data.IndexOf(checkProg);
                    string fragmentErrorString = data.Substring(indexOfFrag + checkFrag.Length, indexOfProg - (indexOfFrag + checkFrag.Length));
                    foreach (Match match in errorRegex.Matches(fragmentErrorString))
                    {
                        string numValue = match.Value.Substring(match.Value.LastIndexOf(':') + 1);
                        errors.Add(new ErrorData(TextEditorTypeAndScrollHelper.ShaderType.FRAGMENT, Int32.Parse(numValue)));
                    }
                }
            }
            return errors;
        }
    }
}
