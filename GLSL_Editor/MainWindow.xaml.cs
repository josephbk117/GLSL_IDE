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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GLSL_Editor
{

    /* C++ file that takes string parameter for vertex and fragment shader, get console output from that */
    /*Ammend : Takes location of vertex and fragment shader*/
    //Issue with saving by overwriting, create new file remove previous one

    public partial class MainWindow : Window
    {
        string vertexShaderSaveLocation, fragmentShaderSaveLocation;
        Process process;

        Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D|struct)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex miscRegex = new Regex(@"(in\s|out\s|inout\s|uniform\s|const\s|\sfalse|\strue)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex commentRegex = new Regex(@"(\/\/.*)|(\/\*[\s\S]*\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex vertexShaderSpecial = new Regex(@"gl_Position\s|gl_PointSize\s|gl_VertexID\s|gl_InstanceID\s", RegexOptions.Compiled);
        Regex fragmentShaderSpecial = new Regex(@"gl_FragCoord\s|gl_FrontFacing\s|gl_FragDepth\s", RegexOptions.Compiled);

        public MainWindow()
        {
            InitializeComponent();
        }
        private void TextBox_keyUp(object sender, KeyEventArgs e)
        {
            RichTextBox currentTextBox = (RichTextBox)sender;
            Regex shaderSpecificRegex;
            if (sender == glslVertexTextbox)
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
            System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (tabControl.SelectedIndex == 0)
                {
                    vertexShaderSaveLocation = sfd.FileName;
                    using (FileStream fs = File.OpenWrite(sfd.FileName))
                    {
                        TextRange text = new TextRange(glslVertexTextbox.Document.ContentStart, glslVertexTextbox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
                else
                {
                    fragmentShaderSaveLocation = sfd.FileName;
                    using (FileStream fs = File.OpenWrite(sfd.FileName))
                    {
                        TextRange text = new TextRange(glslFragmentTextbox.Document.ContentStart, glslFragmentTextbox.Document.ContentEnd);
                        text.Save(fs, DataFormats.Text);
                    }
                }
            }
        }

        private void GenericButton_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
        }

        private void ToolBar_RunButton_OnLeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            ((Rectangle)sender).Fill = new SolidColorBrush(Color.FromArgb((int)((20f / 100f) * 255), 255, 255, 255));
            if (vertexShaderSaveLocation != string.Empty && fragmentShaderSaveLocation != string.Empty)
            {
                process = new Process();
                process.StartInfo.FileName = @"F:\Visual Studio 2017\Projects\GLSL_Editor\GLSL_Editor\bin\Debug\ShaderTool.exe";
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
        }
    }
}
