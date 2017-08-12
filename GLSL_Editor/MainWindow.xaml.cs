using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GLSL_Editor
{

    //Make tab controls of tab controls, each tone tab contains a vertex an frag tab control, this tab control placed vertically

    public partial class MainWindow : Window
    {
        string vertexShaderSaveLocation, fragmentShaderSaveLocation;
        Process process;

        Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D|struct)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex miscRegex = new Regex(@"(in\s|out\s|inout\s|uniform\s|const\s|\sfalse|\strue)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex commentRegex = new Regex(@"(\/\/.*)|(\/\*[\s\S]*\/)|(\/\/.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex vertexShaderSpecial = new Regex(@"gl_Position\s|gl_PointSize\s|gl_VertexID\s|gl_InstanceID\s", RegexOptions.Compiled);
        Regex fragmentShaderSpecial = new Regex(@"gl_FragCoord\s|gl_FrontFacing\s|gl_FragDepth\s", RegexOptions.Compiled);

        public MainWindow()
        {
            InitializeComponent();
            vertexShaderSaveLocation = string.Empty;
            fragmentShaderSaveLocation = string.Empty;
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
            range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.White));
            var start = currentTextBox.Document.ContentStart;
            while (start != null && start.CompareTo(currentTextBox.Document.ContentEnd) < 0)
            {
                if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var match = typesRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match2 = miscRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match3 = commentRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match4 = shaderSpecificRegex.Match(start.GetTextInRun(LogicalDirection.Forward));

                    if (match.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match.Index + match.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(50, 180, 250));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                    }
                    else if (match2.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match2.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match2.Index + match2.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(200, 200, 255));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                    }
                    else if (match3.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match3.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match3.Index + match3.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(100, 255, 100));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                    }
                    else if (match4.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match4.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match4.Index + match4.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(100, 255, 255));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
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
                    System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        vertexShaderSaveLocation = sfd.FileName;
                        using (FileStream fs = File.OpenWrite(sfd.FileName))
                        {
                            TextRange text = new TextRange(glslVertexTextbox.Document.ContentStart, glslVertexTextbox.Document.ContentEnd);
                            text.Save(fs, DataFormats.Text);
                        }
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
                int len = vertexShaderSaveLocation.Split('\\').Length;
                ((TabItem)tabControl.SelectedItem).Header = vertexShaderSaveLocation.Split('\\')[len - 1];
                SavedFileModalWindow("Vertex Shader Saved", vertexShaderSaveLocation, "OK");
            }
            else
            {
                if (fragmentShaderSaveLocation == string.Empty)
                {
                    System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                    if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        fragmentShaderSaveLocation = sfd.FileName;
                        using (FileStream fs = File.OpenWrite(sfd.FileName))
                        {
                            TextRange text = new TextRange(glslFragmentTextbox.Document.ContentStart, glslFragmentTextbox.Document.ContentEnd);
                            text.Save(fs, DataFormats.Text);
                        }
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
                int len = fragmentShaderSaveLocation.Split('\\').Length;
                ((TabItem)tabControl.SelectedItem).Header = fragmentShaderSaveLocation.Split('\\')[len - 1];
                SavedFileModalWindow("Fragment Shader Saved", fragmentShaderSaveLocation, "OK");
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
            coverGrid.Visibility = Visibility.Hidden;
        }
        private void SavedFileModalWindow(string topLabelText, string subLabelText, string buttonText)
        {
            coverGrid.Visibility = Visibility.Visible;
            saveAndIdeErrorGrid_Toplabel.Content = topLabelText;
            saveAndIdeErrorGrid_SubLabel.Content = subLabelText;
            saveAndIdeErrorGrid_Buttonlabel.Content = buttonText;
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
                SavedFileModalWindow("Cannot Run Shaders", "No Shader has been saved yet, Save both shaders to file", "OK");
        }
    }
}
