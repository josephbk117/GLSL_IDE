﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        //There will always be one additional shader set tab, inside it will be a button to add a new shader set

        string vertexShaderSaveLocation, fragmentShaderSaveLocation;
        string defaultVertexSaveType = "(.vs)|*.vs";
        string defaultFragmentSaveType = "(.fs)|*.fs";
        Process process;

        Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D|struct)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex miscRegex = new Regex(@"(in\s|out\s|inout\s|uniform\s|const\s|\sfalse|\strue)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex commentRegex = new Regex(@"(\/\/.*)|(\/\*[\s\S]*\/)|(\/\/.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex vertexShaderSpecial = new Regex(@"gl_Position\s|gl_PointSize\s|gl_VertexID\s|gl_InstanceID\s", RegexOptions.Compiled);
        Regex fragmentShaderSpecial = new Regex(@"gl_FragCoord\s|gl_FrontFacing\s|gl_FragDepth\s", RegexOptions.Compiled);

        SolidColorBrush typesColour, miscColour, commentColour, shaderSpecificColour, baseTextColour;

        SolidColorBrush mainBrush;
        SolidColorBrush subBrush;
        SolidColorBrush tertBrush;
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
        }
        private void TextBox_keyUp(object sender, KeyEventArgs e)
        {
            string sval = ((TabItem)tabControl.SelectedItem).Header.ToString();
            if (!sval.EndsWith("*"))
            {
                ((TabItem)tabControl.SelectedItem).Header = ((TabItem)tabControl.SelectedItem).Header + "*";
            }
            RichTextBox currentTextBox = (RichTextBox)sender;
            SetUpLineAndFormat(currentTextBox);
        }

        private void SetUpLineAndFormat(RichTextBox currentTextBox)
        {
            RichTextBox lineNumberTextBox;
            FlowDocument flowDoc;
            if (currentTextBox == glslVertexTextbox)
            {
                lineNumberTextBox = lineNumberVertex_TextBox;
                flowDoc = lineNumberVertex_FlowDoc;
            }
            else
            {
                lineNumberTextBox = lineNumberFragment_TextBox;
                flowDoc = lineNumberFrag_FlowDoc;
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
            if (currentTextBox == glslVertexTextbox)
                shaderSpecificRegex = vertexShaderSpecial;
            else
                shaderSpecificRegex = fragmentShaderSpecial;

            TextRange range = new TextRange(currentTextBox.Document.ContentStart, currentTextBox.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, baseTextColour);
            var start = currentTextBox.Document.ContentStart;
            while (start != null && start.CompareTo(currentTextBox.Document.ContentEnd) < 0)
            {
                if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var match = commentRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match2 = typesRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match3 = miscRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match4 = shaderSpecificRegex.Match(start.GetTextInRun(LogicalDirection.Forward));

                    //Comments
                    if (match.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match.Index + match.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, commentColour);
                    }
                    //types
                    else if (match2.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match2.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match2.Index + match2.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, typesColour);
                    }
                    //misc
                    else if (match3.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match3.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match3.Index + match3.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, miscColour);
                    }
                    else if (match4.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match4.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match4.Index + match4.Length, LogicalDirection.Backward));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, shaderSpecificColour);
                    }
                }
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        private void DebugWindow_ClearButtonOnLeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 255, 255, 255));
            debugTextBox.Clear();
        }

        private void ToolBar_SaveButton_OnLeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 255, 255, 255));

            if (tabControl.SelectedIndex == 0)
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
                            TextRange text = new TextRange(glslVertexTextbox.Document.ContentStart, glslVertexTextbox.Document.ContentEnd);
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
                        TextRange text = new TextRange(glslVertexTextbox.Document.ContentStart, glslVertexTextbox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
                if (vertexShaderSaveLocation.Length > 2)
                {
                    int len = vertexShaderSaveLocation.Split('\\').Length;
                    ((TabItem)tabControl.SelectedItem).Header = vertexShaderSaveLocation.Split('\\')[len - 1];
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
                            TextRange text = new TextRange(glslFragmentTextbox.Document.ContentStart, glslFragmentTextbox.Document.ContentEnd);
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
                        TextRange text = new TextRange(glslFragmentTextbox.Document.ContentStart, glslFragmentTextbox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
                if (fragmentShaderSaveLocation.Length > 2)
                {
                    int len = fragmentShaderSaveLocation.Split('\\').Length;
                    ((TabItem)tabControl.SelectedItem).Header = fragmentShaderSaveLocation.Split('\\')[len - 1];
                }
            }
        }

        private void GenericButton_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender == vertexScrollBar)
                lineNumberVertex_TextBox.ScrollToVerticalOffset(e.VerticalOffset);
            else
                lineNumberFragment_TextBox.ScrollToVerticalOffset(e.VerticalOffset);
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
            //Create new tab item, set header name,margins
            //Header="Add Shader Set" BorderBrush="{x:Null}" Margin="-2,0,1,-11" Foreground="{StaticResource tertBrush}" Height="27" VerticalAlignment="Bottom"
            TabItem newTabItem = new TabItem()
            {
                Header = "Shader Set 2",
                Margin = new Thickness(-2, 0, 1, -5),
                Foreground = tertBrush,
                Height = 27,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            TabItem temp = addShaderSetTabIncItem;
            shaderSetTabControl.Items.Remove(addShaderSetTabIncItem);
            shaderSetTabControl.Items.Add(newTabItem);
            shaderSetTabControl.Items.Add(temp);
        }

        private void OptionsModalWindow_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            coverGrid.Visibility = Visibility.Visible;
            optionsGrid.Visibility = Visibility.Visible;
            saveAndIdeErrorGrid.Visibility = Visibility.Hidden;
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 255, 255, 255));
        }

        private void Colour_SliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (mainBrush != null)
                mainBrush.Color = Color.FromRgb((byte)bgColour_R.Value, (byte)bgColour_G.Value, (byte)bgColour_B.Value);
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
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 255, 255, 255));
            if (vertexShaderSaveLocation != string.Empty && fragmentShaderSaveLocation != string.Empty)
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
                while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    debugTextBox.Text += line + Environment.NewLine;
                }
            }
            else
                SavedFileModalWindow("Cannot Run Shaders", "Save all shaders in shader set to file, Then Run", "OK");
        }
    }
}
