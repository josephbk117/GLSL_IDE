using System;
using System.Collections.Generic;
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

    public partial class MainWindow : Window
    {
        Regex typesRegex = new Regex(@"(vec1|vec2|vec3|vec4|mat2|mat3|mat4|float|int|uint|double|bool|void|sampler1D|sampler2D|sampler3D|struct)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex miscRegex = new Regex(@"(in\s|out\s|inout\s|uniform\s|const\s|\sfalse|\strue)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex commentRegex = new Regex(@"(\/\/.*)|(\/\*[\s\S]*\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //Special Variables Only vertex shader
        Regex vertexShaderSpecial = new Regex(@"\sgl_Position\s|\sgl_PointSize\s|\sgl_VertexID\s|\sgl_InstanceID\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //Special Variables Only fragment shader
        Regex fragmentShaderSpecial = new Regex(@"\sgl_FragCoord\s|\sgl_FrontFacing\s|\sgl_FragDepth\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public MainWindow()
        {
            InitializeComponent();           
        }
        private void TextBox_keyUp(object sender, KeyEventArgs e)
        {
            TextRange range = new TextRange(glslTextBox.Document.ContentStart, glslTextBox.Document.ContentEnd);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(Colors.White));

            
            var start = glslTextBox.Document.ContentStart;
            while (start != null && start.CompareTo(glslTextBox.Document.ContentEnd) < 0)
            {
                if (start.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    var match = typesRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match2 = miscRegex.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match3 = commentRegex.Match(start.GetTextInRun(LogicalDirection.Forward));

                    //For now checking for both vertex and fragment soecific stuff
                    var match4 = vertexShaderSpecial.Match(start.GetTextInRun(LogicalDirection.Forward));
                    var match5 = fragmentShaderSpecial.Match(start.GetTextInRun(LogicalDirection.Forward));

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
                    else if (match5.Length > 0)
                    {
                        var textrange = new TextRange(start.GetPositionAtOffset(match5.Index, LogicalDirection.Forward), start.GetPositionAtOffset(match5.Index + match5.Length, LogicalDirection.Backward));
                        SolidColorBrush colourBrush = new SolidColorBrush(Color.FromRgb(100, 255, 255));
                        textrange.ApplyPropertyValue(TextElement.ForegroundProperty, colourBrush);
                    }
                }
                start = start.GetNextContextPosition(LogicalDirection.Forward);
            }
        }
    }
}
